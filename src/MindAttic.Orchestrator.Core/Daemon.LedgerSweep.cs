using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

public sealed partial class Daemon
{
    private DateTime? ledgerSweptUtc;
    private Task<Activity.LedgerSweepResult>? ledgerSweep;
    private string ledgerSweepSaid = "";

    // What a dry run already wrote to the disk-cleanup log, so the same plan isn't written at every look. Touched by the
    // sweep's task while it runs, and by the tick only between sweeps.
    private readonly HashSet<string> ledgerSweepLogged = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The ledger sweep (M.ORCH-T1677, <see cref="Activity.LedgerSweeper"/>): on start and at the session-temp prune's cadence
    /// (<see cref="OrchestratorSettings.SessionTempPruneMinutes"/>), reclaims ledger entries whose owner is dead or finished,
    /// crash leftovers in %TEMP% and stale bin/obj in the daemon's worktrees. It walks folders and asks the ticket database
    /// and git, so it runs off the tick, one at a time; what it did is said at the tick after it finishes. With
    /// <see cref="OrchestratorSettings.LedgerSweepDryRun"/> (the default) it deletes nothing and writes what it would to the
    /// disk-cleanup log.
    /// </summary>
    private void SweepLedgerIfDue(string workspaceRoot)
    {
        if (ledgerSweep is { IsCompleted: true } done)
        {
            ledgerSweep = null;
            if (done.Status == TaskStatus.RanToCompletion) ReportLedgerSweep(done.Result);
            else Say(LogEventLevel.Warning, $"ledger sweep: {done.Exception?.GetBaseException().Message ?? "it was stopped"} (trying again at the next look)");
        }
        if (ledgerSweep is not null) return;
        if (ledgerSweptUtc is { } at && DateTime.UtcNow - at < OrchestratorSettings.SecondsSpan(tunables.SessionTempPruneMinutes * 60)) return;
        ledgerSweptUtc = DateTime.UtcNow;
        if (ledgerSweepLogged.Count > 4096) ledgerSweepLogged.Clear();
        var options = Activity.LedgerSweepOptions.From(tunables, TempDir) with { LogPath = Activity.DiskCleanup.LogPath, Logged = ledgerSweepLogged };
        ledgerSweep = Task.Run(() =>
        {
            var owners = Activity.LedgerOwners.Live(store, workspaceRoot, SessionLiveness.Current(workspaceRoot));
            return Activity.LedgerSweeper.Sweep(new Activity.ResourceLedger(store.Db), owners, options);
        });
    }

    /// <summary>Says a sweep that deleted something, and a dry run's plan when it changes (not at every look).</summary>
    private void ReportLedgerSweep(Activity.LedgerSweepResult r)
    {
        foreach (var e in r.Errors.Take(5)) Say(LogEventLevel.Warning, $"ledger sweep: {e}");
        var going = r.Going;
        if (going.Count == 0) { ledgerSweepSaid = ""; return; }
        var text = Activity.LedgerSweeper.Describe(r);
        if (r.DryRun)
        {
            var plan = string.Join("|", going.Select(i => i.Path).Order(StringComparer.OrdinalIgnoreCase));
            if (plan == ledgerSweepSaid) return;
            ledgerSweepSaid = plan;
            text += " Nothing was deleted: orchestrator disk-cleanup --ledger lists it, --ledger-run deletes it, settings --ledger-sweep-dry-run false lets the daemon.";
        }
        Say(LogEventLevel.Information, text);
    }
}
