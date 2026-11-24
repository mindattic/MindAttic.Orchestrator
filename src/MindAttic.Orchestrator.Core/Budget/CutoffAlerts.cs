using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>A login that reached its own usage cutoff: what to tell the owner (alert and Psst text).</summary>
public sealed record CutoffCrossing(string Email, string Name, bool Weekly, string Text);

/// <summary>What the database keeps (<see cref="CutoffAlerts.StateName"/>): per login and window, the reset of the window last alerted.</summary>
public sealed class CutoffAlertBook
{
    /// <summary>Gets or sets the alerted.</summary>
    public Dictionary<string, DateTime> Alerted { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Once per crossing (M.ORCH-T742): a login at or past its weekly or 5-hour cutoff (<see cref="UsageCutoffs"/>) is reported once
/// for that window; it can be reported again only after the window has rolled over (its reset moved on). The record is kept
/// in the database, so a restart doesn't say it twice. The weekly crossing is reported even for a login the owner let past its
/// weekly cutoff (<c>org allow</c>), since <see cref="LoginLocks.AutoLock"/> locks it there regardless (epic M.ORCH-T2108).
/// </summary>
public sealed class CutoffAlerts(JsonStore? store)
{
    /// <summary>The state name constant.</summary>
    public const string StateName = "usage-cutoff-alerts";

    /// <summary>A reset this much later than the one alerted is a new window (readings of one window jitter by seconds).</summary>
    private static readonly TimeSpan NewWindow = TimeSpan.FromHours(1);

    private readonly CutoffAlertBook book = store is null ? new() : Safe(store);

    private static CutoffAlertBook Safe(JsonStore s)
    {
        try { return s.Load<CutoffAlertBook>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return new(); }
    }

    /// <summary>The crossings of <paramref name="email"/>'s weekly and 5-hour cutoffs in <paramref name="usage"/> not yet reported
    /// for their window; each is recorded as reported. <paramref name="allowedPastWeek"/> (an active <c>org allow</c>) only adds
    /// to the weekly text that the allowance didn't hold off the lock.</summary>
    public IReadOnlyList<CutoffCrossing> Check(string email, string name, ReportedUsage? usage, int weeklyCutoff, int fiveHourCutoff, bool allowedPastWeek, DateTime nowUtc)
    {
        var found = new List<CutoffCrossing>();
        var who = email.IndexOf('@') is > 0 and var at ? email[..at] : email;
        if (ReportedUsage.Usable(usage?.Week, nowUtc) is { } week && week.Percent >= weeklyCutoff && Fresh(email, "week", week.ResetUtc))
            found.Add(new(email, name, true,
                $"{name} reached its weekly cutoff ({Pct(week.Percent)}% of the week, cutoff {weeklyCutoff}%) and is now locked" +
                (allowedPastWeek ? " (a lock wins over its org allow)" : "") + ": right-click Unlock, or " +
                $"orchestrator org unlock {who}; to change the cutoff: orchestrator org limit {who} --weekly <pct>."));
        if (ReportedUsage.Usable(usage?.FiveHour, nowUtc) is { } five && five.Percent >= fiveHourCutoff && Fresh(email, "5h", five.ResetUtc))
            found.Add(new(email, name, false,
                $"{name} reached its 5-hour cutoff ({Pct(five.Percent)}% of the window, cutoff {fiveHourCutoff}%) and is cut off until the window resets at " +
                $"{five.ResetUtc.ToLocalTime():h:mm tt}. To change the cutoff: orchestrator org limit {who} --5h <pct>."));
        return found;
    }

    private bool Fresh(string email, string kind, DateTime resetUtc)
    {
        var key = $"{email}|{kind}";
        if (book.Alerted.TryGetValue(key, out var last) && resetUtc - last < NewWindow) return false;
        book.Alerted[key] = resetUtc;
        if (store is null) return true;
        try { store.Update<CutoffAlertBook>(StateName, b => b.Alerted[key] = resetUtc); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        return true;
    }

    private static string Pct(double v) => v.ToString("0", CultureInfo.InvariantCulture);
}
