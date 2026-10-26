using System.Text;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost.Replay;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>TermHost's late-attach replay (M.ORCH-T927): the output ring, the sticky-mode tracker and the replay they build.</summary>
public sealed class TermReplayTests
{
    private static byte[] B(string text) => Encoding.UTF8.GetBytes(text);

    private static string S(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Test]
    public void The_ring_keeps_what_was_written()
    {
        var ring = new Ring(64);
        Assert.That(ring.Append(B("abc")), Is.False);
        Assert.That(ring.Append(B("def")), Is.False);
        Assert.That(S(ring.ToArray()), Is.EqualTo("abcdef"));
    }

    [TestCase("\u001bc")]
    [TestCase("\u001b[2J\u001b[3J")]
    public void A_full_clear_restarts_the_ring_at_the_clear(string clear)
    {
        var ring = new Ring(64);
        ring.Append(B("old screen"));
        Assert.That(ring.Append(B("more" + clear + "new")), Is.True);
        Assert.That(S(ring.ToArray()), Is.EqualTo(clear + "new"));
    }

    [Test]
    public void A_clear_of_the_screen_alone_keeps_the_scrollback()
    {
        var ring = new Ring(64);
        ring.Append(B("old"));
        Assert.That(ring.Append(B("\u001b[2Jnew")), Is.False);
        Assert.That(S(ring.ToArray()), Is.EqualTo("old\u001b[2Jnew"));
    }

    [Test]
    public void A_clear_split_across_two_chunks_is_found()
    {
        var ring = new Ring(64);
        ring.Append(B("old\u001b[2J\u001b"));
        Assert.That(ring.Append(B("[3Jnew")), Is.True);
        Assert.That(S(ring.ToArray()), Is.EqualTo("\u001b[2J\u001b[3Jnew"));
    }

    [Test]
    public void The_clear_the_ring_starts_at_is_not_found_again()
    {
        var ring = new Ring(64);
        Assert.That(ring.Append(B("\u001bc")), Is.True);
        Assert.That(ring.Append(B("x")), Is.False);
        Assert.That(S(ring.ToArray()), Is.EqualTo("\u001bcx"));
    }

    [Test]
    public void A_full_ring_drops_its_oldest_bytes()
    {
        var ring = new Ring(32);
        ring.Append(B(new string('a', 30)));
        ring.Append(B("bbbbbbbb"));
        var kept = S(ring.ToArray());
        Assert.That(kept.Length, Is.LessThanOrEqualTo(32));
        Assert.That(kept, Does.EndWith("aabbbbbbbb"));
    }

    [Test]
    public void A_chunk_bigger_than_the_ring_keeps_its_end()
    {
        var ring = new Ring(32);
        ring.Append(B(new string('x', 40) + "0123456789"));
        Assert.That(ring.Length, Is.EqualTo(32));
        Assert.That(S(ring.ToArray()), Does.EndWith("0123456789"));
    }

    [Test]
    public void The_tracker_follows_the_sticky_modes()
    {
        var modes = new ModeTracker();
        Assert.That(modes.Flags, Is.EqualTo(TermModes.CursorVisible));

        modes.Feed(B("\u001b[?2004h\u001b[?25l\u001b[?1h\u001b[?1002;1006h\u001b[?1004h\u001b[?2026h\u001b[?1049h"));
        Assert.That(modes.Flags, Is.EqualTo(TermModes.BracketedPaste | TermModes.AppCursorKeys | TermModes.Mouse | TermModes.MouseSgr
                                            | TermModes.FocusEvents | TermModes.SyncOutput | TermModes.AltScreen));

        modes.Feed(B("\u001b[?2004l\u001b[?25h\u001b[?1049l"));
        Assert.That(modes.Flags.HasFlag(TermModes.BracketedPaste), Is.False);
        Assert.That(modes.Flags.HasFlag(TermModes.CursorVisible), Is.True);
        Assert.That(modes.Flags.HasFlag(TermModes.AltScreen), Is.False);
    }

    [Test]
    public void A_mode_split_across_chunks_is_seen()
    {
        var modes = new ModeTracker();
        modes.Feed(B("text\u001b[?20"));
        Assert.That(modes.Flags.HasFlag(TermModes.BracketedPaste), Is.False);
        modes.Feed(B("04h more"));
        Assert.That(modes.Flags.HasFlag(TermModes.BracketedPaste), Is.True);
    }

    [Test]
    public void Untracked_and_non_private_modes_are_ignored()
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b[4h\u001b[?7h\u001b[?9001h\u001b[2004h"));
        Assert.That(modes.Flags, Is.EqualTo(TermModes.CursorVisible));
        Assert.That(modes.Reassert(), Is.Empty);
    }

    [Test]
    public void A_full_reset_forgets_the_modes_but_a_screen_clear_does_not()
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b[?2004h\u001b[2J\u001b[3J"));
        Assert.That(modes.Flags.HasFlag(TermModes.BracketedPaste), Is.True);
        modes.Feed(B("\u001bc"));
        Assert.That(modes.Flags.HasFlag(TermModes.BracketedPaste), Is.False);
    }

    [TestCase("\u001b]0;Claude ✳\u0007")]
    [TestCase("\u001b]2;Claude ✳\u001b\\")]
    public void The_title_is_taken_from_osc_0_and_2(string osc)
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b]1;icon\u0007"));
        Assert.That(modes.Title, Is.Null);
        modes.Feed(B(osc));
        Assert.That(modes.Title, Is.EqualTo("Claude ✳"));
    }

    [Test]
    public void A_title_split_across_chunks_is_seen()
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b]2;Hal"));
        Assert.That(modes.Title, Is.Null);
        modes.Feed(B("f\u0007"));
        Assert.That(modes.Title, Is.EqualTo("Half"));
    }

    [Test]
    public void The_reassertion_leaves_out_the_alternate_screen()
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b[?1049h\u001b[?2004h\u001b[?25l\u001b]0;T\u0007"));
        Assert.That(modes.Reassert(), Is.EqualTo("\u001b[?2004h\u001b[?25l\u001b]2;T\u0007"));
    }

    [Test]
    public void The_replay_strips_the_queries_a_renderer_would_answer()
    {
        var data = B("a\u001b[6nb\u001b[cc\u001b[>0cd\u001b[?6ne\u001b[5nf\u001b[0cg\u001b[>ch✳");
        Assert.That(S(ReplayScreen.StripQueries(data)), Is.EqualTo("abcdefgh✳"));
        var clean = B("plain \u001b[31mred\u001b[0m");
        Assert.That(ReplayScreen.StripQueries(clean), Is.SameAs(clean));
    }

    [Test]
    public void The_replay_carries_the_ring_then_the_reassertion()
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b[?2004h"));
        var (data, reassert) = ReplayScreen.Build(B("screen\u001b[6n"), modes);
        Assert.That(S(data), Is.EqualTo("screen"));
        Assert.That(S(reassert), Is.EqualTo("\u001b[?2004h"));
    }

    [Test]
    public void A_replay_on_the_alternate_screen_switches_before_the_ring_when_the_ring_lost_the_switch()
    {
        var modes = new ModeTracker();
        modes.Feed(B("\u001b[?1049h"));
        Assert.That(S(ReplayScreen.Build(B("\u001b[2J\u001b[3Jvim"), modes).Data), Is.EqualTo("\u001b[?1049h\u001b[2J\u001b[3Jvim"));
        Assert.That(S(ReplayScreen.Build(B("\u001b[?1049hvim"), modes).Data), Is.EqualTo("\u001b[?1049hvim"));
    }
}
