using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

public sealed partial class Daemon
{
    private FeatureGate? featureGate;
    private volatile bool featureGateSuiteRunning;

    /// <summary>
    /// The feature test gate (M.ORCH-T2398): an owner-assigned branch whose tasks are all done gets its repo's full suite on main,
    /// no LLM, by the daemon that runs the main-line suites (<paramref name="mayStart"/>), never while one of those runs or the
    /// machine is held. Finished runs are reported whoever holds the lease.
    /// </summary>
    private void RunFeatureGate(bool mayStart, bool suiteRunning, Snapshot snapshot, OrchestratorSettings s, string workspaceRoot)
    {
        featureGateSuiteRunning = suiteRunning;
        try
        {
            var gate = featureGate ??= new FeatureGate(store)
            {
                Held = () => MachineHeld() ?? (featureGateSuiteRunning ? "a main-line suite is running" : null),
            };
            gate.Timeout = OrchestratorSettings.MinutesSpan(s.FeatureGateTimeoutMinutes, MainlineTests.SuiteTimeout);
            gate.CheckEvery = OrchestratorSettings.SecondsSpan(s.FeatureGateCheckSeconds, 5);
            gate.LockRetries = (int)Math.Clamp(s.MainTestsLockRetries, 0, 10);
            gate.LockRetryDelay = OrchestratorSettings.SecondsSpan(s.MainTestsLockRetrySeconds, 0);
            var repos = snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('('))
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, g.First().Path));
            foreach (var line in gate.Tick(repos, s.WorktreeRoot, () => MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot), DateTime.UtcNow, mayStart))
                Say(line.Warn ? LogEventLevel.Warning : LogEventLevel.Information, line.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            Say(LogEventLevel.Warning, $"feature gate: {ex.Message}");
        }
    }
}
