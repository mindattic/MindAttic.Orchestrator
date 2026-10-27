using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Several viewers of one TermHost session (M.ORCH-T930): who owns the size, whose input goes through, and the
/// protocol 2 frames, with protocol 1 viewers still accepted.</summary>
public sealed class TermMultiViewerTests
{
    private static readonly DateTime T0 = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void The_first_interactive_viewer_owns_the_size_and_a_later_one_letterboxes()
    {
        var policy = new ViewerPolicy();
        Assert.That(policy.Join(1, interactive: true, 120, 30), Is.True);
        Assert.That(policy.Join(2, interactive: true, 80, 24), Is.False);
        Assert.That(policy.Owner, Is.EqualTo(1));
        Assert.That(policy.OwnerSize, Is.EqualTo(((ushort)120, (ushort)30)));
    }

    [Test]
    public void A_zero_size_hello_never_takes_the_size_nor_inherits_it()
    {
        var policy = new ViewerPolicy();
        Assert.That(policy.Join(1, interactive: true, 0, 0), Is.False, "the gateway's protocol 1 watcher");
        Assert.That(policy.Join(2, interactive: true, 100, 30), Is.True);
        Assert.That(policy.Leave(2), Is.True);
        Assert.That(policy.Owner, Is.Null);
    }

    [Test]
    public void A_read_only_viewer_never_owns_the_size_even_alone()
    {
        var policy = new ViewerPolicy();
        Assert.That(policy.Join(1, interactive: false, 120, 30), Is.False);
        Assert.That(policy.Owner, Is.Null);
        Assert.That(policy.TakeSize(1), Is.False);
        Assert.That(policy.Resize(1, 100, 40), Is.False);
        Assert.That(policy.Owner, Is.Null);
        Assert.That(policy.Join(2, interactive: true, 90, 20), Is.True, "the first interactive viewer takes it");
    }

    [Test]
    public void Only_the_owners_resize_applies_and_the_others_are_kept_for_when_they_take_it()
    {
        var policy = new ViewerPolicy();
        policy.Join(1, true, 120, 30);
        policy.Join(2, true, 80, 24);
        Assert.That(policy.Resize(1, 130, 35), Is.True);
        Assert.That(policy.Resize(2, 100, 28), Is.False);
        Assert.That(policy.OwnerSize, Is.EqualTo(((ushort)130, (ushort)35)));

        Assert.That(policy.TakeSize(2), Is.True);
        Assert.That(policy.Owner, Is.EqualTo(2));
        Assert.That(policy.OwnerSize, Is.EqualTo(((ushort)100, (ushort)28)), "the size it last asked for, not its Hello's");
        Assert.That(policy.TakeSize(2), Is.False, "taking a size it owns changes nothing");
        Assert.That(policy.Resize(2, 0, 28), Is.False, "a zero size is ignored");
        Assert.That(policy.OwnerSize, Is.EqualTo(((ushort)100, (ushort)28)));
    }

    [Test]
    public void A_huge_size_is_capped_so_the_conpty_coord_never_wraps()
    {
        // bughunt 2026-10-27: 40000x40000 wrapped negative in the short COORD and asked the emulator for 1.6G cells.
        var policy = new ViewerPolicy();
        Assert.That(policy.Join(1, true, 40000, 40000), Is.True);
        Assert.That(policy.OwnerSize, Is.EqualTo((ViewerPolicy.MaxCols, ViewerPolicy.MaxRows)));
        Assert.That(policy.Resize(1, ushort.MaxValue, 50), Is.True);
        Assert.That(policy.OwnerSize, Is.EqualTo((ViewerPolicy.MaxCols, (ushort)50)));
        Assert.That(policy.Resize(1, 200, 60), Is.True);
        Assert.That(policy.OwnerSize, Is.EqualTo(((ushort)200, (ushort)60)), "a sane size is kept as asked");
    }

    [Test]
    public void The_viewer_that_last_typed_owns_the_size()
    {
        var policy = new ViewerPolicy();
        policy.Join(1, true, 120, 30);
        policy.Join(2, true, 80, 24);

        var first = policy.Input(2, T0);
        Assert.That(first.Accept, Is.True);
        Assert.That(first.OwnerChanged, Is.True);
        Assert.That(policy.Owner, Is.EqualTo(2));

        var again = policy.Input(2, T0.AddSeconds(1));
        Assert.That(again.OwnerChanged, Is.False);

        var back = policy.Input(1, T0.AddSeconds(10));
        Assert.That(back.OwnerChanged, Is.True);
        Assert.That(policy.Owner, Is.EqualTo(1));
    }

    [Test]
    public void Input_from_a_read_only_viewer_is_dropped_and_takes_nothing()
    {
        var policy = new ViewerPolicy();
        policy.Join(1, true, 120, 30);
        policy.Join(2, false, 80, 24);
        var verdict = policy.Input(2, T0);
        Assert.That(verdict.Accept, Is.False);
        Assert.That(verdict.OwnerChanged, Is.False);
        Assert.That(policy.Owner, Is.EqualTo(1));
        Assert.That(policy.Input(99, T0).Accept, Is.False, "an unknown viewer's input is dropped too");
    }

