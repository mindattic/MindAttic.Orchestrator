using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The opt-in loop: when <see cref="OrchestratorSettings.AutoDispatch"/> is on, start the top
/// proposal, at most one per interval, so a burst of free slots can't open a dozen tabs at once.
/// Tabs open with the prompt pre-filled and unsent, so nothing spends tokens until you press Enter,
/// except in <see cref="OrchestratorSettings.AutoSubmitRepos"/>, where the tab submits it by itself.
/// With a <see cref="DispatchLease"/>, only the process holding it dispatches.
/// </summary>
public sealed class AutoDispatch(Dispatcher dispatcher, TimeSpan? interval = null, DispatchLease? lease = null, Func<string?>? busy = null)
{
    private readonly TimeSpan interval = interval ?? TimeSpan.FromMinutes(1);
    private readonly Func<string?> busy = busy ?? Sessions.Foreground.Busy;
    private DateTime lastUtc = DateTime.MinValue;
    private Task<bool>? lastCameUp;

    /// <summary>Why it's waiting to open a window (you're in a game or fullscreen app), if it is.</summary>
    public string? Deferred { get; private set; }

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
        // One unconfirmed tab at a time: a tab that never comes up must stop the run, not repeat every minute.
        if (lastCameUp is { IsCompleted: false }) return null;
        // Never take the screen from a game or a fullscreen app; the work waits until you're out of it.
        Deferred = busy();
        if (Deferred is not null) return null;
        var p = plan.Proposals[0];
        lastUtc = nowUtc;
        var result = dispatcher.Dispatch(p.Item, settings.DefaultProvider, p.UseWorktree, Submits(settings, p.Item.Repo));
        lastCameUp = result.CameUp;
        return result;
    }

    /// <summary>Whether auto-dispatch may start tickets in <paramref name="repo"/>: every repo when the allowlist is empty.</summary>
    public static bool Allowed(OrchestratorSettings settings, string repo) =>
        settings.DispatchRepos.Count == 0 || settings.DispatchRepos.Contains(repo, StringComparer.OrdinalIgnoreCase);

    /// <summary>The plan auto-dispatch acts on: <paramref name="plan"/> itself, or one proposing only allowed repos.</summary>
    public static SchedulePlan PlanFor(SchedulePlan plan, Snapshot snapshot, OrchestratorSettings settings, IReadOnlyList<Assignment> assignments) =>
        settings.DispatchRepos.Count == 0 ? plan : Scheduler.Plan(snapshot, settings, assignments, forAutoDispatch: true);

    /// <summary>Whether a tab auto-dispatch opens in <paramref name="repo"/> presses Enter by itself.</summary>
    public static bool Submits(OrchestratorSettings settings, string repo) =>
        settings.AutoSubmitRepos.Contains(repo, StringComparer.OrdinalIgnoreCase) || settings.AutoSubmitRepos.Contains("*");
}
