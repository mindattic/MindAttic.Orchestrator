using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1436: each person keeps the CLI tab they last had chosen, across switching to another person and back, and
/// across a restart (by its TermHost session, in the session-restore file); a remembered tab that's gone leaves the default.</summary>
public sealed class RememberedCliTabTests
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
    public void Each_person_keeps_their_own_choice_while_another_is_shown_and_through_a_refresh()
    {
        var all = new TerminalsViewModel();
        var a1 = Reattach(all, "a1"); var a2 = Reattach(all, "a2"); var a3 = Reattach(all, "a3");
        var b1 = Reattach(all, "b1"); var b2 = Reattach(all, "b2");
        var alice = new PersonTerminalsViewModel(all);
        var bob = new PersonTerminalsViewModel(all);
        alice.SyncTo([a1, a2, a3]);
        bob.SyncTo([b1, b2]);

        alice.SelectedPane = a1;
        bob.SelectedPane = b1;
        // Showing the other person (the window's tab strip writes null for the one it left): ignored, the choice holds.
        alice.SelectedPane = null;
        alice.SyncTo([a1, a2, a3]);
        bob.SyncTo([b1, b2]);
        Assert.That(alice.SelectedPane, Is.SameAs(a1));
        Assert.That(bob.SelectedPane, Is.SameAs(b1));
        foreach (var p in all.Panes.ToList()) all.Close(p);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_choice_round_trips_through_the_restore_file_by_session_and_a_gone_one_keeps_the_default()
    {
        // As the last run captured it: Alice had a1 chosen, Bob b1 (whose host has since ended), an unhosted tab chosen too.
        var saved = new RestoreState
        {
            Tabs =
            [
                new RestoreTab { Request = new PaneRequest("A1", "C:\\a"), HostId = "a1", Chosen = true },
                new RestoreTab { Request = new PaneRequest("A2", "C:\\a"), HostId = "a2" },
                new RestoreTab { Request = new PaneRequest("B1", "C:\\b"), HostId = "b1", Chosen = true },
                new RestoreTab { Request = new PaneRequest("Local", "C:\\c"), Chosen = true },
            ],
        };
        var store = new SessionRestoreStore(temp.New());
        Assert.That(store.Save(saved), Is.True);
        var previous = store.Load();
        Assert.That(MainViewModel.RememberedTerms(previous), Is.EqualTo(new[] { "a1", "b1" }));

        // The restarted dashboard reattaches the live hosts (b1 is gone), each person defaulting to their last tab.
        var all = new TerminalsViewModel();
        var a1 = Reattach(all, "a1"); var a2 = Reattach(all, "a2"); var b2 = Reattach(all, "b2");
        var alice = new PersonTerminalsViewModel(all);
        var bob = new PersonTerminalsViewModel(all);
        alice.SyncTo([a1, a2]);
        bob.SyncTo([b2]);
        Assert.That(alice.SelectedPane, Is.SameAs(a2), "today's default: the last tab");

        foreach (var id in MainViewModel.RememberedTerms(previous))
            if (!alice.Choose(id)) bob.Choose(id);
        Assert.That(alice.SelectedPane, Is.SameAs(a1), "Alice is back on the tab she had chosen");
        Assert.That(bob.SelectedPane, Is.SameAs(b2), "Bob's remembered tab is gone: his default stays");
        Assert.That(alice.Choose(null), Is.False);
        Assert.That(MainViewModel.RememberedTerms(null), Is.Empty);
        foreach (var p in all.Panes.ToList()) all.Close(p);
    }
}
