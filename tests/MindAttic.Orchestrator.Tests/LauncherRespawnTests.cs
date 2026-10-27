using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1150: a person's last CLI tab closed (or its CLI exited) opens a new Launcher signed in as them, capped.</summary>
public sealed class LauncherRespawnTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc);

    [Test]
    public void The_last_tab_closed_spawns_a_Launcher()
    {
        var respawn = new LauncherRespawn();
        Assert.That(respawn.Decide("amy@example.com", signedIn: true, hasLiveTab: false, shuttingDown: false, restoring: false, Now),
            Is.EqualTo(LauncherRespawnOutcome.Spawn));
    }

    [Test]
    public void Another_tab_still_open_spawns_nothing_and_takes_no_turn()
    {
        var respawn = new LauncherRespawn();
        Assert.That(respawn.Decide("amy@example.com", true, hasLiveTab: true, false, false, Now), Is.EqualTo(LauncherRespawnOutcome.TabStillOpen));
        // It didn't use the person's turn: their last tab closing a moment later still spawns.
        Assert.That(respawn.Decide("amy@example.com", true, false, false, false, Now.AddSeconds(1)), Is.EqualTo(LauncherRespawnOutcome.Spawn));
    }

    [Test]
    public void A_signed_out_person_gets_no_Launcher()
    {
        var respawn = new LauncherRespawn();
        Assert.That(respawn.Decide("amy@example.com", signedIn: false, false, false, false, Now), Is.EqualTo(LauncherRespawnOutcome.SignedOut));
    }

    [Test]
    public void Nothing_spawns_while_the_dashboard_shuts_down_or_restores()
    {
        var respawn = new LauncherRespawn();
        Assert.That(respawn.Decide("amy@example.com", true, false, shuttingDown: true, false, Now), Is.EqualTo(LauncherRespawnOutcome.ShuttingDown));
        Assert.That(respawn.Decide("amy@example.com", true, false, false, restoring: true, Now), Is.EqualTo(LauncherRespawnOutcome.Restoring));
    }

    [Test]
    public void A_rapid_repeat_is_capped_per_person_until_the_spacing_is_up()
    {
        var respawn = new LauncherRespawn();
        Assert.That(respawn.Decide("amy@example.com", true, false, false, false, Now), Is.EqualTo(LauncherRespawnOutcome.Spawn));
        // The Launcher exited at once: no second one inside 30s, so it can't loop.
        Assert.That(respawn.Decide("AMY@example.com", true, false, false, false, Now.AddSeconds(2)), Is.EqualTo(LauncherRespawnOutcome.Capped));
        Assert.That(respawn.Decide("amy@example.com", true, false, false, false, Now.AddSeconds(29)), Is.EqualTo(LauncherRespawnOutcome.Capped));
        // Someone else isn't held by Amy's cap.
        Assert.That(respawn.Decide("api-key@mindattic.vault", true, false, false, false, Now.AddSeconds(2)), Is.EqualTo(LauncherRespawnOutcome.Spawn));
        Assert.That(respawn.Decide("amy@example.com", true, false, false, false, Now.AddSeconds(30)), Is.EqualTo(LauncherRespawnOutcome.Spawn));
    }

    [Test]
    public void The_refresh_s_reopen_shares_the_cap()
    {
        var respawn = new LauncherRespawn();
        Assert.That(respawn.TryTake("amy@example.com", Now), Is.True);
        Assert.That(respawn.Decide("amy@example.com", true, false, false, false, Now.AddSeconds(5)), Is.EqualTo(LauncherRespawnOutcome.Capped));
        Assert.That(respawn.TryTake("amy@example.com", Now.AddSeconds(10)), Is.False);
    }

    [Test]
    public void A_new_Launcher_replaces_only_that_persons_ended_Launcher_tabs()
    {
        // M.ORCH-T1153: a Launcher that kept failing left one more ended tab per retry; each retry now takes its place.
        var panes = new[]
        {
            (Who: "amy", Launcher: true, Ended: true),
            (Who: "amy", Launcher: true, Ended: true),
            (Who: "amy", Launcher: true, Ended: false),
            (Who: "amy", Launcher: false, Ended: true),
            (Who: "erin", Launcher: true, Ended: true),
        };
        var replaced = LauncherRespawn.Replaced(panes, p => p.Launcher, p => p.Ended, p => p.Who == "amy");
        Assert.That(replaced, Is.EqualTo(new[] { panes[0], panes[1] }), "Amy's ended Launchers only: not her live one, not her ended CLI, not Erin's");
    }
}
