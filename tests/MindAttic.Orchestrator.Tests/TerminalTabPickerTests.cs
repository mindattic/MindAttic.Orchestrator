using System.Runtime.InteropServices;
using System.Windows.Automation;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>"Go to tab" (M.ORCH-T619): which Windows Terminal tab is a Launcher host's.</summary>
public sealed class TerminalTabPickerTests
{
    private const long W1 = 100, W2 = 200;

    private static HostSession Host(int pid, string? title, string? dir = null) =>
        new("Claude", pid, $"mindattic-host-claude-{pid}", dir) { Title = title };

    // What a live machine showed: the Launcher's glyph in front, other hosts' tabs, plain shells and a Claude Code retitle.
    private static readonly TerminalTab Launcher = new(W1, "MindAttic.Launcher");
    private static readonly TerminalTab Bloodlines = new(W1, "▶  REA.Bloodlines [Claude]");
    private static readonly TerminalTab General = new(W1, "⏸  General [Claude]");
    private static readonly TerminalTab Retitled = new(W1, "◐ Per-account CLI sessions via orchestrator");
    private static readonly TerminalTab PlainClaude = new(W2, "claude");

    [TestCase("▶  REA.Bloodlines [Claude]", "REA.Bloodlines [Claude]")]
    [TestCase("⏸  General [Claude]", "General [Claude]")]
    [TestCase("✳ JR: Repo [Claude]", "JR: Repo [Claude]")]
    [TestCase("✳ [JR] Repo [Claude]", "[JR] Repo [Claude]")]
    [TestCase("MindAttic.Launcher", "MindAttic.Launcher")]
    [TestCase("◐", "")]
    public void The_status_glyph_in_front_is_dropped(string name, string normalized) =>
        Assert.That(TerminalTabPicker.Normalize(name), Is.EqualTo(normalized));

    [Test]
    public void The_tab_titled_like_the_host_is_picked_whatever_glyph_it_carries() =>
        Assert.That(TerminalTabPicker.PickTab([Launcher, Bloodlines, General, PlainClaude], "REA.Bloodlines [Claude]", ["General [Claude]"]),
            Is.SameAs(Bloodlines));

    [Test]
    public void A_title_found_inside_a_longer_tab_name_is_picked() =>
        Assert.That(TerminalTabPicker.PickTab([Launcher, new(W1, "▶ Admin: General [Claude]")], "General [Claude]", []),
            Is.EqualTo(new TerminalTab(W1, "▶ Admin: General [Claude]")));

    [Test]
    public void A_tab_opened_before_the_colon_badge_is_still_picked_by_its_old_title()
    {
        // M.ORCH-T632: a host started before it keeps its "[JR] ..." --title, and its tab still reads that way.
        var old = new TerminalTab(W1, "▶ [JR] MindAttic.Launcher [Claude]");
        var now = new TerminalTab(W1, "▶ JR: EZ [Claude]");
        Assert.That(TerminalTabPicker.PickTab([old, now], "[JR] MindAttic.Launcher [Claude]", ["JR: EZ [Claude]"]), Is.SameAs(old));
        Assert.That(TerminalTabPicker.PickTab([old, now], "JR: EZ [Claude]", ["[JR] MindAttic.Launcher [Claude]"]), Is.SameAs(now));
    }

    [Test]
    public void Two_tabs_with_the_hosts_title_are_not_guessed_between() =>
        Assert.That(TerminalTabPicker.PickTab([Bloodlines, new(W2, "⏸  REA.Bloodlines [Claude]")], "REA.Bloodlines [Claude]", []), Is.Null);

    [Test]
    public void A_tab_Claude_Code_retitled_is_the_one_glyph_tab_no_other_host_claims() =>
        Assert.That(TerminalTabPicker.PickTab([Launcher, Bloodlines, Retitled, PlainClaude], "General [Claude]", ["REA.Bloodlines [Claude]"]),
            Is.SameAs(Retitled));

    [Test]
    public void Two_unclaimed_glyph_tabs_are_not_guessed_between() =>
        Assert.That(TerminalTabPicker.PickTab([Bloodlines, Retitled, new(W2, "✳ Something else")], "General [Claude]", ["REA.Bloodlines [Claude]"]),
            Is.Null);

    [Test]
    public void With_no_tab_picked_the_terminals_only_window_is_brought_up()
    {
        Assert.That(TerminalTabPicker.PickWindow([Launcher], [W1], "General [Claude]"), Is.EqualTo(W1));
        Assert.That(TerminalTabPicker.PickWindow([Launcher, PlainClaude], [W1, W2], "General [Claude]"), Is.Null);
    }

