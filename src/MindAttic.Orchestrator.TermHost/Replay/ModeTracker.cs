using System.Text;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.TermHost.Replay;

/// <summary>
/// Follows the sticky terminal modes in the CLI's output (M.ORCH-T927), the way TerminalPaste.Scan follows bracketed paste,
/// plus the OSC 0/2 title: bracketed paste <c>?2004</c>, cursor <c>?25</c>, alternate screen <c>?1049</c>/<c>?47</c>/<c>?1047</c>,
/// application cursor keys <c>?1</c>, mouse <c>?1000</c>/<c>?1002</c>/<c>?1003</c>/<c>?1006</c>, focus <c>?1004</c>, and
/// synchronized output <c>?2026</c>. A sequence split across chunks is held until the rest arrives. <c>ESC c</c> (a full
/// reset) forgets the modes, as a terminal does; the title is kept. Not thread-safe: the host feeds it under its lock.
/// </summary>
internal sealed class ModeTracker
{
    private static readonly int[] Tracked = [2004, 25, 1049, 47, 1047, 1, 1000, 1002, 1003, 1006, 1004, 2026];
    private static readonly int[] AltScreenModes = [1049, 47, 1047];

    /// <summary>An unfinished sequence longer than this is junk, not held for its end.</summary>
    private const int MaxPending = 4096;

    // In the order first seen, so the re-assertion is stable.
    private readonly List<int> order = [];
    private readonly Dictionary<int, bool> modes = [];
    private byte[] pending = [];

    /// <summary>The last OSC 0 or 2 title, or null when the CLI never set one.</summary>
    public string? Title { get; private set; }

    public void Feed(ReadOnlySpan<byte> chunk)
    {
        byte[] text;
        if (pending.Length == 0) text = chunk.ToArray();
        else
        {
            text = new byte[pending.Length + chunk.Length];
            pending.CopyTo(text, 0);
            chunk.CopyTo(text.AsSpan(pending.Length));
        }

        var keep = -1;
        var i = 0;
        while (i < text.Length)
        {
            var esc = Array.IndexOf(text, (byte)0x1b, i);
            if (esc < 0) break;
            if (esc + 1 >= text.Length) { keep = esc; break; }
            var next = text[esc + 1];
            if (next == '[')
            {
                var j = esc + 2;
                while (j < text.Length && text[j] is >= 0x20 and <= 0x3F) j++;
                if (j >= text.Length)
                {
                    if (text.Length - esc > MaxPending) { i = esc + 2; continue; }
                    keep = esc;
                    break;
                }
                if (text[j] is (byte)'h' or (byte)'l' && j > esc + 2 && text[esc + 2] == '?')
                    ApplyModes(text.AsSpan(esc + 3, j - esc - 3), text[j] == 'h');
                i = j + 1;
            }
            else if (next == ']')
            {
                // OSC: ends at BEL or ST (ESC \).
                var end = -1;
                var terminator = 0;
                var complete = false;
                var j = esc + 2;
                for (; j < text.Length; j++)
                {
                    if (text[j] == 7) { end = j; terminator = 1; complete = true; break; }
                    if (text[j] != 0x1b) continue;
                    if (j + 1 >= text.Length) break;
                    complete = true;
                    if (text[j + 1] == '\\') { end = j; terminator = 2; }
                    break;
                }
                if (!complete)
                {
                    if (text.Length - esc > MaxPending) { i = esc + 2; continue; }
                    keep = esc;
                    break;
                }
                if (end >= 0) ApplyOsc(text.AsSpan(esc + 2, end - esc - 2));
                // An OSC cut short by another ESC: carry on from that ESC.
                i = end >= 0 ? end + terminator : j;
            }
            else
            {
                if (next == 'c') ResetModes();
                i = esc + 2;
            }
        }
        pending = keep >= 0 ? text[keep..] : [];
    }

    private void ResetModes()
    {
        modes.Clear();
        order.Clear();
    }

    private void ApplyModes(ReadOnlySpan<byte> parameters, bool on)
    {
        foreach (var part in Encoding.ASCII.GetString(parameters).Split(';'))
        {
            if (!int.TryParse(part, out var mode) || Array.IndexOf(Tracked, mode) < 0) continue;
            if (!modes.ContainsKey(mode)) order.Add(mode);
            modes[mode] = on;
        }
    }

    private void ApplyOsc(ReadOnlySpan<byte> body)
    {
        var semi = body.IndexOf((byte)';');
        if (semi < 0) return;
        if (body[..semi].SequenceEqual("0"u8) || body[..semi].SequenceEqual("2"u8)) Title = Encoding.UTF8.GetString(body[(semi + 1)..]);
    }

    private bool On(params int[] which) => which.Any(w => modes.TryGetValue(w, out var on) && on);

    /// <summary>The modes as the bits of a Modes or Replay frame.</summary>
    public TermModes Flags
    {
        get
        {
            var flags = TermModes.None;
            if (On(2004)) flags |= TermModes.BracketedPaste;
            if (!modes.TryGetValue(25, out var cursor) || cursor) flags |= TermModes.CursorVisible;
            if (On(AltScreenModes)) flags |= TermModes.AltScreen;
            if (On(1)) flags |= TermModes.AppCursorKeys;
            if (On(1000, 1002, 1003)) flags |= TermModes.Mouse;
            if (On(1006)) flags |= TermModes.MouseSgr;
            if (On(1004)) flags |= TermModes.FocusEvents;
            if (On(2026)) flags |= TermModes.SyncOutput;
            return flags;
        }
    }

    /// <summary>
    /// The modes seen and the title, as VT to write after a replay. The alternate-screen switches are left out (Phase 0):
    /// entering <c>?1049</c> again would clear the screen the replay just painted.
    /// </summary>
    public string Reassert()
    {
        var vt = new StringBuilder();
        foreach (var mode in order)
            if (Array.IndexOf(AltScreenModes, mode) < 0) vt.Append("\u001b[?").Append(mode).Append(modes[mode] ? 'h' : 'l');
        if (Title is not null) vt.Append("\u001b]2;").Append(Title).Append('\a');
        return vt.ToString();
    }
}
