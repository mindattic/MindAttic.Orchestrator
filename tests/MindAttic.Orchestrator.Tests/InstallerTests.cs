using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class InstallerTests
{
    [Test]
    public void The_daemon_is_started_from_the_exe_with_the_daemon_arg_and_no_window()
    {
        // M.ORCH-T475: Scheduler's Orchestrator.Daemon job starts it at logon; the exe only starts one itself after an upgrade.
        var psi = Installer.DaemonStartInfo(@"C:\Apps\Mind & Attic\MindAttic.Orchestrator.Desktop.exe");
        Assert.That(psi.FileName, Is.EqualTo(@"C:\Apps\Mind & Attic\MindAttic.Orchestrator.Desktop.exe"));
        Assert.That(psi.ArgumentList, Is.EqualTo(new[] { Installer.DaemonArg }));
        Assert.That(psi.WorkingDirectory, Is.EqualTo(@"C:\Apps\Mind & Attic"));
        Assert.That(psi.CreateNoWindow, Is.True);
    }

    [Test]
    public void The_shortcuts_open_the_redeploying_launcher_when_a_checkout_wrote_one_and_the_exe_otherwise()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"orch-shortcut-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exe = Path.Combine(dir, Installer.ExeName);
            Assert.That(Installer.ShortcutTarget(exe), Is.EqualTo(exe), "a drag-and-drop install has no launcher");
            File.WriteAllText(Path.Combine(dir, Installer.LauncherName), "@echo off");
            Assert.That(Installer.ShortcutTarget(exe), Is.EqualTo(Path.Combine(dir, Installer.LauncherName)));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void No_logon_task_is_registered_any_more()
    {
        // M.ORCH-T475: the task (and its XML) is gone; only its name is kept, to end and delete one an older install left.
        Assert.That(typeof(Installer).GetMethod("TaskXml"), Is.Null);
        Assert.That(Installer.LegacyTaskName, Is.EqualTo(@"\MindAttic\Orchestrator\Orchestrator daemon"));
    }

    [TestCase(@"D:\Projects\MindAttic\MindAttic.Orchestrator\src\MindAttic.Orchestrator.Wpf\bin\Debug\net10.0-windows\MindAttic.Orchestrator.Desktop.exe", true)]
    [TestCase(@"Z:\x\src\MindAttic.Orchestrator.Wpf\obj\Release\net10.0-windows\win-x64\MindAttic.Orchestrator.Desktop.exe", true)]
    [TestCase(@"Z:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T200\artifacts\MindAttic.Orchestrator.Desktop.exe", true)]
    [TestCase(@"Z:\Temp\claude\scratchpad\orch-test\MindAttic.Orchestrator.Desktop.exe", true)]
    [TestCase(@"C:\Users\me\Downloads\MindAttic.Orchestrator.Desktop.exe", false)]
    [TestCase(@"C:\Apps\MindAttic.Orchestrator\MindAttic.Orchestrator.Desktop.exe", false)]
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
        var exe = @"C:\Apps\MindAttic.Orchestrator\MindAttic.Orchestrator.Desktop.exe";
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

            // An install still holding only the pre-M.ORCH-T844 name stays where it is too, to be upgraded in place.
            File.Delete(Path.Combine(fallback, Installer.ExeName));
            File.WriteAllText(Path.Combine(fallback, Installer.LegacyExeName), "");
            Assert.That(Installer.ChooseInstallDir(preferred, fallback), Is.EqualTo(fallback), "a legacy-named install stays where it is");
            File.WriteAllText(Path.Combine(fallback, Installer.ExeName), "");

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
            Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(MindAttic.Tickets.Workspace.DefaultRoot));
            store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = @"E:\Code" });
            Assert.That(OrchestratorOptions.Load(store).WorkspaceRoot, Is.EqualTo(@"E:\Code"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void An_unreadable_settings_file_names_no_workspace_to_switch_to()
    {
        // M.ORCH-T513: the daemon took an unreadable settings.json's defaults as the workspace, and ran a tick against another one.
        var dir = Path.Combine(Path.GetTempPath(), "orch-ws-" + Guid.NewGuid().ToString("N"));
        var store = new JsonStore(dir);
        try
        {
            Assert.That(OrchestratorOptions.TryLoad(store, out var none), Is.True, "no file yet: the default is the choice");
            Assert.That(none.WorkspaceRoot, Is.EqualTo(MindAttic.Tickets.Workspace.DefaultRoot));
            store.SaveSettings(new OrchestratorSettings { WorkspaceRoot = @"E:\Code" });
            Assert.That((OrchestratorOptions.TryLoad(store, out var picked), picked.WorkspaceRoot), Is.EqualTo((true, @"E:\Code")));
            // Settings that can't be read: a garbled row, as a hand-edited settings.json's typo once was.
            using (var connection = store.Db.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE settings SET value = '\"E:\\\\Code\", oops' WHERE name = 'WorkspaceRoot';";
                command.ExecuteNonQuery();
            }
            Assert.That(OrchestratorOptions.TryLoad(store, out _), Is.False);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
