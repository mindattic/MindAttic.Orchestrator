using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The dashboard's live-dialed intervals (M.ORCH-T1595): each was a constant, its value then the default here and in the Vault
/// setting it now comes from. The global login's tab applies the settings at each refresh (<see cref="Apply"/>, beside
/// EndedTabMinutes), and the timers read these, so an edit in MindAttic.Vault.Desktop takes effect at the next refresh without a
/// restart. Struct-valued: reading one allocates nothing.
/// </summary>
internal static class DashboardTimings
{
    /// <summary>Shown and polling: <see cref="OrchestratorSettings.DashboardPollSeconds"/>.</summary>
    public static TimeSpan Poll { get; private set; } = TimeSpan.FromSeconds(5);

    /// <summary>Minimized, no daemon status stream: <see cref="OrchestratorSettings.DashboardHiddenPollSeconds"/>.</summary>
    public static TimeSpan HiddenPoll { get; private set; } = TimeSpan.FromSeconds(60);

    /// <summary>Minimized but holding the status lease: <see cref="OrchestratorSettings.DashboardHiddenHolderPollSeconds"/>.</summary>
    public static TimeSpan HiddenHolderPoll { get; private set; } = TimeSpan.FromSeconds(15);

    /// <summary>The hosted-session registry's reread (M.ORCH-T1001): <see cref="OrchestratorSettings.TermResyncSeconds"/>.</summary>
    public static TimeSpan TermResync { get; private set; } = TimeSpan.FromSeconds(45);

    /// <summary>The least time between two Launchers for one person: <see cref="OrchestratorSettings.LauncherRespawnSeconds"/>.</summary>
    public static TimeSpan LauncherSpacing { get; private set; } = TimeSpan.FromSeconds(30);

    /// <summary>The login meters' reread: <see cref="OrchestratorSettings.MeterStatesSeconds"/>.</summary>
    public static TimeSpan MeterStates { get; private set; } = TimeSpan.FromSeconds(30);

    // The rest of the sweep (M.ORCH-T1624).

    /// <summary>The shortest gap from one tab blink to the next: <see cref="OrchestratorSettings.TabBlinkMinSpacingMs"/>.</summary>
    public static TimeSpan TabBlinkMinSpacing { get; private set; } = TimeSpan.FromMilliseconds(320);

    /// <summary>How long a read of the Open Project roster is kept: <see cref="OrchestratorSettings.ProjectsRefreshMinutes"/>.</summary>
    public static TimeSpan ProjectsRefresh { get; private set; } = TimeSpan.FromMinutes(10);

    /// <summary>How long a read of the projects' change counts is kept: <see cref="OrchestratorSettings.ProjectChangesRefreshSeconds"/>.</summary>
    public static TimeSpan ProjectChangesRefresh { get; private set; } = TimeSpan.FromSeconds(30);

    /// <summary>The closed tickets' reread with no pushed changes: <see cref="OrchestratorSettings.ClosedTicketsReadSeconds"/>.</summary>
    public static TimeSpan ClosedRead { get; private set; } = TimeSpan.FromMinutes(1);

    /// <summary>The same while ticket changes are pushed: <see cref="OrchestratorSettings.ClosedTicketsReadLiveMinutes"/>.</summary>
    public static TimeSpan ClosedReadLive { get; private set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long after an Enter a status read still saying idle ends a CLI tab's presumed busy (M.ORCH-T1413):
    /// <see cref="OrchestratorSettings.PresumeBusyConfirmSeconds"/>.</summary>
    public static TimeSpan PresumeConfirmAfter { get; private set; } = TimeSpan.FromSeconds(OrchestratorSettings.DefaultPresumeBusyConfirmSeconds);

    /// <summary>The longest a presumed busy lasts with no status read at all, the grace period (M.ORCH-T1413):
    /// <see cref="OrchestratorSettings.PresumeBusyMaxSeconds"/>.</summary>
    public static TimeSpan PresumeLife { get; private set; } = TimeSpan.FromSeconds(OrchestratorSettings.DefaultPresumeBusyMaxSeconds);

    /// <summary>Takes the intervals from <paramref name="s"/>, each kept to at least a second so no timer spins.</summary>
    public static void Apply(OrchestratorSettings s)
    {
        Poll = OrchestratorSettings.SecondsSpan(s.DashboardPollSeconds, 1);
        HiddenPoll = OrchestratorSettings.SecondsSpan(s.DashboardHiddenPollSeconds, 1);
        HiddenHolderPoll = OrchestratorSettings.SecondsSpan(s.DashboardHiddenHolderPollSeconds, 1);
        TermResync = OrchestratorSettings.SecondsSpan(s.TermResyncSeconds, 1);
        LauncherSpacing = OrchestratorSettings.SecondsSpan(s.LauncherRespawnSeconds, 1);
        MeterStates = OrchestratorSettings.SecondsSpan(s.MeterStatesSeconds, 1);
        // At least 50ms, so a refresh's blink queue stays bounded.
        TabBlinkMinSpacing = OrchestratorSettings.SecondsSpan(s.TabBlinkMinSpacingMs / 1000, 0.05);
        ProjectsRefresh = OrchestratorSettings.SecondsSpan(s.ProjectsRefreshMinutes * 60, 1);
        ProjectChangesRefresh = OrchestratorSettings.SecondsSpan(s.ProjectChangesRefreshSeconds, 1);
        ClosedRead = OrchestratorSettings.SecondsSpan(s.ClosedTicketsReadSeconds, 1);
        ClosedReadLive = OrchestratorSettings.SecondsSpan(s.ClosedTicketsReadLiveMinutes * 60, 1);
        PresumeConfirmAfter = OrchestratorSettings.SecondsSpan(s.PresumeBusyConfirmSeconds, 1);
        PresumeLife = OrchestratorSettings.SecondsSpan(s.PresumeBusyMaxSeconds, 1);
    }
}
