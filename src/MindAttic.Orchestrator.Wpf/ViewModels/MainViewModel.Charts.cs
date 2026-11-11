using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
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

    // ── which chart the utilization panel draws (M.ORCH-T1395) ────────────

    /// <summary>The 5hr/Weekly limit boxes are toggles picking the one chart the panel draws: 5hr by default, remembered per
    /// tab for the dashboard's run. Both series are already in the snapshot, so a switch only swaps which card shows.</summary>
    private bool weeklyChartSelected;

    /// <summary>The Weekly box reads as selected: picked, or the plan has no 5-hour chart (solo Ultra, whose 5hr box is
    /// hidden, so the week is all there is to draw).</summary>
    public bool WeeklyChartSelected => weeklyChartSelected || !ShowsFiveHourChart;

    /// <summary>The 5hr box reads as selected.</summary>
    public bool FiveHourChartSelected => !WeeklyChartSelected;

    /// <summary>The 5-hour chart shows: the plan has one (<see cref="ShowsFiveHourChart"/>) and its box is selected.</summary>
    public bool ShowsFiveHourSection => ShowsFiveHourChart && !weeklyChartSelected;

    /// <summary>The weekly chart shows: the login has a week (<see cref="ShowsWeeklyChart"/>) and its box is selected.</summary>
    public bool ShowsWeeklySection => ShowsWeeklyChart && WeeklyChartSelected;

    private ICommand? showFiveHourChartCommand, showWeeklyChartCommand;

    /// <summary>The 5hr box: draw the 5-hour chart.</summary>
    public ICommand ShowFiveHourChartCommand => showFiveHourChartCommand ??= new RelayCommand(() => SelectWeeklyChart(false));

    /// <summary>The Weekly box: draw the weekly chart.</summary>
    public ICommand ShowWeeklyChartCommand => showWeeklyChartCommand ??= new RelayCommand(() => SelectWeeklyChart(true));

    internal void SelectWeeklyChart(bool weekly)
    {
        if (weeklyChartSelected == weekly) return;
        weeklyChartSelected = weekly;
        foreach (var name in new[] { nameof(FiveHourChartSelected), nameof(WeeklyChartSelected), nameof(ShowsFiveHourSection), nameof(ShowsWeeklySection) })
            Raise(name);
    }

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
