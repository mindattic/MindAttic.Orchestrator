using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The opt-in loop: when <see cref="OrchestratorSettings.AutoDispatch"/> is on, start the top
/// proposal, at most one per interval, so a burst of free slots can't open a dozen tabs at once.
/// Tabs open with the prompt pre-filled and unsent, so nothing spends tokens until you press Enter.
/// With a <see cref="DispatchLease"/>, only the process holding it dispatches.
/// </summary>
public sealed class AutoDispatch(Dispatcher dispatcher, TimeSpan? interval = null, DispatchLease? lease = null)
{
    private readonly TimeSpan interval = interval ?? TimeSpan.FromMinutes(1);
    private DateTime lastUtc = DateTime.MinValue;

    /// <summary>Auto-dispatch is on, but another orchestrator process (usually the logon daemon) is doing it.</summary>
    public bool HeldElsewhere { get; private set; }

    /// <summary>The quiet-hours window it's pausing for, if any.</summary>
    public string? Quiet { get; private set; }

    public Dispatcher.Result? Tick(SchedulePlan plan, OrchestratorSettings settings, DateTime nowUtc)
    {
        if (!settings.AutoDispatch) { HeldElsewhere = false; Quiet = null; return null; }
        Quiet = QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime());
        if (Quiet is not null) return null;
        HeldElsewhere = lease is not null && !lease.TryAcquire();
        if (HeldElsewhere || plan.Proposals.Count == 0 || nowUtc - lastUtc < interval) return null;
        var p = plan.Proposals[0];
        lastUtc = nowUtc;
        return dispatcher.Dispatch(p.Item, settings.DefaultProvider, p.UseWorktree);
    }
}
