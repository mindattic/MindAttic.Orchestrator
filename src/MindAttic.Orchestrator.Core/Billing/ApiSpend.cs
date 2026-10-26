using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Billing;

/// <summary>
/// What a Claude response costs on the API, in US dollars per million tokens (M.ORCH-T906), by model family: Anthropic's
/// first-party list prices, cache writes at 1.25x input. An estimate from the transcripts' token counts, not Anthropic's bill:
/// the real balance isn't read anywhere yet.
/// </summary>
public static class ApiPricing
{
    /// <summary>(input, output, cache write, cache read) dollars per million tokens.</summary>
    public sealed record Rates(double Input, double Output, double CacheWrite, double CacheRead);

    public static readonly Rates Opus = new(4.00, 20.00, 5.00, 0.20);
    public static readonly Rates Sonnet = new(2.00, 10.00, 2.50, 0.20);
    public static readonly Rates Haiku = new(0.10, 0.50, 0.125, 0.01);
    public static readonly Rates Fable = new(10.00, 50.00, 12.50, 0.25);

    /// <summary>The rates for <paramref name="model"/> (a model id or family); an unknown one is priced as Opus, the default
    /// model, so spend is never under-counted.</summary>
    public static Rates For(string? model) => Scheduling.ModelChoice.Family(model) switch
    {
        "sonnet" => Sonnet,
        "haiku" => Haiku,
        "fable" => Fable,
        _ => Opus,
    };

    /// <summary>What <paramref name="usage"/> costs on <paramref name="model"/>, in dollars.</summary>
    public static double Cost(string? model, TokenUsage usage)
    {
        var r = For(model);
        return (usage.Input * r.Input + usage.Output * r.Output + usage.CacheCreation * r.CacheWrite + usage.CacheRead * r.CacheRead) / 1_000_000d;
    }
}

/// <summary>One point of the API key's burndown: what it had spent, in all since its credit started, by <see cref="AtUtc"/>.</summary>
public sealed record ApiSpendPoint(DateTime AtUtc, double SpentUsd);

/// <summary>
/// The API key person's money (M.ORCH-T906): its starting credit (the <c>ApiCreditUsd</c> setting, entered by you with
/// <c>orchestrator billing credit &lt;usd&gt;</c>; null until then), what its CLIs have spent since, today and at the recent
/// rate, and the burndown to draw. It has no 5-hour or weekly window: this is all that limits it.
/// </summary>
public sealed record ApiSpend(double? CreditUsd, DateTime? SinceUtc, double SpentUsd, double TodayUsd, double PerHourUsd, IReadOnlyList<ApiSpendPoint> Burn)
{
    /// <summary>What's left of the credit; null while no credit is set.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double? LeftUsd => CreditUsd is { } c ? c - SpentUsd : null;

    /// <summary>Whether the credit is spent: no CLI on the key should take more work.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool OutOfCredit => LeftUsd is <= 0;

    /// <summary>When the credit runs out at <see cref="PerHourUsd"/>; null with no credit, nothing being spent, or none left.</summary>
    public DateTime? RunsOutUtc(DateTime nowUtc) =>
        LeftUsd is > 0 and var left && PerHourUsd > 0 ? nowUtc + TimeSpan.FromHours(Math.Min(left / PerHourUsd, 24 * 365)) : null;

    /// <summary>"$12.34", the way every money figure is shown.</summary>
    public static string Usd(double usd) =>
        Math.Abs(usd) >= 100 ? usd.ToString("$#,0", CultureInfo.InvariantCulture) : usd.ToString("$#,0.00", CultureInfo.InvariantCulture);

    /// <summary>One line for <c>orchestrator billing</c> and the dashboard: "$37.66 left of $50.00 · $12.34 spent ($1.20 today)".</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Line =>
        (LeftUsd is { } left ? $"{Usd(left)} left of {Usd(CreditUsd!.Value)} · " : "no starting credit set (orchestrator billing credit <usd>) · ") +
        $"{Usd(SpentUsd)} spent{(SinceUtc is { } s ? $" since {s.ToLocalTime():MMM d}" : "")} ({Usd(TodayUsd)} today, {Usd(PerHourUsd)}/h lately)";

    /// <summary>The window the recent rate is read over.</summary>
    public static readonly TimeSpan RateWindow = TimeSpan.FromHours(24);
}

/// <summary>
/// What the API key person's CLIs have spent, kept across restarts (M.ORCH-T906): per session, per hour, the most its transcript
/// has shown, so the burndown outlives the transcripts' week-long window. A JSON file beside the settings (<c>api-spend.json</c>);
/// reading or writing it never throws.
/// </summary>
public sealed class ApiSpendLedger(string path)
{
    public const string FileName = "api-spend.json";

