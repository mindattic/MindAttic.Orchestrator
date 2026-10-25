using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Dock/Undock's in-flight bookkeeping (M.ORCH-Tnnn): a relocation locks its session until its old host pid leaves
/// the live scan (reopen it elsewhere) or it's stuck too long (give up). A scratch store, never the real one.</summary>
public sealed class RelocateLocksTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 16, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();
    private JsonStore store = null!;

    [SetUp]
    public void SetUp() => store = new JsonStore(Path.Combine(temps.New(), "store"));

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static RelocateLock Lock(string session = "sess-1", int hostPid = 77, RelocateDirection direction = RelocateDirection.Undock) => new()
    {
        SessionId = session, Pid = 4242, HostPid = hostPid, Repo = "Repo", Cwd = @"D:\W\Repo",
        Direction = direction, StartedUtc = Now,
    };

    [Test]
    public void A_session_already_relocating_refuses_a_second_lock()
    {
        var locks = new RelocateLocks(store);
        Assert.That(locks.TryBegin(Lock(), out var already), Is.True);
        Assert.That(already, Is.Null);
        Assert.That(locks.TryBegin(Lock(hostPid: 99), out already), Is.False, "same session, a different host pid: still one relocation");
        Assert.That(already?.SessionId, Is.EqualTo("sess-1"));
        Assert.That(locks.TryBegin(Lock(session: "sess-2", hostPid: 77), out already), Is.False, "a different session, the same host pid: still refused");
        Assert.That(locks.All(), Has.Count.EqualTo(1));
    }

    [Test]
    public void End_unlocks_a_refused_send()
    {
        var locks = new RelocateLocks(store);
        locks.TryBegin(Lock(), out _);
        Assert.That(locks.End("sess-1"), Is.True);
        Assert.That(locks.All(), Is.Empty);
        Assert.That(locks.End("sess-1"), Is.False, "nothing left to unlock");
    }

    [Test]
    public void Reconcile_ends_a_lock_whose_host_pid_left_the_live_scan_and_leaves_one_still_live()
    {
        var locks = new RelocateLocks(store);
        locks.TryBegin(Lock(session: "gone", hostPid: 1), out _);
        locks.TryBegin(Lock(session: "still-here", hostPid: 2), out _);
        var ended = locks.Reconcile(new HashSet<int> { 2 }, Now.AddMinutes(1));
        Assert.That(ended, Has.Count.EqualTo(1));
        Assert.That((ended[0].Lock.SessionId, ended[0].Gone), Is.EqualTo(("gone", true)));
        Assert.That(locks.All().Select(m => m.SessionId), Is.EqualTo(new[] { "still-here" }));
    }

    [Test]
    public void Reconcile_gives_up_on_a_lock_stuck_past_timeout_while_still_live()
    {
        var locks = new RelocateLocks(store);
        locks.TryBegin(Lock(hostPid: 1), out _);
        Assert.That(locks.Reconcile(new HashSet<int> { 1 }, Now.AddMinutes(20)), Is.Empty, "well within the timeout: still waiting");
        var ended = locks.Reconcile(new HashSet<int> { 1 }, Now.Add(RelocateLocks.Timeout).AddMinutes(1));
        Assert.That(ended, Has.Count.EqualTo(1));
        Assert.That(ended[0].Gone, Is.False, "stuck, not confirmed gone: nothing should be reopened");
    }

    [Test]
    public void Reconcile_does_nothing_with_no_locks_in_flight() =>
        Assert.That(new RelocateLocks(store).Reconcile(new HashSet<int>(), Now), Is.Empty);
}
