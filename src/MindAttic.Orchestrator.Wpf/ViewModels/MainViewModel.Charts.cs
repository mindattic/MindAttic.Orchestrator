using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The person page's weekly chart (M.ORCH-T1403), alongside the existing 5-hour one: every Claude login but a solo Ultra
/// account (no Team/Enterprise org backing it, so <see cref="AccountRoster.PlanOf"/> gives it exactly "Ultra") shows both its
/// 5-hour and its weekly chart; a solo Ultra account shows only its week. The API key's person is unchanged (its credit
/// burndown, never these).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Whether this plan's own 5-hour chart shows: every signed-in Claude login but a solo Ultra account, which has
    /// only a weekly limit worth charting. Shown by default (true) before the plan is known, so a fresh sign-in isn't held
    /// back from its 5-hour chart on a guess.</summary>
    public bool ShowsFiveHourChart => ShowsClaudeMeters && OwnRow?.Plan != "Ultra";

    /// <summary>Whether this plan's own weekly chart shows: every signed-in Claude login, Team seat or solo Ultra alike -- only
    /// the API key's person (no weekly window of its own) has none.</summary>
    public bool ShowsWeeklyChart => ShowsClaudeMeters;

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
