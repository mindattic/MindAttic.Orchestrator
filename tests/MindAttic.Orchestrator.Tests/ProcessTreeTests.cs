using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>What <see cref="MemoryReclaim"/>'s foreign trim and kill steps may ever touch (M.ORCH-T1671): everything under
/// a Claude CLI or an Orchestrator hosting process is protected, everything else isn't - on a made-up process map, never
/// the real machine's.</summary>
public sealed class ProcessTreeTests
{
    [TestCase("claude.exe")]
    [TestCase("claude.exe.old.3")]
    [TestCase("MindAttic.Launcher.exe")]
    [TestCase("MindAttic.Orchestrator.TermHost.exe")]
    [TestCase("MindAttic.Orchestrator.Desktop.exe")]
    [TestCase("CLAUDE.EXE")]
    public void A_protected_root_name_is_recognised(string exe) => Assert.That(ProcessTree.IsProtectedRootName(exe), Is.True);

    [TestCase("chrome.exe")]
    [TestCase("svchost.exe")]
    [TestCase("node.exe")]
    [TestCase("sqlservr.exe")]
    public void Everything_else_is_not_a_protected_root_name(string exe) => Assert.That(ProcessTree.IsProtectedRootName(exe), Is.False);

    [Test]
    public void A_subagent_under_a_cli_under_a_launcher_tab_is_protected()
    {
        // 3 claude.exe (subagent) -> 2 claude.exe (CLI) -> 1 MindAttic.Launcher.exe -> 0 (root)
        var map = new Dictionary<int, (int Parent, string Exe)>
        {
            [1] = (0, "MindAttic.Launcher.exe"),
            [2] = (1, "claude.exe"),
            [3] = (2, "claude.exe"),
            [4] = (3, "node.exe"), // a subagent's own MCP helper
        };
        Assert.That(ProcessTree.IsProtected(1, map), Is.True);
        Assert.That(ProcessTree.IsProtected(2, map), Is.True);
        Assert.That(ProcessTree.IsProtected(3, map), Is.True);
        Assert.That(ProcessTree.IsProtected(4, map), Is.True, "a subagent's own helper is covered too");
    }

    [Test]
    public void A_foreign_app_is_not_protected_even_with_a_long_ancestor_chain()
    {
        var map = new Dictionary<int, (int Parent, string Exe)>
        {
            [1] = (0, "explorer.exe"),
            [2] = (1, "chrome.exe"),
            [3] = (2, "chrome.exe"),
        };
        Assert.That(ProcessTree.IsProtected(3, map), Is.False);
    }

    [Test]
    public void An_unknown_pid_is_not_protected() =>
        Assert.That(ProcessTree.IsProtected(999, new Dictionary<int, (int Parent, string Exe)>()), Is.False);
}
