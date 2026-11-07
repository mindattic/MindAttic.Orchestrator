using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1511: redeploying MindAttic.Launcher (its own run.ps1 -Force) before a closed Launcher tab reopens.</summary>
public sealed class LauncherRedeployTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private string WorkspaceWithScript(string script)
    {
        var root = temp.New();
        var dir = Path.Combine(root, "MindAttic.Launcher");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "run.ps1"), script);
        return root;
    }

    [Test]
    public void No_run_ps1_at_the_workspace_says_so_instead_of_shelling_out()
    {
        var root = temp.New();
        Assert.That(LauncherRedeploy.Run(root), Does.Contain("run.ps1 not found").And.Contain(Path.Combine(root, "MindAttic.Launcher", "run.ps1")));
    }

    [Test]
    public void A_successful_run_ps1_answers_null()
    {
        var root = WorkspaceWithScript("Write-Host 'Deployed'; exit 0");
        Assert.That(LauncherRedeploy.Run(root), Is.Null);
    }

    [Test]
    public void A_failed_run_ps1_reports_its_exit_code_and_the_tail_of_its_output()
    {
        var root = WorkspaceWithScript("Write-Host 'line one'; Write-Host 'line two'; Write-Host 'line three'; exit 1");
        var error = LauncherRedeploy.Run(root);
        Assert.That(error, Does.Contain("exit 1").And.Contain("line one").And.Contain("line two").And.Contain("line three"));
    }
}
