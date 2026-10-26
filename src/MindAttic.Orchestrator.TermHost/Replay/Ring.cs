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
            chunk[^buffer.Length..].CopyTo(buffer);
            length = buffer.Length;
            searchFrom = oldLength = 0;
        }
        else
        {
            if (length + chunk.Length > buffer.Length)
            {
                // Drop an eighth more than needed, so the shifting is amortized.
                var drop = Math.Min(length, length + chunk.Length - buffer.Length + buffer.Length / 8);
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

    private int LastClear(byte[] clear, int searchFrom, int oldLength)
    {
        var at = buffer.AsSpan(searchFrom, length - searchFrom).LastIndexOf(clear);
        return at >= 0 && searchFrom + at + clear.Length > oldLength ? searchFrom + at : -1;
    }

    public byte[] ToArray() => buffer.AsSpan(0, length).ToArray();

    public void Clear() => length = 0;
}
