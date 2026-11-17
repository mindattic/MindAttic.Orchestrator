using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The person page's compact utilization preview (M.ORCH-T1670, replacing the picked pair from M.ORCH-T1403/M.ORCH-T1395):
/// one small chart, chosen by plan rather than by a click, since the dashboard already shows both live percentages on the
/// tab strip and the login list (LoginUsageBarsTemplate) and a second, user-picked copy here was pure repetition. Every
/// Claude login but a solo Ultra account (no Team/Enterprise org backing it, so <see cref="AccountRoster.PlanOf"/> gives it
/// exactly "Ultra") gets its 5-hour chart, with its target line and dotted projection previewing where the recent rate
/// crosses the cutoff; a solo Ultra account, whose 5-hour window isn't the one worth charting, gets its weekly chart
/// instead, with the same preview over the week. The API key's person is unchanged (its credit burndown, never these).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Whether this plan's own 5-hour chart shows: every signed-in Claude login but a solo Ultra account, which has
    /// only a weekly limit worth charting. Shown by default (true) before the plan is known, so a fresh sign-in isn't held
    /// back from its 5-hour chart on a guess.</summary>
    public bool ShowsFiveHourChart => ShowsClaudeMeters && HasFiveHourWindow;

    /// <summary>This login's plan: its roster row's, else the one its own login folder's sign-in names (M.ORCH-T1751: a login
    /// the roster has no row for read as "plan unknown", so a solo Ultra login kept its 5-hour figure in the sidebar).</summary>
    internal string? OwnPlan => OwnRow?.Plan ?? (CutoffEmail is { } email && Root.loginPlans.TryGetValue(email, out var plan) ? plan : null);

    /// <summary>Whether this login has a 5-hour window at all: every plan but a solo Ultra account. True while the plan isn't known.</summary>
    internal bool HasFiveHourWindow => OwnPlan != "Ultra";

    /// <summary>Whether this plan's own weekly chart shows: a solo Ultra account only, whose cutoff to preview is the week,
    /// not a 5-hour window it doesn't have.</summary>
    public bool ShowsWeeklyChart => ShowsClaudeMeters && !ShowsFiveHourChart;

    // ── which chart the compact utilization panel draws (M.ORCH-T1670: by plan, not a user-picked pair) ──

    /// <summary>The 5-hour chart shows: every plan but a solo Ultra account.</summary>
    public bool ShowsFiveHourSection => ShowsFiveHourChart;

    /// <summary>The weekly chart shows: a solo Ultra account only.</summary>
    public bool ShowsWeeklySection => ShowsWeeklyChart;

    /// <summary>The current week against its pace, for the weekly graph; null before the first snapshot or without a weekly
    /// reset known yet (<see cref="WeeklyUtilizationSeries"/>, built the same way for every login, Team seat or solo Ultra).</summary>
    public Utilization? WeeklyUtilization => snapshot?.WeeklyUtilization;

    /// <summary>The weekly graph in words (it's also the card's accessible name).</summary>
    public string WeeklyUtilizationText => WeeklyUtilizationCaption(WeeklyUtilization, snapshot is not null, LoginName);

    internal static string WeeklyUtilizationCaption(Utilization? utilization, bool loaded, string login = "this login") => utilization switch
    {
        null => loaded ? "No weekly reset known yet, so there's no pace to draw this week against." : "",
        { HasLastWindow: true } u =>
            $"No week running: the last ended at {Formatting.ResetTime(u.EndUtc, DateTime.UtcNow)} at {Formatting.FiveHourPercent(u.NowPercent)}; " +
            $"the next starts with {login}'s next prompt.",
        { Ended: true } => "No week running: the next starts with your next prompt.",
        { } u => RunningWeek(u),
    };

    private static string RunningWeek(Utilization u) =>
        $"{Formatting.FiveHourPercent(u.NowPercent)} of the week used, {(u.OverNow ? "over" : "under")} pace ({u.PaceAt(u.NowUtc):0}% by now). " +
        $"At the recent rate it ends near {u.ProjectedEndPercent:0}% at {Formatting.ResetTime(u.EndUtc, u.NowUtc)}; the target is {u.TargetPercent:0}%.";
}
