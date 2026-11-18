using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

public sealed partial class Daemon
{
    private MilestoneAcceptance? milestoneChecks;
    private Task<IReadOnlyList<MilestoneAcceptance.Line>>? milestonePass;
    private DateTime milestonesCheckedUtc = DateTime.MinValue;
    private MilestoneForecasts? milestoneForecasts;
    private DateTime milestonesForecastUtc = DateTime.MinValue;
    private MilestoneStewards? milestoneStewards;
    private volatile bool milestoneSuiteRunning;
    private volatile int milestoneHeavyLimit;
    private volatile int milestoneHeavyGrace = OrchestratorSettings.DefaultHeavyProcessGraceSeconds;

    /// <summary>
    /// The active and accepting milestones' build and exists checks, run on their own (M.ORCH-T1684), at the main-line suites'
    /// cadence (<see cref="MainlineTests.CheckEvery"/>) by the daemon that runs those (<paramref name="mayStart"/>). A pass runs
    /// off the tick, one at a time: with no such milestone it's one read of the ticket database. A build starts only while
    /// the machine isn't held (memory, disk), no main-line suite runs and the heavy-process lease is free. Once every
    /// <see cref="MilestoneForecasts.Every"/> the same pass also works out and records each one's forecast (M.ORCH-T1688), and
    /// then lets the steward look (M.ORCH-T1686), which acts on a milestone at most once every <see cref="MilestoneStewards.Every"/>.
    /// </summary>
    private void RunMilestoneChecks(bool mayStart, bool suiteRunning, Snapshot snapshot, OrchestratorSettings s, string workspaceRoot)
    {
        milestoneSuiteRunning = suiteRunning;
        milestoneHeavyLimit = s.HeavyProcessLimit;
        milestoneHeavyGrace = s.HeavyProcessGraceSeconds;
        if (milestonePass is { IsCompleted: true } done)
        {
            milestonePass = null;
            if (done.IsCompletedSuccessfully)
                foreach (var line in done.Result) Say(line.Warn ? LogEventLevel.Warning : LogEventLevel.Information, line.Text);
            else Say(LogEventLevel.Warning, $"milestone checks: {done.Exception?.GetBaseException().Message ?? "the run was cancelled"}");
        }
        var now = DateTime.UtcNow;
        if (!mayStart || milestonePass is not null || now - milestonesCheckedUtc < MainlineTests.CheckEvery) return;
        milestonesCheckedUtc = now;

        var paths = snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('('))
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Path, StringComparer.OrdinalIgnoreCase);
        var checks = milestoneChecks ??= new MilestoneAcceptance(store)
        {
            Held = () => MachineHeld() ?? (milestoneSuiteRunning ? "a main-line suite is running" : null)
                ?? MilestoneAcceptance.HeavyHeld(store.Db, milestoneHeavyLimit, milestoneHeavyGrace, DateTime.UtcNow),
        };
        checks.RepoPath = name => paths.GetValueOrDefault(name);
        checks.WorkspaceRoot = workspaceRoot;
        checks.WorktreeRoot = s.WorktreeRoot;
        checks.Timeout = MilestoneAcceptance.TimeoutOf(s);
        var forecasts = milestoneForecasts ??= new MilestoneForecasts(store);
        var stewards = milestoneStewards ??= new MilestoneStewards(store);
        var forecastDue = now - milestonesForecastUtc >= MilestoneForecasts.Every;
        if (forecastDue) milestonesForecastUtc = now;
        Func<MindAttic.Tickets.Data.TicketsDb?> open = () => MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot);
        milestonePass = Task.Factory.StartNew<IReadOnlyList<MilestoneAcceptance.Line>>(
            () => forecastDue ? [.. checks.RunDue(open), .. forecasts.RecordDue(open, DateTime.UtcNow), .. stewards.RunDue(open, DateTime.UtcNow)]
                : checks.RunDue(open),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
}
