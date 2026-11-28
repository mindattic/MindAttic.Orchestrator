using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary><see cref="TermSessionReadiness"/> (M.ORCH-T2409): whether a TermHost session is up (its registry record Running,
/// still so a few seconds later), and submitting its prompt through the pipe once it is -- driven here with a fake registry,
/// clock and send, never a real TermHost.</summary>
public sealed class TermSessionReadinessTests
{
    private static TermSessionRecord Running(string id) => new() { Id = id, HostPid = 1, State = TermSessionStates.Running };
    private static TermSessionRecord Exited(string id) => new() { Id = id, HostPid = 1, State = TermSessionStates.Exited };

    // ── WaitUpAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task Up_once_running_and_still_running_after_the_confirm_wait()
    {
        var calls = 0;
        TermSessionRecord? Lookup(string id) => ++calls switch { 1 => null, _ => Running(id) };
        var clock = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);
        var delays = new List<TimeSpan>();
        Task Delay(TimeSpan ts, CancellationToken _) { delays.Add(ts); clock += ts; return Task.CompletedTask; }

        var up = await TermSessionReadiness.WaitUpAsync("s1", TimeSpan.FromSeconds(30), Lookup, () => clock, Delay);

        Assert.That(up, Is.True);
        Assert.That(delays, Is.EqualTo(new[] { TermSessionReadiness.PollInterval, TermSessionReadiness.StillUpAfter }));
    }

    [Test]
    public async Task A_session_that_exits_within_the_confirm_wait_is_not_trusted()
    {
        var calls = 0;
        TermSessionRecord? Lookup(string id) => ++calls switch { 1 => Running(id), _ => Exited(id) };
        var clock = DateTime.UtcNow;
        Task Delay(TimeSpan ts, CancellationToken _) { clock += ts; return Task.CompletedTask; }

        var up = await TermSessionReadiness.WaitUpAsync("s1", TimeSpan.FromSeconds(30), Lookup, () => clock, Delay);

        Assert.That(up, Is.False);
    }

    [Test]
    public async Task A_session_that_never_appears_running_times_out()
    {
        var clock = DateTime.UtcNow;
        Task Delay(TimeSpan ts, CancellationToken _) { clock += ts; return Task.CompletedTask; }

        var up = await TermSessionReadiness.WaitUpAsync("s1", TimeSpan.FromSeconds(3), _ => null, () => clock, Delay);

        Assert.That(up, Is.False);
    }

    [Test]
    public async Task WaitUpAsync_public_overload_uses_the_real_registry_and_gives_up_at_once_for_a_zero_timeout()
    {
        var up = await TermSessionReadiness.WaitUpAsync("no-such-session-" + Guid.NewGuid().ToString("N"), TimeSpan.Zero);
        Assert.That(up, Is.False);
    }

    // ── SubmitWhenReadyAsync ─────────────────────────────────────────────

    [Test]
    public async Task Waits_up_settles_then_sends_a_bare_enter_through_the_pipe()
    {
        var clock = DateTime.UtcNow;
        Task Delay(TimeSpan ts, CancellationToken _) { clock += ts; return Task.CompletedTask; }
        var sent = new List<string>();
        Task Send(string id) { sent.Add(id); return Task.CompletedTask; }

        var ok = await TermSessionReadiness.SubmitWhenReadyAsync("s1", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10),
            Running, () => clock, Delay, Send);

        Assert.That(ok, Is.True);
        Assert.That(sent, Is.EqualTo(new[] { "s1" }));
    }

    [Test]
    public async Task Never_sends_when_the_session_never_comes_up()
    {
        var clock = DateTime.UtcNow;
        Task Delay(TimeSpan ts, CancellationToken _) { clock += ts; return Task.CompletedTask; }
        var sent = new List<string>();
        Task Send(string id) { sent.Add(id); return Task.CompletedTask; }

        var ok = await TermSessionReadiness.SubmitWhenReadyAsync("s1", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3),
            _ => null, () => clock, Delay, Send);

        Assert.That(ok, Is.False);
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public async Task SubmitWhenReadyAsync_public_overload_never_sends_for_a_session_that_never_comes_up()
    {
        var ok = await TermSessionReadiness.SubmitWhenReadyAsync("no-such-session-" + Guid.NewGuid().ToString("N"), TimeSpan.Zero, TimeSpan.Zero);
        Assert.That(ok, Is.False);
    }
}
