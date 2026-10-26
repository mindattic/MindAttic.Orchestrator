using System.Text;

namespace MindAttic.Orchestrator.TermHost.Replay;

/// <summary>
/// What a viewer that attaches late is sent: the headless emulator's snapshot (M.ORCH-T929), or, without one, the ring
/// (M.ORCH-T927), cleaned of the queries a renderer would answer again; then the sticky modes and title to re-assert.
/// </summary>
internal static class ReplayScreen
{
    /// <summary>The queries conhost and TUIs send at start: cursor position (<c>ESC[6n</c>, <c>ESC[?6n</c>), status
    /// (<c>ESC[5n</c>) and device attributes (<c>ESC[c</c>, <c>ESC[0c</c>, <c>ESC[&gt;c</c>, <c>ESC[&gt;0c</c>).</summary>
    private static readonly string[] Queries = ["\u001b[6n", "\u001b[?6n", "\u001b[5n", "\u001b[c", "\u001b[0c", "\u001b[>c", "\u001b[>0c"];

    private static readonly string[] AltScreenEnters = ["\u001b[?1049h", "\u001b[?47h", "\u001b[?1047h"];

    /// <summary>
    /// The replay (M.ORCH-T929): the emulator's <paramref name="snapshot"/> when there is one and it works (Exact: no redraw
    /// nudge needed), else the ring (Option A). A snapshot that throws is reported to <paramref name="failed"/>, never thrown.
    /// </summary>
    public static (byte[] Data, byte[] Reassert, bool Exact) Build(Func<byte[]>? snapshot, Func<byte[]> ring, ModeTracker modes,
        Action<Exception>? failed = null)
    {
        if (snapshot is not null)
        {
            try { return (snapshot(), Encoding.UTF8.GetBytes(modes.Reassert()), true); }
            catch (Exception ex) { failed?.Invoke(ex); }
        }
        var (data, reassert) = Build(ring(), modes);
        return (data, reassert, false);
    }

    /// <summary>The ring replay's data and re-assertion VT.</summary>
    public static (byte[] Data, byte[] Reassert) Build(byte[] ring, ModeTracker modes)
    {
        var data = StripQueries(ring);
        // The re-assertion never enters the alternate screen (it would wipe the replay). But when the ring no longer holds the
        // switch (cleared or wrapped since), the replay must still land on the alternate screen: switch before it, not after.
        if (modes.Flags.HasFlag(Term.TermModes.AltScreen) && !HoldsAltScreenEnter(data))
            data = [.. "\u001b[?1049h"u8, .. data];
        return (data, Encoding.UTF8.GetBytes(modes.Reassert()));
    }

    /// <summary>
    /// <paramref name="data"/> without the queries: replayed, the viewer's renderer would answer them again, and the answers
    /// would reach the CLI's input as typed text.
    /// </summary>
    public static byte[] StripQueries(byte[] data)
    {
        // Latin-1 maps each byte to one char and back, so UTF-8 text in between is untouched.
        var text = Encoding.Latin1.GetString(data);
        var changed = false;
        foreach (var query in Queries)
            if (text.Contains(query, StringComparison.Ordinal)) { text = text.Replace(query, "", StringComparison.Ordinal); changed = true; }
        return changed ? Encoding.Latin1.GetBytes(text) : data;
    }

    private static bool HoldsAltScreenEnter(byte[] data)
    {
        var text = Encoding.Latin1.GetString(data);
        return AltScreenEnters.Any(enter => text.Contains(enter, StringComparison.Ordinal));
    }
}
