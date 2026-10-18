using System.Xml.Linq;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class InstallerTests
{
    [Test]
    public void The_logon_task_runs_the_daemon_as_you_forever()
    {
        var xml = XDocument.Parse(Installer.TaskXml(@"C:\Apps\Mind & Attic\MindAttic.Orchestrator.exe", @"PC\ryan"));
        XNamespace t = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        Assert.That(xml.Descendants(t + "Command").Single().Value, Is.EqualTo(@"C:\Apps\Mind & Attic\MindAttic.Orchestrator.exe"), "escaped, so odd paths survive");
        Assert.That(xml.Descendants(t + "Arguments").Single().Value, Is.EqualTo(Installer.DaemonArg));
        Assert.That(xml.Descendants(t + "LogonTrigger").Single().Element(t + "UserId")!.Value, Is.EqualTo(@"PC\ryan"));
        Assert.That(xml.Descendants(t + "ExecutionTimeLimit").Single().Value, Is.EqualTo("PT0S"), "Task Scheduler's default would kill it after 3 days");
        Assert.That(xml.Descendants(t + "LogonType").Single().Value, Is.EqualTo("InteractiveToken"), "autopilot types into CLIs on your desktop");
    }

    [Test]
    public void The_logon_task_is_recognised_as_task_scheduler_exports_it_with_an_apostrophe_or_ampersand_in_the_path()
    {
        // M.ORCH-T334: the export writes an apostrophe as is; only &, < and > are escaped.
        const string exe = @"C:\Users\o'brien\AppData\Local\Programs\R&D\MindAttic.Orchestrator.exe";
        var exported = $@"<Exec><Command>C:\Users\o'brien\AppData\Local\Programs\R&amp;D\MindAttic.Orchestrator.exe</Command><Arguments>{Installer.DaemonArg}</Arguments></Exec>";
        Assert.That(Installer.TaskRuns(exported, exe), Is.True);
        Assert.That(Installer.TaskRuns(Installer.TaskXml(exe, @"PC\o'brien"), exe), Is.True, "as written, too");
        Assert.That(Installer.TaskRuns(exported.Replace("o'brien", "someone"), exe), Is.False, "another exe is registered again");
    }

    [TestCase(@"D:\Projects\MindAttic\MindAttic.Orchestrator\src\MindAttic.Orchestrator.Wpf\bin\Debug\net10.0-windows\MindAttic.Orchestrator.exe", true)]
    [TestCase(@"Z:\x\src\MindAttic.Orchestrator.Wpf\obj\Release\net10.0-windows\win-x64\MindAttic.Orchestrator.exe", true)]
    [TestCase(@"Z:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T200\artifacts\MindAttic.Orchestrator.exe", true)]
    [TestCase(@"Z:\Temp\claude\scratchpad\orch-test\MindAttic.Orchestrator.exe", true)]
    [TestCase(@"C:\Users\me\Downloads\MindAttic.Orchestrator.exe", false)]
    [TestCase(@"C:\Apps\MindAttic.Orchestrator\MindAttic.Orchestrator.exe", false)]
    public void Dev_builds_never_install_themselves(string exe, bool dev) =>
        Assert.That(Installer.IsDevBuild(exe, @"Z:\Temp\"), Is.EqualTo(dev));

    [Test]
    public void A_copy_inside_a_git_checkout_or_worktree_is_a_dev_build()
    {
        // M.ORCH-T225: a test build run from a worktree folder self-installed over C:\Apps.
        var root = Path.Combine(Path.GetTempPath(), "orch-dev-" + Guid.NewGuid().ToString("N"));
        try
        {
            var checkout = Path.Combine(root, "Repo");
            var worktree = Path.Combine(root, "Worktree");
            Directory.CreateDirectory(Path.Combine(checkout, ".git"));
            Directory.CreateDirectory(Path.Combine(worktree, "artifacts"));
            File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: elsewhere");
            var notTemp = Path.Combine(root, "not-temp");
            Assert.That(Installer.IsDevBuild(Path.Combine(checkout, "artifacts", Installer.ExeName), notTemp), Is.True, "a checkout's .git folder");
            Assert.That(Installer.IsDevBuild(Path.Combine(worktree, "artifacts", Installer.ExeName), notTemp), Is.True, "a worktree's .git file");
            Assert.That(Installer.IsDevBuild(Path.Combine(root, "Downloads", Installer.ExeName), notTemp), Is.False, "no repo above it: a download");
            Assert.That(Installer.IsDevBuild(Path.Combine(root, "Downloads", Installer.ExeName), root), Is.True, "under the temp folder: a scratch copy");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public void A_newer_installed_exe_is_never_replaced_by_an_older_copy()
    {
        var root = Path.Combine(Path.GetTempPath(), "orch-newer-" + Guid.NewGuid().ToString("N"));
        try
        {
            var installer = new Installer(Path.Combine(root, "Apps"));
            var copy = Path.Combine(root, "Downloads", Installer.ExeName);
            Directory.CreateDirectory(installer.InstallDir);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.WriteAllText(installer.InstalledExe, "installed");
            File.WriteAllText(copy, "copy");
            Assert.That(installer.WhyNotReplace(installer.InstalledExe), Is.Null, "the installed copy itself");

            File.SetLastWriteTimeUtc(installer.InstalledExe, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddHours(-1));
            Assert.That(installer.WhyNotReplace(copy), Does.Contain("newer"));
            Assert.Throws<IOException>(() => installer.Install(copy));
            Assert.That(File.ReadAllText(installer.InstalledExe), Is.EqualTo("installed"), "the newer install is untouched");

            File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddHours(1));
            Assert.That(installer.WhyNotReplace(copy), Is.Null, "a newer copy upgrades");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public void A_shortcut_with_no_icon_set_is_not_the_same_path_and_does_not_throw()
    {
        // WScript reports an unset IconLocation as ",0": its path part is empty (every shortcut made before M.ORCH-T190).
        var exe = @"C:\Apps\MindAttic.Orchestrator\MindAttic.Orchestrator.exe";
        Assert.That(Installer.SamePath(",0".Split(',')[0], exe), Is.False);
        Assert.That(Installer.SamePath("", ""), Is.False);
        Assert.That(Installer.SamePath(exe.ToUpperInvariant(), exe), Is.True);
    }

    [Test]
    public void Installs_to_the_house_folder_unless_a_copy_already_lives_elsewhere()
    {
        var root = Path.Combine(Path.GetTempPath(), "orch-install-" + Guid.NewGuid().ToString("N"));
        var preferred = Path.Combine(root, "Apps");
        var fallback = Path.Combine(root, "Programs");
        try
        {
            Assert.That(Installer.ChooseInstallDir(preferred, fallback), Is.EqualTo(preferred));

            Directory.Delete(preferred);
            Directory.CreateDirectory(fallback);
            File.WriteAllText(Path.Combine(fallback, Installer.ExeName), "");
            Assert.That(Installer.ChooseInstallDir(preferred, fallback), Is.EqualTo(fallback), "an existing install stays where it is");

            var installer = new Installer(fallback);
            Assert.That(installer.IsInstalled(Path.Combine(fallback, Installer.ExeName.ToUpperInvariant())), Is.True);
            Assert.That(installer.IsInstalled(@"C:\Users\me\Downloads\" + Installer.ExeName), Is.False);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public void The_workspace_picked_on_first_run_is_the_one_everything_uses()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-ws-" + Guid.NewGuid().ToString("N"));
        var store = new JsonStore(dir);
        try
        {
            Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(MindAttic.Workflow.Workspace.DefaultRoot));
            store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = @"E:\Code" });
            Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(@"E:\Code"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
