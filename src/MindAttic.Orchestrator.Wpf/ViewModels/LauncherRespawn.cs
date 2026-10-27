namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>What <see cref="LauncherRespawn.Decide"/> said about a person whose CLI tab just closed or ended.</summary>
public enum LauncherRespawnOutcome
{
    /// <summary>Their page has no live tab left: a new Launcher opens on their login.</summary>
    Spawn,
    /// <summary>Another of their tabs is still open: nothing to do.</summary>
    TabStillOpen,
    /// <summary>They aren't signed in: no Launcher, and no sign-in started for them.</summary>
    SignedOut,
    /// <summary>The dashboard is closing.</summary>
    ShuttingDown,
    /// <summary>The dashboard is still reattaching and restoring its tabs at start.</summary>
    Restoring,
    /// <summary>A Launcher was opened for them less than <see cref="LauncherRespawn.Spacing"/> ago (one that exits or fails at
    /// once would otherwise loop); the next refresh after the spacing opens it.</summary>
    Capped,
}

/// <summary>
/// M.ORCH-T1150: when a person's last open CLI tab is closed (its x, its close command) or its CLI exits, the dashboard opens a
/// new Launcher signed in as them, so a person's page is never left with no tab. This holds the decision and its cap: at most
/// one Launcher per person every <see cref="Spacing"/>, shared with the refresh's own reopen of a closed Launcher tab
/// (M.ORCH-T855), so a Launcher that dies at once can't spawn another every tick. Keyed by the person's tab key.
/// </summary>
public sealed class LauncherRespawn
{
    private readonly Dictionary<string, DateTime> lastOpened = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The least time between two Launchers opened for one person.</summary>
    public TimeSpan Spacing { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Whether a Launcher may open for <paramref name="key"/> now (its last one opened at least <see cref="Spacing"/>
    /// ago, or never); a yes records it as opened at <paramref name="nowUtc"/>.</summary>
    public bool TryTake(string key, DateTime nowUtc)
    {
        if (lastOpened.TryGetValue(key, out var last) && nowUtc - last < Spacing) return false;
        lastOpened[key] = nowUtc;
        return true;
    }

    /// <summary>
    /// A tab of <paramref name="key"/>'s just closed or ended: <see cref="LauncherRespawnOutcome.Spawn"/> (and the open recorded)
    /// when they're signed in, none of their tabs is still live, the dashboard is neither closing nor restoring, and their last
    /// Launcher opened at least <see cref="Spacing"/> ago; otherwise why not.
    /// </summary>
    public LauncherRespawnOutcome Decide(string key, bool signedIn, bool hasLiveTab, bool shuttingDown, bool restoring, DateTime nowUtc)
    {
        if (shuttingDown) return LauncherRespawnOutcome.ShuttingDown;
        if (restoring) return LauncherRespawnOutcome.Restoring;
        if (!signedIn) return LauncherRespawnOutcome.SignedOut;
        if (hasLiveTab) return LauncherRespawnOutcome.TabStillOpen;
        return TryTake(key, nowUtc) ? LauncherRespawnOutcome.Spawn : LauncherRespawnOutcome.Capped;
    }
}
