using System.Windows.Threading;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1177: an ended CLI tab stays for its own person to come back and close, but a member who never does (on
/// leave, between projects) would otherwise keep every CLI that ends on their page piling up a tab, forever.</summary>
public sealed class TerminalEndedPruneTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private TerminalPaneViewModel Reattach(TerminalsViewModel terminals, string id)
    {
        var session = new Term.TermSessionRecord { Id = id, HostPid = 999999, Pipe = "mindattic-term-nobody-" + id, Cwd = temp.New(), CommandLine = "cmd.exe" };
        return terminals.Reattach(session, new PaneRequest("T: Repo [Claude]", session.Cwd!) { CommandLine = "cmd.exe" })!;
    }

    [Test, Apartment(ApartmentState.STA)]
    public void Becoming_ended_stamps_the_time_and_reviving_clears_it()
    {
        var pane = new TerminalPaneViewModel(new PaneRequest("T", temp.New()) { CommandLine = "cmd.exe" });
        Assert.That(pane.EndedAtUtc, Is.Null);
        var before = DateTime.UtcNow;
        pane.Ended = true;
        Assert.That(pane.EndedAtUtc, Is.Not.Null.And.GreaterThanOrEqualTo(before));
        pane.Ended = false;
        Assert.That(pane.EndedAtUtc, Is.Null, "a tab that comes back to life is no longer counted as ended");
        pane.DisposeControl();
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_tab_ended_longer_than_its_lifetime_is_pruned_but_a_fresher_one_and_a_live_one_are_left_alone()
    {
        var terminals = new TerminalsViewModel();
        var stale = Reattach(terminals, "stale1");
        var fresh = Reattach(terminals, "fresh1");
        var live = Reattach(terminals, "live1");
        stale.Ended = true;
        fresh.Ended = true;
        Assert.That(terminals.Panes, Has.Count.EqualTo(3));

        // Still within the lifetime: nothing is pruned yet, ended or not.
        terminals.PruneEnded(DateTime.UtcNow);
        Assert.That(terminals.Panes, Is.EqualTo(new[] { stale, fresh, live }));

        // Past the lifetime: only the ended tabs go; the still-live one stays whatever its age.
        var later = DateTime.UtcNow + TerminalsViewModel.EndedLifetime + TimeSpan.FromSeconds(1);
        terminals.PruneEnded(later);
        Assert.That(terminals.Panes, Is.EqualTo(new[] { live }));

        live.DisposeControl();
    }
}
