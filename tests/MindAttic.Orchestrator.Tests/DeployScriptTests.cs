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

    [Test]
    public void A_newer_installed_build_is_left_alone_unless_forced()
    {
        // M.ORCH-T554: another checkout installed a newer build, so the hashes differed, --install ran without --force,
        // the installer refused the downgrade and every deploy of an unchanged checkout ended 'Install FAILED'.
        string File(string name, string text, DateTime written)
        {
            var path = Path.Combine(dir, name);
            System.IO.File.WriteAllText(path, text);
            System.IO.File.SetLastWriteTimeUtc(path, written);
            return path;
        }
        var then = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var artifact = File("artifact.exe", "this build", then);
        var newer = File("newer.exe", "another checkout's build", then.AddHours(1));
        var older = File("older.exe", "an old build", then.AddHours(-1));
        var same = File("same.exe", "this build", then);
        string Q(string s) => "'" + s.Replace("'", "''") + "'";

        var lines = RunFunction("Get-InstallStep",
            $"Get-InstallStep {Q(artifact)} {Q(newer)} $false $false",
            $"Get-InstallStep {Q(artifact)} {Q(newer)} $false $true",
            $"Get-InstallStep {Q(artifact)} {Q(older)} $false $false",
            $"Get-InstallStep {Q(artifact)} {Q(same)} $false $false",
            $"Get-InstallStep {Q(artifact)} $null $false $false",
            $"Get-InstallStep {Q(Path.Combine(dir, "missing.exe"))} {Q(newer)} $false $false");

        Assert.That(lines, Is.EqualTo(new[] { "newer", "install", "install", "current", "install", "none" }));
        var script = System.IO.File.ReadAllText(DeployScript());
        Assert.That(script, Does.Contain("if ($step -eq 'newer')"), "a newer install is said and skipped, not run and failed");
        Assert.That(script, Does.Contain("@('--install', '--force')"), "-Force installs over a newer build");
    }

    [Test]
    public void Publishing_the_CLI_swaps_files_in_even_when_a_running_process_holds_the_old_exe()
    {
        // M.ORCH-T765: dotnet publish straight into Cli\ failed (MSB3027) whenever an orchestrator.exe was running from it.
        // A running exe can't be overwritten but can be renamed, so the locked file is renamed aside and the new one copied in.
        var from = Path.Combine(dir, "stage");
        var to = Path.Combine(dir, "Cli");
        Directory.CreateDirectory(Path.Combine(from, "sub"));
        Directory.CreateDirectory(to);
        File.WriteAllText(Path.Combine(from, "new.txt"), "new");
        File.WriteAllText(Path.Combine(from, "sub", "lib.txt"), "lib");
        var running = Path.Combine(to, "orchestrator.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), running);
        File.WriteAllText(Path.Combine(from, "orchestrator.exe"), "replacement");
        File.WriteAllText(Path.Combine(to, "stale.dll.old-0123abcd"), "left by an earlier deploy");
        string Q(string s) => "'" + s.Replace("'", "''") + "'";

        using var held = Process.Start(new ProcessStartInfo(running, "-n 60 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            Thread.Sleep(500);
            Assert.That(() => File.WriteAllText(running, "x"), Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>(), "the exe really is locked");
            RunFunction("Remove-AsideFiles;Copy-Published", $"Copy-Published {Q(from)} {Q(to)}");

            Assert.That(File.ReadAllText(running), Is.EqualTo("replacement"));
            Assert.That(File.ReadAllText(Path.Combine(to, "new.txt")), Is.EqualTo("new"));
            Assert.That(File.ReadAllText(Path.Combine(to, "sub", "lib.txt")), Is.EqualTo("lib"));
            Assert.That(File.Exists(Path.Combine(to, "stale.dll.old-0123abcd")), Is.False, "an earlier deploy's aside copy is cleaned up");
            Assert.That(Directory.GetFiles(to, "orchestrator.exe.old-*"), Has.Length.EqualTo(1), "the running exe was moved aside");
        }
        finally { try { held.Kill(); held.WaitForExit(5000); } catch (Exception) { } }

        RunFunction("Remove-AsideFiles;Copy-Published", $"Remove-AsideFiles {Q(to)}");
        Assert.That(Directory.GetFiles(to, "*.old-*"), Is.Empty, "once the process has exited the aside copy goes");

        var script = File.ReadAllText(DeployScript());
        Assert.That(script, Does.Contain("--output $cliStage"), "the CLI is published to a staging folder, not into the installed one");
        Assert.That(script, Does.Not.Contain("--output $cliOut"));
        Assert.That(script.Contains((char)13), Is.False, "deploy.ps1 keeps its LF line endings");
    }

    /// <summary>Takes <paramref name="function"/> out of deploy.ps1 by the PowerShell parser, runs each call with it, and
    /// returns what they printed, line by line. Nothing else in the script runs.</summary>
    private static string[] RunFunction(string function, params string[] calls)
    {
        var script =
            "$ErrorActionPreference = 'Stop'\n" +
            $"$ast = [System.Management.Automation.Language.Parser]::ParseFile('{DeployScript().Replace("'", "''")}', [ref]$null, [ref]$null)\n" +
            $"foreach ($name in '{function.Replace(";", "','")}') {{ " +
            "$f = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true) | Select-Object -First 1; " +
            "if (-not $f) { throw \"deploy.ps1 has no $name\" }; " +
            ". ([scriptblock]::Create($f.Extent.Text)) }\n" +
            string.Join("\n", calls) + "\n";
        var psi = new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        using var ps = Process.Start(psi)!;
        var stdout = ps.StandardOutput.ReadToEndAsync();
        var stderr = ps.StandardError.ReadToEndAsync();
        if (!ps.WaitForExit(60_000)) { ps.Kill(entireProcessTree: true); Assert.Fail("powershell didn't finish"); }
        Assert.That(ps.ExitCode, Is.EqualTo(0), stderr.Result);
        return stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsRunning(int pid)
    {
        try { return !Process.GetProcessById(pid).HasExited; }
        catch (ArgumentException) { return false; }
    }
}
