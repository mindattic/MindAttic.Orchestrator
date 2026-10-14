using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

public enum BudgetLevel { Ok, Warn, Over }

/// <summary>
/// A 5-hour usage block, the unit Claude's subscription limits reset on: it starts at the top of
/// the hour of the first response after the previous block ended, and lasts five hours.
/// </summary>
public sealed record UsageBlock(DateTime StartUtc, DateTime EndUtc, TokenUsage Usage, DateTime LastActivityUtc)
{
    public bool IsActive(DateTime nowUtc) => nowUtc < EndUtc;
}

public sealed record BudgetStatus(
    UsageBlock? CurrentBlock,
    long BlockLimit,
    bool BlockLimitIsPeak,
    double BlockPercent,
    TokenUsage Today,
    TokenUsage Week,
    long? WeeklyLimit,
    double? WeeklyPercent,
    double FreshPerHour,
    DateTime? ProjectedLimitUtc,
    int WarnPercent)
{
    /// <summary>The highest percentage of a real limit. Peak-relative block numbers are context, not a
    /// limit, so they never raise the level on their own.</summary>
    public double WorstPercent => Math.Max(BlockLimitIsPeak ? 0 : BlockPercent, WeeklyPercent ?? 0);

    public BudgetLevel Level =>
        WorstPercent >= 100 ? BudgetLevel.Over
        : WorstPercent >= WarnPercent ? BudgetLevel.Warn
        : BudgetLevel.Ok;

    public long BlockRemaining => Math.Max(0, BlockLimit - (CurrentBlock?.Usage.Fresh ?? 0));
}

/// <summary>
/// Measures usage against budgets. The budget metric is uncached tokens (input + cache write +
/// output): cache reads are cheap and dominate raw totals, so counting them would make every
/// percentage meaningless. Blocks follow the convention community tools such as ccusage use, since
/// Anthropic doesn't publish the limit or the window to clients.
/// </summary>
public static class BudgetCalculator
{
    public static readonly TimeSpan BlockLength = TimeSpan.FromHours(5);

    public static IReadOnlyList<UsageBlock> Blocks(IEnumerable<UsageEvent> events)
    {
        var blocks = new List<UsageBlock>();
        DateTime start = default, end = default, last = default;
        TokenUsage sum = default;
        var open = false;
        foreach (var e in events.OrderBy(e => e.AtUtc))
        {
            if (!open || e.AtUtc >= end)
            {
                if (open) blocks.Add(new UsageBlock(start, end, sum, last));
                start = new DateTime(e.AtUtc.Year, e.AtUtc.Month, e.AtUtc.Day, e.AtUtc.Hour, 0, 0, DateTimeKind.Utc);
                end = start + BlockLength;
                sum = default;
                open = true;
            }
            sum += e.Usage;
            last = e.AtUtc;
        }
        if (open) blocks.Add(new UsageBlock(start, end, sum, last));
        return blocks;
    }

    public static BudgetStatus Compute(IReadOnlyList<UsageEvent> events, OrchestratorSettings settings, DateTime nowUtc, DateTime startOfTodayUtc)
    {
        var blocks = Blocks(events);
        var current = blocks.Count > 0 && blocks[^1].IsActive(nowUtc) ? blocks[^1] : null;
        var currentFresh = current?.Usage.Fresh ?? 0;

        // Without a budget, compare against the heaviest earlier block: "how hard am I going vs usual".
        // It can pass 100%, and it isn't a limit, so it never warns or projects.
        var peak = blocks.Where(b => b != current).Select(b => b.Usage.Fresh).DefaultIfEmpty(0).Max();
        var isPeak = settings.BlockBudget is null;
        var limit = settings.BlockBudget ?? (peak > 0 ? peak : currentFresh);
        var blockPercent = limit > 0 ? 100.0 * currentFresh / limit : 0;

        var weekStart = nowUtc - TimeSpan.FromDays(7);
        var week = Sum(events.Where(e => e.AtUtc >= weekStart));
        double? weeklyPercent = settings.WeeklyBudget is > 0 ? 100.0 * week.Fresh / settings.WeeklyBudget.Value : null;

        // Burn rate over the current block (at least 10 minutes, so one burst isn't extrapolated).
        double perHour = 0;
        DateTime? projected = null;
        if (current is not null)
        {
            var hours = Math.Max((nowUtc - current.StartUtc).TotalHours, 10.0 / 60);
            perHour = currentFresh / hours;
            if (!isPeak && limit > 0 && currentFresh >= limit)
            {
                projected = nowUtc;
            }
            else if (!isPeak && perHour > 0)
            {
                var at = nowUtc + TimeSpan.FromHours((limit - currentFresh) / perHour);
                if (at < current.EndUtc) projected = at;
            }
        }

        return new BudgetStatus(current, limit, isPeak, blockPercent, Sum(events.Where(e => e.AtUtc >= startOfTodayUtc)),
            week, settings.WeeklyBudget, weeklyPercent, perHour, projected, settings.WarnPercent);
    }

    private static TokenUsage Sum(IEnumerable<UsageEvent> events) =>
        events.Aggregate(default(TokenUsage), (s, e) => s + e.Usage);
}

/// <summary>
/// Keeps <c>~/.claude/rl-status.json</c> current, the file the global <c>rl-check.py</c> prompt hook
/// and the Launcher's status block already read but nothing had been writing.
/// </summary>
public sealed class RateLimitStatusWriter(string? path = null)
{
    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "rl-status.json");

    private string? lastWritten;
    private DateTime lastWrittenUtc;

    public void Write(BudgetStatus status)
    {
        // The file means "this much of a limit is used". With no budget there's no limit to report,
        // so withdraw a file this writer produced rather than leave stale numbers behind.
        if (status.BlockLimitIsPeak)
        {
            RemoveOwnFile();
            return;
        }

        var reset = status.CurrentBlock is { } b
            ? b.EndUtc.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture)
            : "no active block";
        var body = new Dictionary<string, object>
        {
            ["percent_used"] = Math.Round(status.BlockPercent, 1),
            ["reset_time"] = reset,
            ["tokens_remaining"] = status.BlockRemaining,
            ["basis"] = status.BlockLimitIsPeak ? "uncached tokens vs your 7-day peak 5h block" : "uncached tokens vs your 5h block budget",
            ["source"] = "MindAttic.Orchestrator",
        };
        var key = JsonSerializer.Serialize(body);
        // Readers show the file's age, so refresh it at least once a minute even when nothing changed.
        if (key == lastWritten && DateTime.UtcNow - lastWrittenUtc < TimeSpan.FromMinutes(1)) return;

        body["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, Path, overwrite: true);
            lastWritten = key;
            lastWrittenUtc = DateTime.UtcNow;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void RemoveOwnFile()
    {
        lastWritten = null;
        try
        {
            if (!File.Exists(Path)) return;
            using (var doc = JsonDocument.Parse(File.ReadAllText(Path)))
            {
                if (!doc.RootElement.TryGetProperty("source", out var s) || s.GetString() != "MindAttic.Orchestrator") return;
            }
            File.Delete(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }
}
