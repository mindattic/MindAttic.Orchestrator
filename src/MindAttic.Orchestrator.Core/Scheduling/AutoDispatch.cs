using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The opt-in loop: when <see cref="OrchestratorSettings.AutoDispatch"/> is on, start the top
/// proposal, at most one per interval, so a burst of free slots can't open a dozen tabs at once.
/// Tabs open with the prompt pre-filled and unsent, so nothing spends tokens until you press Enter.
/// </summary>
public sealed class AutoDispatch(Dispatcher dispatcher, TimeSpan? interval = null)
{
    private readonly TimeSpan interval = interval ?? TimeSpan.FromMinutes(1);
    private DateTime lastUtc = DateTime.MinValue;

    public Dispatcher.Result? Tick(SchedulePlan plan, OrchestratorSettings settings, DateTime nowUtc)
    {
        if (!settings.AutoDispatch || plan.Proposals.Count == 0 || nowUtc - lastUtc < interval) return null;
        var p = plan.Proposals[0];
        lastUtc = nowUtc;
        return dispatcher.Dispatch(p.Item, settings.DefaultProvider, p.UseWorktree);
    }
}
