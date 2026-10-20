using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

// deploy.ps1's install step, run in isolation: its Invoke-Installer is taken out of the script by the PowerShell parser
// and given a stand-in installer, so nothing here builds or touches the real install.
public sealed class DeployScriptTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(dir = Path.Combine(Path.GetTempPath(), "orch-deploy-" + Guid.NewGuid().ToString("N")));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string DeployScript()
    {
        for (var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); d != null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, "deploy.ps1");
            if (File.Exists(path) && File.Exists(Path.Combine(d.FullName, "MindAttic.Orchestrator.slnx"))) return path;
        }
        throw new FileNotFoundException("deploy.ps1 not found above the test directory");
    }

    [Test]
    public void The_install_step_returns_when_the_installer_exits_though_what_it_started_runs_on()
    {
        // M.ORCH-T525: Start-Process -Wait waited for the installer's whole tree, and --install starts the daemon, so
        // deploy sat at 'Installing...' for good. The stand-in starts a long-lived child (the daemon) and exits 3.
        var pidFile = Path.Combine(dir, "child.pid");
        var installer = Path.Combine(dir, "install.cmd");
        File.WriteAllText(installer,
            "@echo off\r\n" +
            "start \"\" /min powershell.exe -NoProfile -WindowStyle Hidden -Command \"Set-Content -LiteralPath '%~1' $PID; Start-Sleep 120\"\r\n" +
            "exit /b 3\r\n");

        var script =
            "$ErrorActionPreference = 'Stop'\n" +
            $"$ast = [System.Management.Automation.Language.Parser]::ParseFile('{DeployScript().Replace("'", "''")}', [ref]$null, [ref]$null)\n" +
            "$f = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-Installer' }, $true) | Select-Object -First 1\n" +
            "if (-not $f) { throw 'deploy.ps1 has no Invoke-Installer' }\n" +
            ". ([scriptblock]::Create($f.Extent.Text))\n" +
            $"$code = Invoke-Installer '{installer.Replace("'", "''")}' @('{pidFile.Replace("'", "''")}')\n" +
            "\"exit=$code\"\n";

        var psi = new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        int? childPid = null;
        try
        {
            using var ps = Process.Start(psi)!;
            var stdout = ps.StandardOutput.ReadToEndAsync();
            var stderr = ps.StandardError.ReadToEndAsync();
            var returned = ps.WaitForExit(60_000);
            if (!returned) ps.Kill(entireProcessTree: true);

            for (var i = 0; i < 100 && !File.Exists(pidFile); i++) Thread.Sleep(100);
            if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid)) childPid = pid;

            Assert.That(returned, Is.True, "the install step waited on what the installer started, not the installer alone");
            Assert.That(stdout.Result, Does.Contain("exit=3"), "the installer's own exit code comes back; stderr: " + stderr.Result);
            Assert.That(childPid, Is.Not.Null, "the stand-in installer started its long-lived child");
            Assert.That(IsRunning(childPid!.Value), Is.True, "the child was still running when the install step returned");
        }
        finally
        {
            if (childPid is { } id && IsRunning(id)) try { Process.GetProcessById(id).Kill(); } catch (Exception) { }
        }
    }

    private static bool IsRunning(int pid)
    {
        try { return !Process.GetProcessById(pid).HasExited; }
        catch (ArgumentException) { return false; }
    }
}
