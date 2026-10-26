using System.Text;
using MindAttic.Orchestrator.TermHost.Replay;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// TermHost's exact replay (M.ORCH-T929): recorded VT goes into the headless emulator, its snapshot goes into a fresh one
/// of the same size, and the two must hold the same grids (both screens, scrollback, attributes, cursor and modes). The
/// snapshot is a canonical form of the state, so comparing two snapshots compares the grids.
/// </summary>
public sealed class HeadlessScreenTests
{
    private const string Esc = "\u001b";

    private static byte[] B(string text) => Encoding.UTF8.GetBytes(text);

    private static string S(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static HeadlessScreen Fed(int cols, int rows, params string[] chunks)
    {
        var screen = new HeadlessScreen(cols, rows);
        foreach (var chunk in chunks) screen.Feed(B(chunk));
        return screen;
    }

    /// <summary>Replays <paramref name="source"/>'s snapshot into a fresh emulator of its size and checks the two agree.</summary>
    private static HeadlessScreen AssertReplaysExactly(HeadlessScreen source)
    {
        var snapshot = source.Snapshot();
        var replica = new HeadlessScreen(source.Cols, source.Rows);
        replica.Feed(snapshot);
        Assert.Multiple(() =>
        {
            Assert.That(S(replica.Snapshot()), Is.EqualTo(S(source.Snapshot())), "the grids differ");
            Assert.That((replica.CursorX, replica.CursorY), Is.EqualTo((source.CursorX, source.CursorY)), "the cursor moved");
            Assert.That(replica.OnAlternateScreen, Is.EqualTo(source.OnAlternateScreen), "the active screen differs");
            Assert.That(replica.ScrollbackRows, Is.EqualTo(source.ScrollbackRows), "the scrollback differs");
        });
        return replica;
    }

    /// <summary>A Claude-like session: a transcript that scrolls into scrollback, and a live region (spinner, prompt box)
    /// redrawn in place with cursor-up and erase-line, several times.</summary>
    private static string[] ClaudeLike()
    {
        var chunks = new List<string> { $"{Esc}[?25l{Esc}[?2004h" };
        for (var i = 0; i < 40; i++) chunks.Add($"{Esc}[38;2;215;119;87m●{Esc}[0m Transcript line {i}: some tool output\r\n");
        var frames = new[] { "✻", "✶", "✳", "✢" };
        for (var f = 0; f < 12; f++)
        {
            var redraw = f == 0 ? "" : $"{Esc}[2K{Esc}[1A{Esc}[2K{Esc}[1A{Esc}[2K{Esc}[G";
            chunks.Add(redraw
                       + $"{Esc}[38;2;215;119;87m{frames[f % frames.Length]} Thinking… ({f}s · ↓ {f * 37} tokens){Esc}[0m\r\n"
                       + $"{Esc}[48;2;55;55;55m> {Esc}[0m{Esc}[2mType a message{Esc}[0m\r\n"
                       + $"{Esc}[38;5;244m  ⏵⏵ auto-accept edits on{Esc}[0m");
        }
        return [.. chunks];
    }

    [Test]
    public void A_claude_like_inline_redraw_replays_exactly()
    {
        using var source = Fed(80, 24, ClaudeLike());
        Assert.That(source.ScrollbackRows, Is.GreaterThan(0), "the transcript should have scrolled");
        using var replica = AssertReplaysExactly(source);
        var text = S(source.Snapshot());
        Assert.That(text, Does.Contain("Thinking… (11s · ↓ 407 tokens)"));
        Assert.That(text, Does.Not.Contain("(10s"), "an erased frame of the live region came back");
    }

    [Test]
    public void Twenty_four_bit_colours_survive_the_snapshot()
    {
        using var source = Fed(40, 6, $"{Esc}[38;2;255;100;0morange{Esc}[48;2;1;2;3m on dark{Esc}[0m plain\r\n",
            $"{Esc}[1;3;4m{Esc}[38;5;196mstyled{Esc}[0m{Esc}[7mreverse{Esc}[0m");
        using var replica = AssertReplaysExactly(source);
        var text = S(source.Snapshot());
        Assert.That(text, Does.Contain("38;2;255;100;0"));
        Assert.That(text, Does.Contain("48;2;1;2;3"));
    }

    [Test]
    public void An_alternate_screen_app_replays_both_screens()
    {
        using var source = Fed(40, 8,
            "shell line 1\r\nshell line 2\r\nPS> vim notes.txt",
            $"{Esc}[?1049h{Esc}[H{Esc}[2J{Esc}[44m~ vim ~{Esc}[0m\r\n{Esc}[32mgreen text{Esc}[0m{Esc}[3;5H");
        Assert.That(source.OnAlternateScreen, Is.True);
        var before = S(source.Snapshot());
        Assert.That(S(source.Snapshot()), Is.EqualTo(before), "taking a snapshot changed the emulator");
        Assert.That(before, Does.Contain("PS> vim notes.txt"), "the primary screen is missing");
        Assert.That(before.IndexOf("PS> vim", StringComparison.Ordinal), Is.LessThan(before.IndexOf("?1049h", StringComparison.Ordinal)),
            "the primary screen should come before the switch");
        using var replica = AssertReplaysExactly(source);

        // Leaving the program brings back the same shell screen, cursor included, on both sides.
        const string leave = "\u001b[?1049lexit\r\n";
        source.Feed(B(leave));
        replica.Feed(B(leave));
        Assert.That(source.OnAlternateScreen, Is.False);
        Assert.That(S(replica.Snapshot()), Is.EqualTo(S(source.Snapshot())));
        Assert.That(S(source.Snapshot()), Does.Contain("PS> vim notes.txtexit"));
    }

    [Test]
    public void The_alternate_screen_keeps_working_after_a_snapshot()
    {
        using var control = Fed(30, 6, "prompt> top", $"{Esc}[?1049h{Esc}[H{Esc}[2Jheader{Esc}[4;7H{Esc}[33m");
        using var source = Fed(30, 6, "prompt> top", $"{Esc}[?1049h{Esc}[H{Esc}[2Jheader{Esc}[4;7H{Esc}[33m");
        source.Snapshot();
        const string more = "yellow\u001b[0m\u001b[1;1Hnew header";
        control.Feed(B(more));
        source.Feed(B(more));
        Assert.That(S(source.Snapshot()), Is.EqualTo(S(control.Snapshot())));
        Assert.That((source.CursorX, source.CursorY), Is.EqualTo((control.CursorX, control.CursorY)));
    }

    [Test]
    public void A_resize_mid_stream_replays_exactly_at_the_new_size()
    {
        using var source = Fed(120, 30);
        for (var i = 0; i < 60; i++) source.Feed(B($"wide line {i} " + new string('=', 90) + "\r\n"));
        source.Resize(100, 25);
        foreach (var chunk in ClaudeLike()) source.Feed(B(chunk));
        source.Resize(118, 32);
        source.Feed(B($"{Esc}[2K{Esc}[Gafter the second resize"));
        Assert.That((source.Cols, source.Rows), Is.EqualTo((118, 32)));
        using var replica = AssertReplaysExactly(source);
    }

    [Test]
    public void Soft_wrapped_lines_stay_wrapped_and_reflow_alike()
    {
        using var source = Fed(10, 4, "abcdefghijklmnopqrstuvwxyz\r\n中文字符测试😀 ok\r\nabcdefghi中");
        using var replica = AssertReplaysExactly(source);
        source.Resize(14, 4);
        replica.Resize(14, 4);
        Assert.That(S(replica.Snapshot()), Is.EqualTo(S(source.Snapshot())));
    }

    [Test]
    public void Modes_come_back_with_the_snapshot()
    {
        using var source = Fed(40, 5, $"{Esc}[?2004h{Esc}[?1h{Esc}[?25lhidden cursor");
        using var replica = AssertReplaysExactly(source);
        var text = S(source.Snapshot());
        Assert.That(text, Does.Contain("?2004h"));
        Assert.That(text, Does.Contain("?1h"));
    }

    [Test]
    public void Scrollback_is_capped()
    {
        using var source = new HeadlessScreen(120, 30, scrollbackLines: 1_000);
        Assert.That(source.ScrollbackLines, Is.EqualTo(1_000));
        for (var i = 0; i < 5_000; i++) source.Feed(B($"line {i}\r\n"));
        Assert.That(source.ScrollbackRows, Is.InRange(500, 1_000));
        Assert.That(S(source.Snapshot()), Does.Contain("line 4999"));
        Assert.That(S(source.Snapshot()), Does.Not.Contain("line 10\r\n"));
    }

    [Test]
    public void The_default_cap_keeps_ten_thousand_lines()
    {
        using var source = new HeadlessScreen(120, 30);
        Assert.That(source.ScrollbackLines, Is.EqualTo(HeadlessScreen.DefaultScrollbackLines));
        for (var i = 0; i < 12_000; i++) source.Feed(B($"line {i}\r\n"));
        Assert.That(source.ScrollbackRows, Is.InRange(9_000, 10_000));
    }

    [Test]
    public void The_cursor_tail_is_the_last_position_and_the_pen_after_it()
    {
        Assert.That(S(HeadlessScreen.CursorTail(B("text\r\n\u001b[2;3Hmore\u001b[5;9H\u001b[0m\u001b[38;5;2m"))),
            Is.EqualTo("\u001b[5;9H\u001b[0m\u001b[38;5;2m"));
        Assert.That(HeadlessScreen.CursorTail(B("no cursor")), Is.Empty);
    }

    [Test]
    public void A_failing_snapshot_falls_back_to_the_ring()
    {
        var modes = new ModeTracker();
        modes.Feed(B($"{Esc}[?2004h"));
        Exception? reported = null;
        var (data, reassert, exact) = ReplayScreen.Build(() => throw new DllNotFoundException("ghostty-vt"),
            () => B("ring bytes\u001b[6n"), modes, ex => reported = ex);
        Assert.That(exact, Is.False);
        Assert.That(S(data), Is.EqualTo("ring bytes"));
        Assert.That(S(reassert), Is.EqualTo($"{Esc}[?2004h"));
        Assert.That(reported, Is.TypeOf<DllNotFoundException>());
    }

    [Test]
    public void Without_an_emulator_the_ring_serves()
    {
        var (data, _, exact) = ReplayScreen.Build(null, () => B("ring"), new ModeTracker());
        Assert.That(exact, Is.False);
        Assert.That(S(data), Is.EqualTo("ring"));
    }

    [Test]
    public void A_working_emulator_serves_its_snapshot()
    {
        using var screen = Fed(20, 4, "from the emulator");
        var (data, _, exact) = ReplayScreen.Build(screen.Snapshot, () => B("ring"), new ModeTracker());
        Assert.That(exact, Is.True);
        Assert.That(S(data), Does.Contain("from the emulator"));
        Assert.That(S(data), Does.Not.Contain("ring"));
    }
}
