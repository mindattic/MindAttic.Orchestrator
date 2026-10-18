using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Opening a listed file shows it, never runs it (M.ORCH-T246), and a path reaches the editor whole.</summary>
public sealed class OpenerTests
{
    [TestCase(@"D:\X\hook.py", true)]
    [TestCase(@"D:\X\deploy.PS1", true)]
    [TestCase(@"D:\X\run.cmd", true)]
    [TestCase(@"D:\X\notes.md", false)]
    [TestCase(@"D:\X\Daemon.cs", false)]
    public void A_script_is_known_to_run_on_its_default_action(string path, bool runs) =>
        Assert.That(Opener.Runs(path), Is.EqualTo(runs));

    [Test]
    public void A_path_goes_to_cmd_in_quotes_so_an_ampersand_in_it_isnt_a_second_command() =>
        Assert.That(Opener.CmdQuoted(@"D:\X\R&D\hook.py"), Is.EqualTo("\"D:\\X\\R&D\\hook.py\""));
}
