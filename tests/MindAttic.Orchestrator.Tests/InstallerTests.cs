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

    [TestCase(@"D:\Projects\MindAttic\MindAttic.Orchestrator\src\MindAttic.Orchestrator.Wpf\bin\Debug\net10.0-windows\MindAttic.Orchestrator.exe", true)]
    [TestCase(@"C:\Users\me\Downloads\MindAttic.Orchestrator.exe", false)]
    [TestCase(@"C:\Apps\MindAttic.Orchestrator\MindAttic.Orchestrator.exe", false)]
    public void Dev_builds_never_install_themselves(string exe, bool dev) =>
        Assert.That(Installer.IsDevBuild(exe), Is.EqualTo(dev));

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
