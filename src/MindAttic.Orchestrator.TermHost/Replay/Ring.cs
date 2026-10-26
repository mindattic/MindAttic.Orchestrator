namespace MindAttic.Orchestrator.TermHost.Replay;

/// <summary>
/// The raw output ring replayed to a viewer that attaches (M.ORCH-T927, spike section 3 option A): the CLI's output since it
/// last cleared the whole screen (<c>ESC c</c>, or <c>ESC[2J</c> followed by <c>ESC[3J</c>), which the ring then starts at,
/// bounded by dropping the oldest bytes. Not thread-safe: the host appends and copies under its one lock.
/// </summary>
internal sealed class Ring
{
    /// <summary>The default capacity, 4 MB per session.</summary>
    public const int DefaultCapacity = 4 * 1024 * 1024;

    private static readonly byte[] FullReset = "\u001bc"u8.ToArray(), ClearAll = "\u001b[2J\u001b[3J"u8.ToArray();
    private readonly byte[] buffer;
    private int length;

    public Ring(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, ClearAll.Length * 2);
        buffer = new byte[capacity];
    }

    public int Capacity => buffer.Length;

    public int Length => length;

    /// <summary>Appends <paramref name="chunk"/>; true when it held a full clear (the ring now starts at the last one). A clear
    /// split across two chunks is found when its second half arrives.</summary>
    public bool Append(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty) return false;
        int searchFrom, oldLength;
        if (chunk.Length >= buffer.Length)
        {
            var kept = chunk[SafeCut(chunk, chunk.Length - buffer.Length)..];
            kept.CopyTo(buffer);
            length = kept.Length;
            searchFrom = oldLength = 0;
        }
        else
        {
            if (length + chunk.Length > buffer.Length)
            {
                // Drop an eighth more than needed, so the shifting is amortized; and on to a safe place to start (M.ORCH-T998).
                var drop = Math.Min(length, length + chunk.Length - buffer.Length + buffer.Length / 8);
                drop = SafeCut(buffer.AsSpan(0, length), drop);
                Buffer.BlockCopy(buffer, drop, buffer, 0, length - drop);
                length -= drop;
            }
            oldLength = length;
            // Back up far enough to catch a clear that began in the previous chunk.
            searchFrom = Math.Max(0, length - (ClearAll.Length - 1));
            chunk.CopyTo(buffer.AsSpan(length));
            length += chunk.Length;
        }

        // The last clear that ends in the new bytes; one wholly in the old bytes was handled when they came.
        var start = Math.Max(LastClear(FullReset, searchFrom, oldLength), LastClear(ClearAll, searchFrom, oldLength));
        if (start < 0) return false;
        if (start > 0)
        {
            Buffer.BlockCopy(buffer, start, buffer, 0, length - start);
            length -= start;
        }
        return true;
    }

    /// <summary>How far past the cut a line feed is looked for, and how far before it the start of an escape sequence.</summary>
    private const int LineSearch = 64 * 1024, SequenceLookBack = 512;

    /// <summary>
    /// Where the bytes kept from <paramref name="data"/> may start, at or after <paramref name="from"/> (M.ORCH-T998), so a ring
    /// replay never begins mid UTF-8 character or mid escape sequence: just after the next line feed when one is near, else
    /// past the end of an escape sequence the cut falls inside, and then on to a UTF-8 lead byte.
    /// </summary>
    internal static int SafeCut(ReadOnlySpan<byte> data, int from)
    {
        if (from <= 0) return 0;
        if (from >= data.Length) return data.Length;
        var lf = data.Slice(from, Math.Min(LineSearch, data.Length - from)).IndexOf((byte)'\n');
        if (lf >= 0) return from + lf + 1;

        var at = from;
        var back = Math.Max(0, from - SequenceLookBack);
        var esc = data[back..from].LastIndexOf(Esc);
        if (esc >= 0)
        {
            var end = SequenceEnd(data, back + esc);
            // Unterminated in the ring: start at the next sequence, if any.
            if (end < 0) { var next = data[from..].IndexOf(Esc); at = next < 0 ? data.Length : from + next; }
            else if (end > from) at = end;
        }
        while (at < data.Length && (data[at] & 0xc0) == 0x80) at++;
        return at;
    }

    private const byte Esc = 0x1b;

    /// <summary>The end (exclusive) of the escape sequence at <paramref name="at"/>; -1 when it doesn't end in <paramref name="data"/>.</summary>
    private static int SequenceEnd(ReadOnlySpan<byte> data, int at)
    {
        var i = at + 1;
        if (i >= data.Length) return -1;
        switch (data[i])
        {
            case (byte)'[':
                for (i++; i < data.Length; i++)
                    if (data[i] is >= 0x40 and <= 0x7e) return i + 1;
                return -1;
            case (byte)']' or (byte)'P' or (byte)'_' or (byte)'^' or (byte)'X':
                for (i++; i < data.Length; i++)
                {
                    if (data[i] == 0x07) return i + 1;
                    if (data[i] == Esc) return i + 1 < data.Length ? i + 2 : -1;
                }
                return -1;
            default:
                // ESC, intermediates, then one final byte (ESC c, ESC ( B, ESC 7).
                while (i < data.Length && data[i] is >= 0x20 and <= 0x2f) i++;
                return i < data.Length ? i + 1 : -1;
        }
    }

    private int LastClear(byte[] clear, int searchFrom, int oldLength)
    {
        var at = buffer.AsSpan(searchFrom, length - searchFrom).LastIndexOf(clear);
        return at >= 0 && searchFrom + at + clear.Length > oldLength ? searchFrom + at : -1;
    }

    public byte[] ToArray() => buffer.AsSpan(0, length).ToArray();

    public void Clear() => length = 0;
}
