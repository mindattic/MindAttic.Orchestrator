using System.Text;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// The input a viewer's terminal sends by itself rather than its user typing (M.ORCH-T993): focus reports (<c>ESC[I</c>,
/// <c>ESC[O</c>) and replies to the CLI's queries (cursor position <c>ESC[r;cR</c>, status <c>ESC[0n</c>, device attributes
/// <c>ESC[?..c</c> / <c>ESC[&gt;..c</c> / <c>ESC[=..c</c>, mode reports <c>ESC[?..$y</c>, window reports <c>ESC[..t</c>,
/// kitty keyboard flags <c>ESC[?..u</c>, and terminated OSC and DCS strings). With several viewers every one of them answers
/// each query and reports its own focus, so the host passes these from the size's owner only, and they never count as typing.
/// <para>One known ambiguity: xterm sends a modified F3 as <c>ESC[1;&lt;mod&gt;R</c>, which reads as a cursor position report;
/// from a viewer that doesn't own the size it is dropped (the owner's always passes).</para>
/// </summary>
internal static class AutoReplies
{
    private const byte Esc = 0x1b, Bel = 0x07;

    /// <summary><paramref name="input"/> without its automatic replies (the same array when it holds none);
    /// <paramref name="found"/> says whether it held any. A sequence split across two frames is left as typed input.</summary>
    public static byte[] Strip(byte[] input, out bool found)
    {
        found = false;
        if (input.AsSpan().IndexOf(Esc) < 0) return input;
        List<byte>? kept = null;
        int i = 0, copyFrom = 0;
        while (i < input.Length)
        {
            if (input[i] != Esc) { i++; continue; }
            var end = ReplyEnd(input, i);
            if (end < 0) { i++; continue; }
            found = true;
            kept ??= new List<byte>(input.Length);
            kept.AddRange(input.AsSpan(copyFrom, i - copyFrom));
            i = copyFrom = end;
        }
        if (kept is null) return input;
        kept.AddRange(input.AsSpan(copyFrom));
        return [.. kept];
    }

    /// <summary>Where the automatic reply that starts at <paramref name="at"/> (an ESC) ends, exclusive; -1 when none starts there.</summary>
    private static int ReplyEnd(byte[] d, int at)
    {
        if (at + 1 >= d.Length) return -1;
        return d[at + 1] switch
        {
            (byte)'[' => CsiReplyEnd(d, at + 2),
            // OSC (colour and clipboard replies) and DCS (XTVERSION, DECRQSS): only with their terminator, so Alt+] or Alt+P
            // typed on its own stays input.
            (byte)']' or (byte)'P' => StringEnd(d, at + 2),
            _ => -1,
        };
    }

    private static int CsiReplyEnd(byte[] d, int from)
    {
        var i = from;
        while (i < d.Length && d[i] is >= 0x30 and <= 0x3f) i++;
        var parameters = Encoding.ASCII.GetString(d, from, i - from);
        var intermediateStart = i;
        while (i < d.Length && d[i] is >= 0x20 and <= 0x2f) i++;
        var intermediates = Encoding.ASCII.GetString(d, intermediateStart, i - intermediateStart);
        if (i >= d.Length || d[i] is < 0x40 or > 0x7e) return -1;
        var final = (char)d[i];
        return IsReply(parameters, intermediates, final) ? i + 1 : -1;
    }

    private static bool IsReply(string p, string intermediates, char final)
    {
        if (final == 'y') return intermediates == "$";
        if (intermediates.Length > 0) return false;
        return final switch
        {
            'I' or 'O' => p.Length == 0,
            'R' => IsNumbers(p.StartsWith('?') ? p[1..] : p, minCount: 2),
            'n' => p is "0" or "3" || (p.StartsWith('?') && IsNumbers(p[1..], minCount: 1)),
            'c' => p.Length > 0 && p[0] is '?' or '>' or '=',
            't' => IsNumbers(p, minCount: 2),
            'u' => p.StartsWith('?') && IsNumbers(p[1..], minCount: 1),
            _ => false,
        };
    }

    private static bool IsNumbers(string p, int minCount)
    {
        if (p.Length == 0) return false;
        var parts = p.Split(';');
        return parts.Length >= minCount && parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit));
    }

    /// <summary>The end of an OSC or DCS string, after its BEL or ST (<c>ESC \</c>); -1 when it is unterminated in this frame.</summary>
    private static int StringEnd(byte[] d, int from)
    {
        for (var i = from; i < d.Length; i++)
        {
            if (d[i] == Bel) return i + 1;
            if (d[i] == Esc) return i + 1 < d.Length && d[i + 1] == (byte)'\\' ? i + 2 : -1;
        }
        return -1;
    }
}
