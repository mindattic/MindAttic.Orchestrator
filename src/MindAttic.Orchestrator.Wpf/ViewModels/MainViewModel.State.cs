using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The member roster, the orgs, the settings and the reminders, pushed on the status bus by whichever process
/// changed them (M.ORCH-T891, <see cref="StateBus"/>): laid over what's shown the moment they change, so the dashboard needn't
/// reread the store for them while its stream is up. Each message is the whole of its kind; only this dashboard's own store's
/// are taken. Kept on the global login's tab; forgotten when the stream drops (the poll reads the store again then).
/// </summary>
public sealed partial class MainViewModel
{
    // The pending reminders as last pushed on this connection; null reads them from the store.
    private IReadOnlyList<Reminder>? pushedReminders;

    /// <summary>
    /// Takes one pushed payload on the UI thread. The reminders go straight to every tab; the roster and the orgs
    /// are kept for the tab split, and the settings reload an open, unedited Settings panel. Returns whether the tabs should be
    /// applied again (<see cref="ReapplyStateAsync"/>): the roster, the orgs or the settings changed what they show.
    /// </summary>
    public bool ApplyPushedState(StatePayload payload)
    {
        if (root is not null) return root.ApplyPushedState(payload);
        // Another store's (a test's scratch database, another workspace's daemon) says nothing of this one.
        if (!StateBus.SameStore(payload.Store, StateBus.StoreOf(store.Db))) return false;
        // Older than the one of its kind applied already (M.ORCH-T1002: two quick changes can arrive in either order): dropped.
        if (!NewerThanApplied(payload)) return false;
        switch (payload)
        {
            case MembersPayload m:
                lastMembers = m.Members;
                return true;
            case OrgsPayload o:
                lastOrgs = o.ToDirectory();
                return true;
            case RemindersPayload r:
                pushedReminders = r.Pending;
                var now = DateTime.UtcNow;
                // This tab first: every other one shows its lines.
                ApplyReminders(now);
                foreach (var t in LoginTabs)
                    if (!ReferenceEquals(t, this)) t.ApplyReminders(now);
                return false;
            case SettingsPayload:
                // Settings are edited in MindAttic.Vault.Desktop now (LAMP, M.ORCH-T1308); what reads them is applied again.
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies the snapshot shown to every tab again with the roster and orgs as last pushed, with no read of the published file:
    /// a member added or renamed gets or retitles their tab, a login moved between orgs moves, and what reads the settings is
    /// raised again. Hidden, nothing is applied (shown again, the window reads the store at once). Returns whether the caller
    /// should refresh instead: a read was under way, and may have read the store before the change.
    /// </summary>
    public async Task<bool> ReapplyStateAsync(bool hidden)
    {
        if (root is not null) return await root.ReapplyStateAsync(hidden);
        if (hidden || workspace is not { } shown || lastOrgs is not { } orgsNow) return false;
        if (refreshing) return true;
        var dirs = OwnLoginDirs();
        var members = lastMembers;
        // Each login's tab and its signed-in state, off the UI thread, as a read does it.
        var tabs = await Task.Run(() => LoginScope.Tabs(shown, dirs, members, lastApiKey));
        // A read or pushed figures that landed meanwhile applied the roster as it is now themselves.
        if (refreshing || !ReferenceEquals(workspace, shown)) return false;
        ApplyTabs(shown, tabs, lastOrgs ?? orgsNow);
        return false;
    }

    // When each kind's payload applied last was read from the store; kept across a dropped stream, since what comes again then is
    // the daemon's latest of each, no older than what was applied.
    private readonly Dictionary<Type, DateTime> appliedAt = [];

    /// <summary>Whether <paramref name="payload"/> was read no earlier than the last of its kind applied, noting it when so. One
    /// with no time (an older writer) is taken.</summary>
    internal bool NewerThanApplied(StatePayload payload)
    {
        if (payload.AtUtc == default) return true;
        var at = payload.AtUtc.ToUniversalTime();
        if (appliedAt.TryGetValue(payload.GetType(), out var held) && at < held) return false;
        appliedAt[payload.GetType()] = at;
        return true;
    }

    /// <summary>The stream dropped: the reminders are read from the store again until it's back.</summary>
    private void ForgetPushedState() => pushedReminders = null;
}
