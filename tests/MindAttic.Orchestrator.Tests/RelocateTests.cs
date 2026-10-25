using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Dock/Undock's reopen targets (M.ORCH-Tnnn): the control-pipe message that ends a CLI without restarting it in
/// place, and the embedded/standalone command lines that resume it elsewhere.</summary>
public sealed class RelocateTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 16, 0, 0, DateTimeKind.Utc);

    [Test]
    public void The_relocate_message_is_restart_resume_with_no_billing_or_config_dir()
    {
        var json = Relocate.Message("sess-1", 4242, @"D:\W\Repo", Now);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("type").GetString(), Is.EqualTo("restart-resume"));
        Assert.That(root.GetProperty("reason").GetString(), Is.EqualTo("relocate"));
        Assert.That(root.GetProperty("session").GetString(), Is.EqualTo("sess-1"));
        Assert.That(root.GetProperty("cliPid").GetInt32(), Is.EqualTo(4242));
        Assert.That(root.GetProperty("cwd").GetString(), Is.EqualTo(@"D:\W\Repo"));
        Assert.That(root.GetProperty("requestedAt").GetString(), Is.EqualTo("2026-10-25T16:00:00Z"));
        Assert.That(root.TryGetProperty("billing", out _), Is.False);
        Assert.That(root.TryGetProperty("configDir", out _), Is.False);
    }

    private static SessionControl Control() => new(new OrchestratorOptions { LauncherExe = @"C:\L\MindAttic.Launcher.exe" })
    {
        Look = d => d is null ? new LoginLook("ryan", "RD", "#654321") : new LoginLook("Amy", "AR", "#123456"),
        RepoNames = () => ["Repo"],
    };

    private static RelocateLock DockLock => new()
    {
        SessionId = "sess-1", Pid = 4242, HostPid = 77, Repo = "Repo", Cwd = @"D:\W\Repo",
        ConfigDir = @"C:\a\.claude-accounts\amy@example.com", Email = "amy@example.com",
        Direction = RelocateDirection.Dock, StartedUtc = Now,
    };

    [Test]
    public void A_resumed_pane_request_embeds_the_host_with_resume_and_the_same_login()
    {
        var request = Relocate.ResumedPaneRequest(DockLock, new OrchestratorOptions { LauncherExe = @"C:\L\MindAttic.Launcher.exe" }, Control());
        Assert.That(request.Title, Is.EqualTo("AR: Repo [Claude]"));
        Assert.That(request.Directory, Is.EqualTo(@"D:\W\Repo"));
        Assert.That(request.Command, Is.EqualTo(new[] { @"C:\L\MindAttic.Launcher.exe", "host", "--path", @"D:\W\Repo", "--title", "AR: Repo [Claude]",
            "--provider", "Claude", "--resume", "sess-1", "--config-dir", @"C:\a\.claude-accounts\amy@example.com" }));
        Assert.That((request.Repo, request.Provider, request.ConfigDir, request.Email, request.Initials, request.TabColor, request.IsMenu),
            Is.EqualTo(("Repo", "Claude", @"C:\a\.claude-accounts\amy@example.com", "amy@example.com", "AR", "#123456", false)));
    }

    [Test]
    public void A_resumed_pane_request_for_the_global_login_has_no_config_dir_or_account_argument()
    {
        var move = DockLock with { ConfigDir = null, Email = null };
        var request = Relocate.ResumedPaneRequest(move, new OrchestratorOptions { LauncherExe = @"C:\L\MindAttic.Launcher.exe" }, Control());
        Assert.That(request.Command, Is.EqualTo(new[] { @"C:\L\MindAttic.Launcher.exe", "host", "--path", @"D:\W\Repo", "--title", "RD: Repo [Claude]",
            "--provider", "Claude", "--resume", "sess-1" }));
    }

    [Test]
    public void Resumed_start_arguments_run_through_SessionControl_s_own_wt_argv_with_resume()
    {
        var args = Relocate.ResumedStartArguments(DockLock, new OrchestratorOptions { LauncherExe = @"C:\L\MindAttic.Launcher.exe" }, Control()).ToList();
        var script = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(args[^1]));
        Assert.That(script, Does.Contain("--resume 'sess-1'"));
        Assert.That(script, Does.Not.Contain("--prompt"));
    }
}
