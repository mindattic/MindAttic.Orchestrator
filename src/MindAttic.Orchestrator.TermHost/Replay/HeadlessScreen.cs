using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Ghostty.Vt;
using Ghostty.Vt.Enums;
using Ghostty.Vt.Raw;

namespace MindAttic.Orchestrator.TermHost.Replay;

/// <summary>
/// The Phase 2 replay (M.ORCH-T929, spike docs/spikes/cli-host-viewer.md, Option B): a headless VT emulator, libghostty-vt
/// through the pinned DeBlasis.GhosttyVt bindings, fed every output chunk at the session's size and resized with it. On
/// attach, <see cref="Snapshot"/> serializes its state as a compact VT stream: the primary screen with its scrollback,
/// the alternate screen when it is active (with the switch), attributes including 24-bit colour, the cursor, and the
/// modes. Written to a fresh terminal of the same size it reproduces the screen exactly, whatever sizes the session had
/// before. Not thread-safe: the host feeds it under its lock. Any call may throw (a missing native library above all);
/// the host then drops it and falls back to the raw ring (Option A).
/// </summary>
internal sealed partial class HeadlessScreen : IDisposable
{
    /// <summary>The scrollback kept by default, in lines.</summary>
    public const int DefaultScrollbackLines = 10_000;

    /// <summary>
    /// libghostty-vt caps scrollback in bytes as well as lines, and its byte default (a couple of pages, a few hundred
    /// lines at 120 columns) is far under the line cap; the bindings' <c>TerminalOptions.MaxScrollbackLines</c> doesn't
    /// lift it either. So both are set through the raw ABI: this ceiling in bytes, then the line cap, which is what binds.
    /// </summary>
    private const ulong ScrollbackByteCeiling = 64UL << 20;

    private readonly Terminal terminal;
    private readonly IntPtr handle;

    public HeadlessScreen(int cols, int rows, int scrollbackLines = DefaultScrollbackLines)
    {
        terminal = new Terminal(cols, rows, null);
        try
        {
            handle = NativeHandle(terminal);
            ScrollbackLines = SetScrollbackCaps(scrollbackLines);
        }
        catch
        {
            terminal.Dispose();
            throw;
        }
    }

    /// <summary>The line cap in force: <c>scrollbackLines</c>, or libghostty-vt's default (1,000) when it couldn't be set.
    /// Rows are dropped a page at a time, so a little under the cap is kept.</summary>
    public int ScrollbackLines { get; }

    public int Cols => terminal.Cols;

    public int Rows => terminal.Rows;

    public int ScrollbackRows => terminal.ScrollbackRows;

    public int CursorX => terminal.CursorX;

    public int CursorY => terminal.CursorY;

    public bool OnAlternateScreen => terminal.ActiveScreen == TerminalScreen.Alternate;

    public void Feed(ReadOnlySpan<byte> chunk)
    {
        if (!chunk.IsEmpty) terminal.VTWrite(chunk);
    }

    public void Resize(int cols, int rows)
    {
        if (cols > 0 && rows > 0 && (cols != terminal.Cols || rows != terminal.Rows)) terminal.Resize(cols, rows, 0, 0);
    }

    /// <summary>
    /// The emulator's state as VT, for a fresh terminal of the same size: the primary screen and its scrollback (soft
    /// wraps kept as wraps), then, when an alternate-screen program is running, its switch and screen. Each screen ends
    /// with its cursor position and pen; the last one also carries the modes, scrolling region, keyboard and charsets.
    /// The window title is not in it: the host re-asserts that from its <see cref="ModeTracker"/>.
    /// </summary>
    public byte[] Snapshot()
    {
        if (terminal.ActiveScreen != TerminalScreen.Alternate) return Format(state: true, cursor: true);

        var alt = Format(state: true, cursor: true);
        // libghostty-vt formats only the active screen, so the primary one is reached by switching to it and back. Bytes
        // written in the middle of an escape sequence the CLI hasn't finished would join it: then only the alternate
        // screen goes (the program on it is what the viewer sees anyway).
        if (!InGround()) return alt;

        var on1049 = terminal.ModeGet(TerminalMode.AltScreen);
        var on1047 = terminal.ModeGet(TerminalMode.AltScreenNoSaveCursor);
        var on47 = terminal.ModeGet(TerminalMode.AltScreenLegacy);
        byte[] primary;
        // ?1049l restores the cursor ?1049h saved, so the primary screen's cursor comes out exact; ?47l switches without
        // touching the cursor (or clearing the alternate screen, which ?1047l would), so after it the cursor is left where
        // the primary screen's content ends.
        terminal.VTWrite(on1049 ? "\u001b[?1049l"u8 : "\u001b[?47l"u8);
        try { primary = Format(state: false, cursor: on1049); }
        finally
        {
            // Back without clearing, the switch modes as they were, and the alternate cursor and pen put back.
            terminal.VTWrite("\u001b[?47h"u8);
            terminal.ModeSet(TerminalMode.AltScreenLegacy, on47);
            terminal.ModeSet(TerminalMode.AltScreenNoSaveCursor, on1047);
            terminal.ModeSet(TerminalMode.AltScreen, on1049);
            terminal.VTWrite(CursorTail(alt));
        }
        // The alternate screen's text assumes the cursor starts at home, but the switch leaves it where the primary
        // screen's cursor is. So the switch is taken out of the formatted modes and sent first, then home.
        var mode = on1049 ? 1049 : on1047 ? 1047 : 47;
        var enter = Encoding.ASCII.GetBytes($"\u001b[?{mode}h");
        return [.. primary, .. enter, .. "\u001b[H"u8, .. Without(alt, enter)];
    }

