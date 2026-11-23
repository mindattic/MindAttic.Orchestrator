using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2117: a locked login (<see cref="LoginLocks"/>, M.ORCH-T2122) is paced to a stop by <see cref="AccountPacer"/>
/// the same way a disabled member's is (<see cref="AccountPacer.DisabledPace"/>), until it's unlocked.</summary>
public sealed class AccountPacerLockTests
{
    private readonly TempDirs temps = new();
    private string dir = "";
    private AccountDirs dirs = null!;

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
    }

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static void SignIn(AccountDir account, string uuid = "acct-b") =>
        File.WriteAllText(account.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = uuid, emailAddress = account.Email } }));

    [Test]
    public void A_locked_login_is_paced_to_a_stop_until_unlocked()
    {
        var b = dirs.For("b@example.com");
        Directory.CreateDirectory(b.Directory);
        SignIn(b);
        var now = DateTime.UtcNow;

        static TranscriptSession Session(string id, long fresh, DateTime at) =>
            new(id, @"D:\W\A", at, default, null) { Events = [new UsageEvent(at, new TokenUsage(fresh, 0, 0, 0))] };
        var sessions = new[] { Session("own", 1_000, now.AddMinutes(-2)) };
        var map = new Dictionary<string, string> { ["own"] = b.Directory };

        LoginLock? locked = new LoginLock { Email = "b@example.com", Name = "Bee Dee", Reason = LoginLocks.ReasonManual, SinceUtc = now, By = "you" };
        var pacer = new AccountPacer { Dirs = dirs, Probe = false, ProjectsDir = Path.Combine(dir, "no-projects"), Locks = () => locked is null ? [] : [locked] };

        var paces = pacer.Tick(sessions, map, new OrchestratorSettings(), now, now.AddHours(-1), write: true);
        var pace = paces.Single();
        Assert.That((pace.Pace.Stop, pace.Pace.SubagentAllowance), Is.EqualTo((true, 0)), "locked: stopped, no subagents");
        Assert.That(pace.Pace.Note, Does.Contain("locked").And.Contain("orchestrator org unlock b@example.com"));

        locked = null;
        var unlocked = pacer.Tick(sessions, map, new OrchestratorSettings(), now, now.AddHours(-1), write: true).Single();
        Assert.That(unlocked.Pace.Stop, Is.False, "unlocked: normal pacing again");
    }
}
