using System.Text;

namespace TermHostStub;

/// <summary>
/// The raw output ring (4 MB): the bytes since the CLI last cleared the whole screen (ESC c, or ESC[2J followed by ESC[3J),
/// bounded by dropping the oldest bytes. Replayed to a viewer that attaches.
/// </summary>
internal sealed class Ring(int capacity)
{
    private static readonly byte[] FullReset = "\u001bc"u8.ToArray(), ClearAll = "\u001b[2J\u001b[3J"u8.ToArray();
    private readonly byte[] buf = new byte[capacity];
    private int len;

    public int Length => len;

    /// <summary>Appends <paramref name="chunk"/>; true when it held a full reset (the ring now starts at it).</summary>
    public bool Append(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length >= capacity)
        {
            chunk[^capacity..].CopyTo(buf);
            len = capacity;
            var whole = buf.AsSpan(0, len);
            var resetAt = Math.Max(whole.LastIndexOf(FullReset), whole.LastIndexOf(ClearAll));
            if (resetAt < 0) return false;
            if (resetAt > 0) { Buffer.BlockCopy(buf, resetAt, buf, 0, len - resetAt); len -= resetAt; }
            return true;
        }
        if (len + chunk.Length > capacity)
        {
            // Drop an eighth extra so the shifting is amortized.
            var drop = Math.Min(len, len + chunk.Length - capacity + capacity / 8);
            Buffer.BlockCopy(buf, drop, buf, 0, len - drop);
            len -= drop;
        }
        chunk.CopyTo(buf.AsSpan(len));
        len += chunk.Length;

        var tailFrom = Math.Max(0, len - chunk.Length - ClearAll.Length);
        var tail = buf.AsSpan(tailFrom, len - tailFrom);
        var at = Math.Max(tail.LastIndexOf(FullReset), tail.LastIndexOf(ClearAll));
        if (at < 0) return false;
        var start = tailFrom + at;
        if (start > 0)
        {
            Buffer.BlockCopy(buf, start, buf, 0, len - start);
            len -= start;
        }
        return true;
    }

    public byte[] Copy() => buf.AsSpan(0, len).ToArray();
}

/// <summary>
/// Follows the sticky terminal modes from the output, the way TerminalPaste.Scan follows bracketed paste, plus the OSC 0/2
/// title: bracketed paste ?2004, cursor ?25, alternate screen ?1049/?47/?1047, application cursor ?1, mouse
/// ?1000/?1002/?1003/?1006, focus ?1004, synchronized output ?2026.
/// </summary>
internal sealed class ModeScanner
{
    private static readonly int[] Tracked = [2004, 25, 1049, 47, 1047, 1, 1000, 1002, 1003, 1006, 1004, 2026];
    private readonly Dictionary<int, bool> modes = [];
    private byte[] pending = [];

    public string? Title { get; private set; }

    public void Reset() { modes.Clear(); Title = null; pending = []; }

    public void Feed(ReadOnlySpan<byte> chunk)
    {
        byte[] buf;
        if (pending.Length == 0) buf = chunk.ToArray();
        else { buf = new byte[pending.Length + chunk.Length]; pending.CopyTo(buf, 0); chunk.CopyTo(buf.AsSpan(pending.Length)); }
        var keep = -1;
        var i = 0;
        while (i < buf.Length)
        {
            var esc = Array.IndexOf(buf, (byte)0x1b, i);
            if (esc < 0) break;
            if (esc + 1 >= buf.Length) { keep = esc; break; }
            var next = buf[esc + 1];
            if (next == '[')
            {
                var j = esc + 2;
                while (j < buf.Length && buf[j] is >= 0x20 and <= 0x3F) j++;
                if (j >= buf.Length) { keep = esc; break; }
                if (buf[j] is (byte)'h' or (byte)'l' && buf[esc + 2] == '?')
                    ApplyModes(buf.AsSpan(esc + 3, j - esc - 3), buf[j] == 'h');
                i = j + 1;
            }
            else if (next == ']')
            {
                var j = esc + 2;
                var end = -1;
                var termLen = 0;
                var incomplete = true;
                for (; j < buf.Length; j++)
                {
                    if (buf[j] == 7) { end = j; termLen = 1; incomplete = false; break; }
                    if (buf[j] != 0x1b) continue;
                    if (j + 1 >= buf.Length) break;
                    incomplete = false;
                    if (buf[j + 1] == '\\') { end = j; termLen = 2; }
                    break;
                }
                if (incomplete) { if (buf.Length - esc > 4096) { i = esc + 2; continue; } keep = esc; break; }
                if (end >= 0) ApplyOsc(buf.AsSpan(esc + 2, end - esc - 2));
                i = end >= 0 ? end + termLen : j;
            }
            else i = esc + 2;
        }
        pending = keep >= 0 ? buf[keep..] : [];
    }

    private void ApplyModes(ReadOnlySpan<byte> parms, bool on)
    {
        foreach (var part in Encoding.ASCII.GetString(parms).Split(';'))
            if (int.TryParse(part, out var n) && Array.IndexOf(Tracked, n) >= 0) modes[n] = on;
    }

    private void ApplyOsc(ReadOnlySpan<byte> body)
    {
        var semi = body.IndexOf((byte)';');
        if (semi < 0) return;
        var code = Encoding.ASCII.GetString(body[..semi]);
        if (code is "0" or "2") Title = Encoding.UTF8.GetString(body[(semi + 1)..]);
    }

    private bool On(params int[] which) => which.Any(w => modes.TryGetValue(w, out var v) && v);

    public uint Flags()
    {
        var f = 0u;
        if (On(2004)) f |= Protocol.Bracketed;
        if (!modes.TryGetValue(25, out var cursor) || cursor) f |= Protocol.CursorVisible;
        if (On(1049, 47, 1047)) f |= Protocol.AltScreen;
        if (On(1)) f |= Protocol.AppCursor;
        if (On(1000, 1002, 1003)) f |= Protocol.Mouse;
        if (On(1006)) f |= Protocol.MouseSgr;
        if (On(1004)) f |= Protocol.Focus;
        if (On(2026)) f |= Protocol.SyncOutput;
        return f;
    }

    /// <summary>The seen modes and the title as VT, to re-assert after a replay. The alternate-screen switches are left out:
    /// entering ?1049 again would clear the screen the replay just painted, and the ring already holds its switch.</summary>
    public string Reassert()
    {
        var sb = new StringBuilder();
        foreach (var (mode, on) in modes)
            if (mode is not (1049 or 47 or 1047)) sb.Append("\u001b[?").Append(mode).Append(on ? 'h' : 'l');
        if (Title is not null) sb.Append("\u001b]2;").Append(Title).Append('\a');
        return sb.ToString();
    }
}