    /// <summary><paramref name="data"/> without the first occurrence of <paramref name="sequence"/>.</summary>
    private static byte[] Without(byte[] data, byte[] sequence)
    {
        var at = data.AsSpan().IndexOf(sequence);
        return at < 0 ? data : [.. data.AsSpan(0, at), .. data.AsSpan(at + sequence.Length)];
    }

    public void Dispose() => terminal.Dispose();

    private byte[] Format(bool state, bool cursor)
    {
        using var formatter = terminal.CreateFormatter(FormatterFormat.Vt, o =>
        {
            o.IncludeStyle = true;
            // A soft-wrapped line goes out as one line, so the fresh terminal wraps it again and keeps it reflowable.
            o.Unwrap = true;
            o.Trim = false;
            o.ExtraModes = state;
            o.ExtraScrollingRegion = state;
            o.ExtraKeyboard = state;
            o.ExtraKittyKeyboard = state;
            o.ExtraCharsets = state;
            o.ExtraCursor = cursor;
            o.ExtraHyperlink = cursor;
            o.ExtraProtection = cursor;
            // Left out: the palette (it would override the viewer's theme), tab stops and the working directory.
        });
        return formatter.ToSpan().ToArray();
    }

    /// <summary>The cursor part a formatted screen ends with: its last cursor position and everything after it (the pen).</summary>
    internal static byte[] CursorTail(byte[] formatted)
    {
        var text = Encoding.Latin1.GetString(formatted);
        var last = CursorPosition().Matches(text).LastOrDefault();
        return last is null ? [] : Encoding.Latin1.GetBytes(text[last.Index..]);
    }

    [GeneratedRegex(@"\x1b\[\d+;\d+H")]
    private static partial Regex CursorPosition();

    private unsafe bool InGround()
    {
        if (handle == IntPtr.Zero) return false;
        ulong ground = 0;
        return GhosttyVtNative.ghostty_terminal_get(handle, GhosttyTerminalData.GHOSTTY_TERMINAL_DATA_VT_GROUND, &ground)
               == GhosttyResult.GHOSTTY_SUCCESS && ground != 0;
    }

    private unsafe int SetScrollbackCaps(int lines)
    {
        if (handle != IntPtr.Zero && lines > 0)
        {
            var bytes = (nuint)ScrollbackByteCeiling;
            GhosttyVtNative.ghostty_terminal_set(handle, GhosttyTerminalOption.GHOSTTY_TERMINAL_OPT_SCROLLBACK_MAX_BYTES, &bytes);
            var value = (nuint)lines;
            GhosttyVtNative.ghostty_terminal_set(handle, GhosttyTerminalOption.GHOSTTY_TERMINAL_OPT_SCROLLBACK_MAX_LINES, &value);
        }
        return (int)(terminal.MaxScrollbackLines ?? 0);
    }

    /// <summary>The bindings keep the native terminal pointer internal (1.4.1); the raw ABI needs it for the line cap and the
    /// parser state. Zero when a later version hides it.</summary>
    private static IntPtr NativeHandle(Terminal terminal) =>
        typeof(Terminal).GetProperty("NativeHandle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(terminal)
            is IntPtr pointer ? pointer : IntPtr.Zero;
}
