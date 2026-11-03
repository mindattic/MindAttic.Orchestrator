using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1440: a login renamed, or given a new Prefix, in User Settings reaches the CLIs already open: their tab's
/// title badge and tooltip, the tab file a reattach or pop-out window reads, and the session-restore file, with no reopen; the
/// tab's identity and any name the person gave it stay.</summary>
public sealed class TerminalLookRefreshTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private static Term.TermSessionRecord Session(string id, string cwd) =>
        new() { Id = id, HostPid = 999999, Pipe = "mindattic-term-nobody-" + id, Cwd = cwd, CommandLine = "cmd.exe" };

    private static LoginLook Look(string initials, string alias) => new LoginLook(alias, initials, "#2E7D32") { Alias = alias };

    [Test]
    public void WithLook_swaps_the_badge_and_name_and_keeps_everything_else()
    {
        var request = new PaneRequest("RD: Repo", "C:\\x") { Repo = "Repo", Initials = "RD", Alias = "Ryan", CustomName = "Hotfix", Order = 7 };

        var now = request.WithLook(Look("JR", "Jordan"));

        Assert.That(now.Title, Is.EqualTo("JR: Repo"));
        Assert.That(now.Initials, Is.EqualTo("JR"));
        Assert.That(now.Alias, Is.EqualTo("Jordan"));
        Assert.That(now.CustomName, Is.EqualTo("Hotfix"));
        Assert.That(now.Order, Is.EqualTo(7));
        Assert.That(request.WithLook(Look("RD", "Ryan")), Is.SameAs(request), "the same look changes nothing");
        var unbadged = new PaneRequest("Repo [Codex]", "C:\\x");
        Assert.That(unbadged.WithLook(Look("JR", "Jordan")), Is.SameAs(unbadged), "a tab opened without a badge isn't given one");
    }

    [Test]
    public void Rebadged_swaps_only_a_stale_leading_badge()
    {
        Assert.That(PaneRequest.Rebadged("RD: Repo [Codex]", ["RD"], "JR"), Is.EqualTo("JR: Repo [Codex]"));
        Assert.That(PaneRequest.Rebadged("JR: Repo", ["RD"], "JR"), Is.EqualTo("JR: Repo"));
        Assert.That(PaneRequest.Rebadged("✳ Fixing the build", ["RD"], "JR"), Is.EqualTo("✳ Fixing the build"));
        Assert.That(PaneRequest.Rebadged("RD: Repo", ["RD"], null), Is.EqualTo("RD: Repo"));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void An_open_tab_follows_a_new_prefix_and_saves_it_by_its_session_keeping_its_custom_name()
    {
        var dir = temp.New();
        var look = Look("RD", "Ryan");
        var terminals = new TerminalsViewModel { PaneDirectory = dir, LookOf = _ => look };
        var changed = 0;
        terminals.Changed += () => changed++;
        var cwd = temp.New();
        var request = new PaneRequest("RD: Repo", cwd) { Repo = "Repo", CommandLine = "cmd.exe", Initials = "RD", Alias = "Ryan" };
        var pane = terminals.Reattach(Session("look1", cwd), request)!;
        var renamed = terminals.Reattach(Session("look2", cwd), request with { CustomName = "Lifeboat" })!;
        var raised = new List<string?>();
        pane.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.That(terminals.RefreshLooks(), Is.Zero, "nothing changed yet");
        Assert.That(changed, Is.Zero);

        look = Look("JR", "Jordan");
        Assert.That(terminals.RefreshLooks(), Is.EqualTo(2));
        Assert.That(changed, Is.EqualTo(1), "the session-restore file is rewritten once");
        Assert.That(pane.Title, Is.EqualTo("JR: Repo"));
        Assert.That(pane.Tooltip, Does.StartWith("JR: Repo\n").And.EndWith("\nJordan"));
        Assert.That(raised, Does.Contain(nameof(TerminalPaneViewModel.Tooltip)).And.Contain(nameof(TerminalPaneViewModel.Title)));
        Assert.That(System.Windows.Automation.AutomationProperties.GetName(pane.Control), Is.EqualTo("JR: Repo"));
        Assert.That(pane.TermId, Is.EqualTo("look1"), "its identity is untouched");
        Assert.That(renamed.Name, Is.EqualTo("Lifeboat"), "the person's own name still wins");
        Assert.That(renamed.Tooltip, Does.StartWith("Lifeboat\nJR: Repo"));

        var saved = TermPaneFile.Read("look1", dir)!;
        Assert.That((saved.Title, saved.Initials, saved.Alias), Is.EqualTo(("JR: Repo", "JR", "Jordan")), "a reattach shows the new prefix");
        Assert.That(TermPaneFile.Read("look2", dir)?.CustomName, Is.EqualTo("Lifeboat"));

        Assert.That(terminals.RefreshLooks(), Is.Zero, "applied once");
        pane.DisposeControl();
        renamed.DisposeControl();
    }

    [Test]
    public void CurrentLook_reads_the_logins_own_file()
    {
        var root = temp.New();
        var dirs = new AccountDirs(root);
        var login = dirs.For("pat@example.com").Directory;
        Directory.CreateDirectory(login);
        LoginLook.SetAlias(login, "pat@example.com", "Pat Lee", "PL");

        var look = new PaneRequest("PA: Repo", root) { ConfigDir = login, Initials = "PA" }.CurrentLook();
        Assert.That((look?.Initials, look?.Alias), Is.EqualTo(("PL", "Pat Lee")));
        var byEmail = new PaneRequest("PA: Repo", root) { Email = "pat@example.com", Initials = "PA" }.CurrentLook(dirs);
        Assert.That(byEmail?.Initials, Is.EqualTo("PL"));
        Assert.That(new PaneRequest("NO: Repo", root) { Email = "nobody@example.com" }.CurrentLook(dirs), Is.Null,
            "an email with no login folder here leaves the tab as it is");
    }
}
