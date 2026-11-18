using NUnit.Framework;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The short name a workspace CLI tab is given for what it is doing (M.ORCH-T1817).</summary>
[TestFixture]
public class AutoTabNameTests
{
    private const string Root = @"D:\Projects\MindAttic";

    [Test]
    public void A_CLI_opened_at_the_workspace_root_is_a_workspace_tab_whatever_it_is_called()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutoTabName.IsWorkspaceTab(new PaneRequest("RD: General", Root) { Repo = "General" }, Root), Is.True);
            Assert.That(AutoTabName.IsWorkspaceTab(new PaneRequest("RD: MindAttic", Root + "\\"), Root.ToLowerInvariant()), Is.True);
        });
    }

    [Test]
    public void A_CLI_opened_on_a_repo_and_a_Launcher_menu_are_not()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutoTabName.IsWorkspaceTab(new PaneRequest("RD: Prose", Root + @"\Prose"), Root), Is.False);
            Assert.That(AutoTabName.IsWorkspaceTab(new PaneRequest("RD: Launcher", Root) { IsMenu = true }, Root), Is.False);
            Assert.That(AutoTabName.IsWorkspaceTab(new PaneRequest("RD: General", Root), null), Is.False);
        });
    }

    [Test]
    public void A_name_given_by_hand_ends_eligibility_and_an_auto_name_does_not()
    {
        var plain = new PaneRequest("RD: General", Root);
        Assert.Multiple(() =>
        {
            Assert.That(AutoTabName.IsEligible(plain, Root), Is.True);
            Assert.That(AutoTabName.IsEligible(plain with { CustomName = "Hub tray", AutoNamed = true }, Root), Is.True);
            Assert.That(AutoTabName.IsEligible(plain with { CustomName = "Mine" }, Root), Is.False);
        });
    }

    [TestCase("M.ORCH-T1812", "Tree sidebar alignment", "Deploy and relaunch the dashboard", "Tree sidebar")]
    [TestCase("PRS-T995", "Hub without a window", null, "Hub without window")]
    [TestCase("M.ORCH-T1812", null, "Deploy and relaunch the dashboard", "M.ORCH-T1812")]
    [TestCase(null, null, "Deploy and relaunch the Orchestrator dashboard", "Deploy relaunch")]
    [TestCase(null, null, "Read the Prose Hub startup: Program.cs", "Read Prose Hub")]
    public void The_ticket_names_it_first_then_its_id_then_what_it_is_doing(string? id, string? name, string? doing, string expected) =>
        Assert.That(AutoTabName.NameFor(id, name, doing, 20), Is.EqualTo(expected));

    [Test]
    public void Nothing_known_gives_no_name()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutoTabName.NameFor(null, null, null, 20), Is.Null);
            Assert.That(AutoTabName.NameFor(" ", "", "the of to", 20), Is.Null);
        });
    }

    [Test]
    public void A_name_never_exceeds_the_cap_and_one_long_word_is_cut_to_it()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutoTabName.Shorten("Internationalization everywhere", 12), Is.EqualTo("Internationa"));
            Assert.That(AutoTabName.Shorten("one two three four five", 40), Is.EqualTo("One two three"));
        });
    }
}