    [Test]
    public void Two_same_titled_tabs_in_one_window_bring_up_that_window()
    {
        Assert.That(TerminalTabPicker.PickWindow([Bloodlines, new(W1, "⏸  REA.Bloodlines [Claude]"), PlainClaude], [W1, W2], "REA.Bloodlines [Claude]"),
            Is.EqualTo(W1));
        Assert.That(TerminalTabPicker.PickWindow([Bloodlines, new(W2, "⏸  REA.Bloodlines [Claude]")], [W1, W2], "REA.Bloodlines [Claude]"),
            Is.Null, "split across windows");
    }

    [Test]
    public void The_expected_title_is_the_hosts_title_else_its_folder_and_provider()
    {
        Assert.That(TerminalTabPicker.ExpectedTitle(Host(1, "General [Claude]", @"D:\Projects\MindAttic")), Is.EqualTo("General [Claude]"));
        Assert.That(TerminalTabPicker.ExpectedTitle(Host(1, null, @"D:\Projects\MindAttic\REA.Bloodlines\")), Is.EqualTo("REA.Bloodlines [Claude]"));
        Assert.That(TerminalTabPicker.ExpectedTitle(Host(1, null, @"D:\Projects\MindAttic\GTFO.EZ")), Is.EqualTo("EZ [Claude]"));
        Assert.That(TerminalTabPicker.ExpectedTitle(Host(1, null)), Is.Null);
    }

    [Test]
    public void The_scanner_reads_the_title_off_the_host_command_line()
    {
        Assert.That(HostSessionScanner.TitleOf(@"C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe host --name REA.Bloodlines --title ""REA.Bloodlines [Claude]"" --provider Claude"),
            Is.EqualTo("REA.Bloodlines [Claude]"));
        Assert.That(HostSessionScanner.TitleOf(@"MindAttic.Launcher.exe host --path D:\W"), Is.Null);
    }

    [Test]
    public void Ancestors_walk_up_nearest_first_and_stop_at_a_cycle()
    {
        var processes = new Dictionary<int, (int Parent, string Exe)>
        {
            [10] = (20, "MindAttic.Launcher.exe"), [20] = (30, "WindowsTerminal.exe"), [30] = (0, "svchost.exe"),
            [7] = (8, "a.exe"), [8] = (7, "b.exe"),
        };
        Assert.That(ProcessTree.Ancestors(10, processes).Select(a => a.Exe), Is.EqualTo(new[] { "WindowsTerminal.exe", "svchost.exe" }));
        Assert.That(ProcessTree.Ancestors(7, processes), Has.Count.EqualTo(16));
    }

    [Test]
    public void A_toast_goes_to_the_tab_whose_cli_waits_on_you()
    {
        HostSession a = Host(1, "A [Claude]"), b = Host(2, "A [Claude]");
        var repo = new RepoStatus("A", @"D:\A", RepoState.Waiting, [a, b], [], [], null, default);
        AgentBoardRow Row(HostSession h, AgentState s) =>
            new("s" + h.ProcessId, "A", null, null, DateTime.UtcNow, DateTime.UtcNow, s, "", false, default, []) { Host = h };

        Assert.That(MainViewModel.HostForToast(repo, [Row(a, AgentState.Working), Row(b, AgentState.Waiting)]), Is.SameAs(b));
        Assert.That(MainViewModel.HostForToast(repo, []), Is.SameAs(a));
        Assert.That(MainViewModel.HostForToast(repo with { Hosts = [] }, []), Is.Null);
    }

    /// <summary>Live check against this machine's real Launcher tabs: selects each host's tab, then puts the selection and the
    /// foreground back as they were. Types nothing, closes nothing.</summary>
    [Test, Explicit("Drives the real Windows Terminal on this machine")]
    public void Live_each_host_tab_is_found_and_selected()
    {
        var hosts = new HostSessionScanner(OrchestratorOptions.Load()).Scan();
        if (hosts.Count == 0) Assert.Ignore("No Launcher host is running.");
        var before = GetForegroundWindow();
        var selected = SelectedTabs();
        try
        {
            foreach (var host in hosts)
            {
                var result = TerminalFocus.GoTo(host, hosts);
                TestContext.Out.WriteLine($"PID {host.ProcessId} title \"{host.Title}\": {result.Outcome}: {result.Note}");
                Assert.That(result.Outcome, Is.EqualTo(GoToOutcome.Tab), result.Note);
                Assert.That(SelectedTabs().Select(t => t.Current.Name), Has.Some.Contains(host.Title), "its tab is the selected one now");
            }
        }
        finally
        {
            foreach (var tab in selected)
                if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var p)) ((SelectionItemPattern)p).Select();
            SetForegroundWindow(before);
        }
    }

    private static List<AutomationElement> SelectedTabs()
    {
        var result = new List<AutomationElement>();
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("WindowsTerminal"))
            foreach (AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children,
                         new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id)))
                foreach (AutomationElement tab in window.FindAll(TreeScope.Descendants,
                             new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)))
                    if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var p) && ((SelectionItemPattern)p).Current.IsSelected)
                        result.Add(tab);
        return result;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
}
