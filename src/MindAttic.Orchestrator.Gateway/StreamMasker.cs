using System.Buffers;
using System.Text;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// Masks secrets in one remote stream's VT output, chunk by chunk (M.ORCH-T935): each printable character that is part of a
/// secret (<see cref="SecretMasker"/>) becomes one <c>•</c>, so a token reads <c>•••…</c> and the remote screen's layout (cursor
/// column, wrapping) stays exactly the host's. Nothing else changes: escape sequences, controls and every other character pass
/// through byte for byte. Never applied to a local viewer.
/// <list type="bullet">
/// <item><b>Escapes inside a run.</b> The stream is split into printable text, controls and escape sequences (CSI, OSC, DCS,
/// APC, PM, SOS, and the short ESC forms). Matching runs on the text alone, so an SGR colour change in the middle of a token
/// doesn't hide it; a control (CR, LF, TAB, BS) ends a run. Only text bytes are ever replaced, so an escape sequence is never
/// corrupted.</item>
/// <item><b>Chunk boundaries.</b> A trailing run of token characters, an unfinished escape sequence or UTF-8 character is held
/// back (at most <see cref="MaxHoldChars"/>) until the next chunk, and the text already sent stays as context
/// (<see cref="contextChars"/>), so a token split across two frames is still caught, and a prefix such as <c>token=</c>
/// sent in the previous frame still counts. The caller flushes what's held after <see cref="TermBridge.FlushAfter"/> of quiet,
/// so normal output is delayed by no more than that.</item>
/// <item><b>PEM blocks.</b> After a <c>-----BEGIN … PRIVATE KEY-----</c> line every printable character is masked through the
/// <c>-----END … PRIVATE KEY-----</c> line, across any number of chunks (at most <see cref="PemCap"/> characters).</item>
/// </list>
/// </summary>
internal sealed class StreamMasker(SecretMasker secrets)
{
    /// <summary>The most text characters held back waiting for the rest of a token.</summary>
    public const int MaxHoldChars = 4096;

    /// <summary>The most bytes of an unfinished escape sequence held back (a longer one goes as is).</summary>
    public const int MaxEscape = 64 * 1024;

    /// <summary>The most characters masked after a BEGIN with no END before it gives up.</summary>
    public const int PemCap = 16 * 1024;

    /// <summary>The mask: one per secret character.</summary>
    public static ReadOnlySpan<byte> Bullet => "•"u8;

    private readonly int contextChars = Math.Clamp(Math.Max(256, secrets.LongestLiteral), 256, 4096);
    private readonly StringBuilder context = new();
    private byte[] pending = [];
    private bool inPem;
    private int pemMasked;

    /// <summary>Whether bytes are held back for the next chunk (the caller flushes them after a quiet spell).</summary>
    public bool HasPending => pending.Length > 0;

    /// <summary>Forgets what's held and the context (a pause, a fresh replay): nothing held is ever sent.</summary>
    public void Reset()
    {
        pending = [];
        context.Clear();
        inPem = false;
        pemMasked = 0;
    }

    /// <summary>Everything held, masked; nothing is held after it.</summary>
    public byte[] Flush() => Process([], final: true);

    private enum SegKind : byte { Text, Control, Escape }

    private readonly record struct Seg(int Start, int Length, SegKind Kind, int LogicalStart, int LogicalLength);

    /// <summary>The bytes that can go now, masked; the rest is held for the next call. With <paramref name="final"/>, nothing is
    /// held.</summary>
    public byte[] Process(ReadOnlySpan<byte> chunk, bool final = false)
    {
        byte[] buf;
        if (pending.Length == 0) buf = chunk.ToArray();
        else
        {
            buf = new byte[pending.Length + chunk.Length];
            pending.CopyTo(buf, 0);
            chunk.CopyTo(buf.AsSpan(pending.Length));
        }
        if (buf.Length == 0) return [];

        // 1. Split into text, controls and escapes; the text (with a newline for each control) is what's matched.
        var segs = new List<Seg>();
        var logical = new StringBuilder();
        var parsedEnd = buf.Length;
        var i = 0;
        while (i < buf.Length)
        {
            var b = buf[i];
            if (b == 0x1B)
            {
                var length = EscapeLength(buf, i, final);
                if (length < 0) { parsedEnd = i; break; }
                segs.Add(new Seg(i, length, SegKind.Escape, logical.Length, 0));
                i += length;
            }
            else if (b < 0x20 || b == 0x7F)
            {
                segs.Add(new Seg(i, 1, SegKind.Control, logical.Length, 1));
                logical.Append('\n');
                i++;
            }
            else if (b < 0x80)
            {
                segs.Add(new Seg(i, 1, SegKind.Text, logical.Length, 1));
                logical.Append((char)b);
                i++;
            }
            else
            {
                var status = Rune.DecodeFromUtf8(buf.AsSpan(i), out var rune, out var used);
                if (status == OperationStatus.NeedMoreData && !final) { parsedEnd = i; break; }
                if (status != OperationStatus.Done)
                {
                    // Not UTF-8: passed through, and it ends a run.
                    used = Math.Max(used, 1);
                    segs.Add(new Seg(i, used, SegKind.Control, logical.Length, 1));
                    logical.Append('\n');
                    i += used;
                    continue;
                }
                var text = rune.ToString();
                segs.Add(new Seg(i, used, SegKind.Text, logical.Length, text.Length));
                logical.Append(text);
                i += used;
            }
        }

        // 2. Match on what was sent before (context) plus this, and mark the characters of this to mask.
        var offset = context.Length;
        var combined = context.ToString() + logical;
        var mask = new bool[logical.Length];
        void Mark(int start, int end)
        {
            for (var k = Math.Max(start, offset); k < end; k++) mask[k - offset] = true;
        }
        var pem = PemRegions(combined, offset);
        foreach (var (start, end, _) in pem) Mark(start, end);
        foreach (var (start, end) in secrets.Find(combined))
            if (end > offset) Mark(start, end);

        // 3. Hold back a trailing run of token characters (the rest of a token may follow) and anything unfinished.
        var holdLogical = logical.Length;
        if (!final)
        {
            var k = logical.Length;
            while (k > 0 && IsTokenChar(logical[k - 1])) k--;
            if (logical.Length - k <= MaxHoldChars) holdLogical = k;
        }
        var holdByte = parsedEnd;
        if (holdLogical < logical.Length)
        {
            var held = segs.First(s => s.Kind != SegKind.Escape && s.LogicalStart + s.LogicalLength > holdLogical);
            holdByte = Math.Min(holdByte, held.Start);
        }

        // 4. Write what goes now.
        var output = new ArrayBufferWriter<byte>(holdByte + 16);
        var sentLogical = 0;
        foreach (var seg in segs)
        {
            if (seg.Start >= holdByte) break;
            var masked = false;
            if (seg.Kind == SegKind.Text)
                for (var k = seg.LogicalStart; k < seg.LogicalStart + seg.LogicalLength; k++) masked |= mask[k];
            if (masked) output.Write(Bullet);
            else output.Write(buf.AsSpan(seg.Start, seg.Length));
            sentLogical = seg.LogicalStart + seg.LogicalLength;
        }
        // Inside a PEM block where what was sent ends? Held text is looked at again with the next chunk, markers in it too.
        var boundary = offset + sentLogical;
        var inside = pem.FirstOrDefault(r => r.Start <= boundary && (r.Open || r.End > boundary));
        inPem = inside != default;
        pemMasked = inPem ? pemMasked + boundary - Math.Max(inside.Start, offset) : 0;
        if (pemMasked > PemCap) (inPem, pemMasked) = (false, 0);
        context.Append(logical, 0, sentLogical);
        if (context.Length > contextChars) context.Remove(0, context.Length - contextChars);
        pending = holdByte < buf.Length ? buf[holdByte..] : [];
        return output.WrittenSpan.ToArray();
    }

