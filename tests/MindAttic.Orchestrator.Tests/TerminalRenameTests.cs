using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1432: a CLI tab renamed in place keeps its identity, and its name is kept by that identity (its TermHost
/// session's tab file), so a reattach, a pop-out window or a restarted dashboard shows it renamed.</summary>
public sealed class TerminalRenameTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private static Term.TermSessionRecord Session(string id, string cwd) =>
        new() { Id = id, HostPid = 999999, Pipe = "mindattic-term-nobody-" + id, Cwd = cwd, CommandLine = "cmd.exe" };

    [Test, Apartment(ApartmentState.STA)]
    public void Enter_renames_trimmed_and_blank_or_unchanged_keeps_the_old_name()
    {
        var pane = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe" });
        var original = pane.Name;

        Assert.That(pane.Rename("   "), Is.False, "blank keeps the old name");
        Assert.That(pane.Rename(null), Is.False);
        Assert.That(pane.Name, Is.EqualTo(original));

        Assert.That(pane.Rename("  Hotfix  "), Is.True);
        Assert.That(pane.Name, Is.EqualTo("Hotfix"));
        Assert.That(pane.Title, Is.EqualTo("Hotfix"), "the tab's automation name follows the new name");
        Assert.That(pane.Tooltip, Does.StartWith("Hotfix\nRD: Repo [Claude]"));
        Assert.That(System.Windows.Automation.AutomationProperties.GetName(pane.Control), Is.EqualTo("Hotfix"));
        Assert.That(pane.Rename("Hotfix"), Is.False, "the same name again changes nothing");

        Assert.That(pane.Rename(original), Is.True, "its own default name clears the custom one");
        Assert.That(pane.Request.CustomName, Is.Null);
        Assert.That(pane.Name, Is.EqualTo(original));
        pane.DisposeControl();
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_rename_keeps_the_session_id_and_is_saved_by_it_so_a_reattach_shows_it()
    {
        var dir = temp.New();
        var terminals = new TerminalsViewModel { PaneDirectory = dir };
        var changed = 0;
        terminals.Changed += () => changed++;
        var cwd = temp.New();
        var request = new PaneRequest("RD: Repo [Claude]", cwd) { Repo = "Repo", CommandLine = "cmd.exe" };
        var pane = terminals.Reattach(Session("rename1", cwd), request)!;

        Assert.That(terminals.Rename(pane, "Lifeboat test"), Is.True);
        Assert.That(pane.TermId, Is.EqualTo("rename1"), "the rename is a display name only");
        Assert.That(changed, Is.EqualTo(1), "the session-restore file is rewritten");
        Assert.That(TermPaneFile.Read("rename1", dir)?.CustomName, Is.EqualTo("Lifeboat test"));

        // A dashboard that restarts reattaches the session from its tab file, renamed.
        var again = new TerminalsViewModel { PaneDirectory = dir };
        var back = again.Reattach(Session("rename1", cwd), TermPaneFile.Read("rename1", dir))!;
        Assert.That(back.Name, Is.EqualTo("Lifeboat test"));

        Assert.That(terminals.Rename(pane, ""), Is.False, "blank reverts, nothing written");
        Assert.That(changed, Is.EqualTo(1));
        pane.DisposeControl();
        back.DisposeControl();
    }
}
