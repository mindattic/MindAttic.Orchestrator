using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The global Pause/Resume toggle (M.ORCH-T808).</summary>
[TestFixture]
public sealed class GlobalPauseTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 22, 30, 0, DateTimeKind.Utc);
    private string dir = "";
    private JsonStore store = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "pause-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Test]
    public void Pause_records_itself_first_ends_every_cli_and_puts_each_claude_ticket_back_first()
    {
        store.SaveAssignments([
            new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", Now, true) { Cli = 7 },
            new Assignment("A-T2", "A", @"D:\WT\A\A-T2", "Claude", Now, true) { Pilot = 9, Log = "x.log" },
            new Assignment("B-T1", "B", @"D:\W\B", "Codex", Now, false),
        ]);
        var pausedWhenKilled = new List<bool>();
        bool Kill(PausedCli c) { pausedWhenKilled.Add(GlobalPause.Active(store)); return c.Pid != 3; }

        var result = GlobalPause.Pause(store, "ryan@x", Now,
            clis: () => [new PausedCli(1, @"C:\Users\r\.local\bin\claude.exe"), new PausedCli(2, null), new PausedCli(3, null)], kill: Kill);

        Assert.That(pausedWhenKilled, Is.EqualTo(new[] { true, true, true }), "recorded before anything is ended, so nothing claims in between");
        Assert.That(result.Killed.Select(k => k.Pid), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(result.Failed, Has.Count.EqualTo(1).And.Some.Contains("pid 3"));
        Assert.That(result.Requeued, Is.EquivalentTo(new[] { "A-T1", "A-T2" }), "every Claude ticket: claims and background agents alike");
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "B-T1" }), "another provider's CLI isn't ended, so its ticket stays");
        Assert.That(AgentAbort.Requeued(store, Now), Is.EquivalentTo(new[] { "A-T1", "A-T2" }), "first in the queue for the next round");
        var state = GlobalPause.Load(store);
        Assert.That((state.Paused, state.SinceUtc, state.By), Is.EqualTo((true, (DateTime?)Now, "ryan@x")));

        GlobalPause.Resume(store);
        Assert.That(GlobalPause.Active(store), Is.False);
    }

    [Test]
    public void Off_until_paused()
    {
        Assert.That(GlobalPause.Active(store), Is.False);
    }

    [Test]
    public void Only_cli_processes_are_ended_never_the_claude_desktop_app()
    {
        Assert.That(GlobalPause.IsCli(@"C:\Users\r\.local\bin\claude.exe"), Is.True);
        Assert.That(GlobalPause.IsCli(null), Is.True, "a path that couldn't be read counts as a CLI");
        Assert.That(GlobalPause.IsCli(@"C:\Users\r\AppData\Local\AnthropicClaude\app-1.0.0\claude.exe"), Is.False);
        Assert.That(GlobalPause.IsCli(@"C:\Program Files\WindowsApps\Claude_1.0.0_x64__abc\app\claude.exe"), Is.False);
    }
}
