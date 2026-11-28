using System.IO.Pipes;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SessionControlTests
{
    [Test, NonParallelizable] // SessionControl.KnownRepoNames is process-wide
    public void ShortName_strips_a_prefix_two_or_more_known_repos_share_and_keeps_a_unique_one_whole()
    {
        var saved = SessionControl.KnownRepoNames;
        try
        {
            SessionControl.KnownRepoNames = ["GTFO.EZ", "GTFO.Dust", "REA.Bloodlines"];
            Assert.That(SessionControl.ShortName("GTFO.EZ"), Is.EqualTo("EZ"), "GTFO. is shared by two known repos");
            Assert.That(SessionControl.ShortName("REA.Bloodlines"), Is.EqualTo("REA.Bloodlines"), "REA. is unique to one repo");
        }
        finally { SessionControl.KnownRepoNames = saved; }
    }

    [Test]
    public void Start_says_what_is_missing_and_launches_nothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-sc-" + Guid.NewGuid().ToString("N"));
        Assert.That(new SessionControl(new OrchestratorOptions { LauncherExe = @"Z:\nope\MindAttic.Launcher.exe" }).Start("A", Path.GetTempPath(), "Claude").Error,
            Does.StartWith("Launcher not found at"));
        var launcher = typeof(SessionControlTests).Assembly.Location; // any file that exists stands in for the Launcher
        Assert.That(new SessionControl(new OrchestratorOptions { LauncherExe = launcher }).Start("A", dir, "Claude").Error, Is.EqualTo($"Folder not found: {dir}"));
    }

    [TestCase(true, "hello\n")]
    [TestCase(false, "hello")]
    public async Task SendAsync_delivers_the_text_with_one_newline_only_when_submitting(bool submit, string expected)
    {
        var pipe = "orch-sc-" + Guid.NewGuid().ToString("N");
        var server = Task.Run(async () =>
        {
            using var s = new NamedPipeServerStream(pipe, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await s.WaitForConnectionAsync();
            using var r = new StreamReader(s);
            return await r.ReadToEndAsync();
        });
        Assert.That(await SessionControl.SendAsync(pipe, "hello\n\n", submit), Is.True);
        Assert.That(await server.WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo(submit ? expected : "hello\n\n"),
            submit ? "trailing newlines collapse to one" : "sent as given");
    }

    [Test]
    public async Task Talking_to_a_tab_nobody_listens_on_is_false_not_an_exception()
    {
        Assert.That(await SessionControl.SendAsync("orch-nobody-" + Guid.NewGuid().ToString("N"), "x", submit: true), Is.False);
        Assert.That(await SessionControl.SubmitAsync("orch-nobody-" + Guid.NewGuid().ToString("N"), "x"), Is.False);
    }

    [Test]
    public async Task Waiting_for_a_tab_that_never_opens_gives_up_after_the_timeout()
    {
        var control = new SessionControl(new OrchestratorOptions());
        var dir = Path.Combine(Path.GetTempPath(), "orch-sc-never-" + Guid.NewGuid().ToString("N"));
        var started = DateTime.UtcNow;
        Assert.That(await control.SubmitWhenReadyAsync(dir, new HashSet<int>(), TimeSpan.Zero, TimeSpan.FromSeconds(2)), Is.False);
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(15)));
    }

    private static SessionControl Control() => new(new OrchestratorOptions { LauncherExe = @"C:\L\MindAttic.Launcher.exe" })
    {
        Look = _ => new LoginLook("w", "RD", "#112233"),
        RepoNames = () => [],
    };

    [Test]
    public void RepoPaneRequest_carries_the_callers_agent_id_instead_of_minting_one()
    {
        var request = Control().RepoPaneRequest("Repo T1", @"D:\Repo", "Claude", null, null, "who@example.com", "agt-given");
        Assert.That(request.AgentId, Is.EqualTo("agt-given"));
    }

    [Test]
    public void RepoPaneRequest_still_mints_a_new_agent_id_when_none_is_given()
    {
        var request = Control().RepoPaneRequest("Repo T1", @"D:\Repo", "Claude", null, null, "who@example.com");
        Assert.That(request.AgentId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void RepoPaneRequest_resume_session_adds_resume_and_drops_the_prompt()
    {
        var request = Control().RepoPaneRequest("Repo T1", @"D:\Repo", "Claude", "a prompt a resume never sends", null, "who@example.com",
            resumeSession: "sess-123");
        Assert.That(request.Command, Does.Contain("--resume").And.Contain("sess-123"));
        Assert.That(request.Command, Does.Not.Contain("--prompt"));
    }

    [Test]
    public void RepoPaneRequest_with_no_resume_session_keeps_the_prompt()
    {
        var request = Control().RepoPaneRequest("Repo T1", @"D:\Repo", "Claude", "do the thing", null, "who@example.com");
        Assert.That(request.Command, Does.Contain("--prompt"));
        Assert.That(request.Command, Does.Not.Contain("--resume"));
    }

    [Test]
    public void TicketLaunch_builds_a_spec_pinning_Login_Repo_Title_the_prompt_and_config_dir_args_and_the_agent_id()
    {
        var (id, request, spec) = Control().TicketLaunch("Repo M.ORCH-T2410", @"D:\Repo", "Claude", "do the thing", @"C:\logins\who@example.com",
            "who@example.com", "agt-claim");

        Assert.That(id, Is.Not.Null.And.Not.Empty);
        Assert.That(request.AgentId, Is.EqualTo("agt-claim"));
        Assert.That(request.Repo, Is.EqualTo("Repo M.ORCH-T2410"));
        Assert.That(request.Command, Does.Contain("--prompt").And.Contain("--config-dir").And.Contain(@"C:\logins\who@example.com"));

        Assert.That(spec.Id, Is.EqualTo(id));
        Assert.That(spec.Login, Is.EqualTo("who@example.com"));
        Assert.That(spec.Repo, Is.EqualTo("Repo M.ORCH-T2410"));
        Assert.That(spec.Title, Is.EqualTo(request.Title));
        Assert.That(spec.CommandLine, Is.EqualTo(request.ProcessCommandLine()));
        Assert.That(spec.Environment[AgentIds.EnvVar], Is.EqualTo("agt-claim"));
    }

    [Test]
    public void TicketLaunch_two_calls_for_the_same_ticket_mint_different_session_ids()
    {
        var control = Control();
        var (first, _, _) = control.TicketLaunch("Repo T1", @"D:\Repo", "Claude", null, null, "who@example.com", "agt-claim");
        var (second, _, _) = control.TicketLaunch("Repo T1", @"D:\Repo", "Claude", null, null, "who@example.com", "agt-claim");
        Assert.That(second, Is.Not.EqualTo(first));
    }
}
