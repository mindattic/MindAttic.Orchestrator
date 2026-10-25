using System.IO.Pipes;
using MindAttic.Orchestrator.Sessions;
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
        Assert.That(new SessionControl(new OrchestratorOptions { LauncherExe = @"Z:\nope\MindAttic.Launcher.exe" }).Start("A", Path.GetTempPath(), "Claude"),
            Does.StartWith("Launcher not found at"));
        var launcher = typeof(SessionControlTests).Assembly.Location; // any file that exists stands in for the Launcher
        Assert.That(new SessionControl(new OrchestratorOptions { LauncherExe = launcher }).Start("A", dir, "Claude"), Is.EqualTo($"Folder not found: {dir}"));
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
}
