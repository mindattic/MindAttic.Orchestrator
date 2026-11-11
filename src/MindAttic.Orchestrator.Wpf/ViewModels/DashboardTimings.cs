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

    /// <summary>Takes the intervals from <paramref name="s"/>, each kept to at least a second so no timer spins.</summary>
    public static void Apply(OrchestratorSettings s)
    {
        Poll = OrchestratorSettings.SecondsSpan(s.DashboardPollSeconds, 1);
        HiddenPoll = OrchestratorSettings.SecondsSpan(s.DashboardHiddenPollSeconds, 1);
        HiddenHolderPoll = OrchestratorSettings.SecondsSpan(s.DashboardHiddenHolderPollSeconds, 1);
        TermResync = OrchestratorSettings.SecondsSpan(s.TermResyncSeconds, 1);
        LauncherSpacing = OrchestratorSettings.SecondsSpan(s.LauncherRespawnSeconds, 1);
        MeterStates = OrchestratorSettings.SecondsSpan(s.MeterStatesSeconds, 1);
    }
}