    [Test]
    public void Input_from_every_interactive_viewer_is_merged()
    {
        var policy = new ViewerPolicy();
        policy.Join(1, true, 120, 30);
        policy.Join(2, true, 80, 24);
        Assert.That(policy.Input(1, T0).Accept, Is.True);
        Assert.That(policy.Input(2, T0.AddMilliseconds(10)).Accept, Is.True);
        Assert.That(policy.Input(1, T0.AddMilliseconds(20)).Accept, Is.True);
    }

    [Test]
    public void Two_viewers_typing_within_two_seconds_collide()
    {
        var policy = new ViewerPolicy();
        policy.Join(1, true, 120, 30);
        policy.Join(2, true, 80, 24);
        policy.Join(3, false, 80, 24);

        Assert.That(policy.Input(1, T0).Colliding, Is.Empty);
        Assert.That(policy.Input(1, T0.AddSeconds(1)).Colliding, Is.Empty, "one viewer typing alone never collides");
        Assert.That(policy.Input(2, T0.AddSeconds(2.5)).Colliding, Is.EquivalentTo(new long[] { 2, 1 }));
        Assert.That(policy.Input(2, T0.AddSeconds(5)).Colliding, Is.Empty, "viewer 1 last typed more than two seconds before");
    }

    [Test]
    public void When_the_owner_leaves_the_most_recently_active_interactive_viewer_takes_over()
    {
        var policy = new ViewerPolicy();
        policy.Join(1, true, 120, 30);
        policy.Join(2, true, 80, 24);
        policy.Join(3, true, 100, 40);
        policy.Join(4, false, 90, 20);
        policy.Input(2, T0);
        policy.Input(1, T0.AddSeconds(5));

        Assert.That(policy.Leave(3), Is.False, "a viewer that didn't own it leaves without a handover");
        Assert.That(policy.Leave(1), Is.True);
        Assert.That(policy.Owner, Is.EqualTo(2));
        Assert.That(policy.OwnerSize, Is.EqualTo(((ushort)80, (ushort)24)));

        Assert.That(policy.Leave(2), Is.True);
        Assert.That(policy.Owner, Is.Null, "a read-only viewer is never handed the size");
        Assert.That(policy.Count, Is.EqualTo(1));
    }

    [Test]
    public void A_protocol_2_hello_carries_the_flags_and_the_viewer_id()
    {
        var hello = TermProtocol.ReadViewerHello(TermProtocol.Hello(2, 120, 30, interactive: false, "pop-out-7"));
        Assert.That(hello, Is.EqualTo(new TermHello(2, 120, 30, false, "pop-out-7")));
        Assert.That(TermProtocol.ReadHello(TermProtocol.Hello(2, 120, 30, true, "x")), Is.EqualTo(((ushort)2, (ushort)120, (ushort)30)));
    }

    [Test]
    public void A_protocol_1_hello_is_an_interactive_viewer_and_a_protocol_1_host_gets_the_plain_hello()
    {
        Assert.That(TermProtocol.ReadViewerHello(TermProtocol.Hello(1, 100, 25)), Is.EqualTo(new TermHello(1, 100, 25, true, "")));
        Assert.That(TermProtocol.Hello(1, 100, 25, interactive: false, "ignored"), Is.EqualTo(TermProtocol.Hello(1, 100, 25)));
        Assert.That(TermProtocol.ReadViewerHello(new byte[5]), Is.Null);
    }

    [Test]
    public void The_host_speaks_each_supported_protocol_with_its_viewer()
    {
        Assert.That(TermProtocol.Version, Is.EqualTo(TermProtocol.MultiViewer));
        Assert.That(TermProtocol.Supported, Does.Contain((ushort)1), "protocol 1 hosts may still be running");
        Assert.That(TermProtocol.Negotiate(1), Is.EqualTo((ushort)1));
        Assert.That(TermProtocol.Negotiate(2), Is.EqualTo((ushort)2));
        Assert.That(TermProtocol.Negotiate(3), Is.Null);
        Assert.That(TermProtocol.Check(1).Ok, Is.True);
    }

    [Test]
    public void An_unlisted_host_keeps_the_flag_when_it_relaunches_itself_detached()
    {
        var options = HostOptions.Parse(["--unlisted", "--", "cmd.exe"], out var error);
        Assert.That(error, Is.Null);
        Assert.That(options!.Unlisted, Is.True);
        Assert.That(options.DetachedArguments(), Does.Contain("--unlisted"));
        Assert.That(HostOptions.Parse(["--", "cmd.exe"], out _)!.Unlisted, Is.False);
    }

    [Test]
    public void A_size_frame_says_whether_its_viewer_owns_the_size()
    {
        Assert.That(TermProtocol.ReadSizeOwner(TermProtocol.Size(80, 24, owned: true)), Is.EqualTo(((ushort)80, (ushort)24, (bool?)true)));
        Assert.That(TermProtocol.ReadSizeOwner(TermProtocol.Size(80, 24, owned: false)), Is.EqualTo(((ushort)80, (ushort)24, (bool?)false)));
        Assert.That(TermProtocol.ReadSizeOwner(TermProtocol.Size(80, 24)), Is.EqualTo(((ushort)80, (ushort)24, (bool?)null)));
        Assert.That(TermProtocol.ReadSize(TermProtocol.Size(80, 24, owned: true)), Is.EqualTo(((ushort)80, (ushort)24)), "a protocol 1 reader still reads it");
    }
}
