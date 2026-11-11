using System.Xml.Linq;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1437: a CLI tab's close X sits at its right edge and asks first; only a yes ends the CLI, and a tab whose
/// CLI already ended closes unasked.</summary>
public sealed class TerminalCloseConfirmTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private TerminalPaneViewModel Reattach(TerminalsViewModel terminals, string id)
    {
        var session = new Term.TermSessionRecord { Id = id, HostPid = 999999, Pipe = "mindattic-term-nobody-" + id, Cwd = temp.New(), CommandLine = "cmd.exe" };
        return terminals.Reattach(session, new PaneRequest("T: Repo [Claude]", session.Cwd!) { Repo = "MindAttic", CommandLine = "cmd.exe" })!;
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_close_command_asks_first_and_closes_only_on_yes()
    {
        var asked = new List<TerminalPaneViewModel>();
        var answer = false;
        var terminals = new TerminalsViewModel { ConfirmClose = p => { asked.Add(p); return answer; } };
        var pane = Reattach(terminals, "confirm1");

        terminals.CloseCommand.Execute(pane);
        Assert.That(asked, Is.EqualTo(new[] { pane }), "the X asks about its own tab");
        Assert.That(terminals.Panes, Does.Contain(pane), "Cancel keeps the tab and its CLI");

        answer = true;
        terminals.CloseCommand.Execute(pane);
        Assert.That(asked, Has.Count.EqualTo(2));
        Assert.That(terminals.Panes, Is.Empty, "Close ends it");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void An_ended_tab_closes_unasked_and_pruning_never_asks()
    {
        var asked = 0;
        var terminals = new TerminalsViewModel { ConfirmClose = _ => { asked++; return false; } };
        var ended = Reattach(terminals, "ended1");
        ended.Ended = true;
        terminals.CloseCommand.Execute(ended);
        Assert.That(terminals.Panes, Is.Empty, "no CLI left to end, nothing to ask");

        var stale = Reattach(terminals, "stale1");
        stale.Ended = true;
        terminals.PruneEnded(DateTime.UtcNow + terminals.EndedLifetime + TimeSpan.FromSeconds(1));
        Assert.That(terminals.Panes, Is.Empty);
        Assert.That(asked, Is.Zero);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_question_names_the_tab()
    {
        var terminals = new TerminalsViewModel { PaneDirectory = temp.New() };
        var pane = Reattach(terminals, "named1");
        terminals.Rename(pane, "MindAttic");
        Assert.That(TerminalsViewModel.CloseQuestion(pane), Is.EqualTo("Close 'MindAttic'? This ends the CLI running in it."));
        terminals.Close(pane);
    }

    [Test]
    public void The_close_X_is_docked_at_the_tabs_right_edge()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var close = xaml.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "TerminalCloseButton");
        Assert.That(close.Parent!.Name.LocalName, Is.EqualTo("DockPanel"));
        Assert.That((string?)close.Attribute("DockPanel.Dock"), Is.EqualTo("Right"));
    }
}