    /// <summary>The PEM private key bodies (with their END lines) in <paramref name="combined"/>: from the end of a BEGIN line,
    /// or from <paramref name="offset"/> when the last call ended inside one, to the end of the END line, or open to the end.
    /// Only markers that end past <paramref name="offset"/> count: those before were dealt with already.</summary>
    private List<(int Start, int End, bool Open)> PemRegions(string combined, int offset)
    {
        var regions = new List<(int, int, bool)>();
        // A marker may have started in the context: look a marker's length back.
        var from = Math.Max(0, offset - 64);
        var open = inPem;
        var start = offset;
        while (true)
        {
            if (open)
            {
                var end = SecretMasker.PemEnd.Match(combined, from);
                while (end.Success && end.Index + end.Length <= offset) end = end.NextMatch();
                if (!end.Success)
                {
                    regions.Add((start, combined.Length, true));
                    return regions;
                }
                var stop = end.Index + end.Length;
                regions.Add((start, stop, false));
                open = false;
                from = stop;
            }
            else
            {
                var begin = SecretMasker.PemBegin.Match(combined, from);
                while (begin.Success && begin.Index + begin.Length <= offset) begin = begin.NextMatch();
                if (!begin.Success) return regions;
                start = begin.Index + begin.Length;
                open = true;
                from = start;
            }
        }
    }

    /// <summary>A character a token may continue with: while the text ends in these, the rest may follow in the next chunk.</summary>
    internal static bool IsTokenChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '+' or '/' or '=' or '.' or '~';

    /// <summary>The length of the escape sequence at <paramref name="i"/>, or -1 when it's unfinished (only when not
    /// <paramref name="final"/>). A malformed one ends where it stops making sense, so the bytes after it are parsed again.</summary>
    internal static int EscapeLength(byte[] buf, int i, bool final)
    {
        var n = buf.Length;
        if (i + 1 >= n) return final ? n - i : -1;
        var b = buf[i + 1];
        int j;
        switch (b)
        {
            case (byte)'[':
                // CSI: parameters and intermediates (0x20 to 0x3F), then a final byte (0x40 to 0x7E).
                for (j = i + 2; j < n && j - i < 512; j++)
                {
                    var c = buf[j];
                    if (c is >= 0x40 and <= 0x7E) return j - i + 1;
                    if (c is < 0x20 or > 0x3F) return j - i;
                }
                return j - i >= 512 ? j - i : final ? n - i : -1;
            case (byte)']' or (byte)'P' or (byte)'_' or (byte)'^' or (byte)'X':
                // OSC, DCS, APC, PM, SOS: a string up to BEL or ST (ESC \); CAN or SUB cancels it.
                for (j = i + 2; j < n && j - i < MaxEscape; j++)
                {
                    var c = buf[j];
                    if (c is 0x07 or 0x18 or 0x1A) return j - i + 1;
                    if (c == 0x1B)
                    {
                        if (j + 1 >= n) return final ? n - i : -1;
                        return buf[j + 1] == (byte)'\\' ? j - i + 2 : j - i;
                    }
                }
                return j - i >= MaxEscape ? j - i : final ? n - i : -1;
            case >= 0x20 and <= 0x2F:
                // ESC, intermediates, a final byte (charset designations and the like).
                for (j = i + 1; j < n && buf[j] is >= 0x20 and <= 0x2F && j - i < 16; j++) { }
                return j >= n ? (final ? n - i : -1) : j - i + 1;
            default:
                return 2;
        }
    }
}
