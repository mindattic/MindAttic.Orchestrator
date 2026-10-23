using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// One window weekly-aware pacing planned (M.ORCH-T669): a login's 5-hour window or week, where it was planned to end
/// (<see cref="WeeklyCap.PlannedEndPercent"/>, <see cref="WeeklyCap.PlannedWeekEndPercent"/>) and where it did, so the curve can be
/// dialed in. <see cref="ReachedUtc"/> is when it first reached its plan (null: it never did), which against <see cref="EndUtc"/>
/// says how early it ran out.
/// </summary>
public sealed record PlannedWindow(string Login, string Kind, DateTime EndUtc, double PlannedPercent, double ActualPercent, DateTime? ReachedUtc, bool Closed = false)
{
    public const string FiveHour = "5-hour", Weekly = "weekly";

    /// <summary>"planned 31%, ended at 29%" (or "at 29% so far" while it runs), and when it reached its plan.</summary>
    public string Text(DateTime nowUtc)
    {
        var line = $"planned {Pct(PlannedPercent)}, " + (Closed || EndUtc <= nowUtc ? $"ended at {Pct(ActualPercent)}" : $"at {Pct(ActualPercent)} so far");
        if (ReachedUtc is { } at)
            line += EndUtc - at is var early && early > TimeSpan.FromMinutes(1)
                ? $"; reached it {Formatting.Span(early)} before its reset" : "; reached it as it reset";
        return line;
    }

    private static string Pct(double p) => p.ToString("0", CultureInfo.InvariantCulture) + "%";
}

/// <summary>
/// The log of every window weekly-aware pacing planned, planned against actual end percent (M.ORCH-T669), kept in this machine's
/// <c>state</c> row <see cref="StateName"/> (the readings it's built from are this machine's too), shown by <c>orchestrator ledger</c>,
/// and a line in the log as each window closes.
/// </summary>
public static class PacePlan
{
    public const string StateName = "pace-plan";

    /// <summary>The rows kept: a few weeks of every login's windows.</summary>
    public const int Keep = 400;

    /// <summary>A reading this close under the plan counts as reaching it (Claude reports whole percents).</summary>
    public const double Reach = 0.5;

    /// <summary>
    /// Records what <paramref name="week"/> plans for <paramref name="login"/>'s current 5-hour window and week, and where they stand
    /// (<paramref name="b"/>), in <paramref name="rows"/>; closes the rows whose window ended and returns a line for each, for the
    /// log. Returns whether anything changed. A window is recorded only while it's planned (the week taken in); once recorded its
    /// actual percent is kept up to date till it ends.
    /// </summary>
    public static bool Observe(List<PlannedWindow> rows, string login, BudgetStatus b, WeeklyCap? week, DateTime nowUtc, List<string> closed)
    {
        var changed = false;
        if (b.CurrentBlock is { } block && block.IsActive(nowUtc) && !b.BlockLimitIsPeak)
            changed |= Track(rows, login, PlannedWindow.FiveHour, block.EndUtc, week?.CeilingPercent is null ? null : week.PlannedEndPercent, b.BlockPercent, nowUtc);
        if (b.WeekResetUtc is { } reset && b.WeeklyPercent is { } w)
            changed |= Track(rows, login, PlannedWindow.Weekly, reset, week?.PlannedWeekEndPercent, w, nowUtc);
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Closed || rows[i].EndUtc > nowUtc) continue;
            rows[i] = rows[i] with { Closed = true };
            closed.Add($"pace plan [{rows[i].Login}]: the {rows[i].Kind} window to {rows[i].EndUtc.ToLocalTime():MMM d HH:mm} {rows[i].Text(nowUtc)}");
            changed = true;
        }
        if (rows.Count > Keep)
        {
            rows.Sort((x, y) => x.EndUtc.CompareTo(y.EndUtc));
            rows.RemoveRange(0, rows.Count - Keep);
            changed = true;
        }
        return changed;
    }

    private static bool Track(List<PlannedWindow> rows, string login, string kind, DateTime end, double? planned, double actual, DateTime nowUtc)
    {
        // Claude rounds resets to the 10 minutes: within that it's the same window.
        var i = rows.FindIndex(r => r.Kind == kind && r.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && Math.Abs((r.EndUtc - end).TotalMinutes) <= 10);
        if (i < 0 && planned is null) return false;
        var was = i < 0 ? null : rows[i];
        if (was is { Closed: true }) return false;
        var plan = Math.Round(planned ?? was!.PlannedPercent, 1);
        var at = Math.Round(Math.Max(actual, was?.ActualPercent ?? 0), 1);
        var reached = was?.ReachedUtc ?? (at >= plan - Reach ? nowUtc : null);
        var row = new PlannedWindow(login, kind, was?.EndUtc ?? end, plan, at, reached);
        if (row == was) return false;
        if (i < 0) rows.Add(row); else rows[i] = row;
        return true;
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static string Serialize(IEnumerable<PlannedWindow> rows) => JsonSerializer.Serialize(rows.OrderBy(r => r.EndUtc).ToList(), Json);

    /// <summary>The rows <paramref name="db"/> keeps, oldest first; none when there are none or they can't be read.</summary>
    public static List<PlannedWindow> Load(OrchestratorDb db)
    {
        try { return db.LoadState(StateName) is { } row ? JsonSerializer.Deserialize<List<PlannedWindow>>(row.Value, Json) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Data.Common.DbException or InvalidOperationException) { return []; }
    }
}