    /// <summary>How long an hour's spend is kept with no credit date to bound it.</summary>
    public static readonly TimeSpan Keep = TimeSpan.FromDays(120);

    public string Path { get; } = path;

    /// <summary>Session → hour (Unix seconds at its start) → dollars.</summary>
    private Dictionary<string, Dictionary<long, double>>? hours;

    private Dictionary<string, Dictionary<long, double>> Load()
    {
        if (hours is not null) return hours;
        hours = new(StringComparer.Ordinal);
        try
        {
            if (File.Exists(Path) && JsonNode.Parse(File.ReadAllText(Path)) is JsonObject root && root["sessions"] is JsonObject sessions)
                foreach (var (id, node) in sessions)
                    if (node is JsonObject buckets)
                    {
                        var map = new Dictionary<long, double>();
                        foreach (var (hour, value) in buckets)
                            if (long.TryParse(hour, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && value is JsonValue v && v.TryGetValue<double>(out var usd))
                                map[h] = usd;
                        hours[id] = map;
                    }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        return hours;
    }

    /// <summary>Folds what <paramref name="sessions"/> (the API key person's) show now into the ledger: each hour keeps the most
    /// it has been seen to cost. True when anything changed.</summary>
    public bool Observe(IEnumerable<TranscriptSession> sessions, DateTime nowUtc)
    {
        var all = Load();
        var changed = false;
        foreach (var s in sessions)
        {
            if (s.Events.Count == 0) continue;
            if (!all.TryGetValue(s.SessionId, out var map)) all[s.SessionId] = map = [];
            foreach (var g in s.Events.GroupBy(e => HourOf(e.AtUtc)))
            {
                var usd = g.Sum(e => ApiPricing.Cost(s.Model, e.Usage));
                if (!map.TryGetValue(g.Key, out var had) || usd > had + 1e-9) { map[g.Key] = usd; changed = true; }
            }
        }
        var oldest = HourOf(nowUtc - Keep);
        foreach (var (id, map) in all.ToList())
        {
            foreach (var h in map.Keys.Where(h => h < oldest).ToList()) { map.Remove(h); changed = true; }
            if (map.Count == 0) { all.Remove(id); changed = true; }
        }
        return changed;
    }

    /// <summary>Writes the ledger; false when it couldn't be.</summary>
    public bool Save()
    {
        try
        {
            var sessions = new JsonObject();
            foreach (var (id, map) in Load())
            {
                var buckets = new JsonObject();
                foreach (var (h, usd) in map.OrderBy(x => x.Key)) buckets[h.ToString(CultureInfo.InvariantCulture)] = Math.Round(usd, 6);
                sessions[id] = buckets;
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = $"{Path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(temp, new JsonObject { ["sessions"] = sessions }.ToJsonString(), new UTF8Encoding(false));
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The spend since <paramref name="sinceUtc"/> (null: everything kept) against <paramref name="creditUsd"/>.</summary>
    public ApiSpend Spend(double? creditUsd, DateTime? sinceUtc, DateTime nowUtc, DateTime startOfTodayUtc)
    {
        var from = sinceUtc is { } s ? HourOf(s) : long.MinValue;
        var byHour = Load().Values.SelectMany(m => m).Where(x => x.Key >= from)
            .GroupBy(x => x.Key).Select(g => (Hour: g.Key, Usd: g.Sum(x => x.Value))).OrderBy(x => x.Hour).ToList();
        var spent = byHour.Sum(x => x.Usd);
        var today = byHour.Where(x => x.Hour >= HourOf(startOfTodayUtc)).Sum(x => x.Usd);
        var recentFrom = HourOf(nowUtc - ApiSpend.RateWindow);
        var perHour = byHour.Where(x => x.Hour >= recentFrom).Sum(x => x.Usd) / ApiSpend.RateWindow.TotalHours;
        var burn = new List<ApiSpendPoint>();
        var running = 0d;
        if (sinceUtc is { } since) burn.Add(new ApiSpendPoint(since, 0));
        foreach (var (hour, usd) in byHour)
        {
            running += usd;
            var end = At(hour) + TimeSpan.FromHours(1);
            burn.Add(new ApiSpendPoint(end > nowUtc ? nowUtc : end, running));
        }
        return new ApiSpend(creditUsd, sinceUtc, spent, today, perHour, burn);
    }

    private static long HourOf(DateTime utc) => (long)Math.Floor((utc - DateTime.UnixEpoch).TotalHours) * 3600;

    private static DateTime At(long hour) => DateTime.UnixEpoch.AddSeconds(hour);
}
