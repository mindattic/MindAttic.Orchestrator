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

    [TestCase("M.ORCH-T1812", "Tree sidebar alignment", "Deploy and relaunch the dashboard", "Tree sidebar alignment")]
    [TestCase("PRS-T995", "Hub without a window", null, "Hub without window")]
    [TestCase("M.ORCH-T1812", "Hub tray", "Deploy and relaunch the dashboard", "Hub tray Deploy")]
    [TestCase("M.ORCH-T1812", null, "Deploy and relaunch the dashboard", "Deploy relaunch dashboard")]
    [TestCase(null, null, "Read the Prose Hub startup: Program.cs", "Read Prose Hub")]
    [TestCase(null, null, "Using mcp__rea__list_strings", "Rea list strings")]
    [TestCase(null, null, @"Editing D:\Projects\X\Daemon.cs now", "Editing Daemon.cs now")]
    public void Always_three_words_from_the_ticket_name_then_its_activity_then_its_id(string? id, string? name, string? doing, string expected) =>
        Assert.That(AutoTabName.NameFor(id, name, doing, 32), Is.EqualTo(expected));

    [Test]
    public void Fewer_than_three_words_gives_no_name()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutoTabName.NameFor(null, null, null, 32), Is.Null);
            Assert.That(AutoTabName.NameFor(" ", "", "the of to", 32), Is.Null);
            Assert.That(AutoTabName.NameFor(null, null, "Using", 32), Is.Null, "never a lone 'Using' (M.ORCH-T1878)");
            Assert.That(AutoTabName.NameFor("M.ORCH-T1812", null, "Building", 32), Is.Null);
        });
    }

    [Test]
    public void A_name_fits_the_cap_by_cutting_its_longest_words_never_by_dropping_one()
    {
        var cut = AutoTabName.Shorten("Internationalization everywhere always", 14);
        Assert.Multiple(() =>
        {
            Assert.That(cut, Is.Not.Null);
            Assert.That(cut!.Length, Is.LessThanOrEqualTo(14));
            Assert.That(cut.Split(' '), Has.Length.EqualTo(3));
            Assert.That(AutoTabName.Shorten("one two three four five", 40), Is.EqualTo("One two three"));
            Assert.That(AutoTabName.Shorten("two words", 40), Is.Null);
        });
    }
}
