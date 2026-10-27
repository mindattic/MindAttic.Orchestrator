using System.Buffers;
using System.Text;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// Masks secrets in one remote stream's VT output, chunk by chunk (M.ORCH-T935): each printable character that is part of a
/// secret (<see cref="SecretMasker"/>) becomes one <c>•</c>, so a token reads <c>•••…</c> and the remote screen's layout (cursor
/// column, wrapping) stays exactly the host's. Nothing else changes: controls, escape sequences and every other character pass
/// through byte for byte, except the printable payload of a string sequence (a title, a hyperlink, a clipboard write; see
/// <see cref="WriteEscape"/>), masked the same way (M.ORCH-T980). Never applied to a local viewer.
/// <list type="bullet">
/// <item><b>Escapes inside a run.</b> The stream is split into printable text, controls and escape sequences (CSI, OSC, DCS,
/// APC, PM, SOS, and the short ESC forms). Matching runs on the text alone, so an SGR colour change in the middle of a token
/// doesn't hide it; a control (CR, LF, TAB, BS) ends a run. Only text bytes are ever replaced, so an escape sequence is never
/// corrupted.</item>
/// <item><b>Chunk boundaries.</b> A trailing run of token characters, an unfinished escape sequence or UTF-8 character is held
/// back (at most <see cref="MaxHoldChars"/>) until the next chunk, and the text already sent stays as context
/// (<see cref="contextChars"/>), so a token split across two frames is still caught, and a prefix such as <c>token=</c>
/// sent in the previous frame still counts. The caller flushes what's held once <see cref="DueIn"/> says so
/// (<see cref="FlushDue"/>): ordinary text after <see cref="FlushAfter"/> of quiet, so it is delayed by no more than that.</item>
/// <item><b>Wrapped and slow secrets</b> (M.ORCH-T982). An Ink CLI hard-wraps a long token with a real line break (and the
/// continuation's indent), so matching also runs on the text with such breaks taken out, where a token-character run goes on
/// past one and the part after the break looks generated (a digit, or a capital past its first character), so a whole token
/// ending one line doesn't swallow the next line's first word. A trailing <i>candidate</i>, a run that has begun a known
/// shape (<c>sk-</c> and so on, past the prefix), a value after a secret-sounding name, or the first
/// <see cref="MinLiteralPart"/> characters of a Vault value, is held across a line break and across the quiet-spell flush, until
/// what follows shows it can't be one (it ends, or turns into something else) or at most <see cref="MaxCandidateHold"/>;
/// then the part of it that would be secret is masked rather than sent bare, and the rest of a token that keeps coming is
/// masked as it comes. Ordinary text is never held longer than before.</item>
/// <item><b>PEM blocks.</b> After a <c>-----BEGIN … PRIVATE KEY-----</c> line every printable character is masked through the
/// <c>-----END … PRIVATE KEY-----</c> line, across any number of chunks (at most <see cref="PemCap"/> characters).</item>
/// </list>
/// </summary>
internal sealed class StreamMasker
{
    /// <summary>Held output goes out after this much quiet, so masking never delays ordinary text longer.</summary>
    public static readonly TimeSpan FlushAfter = TimeSpan.FromMilliseconds(40);

    /// <summary>The longest a candidate secret is held waiting to be told apart (M.ORCH-T982); then its secret part is masked.</summary>
    public static readonly TimeSpan MaxCandidateHold = TimeSpan.FromMilliseconds(500);

    /// <summary>A line break a wrapped token may span: at most this many controls (CR, LF), then at most
    /// <see cref="MaxWrapIndent"/> spaces of indent.</summary>
    private const int MaxWrapBreaks = 4, MaxWrapIndent = 16;

    /// <summary>The shortest start of a Vault value held as a candidate (a shorter one would hold ordinary words and prefixes).</summary>
    private const int MinLiteralPart = 8;

    /// <summary>The most text characters held back waiting for the rest of a token.</summary>
    public const int MaxHoldChars = 4096;

    /// <summary>The most bytes of an unfinished escape sequence held back (a longer one goes as is).</summary>
    public const int MaxEscape = 64 * 1024;

    /// <summary>The most characters masked after a BEGIN with no END before it gives up.</summary>
    public const int PemCap = 16 * 1024;

    /// <summary>The mask: one per secret character.</summary>
    public static ReadOnlySpan<byte> Bullet => "•"u8;

    private readonly SecretMasker secrets;
    private readonly TimeProvider time;
    private readonly int contextChars;
    private readonly StringBuilder context = new();
    private byte[] pending = [];
    private bool inPem;
    private int pemMasked;

    // Timing (M.ORCH-T982): when what's held started being held; the candidate held, by where it starts in the whole stream,
    // and since when; and whether all that's held is that candidate (a timed flush let the rest go).
    private long sentTotal;
    private DateTimeOffset pendingSince;
    private long? candidateKey;
    private DateTimeOffset candidateSince;
    private bool candidateOnly;

    // Whether what was sent ends inside a run whose start was masked unfinished (M.ORCH-T1039): the rest of it is masked too.
    private bool maskingRun;

    /// <param name="time">The clock for the hold timings; the system's by default.</param>
    public StreamMasker(SecretMasker secrets, TimeProvider? time = null)
    {
        this.secrets = secrets;
        this.time = time ?? TimeProvider.System;
        contextChars = Math.Clamp(Math.Max(256, secrets.LongestLiteral), 256, 4096);
    }

    /// <summary>Whether bytes are held back for the next chunk (the caller flushes them when <see cref="DueIn"/> says).</summary>
    public bool HasPending => pending.Length > 0;

    /// <summary>How long until what's held should go (<see cref="FlushDue"/>): <see cref="FlushAfter"/> after it started being
    /// held, or for a held candidate alone, <see cref="MaxCandidateHold"/> after it was first held. Null when nothing is held;
    /// zero or less when it's due now.</summary>
    public TimeSpan? DueIn
    {
        get
        {
            if (pending.Length == 0) return null;
            var due = candidateOnly && candidateKey is not null ? candidateSince + MaxCandidateHold : pendingSince + FlushAfter;
            return due - time.GetUtcNow();
        }
    }

    /// <summary>Forgets what's held and the context (a pause, a fresh replay): nothing held is ever sent.</summary>
    public void Reset()
    {
        pending = [];
        context.Clear();
        inPem = false;
        pemMasked = 0;
        candidateKey = null;
        candidateOnly = false;
        maskingRun = false;
    }

    /// <summary>Everything held, masked; nothing is held after it. With <paramref name="maskCandidate"/> (the stream's order needs
    /// it all out now: the session exited, or masking was turned off) a held strong candidate's secret part is masked as if its
    /// hold ran out, never sent bare; a weak one goes as it is (M.ORCH-T1038).</summary>
    public byte[] Flush(bool maskCandidate = false) => Run([], maskCandidate ? Mode.FinalMasking : Mode.Final);

    /// <summary>What's due after a quiet spell: everything held but a candidate still inside <see cref="MaxCandidateHold"/>; once
    /// that runs out, the candidate too, its secret part masked.</summary>
    public byte[] FlushDue() => Run([], Mode.Timed);

    private enum SegKind : byte { Text, Control, Escape }

    private readonly record struct Seg(int Start, int Length, SegKind Kind, int LogicalStart, int LogicalLength);

    /// <summary>How a call ends. Normal holds a trailing run and a candidate; Timed holds only a candidate within its hold;
    /// Final holds nothing; FinalMasking holds nothing and masks a candidate's secret part.</summary>
    private enum Mode : byte { Normal, Timed, Final, FinalMasking }

    /// <summary>The bytes that can go now, masked; the rest is held for the next call. With <paramref name="final"/>, nothing is
    /// held.</summary>
    public byte[] Process(ReadOnlySpan<byte> chunk, bool final = false) => Run(chunk, final ? Mode.Final : Mode.Normal);

    /// <summary>A replay's screen (M.ORCH-T1039): all of it goes now, as with <c>final</c>, but a strong candidate it ends with
    /// (a token half-printed at attach) has its secret part masked rather than sent bare, and the rest of it, as live output
    /// goes on with it, is masked as it comes.</summary>
    public byte[] ProcessReplay(ReadOnlySpan<byte> chunk) => Run(chunk, Mode.FinalMasking);

    private byte[] Run(ReadOnlySpan<byte> chunk, Mode mode)
    {
        var final = mode != Mode.Normal;
        var had = pending.Length > 0;
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

        // 2. Match on what was sent before (context) plus this, and mark the characters to mask (those of this are written).
        var offset = context.Length;
        var combined = context.ToString() + logical;
        var marked = new bool[combined.Length];
        var pem = PemRegions(combined, offset);
        foreach (var (start, end, _) in pem)
            for (var k = start; k < end; k++) marked[k] = true;
        foreach (var (start, end) in secrets.Find(combined))
            for (var k = start; k < end; k++) marked[k] = true;
        // A token hard-wrapped across lines (M.ORCH-T982): matched again with the breaks taken out.
        var (joined, map) = Join(combined);
        if (joined.Length != combined.Length)
            foreach (var (start, end) in secrets.Find(joined))
                for (var k = start; k < end; k++) marked[map[k]] = true;

        // 3. A trailing candidate secret (M.ORCH-T982): held until it's told apart, or its hold runs out and its secret part is
        // masked rather than sent bare.
        var now = time.GetUtcNow();
        // A run whose start was masked when its hold ran out goes on masked to its end (M.ORCH-T1039), even once it no longer
        // looks like a candidate (a short password ends): its secret part was masked, so the rest of it is too.
        var runEndsAt = maskingRun ? ContinueMaskedRun(joined, map, marked, offset) : null;
        var (candidate, strong, literal) = FindCandidate(joined, map, marked);
        var expired = false;
        var forced = false;
        if (mode == Mode.Final)
        {
            // Final holds nothing and lets a weak candidate go, but never the start of a Vault value (M.ORCH-T1049).
            if (literal is { } l)
            {
                for (var k = l.SecretStart; k < l.End; k++) marked[map[k]] = true;
                forced = true;
            }
            (candidate, strong) = (null, null);
        }
        if (candidate is { } c)
        {
            var key = sentTotal - offset + map[c.Start];
            if (candidateKey != key) (candidateKey, candidateSince) = (key, now);
            expired = now - candidateSince >= MaxCandidateHold;
            // Only a strong candidate is masked unfinished (M.ORCH-T1038); a weak one (apiKey = getT) was only held, in case the
            // rest made it a match, and goes as it is. The start of a real Vault value is always strong (M.ORCH-T1049): sent bare
            // after a gap it leaked up to half the secret.
            if (strong is { } s && (mode == Mode.FinalMasking || (mode == Mode.Timed && expired)))
            {
                for (var k = s.SecretStart; k < s.End; k++) marked[map[k]] = true;
                forced = true;
            }
        }
        else candidateKey = null;

        // 4. Hold back a trailing run of token characters (the rest of a token may follow), a candidate, and anything unfinished.
        var holdLogical = logical.Length;
        int? candidateHold = candidate is { } held0 ? Math.Max(map[held0.Start] - offset, 0) : null;
        if (candidateHold is { } ch && logical.Length - ch > MaxHoldChars) candidateHold = null;
        if (mode == Mode.Normal)
        {
            var k = logical.Length;
            while (k > 0 && IsTokenChar(logical[k - 1])) k--;
            if (logical.Length - k <= MaxHoldChars) holdLogical = k;
            if (candidateHold is { } h) holdLogical = Math.Min(holdLogical, h);
        }
        else if (mode == Mode.Timed && !expired && candidateHold is { } h) holdLogical = h;
        var holdByte = parsedEnd;
        if (holdLogical < logical.Length)
        {
            var held = segs.First(s => s.Kind != SegKind.Escape && s.LogicalStart + s.LogicalLength > holdLogical);
            holdByte = Math.Min(holdByte, held.Start);
        }

        // 5. Write what goes now.
        var output = new ArrayBufferWriter<byte>(holdByte + 16);
        var sentLogical = 0;
        foreach (var seg in segs)
        {
            if (seg.Start >= holdByte) break;
            var masked = false;
            if (seg.Kind == SegKind.Text)
                for (var k = seg.LogicalStart; k < seg.LogicalStart + seg.LogicalLength; k++) masked |= marked[offset + k];
            if (masked) output.Write(Bullet);
            else if (seg.Kind == SegKind.Escape) WriteEscape(output, buf.AsSpan(seg.Start, seg.Length));
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
        sentTotal += sentLogical;
        // A forced candidate ends what was sent, so whatever comes next may be the rest of it; a masked run ends once the
        // character that ended it was sent. So does a matched secret that runs to the end of what was sent: the rest of a long one
        // may follow once its start has left the context (bughunt 2026-10-27).
        var endsMatched = !inPem && sentLogical > 0 && marked[offset + sentLogical - 1] && IsTokenChar(logical[sentLogical - 1]);
        if (forced || endsMatched) maskingRun = true;
        else if (runEndsAt is { } ended && ended < sentLogical) maskingRun = false;
        pending = holdByte < buf.Length ? buf[holdByte..] : [];
        if (pending.Length > 0)
        {
            // Timed from when the bytes now held started being held, so held text never waits much longer than FlushAfter.
            if (!had || output.WrittenCount > 0) pendingSince = now;
            // After a timed flush only a candidate is left: it goes when its own hold runs out.
            candidateOnly = mode == Mode.Timed && candidateKey is not null;
        }
        else candidateOnly = false;
        return output.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Marks the rest of a run whose start was masked unfinished (M.ORCH-T1039): the token characters this call's text begins
    /// with (in <paramref name="joined"/>, so a wrap's break is crossed as matching crosses it), when the run goes on into it.
    /// Returns where in this call's text the run ended (the character that ended it; -1 when it ended before), or null while
    /// it may still go on (it reaches the end, or only a line break and an indent follow it).
    /// </summary>
    private static int? ContinueMaskedRun(string joined, int[] map, bool[] marked, int offset)
    {
        // The first character of this call's text: map rises, so a binary search finds it.
        int lo = 0, hi = joined.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (map[mid] < offset) lo = mid + 1;
            else hi = mid;
        }
        var j = lo;
        if (j >= joined.Length) return null;
        if (j == 0 || !IsTokenChar(joined[j - 1])) return -1;
        while (j < joined.Length && IsTokenChar(joined[j])) marked[map[j++]] = true;
        if (j == joined.Length) return null;
        var t = j;
        var breaks = 0;
        for (; t < joined.Length && joined[t] == '\n' && breaks < MaxWrapBreaks; breaks++) t++;
        for (var indent = 0; breaks > 0 && t < joined.Length && joined[t] == ' ' && indent < MaxWrapIndent; indent++) t++;
        return breaks > 0 && t == joined.Length ? null : map[j] - offset;
    }

    /// <summary>A trailing candidate secret, in <c>joined</c>'s positions: where it starts, where its secret part starts (past a
    /// shape's prefix or a name), and its end.</summary>
    private readonly record struct Candidate(int Start, int SecretStart, int End);

    /// <summary><paramref name="combined"/> with the line breaks a wrapped token spans taken out, and for each of its characters
    /// where it is in <paramref name="combined"/>. A break (controls, then an indent) between two token characters is taken out
    /// only when the part after it looks generated (<see cref="LooksGenerated"/>), so a token that ends a line doesn't run on
    /// into an ordinary word, a capitalised one included, on the next.</summary>
    private static (string Joined, int[] Map) Join(string combined)
    {
        var joined = new StringBuilder(combined.Length);
        var map = new int[combined.Length];
        var n = 0;
        for (var i = 0; i < combined.Length;)
        {
            if (combined[i] == '\n' && i > 0 && IsTokenChar(combined[i - 1]))
            {
                var j = i;
                for (var breaks = 0; j < combined.Length && combined[j] == '\n' && breaks < MaxWrapBreaks; breaks++) j++;
                for (var indent = 0; j < combined.Length && combined[j] == ' ' && indent < MaxWrapIndent; indent++) j++;
                if (j < combined.Length && IsTokenChar(combined[j]) && LooksGenerated(combined, j))
                {
                    i = j;
                    continue;
                }
            }
            joined.Append(combined[i]);
            map[n++] = i;
            i++;
        }
        return (joined.ToString(), map);
    }

    /// <summary>Whether the token-character run at <paramref name="at"/> has a digit, or a capital past its first character, as
    /// the rest of a generated token nearly always does and an ordinary word ("next", "Done", "Error:") doesn't.</summary>
    private static bool LooksGenerated(string text, int at)
    {
        for (var k = at; k < text.Length && IsTokenChar(text[k]); k++)
            if (char.IsAsciiDigit(text[k]) || (k > at && char.IsAsciiLetterUpper(text[k]))) return true;
        return false;
    }

    /// <summary>A known shape's prefix, what its body may hold and how long it may get before it is whole (AWS ids are fixed),
    /// and whether a body needs a digit or a capital to count (the bare <c>sk-</c>, so <c>sk-learn</c> isn't held).</summary>
    private sealed record ShapeStart(string Prefix, Func<char, bool> Body, int MaxBody = int.MaxValue, bool NeedsGenerated = false);

    private static readonly ShapeStart[] ShapeStarts =
    [
        new("sk-", c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-', NeedsGenerated: true),
        .. "pousr".Select(k => new ShapeStart($"gh{k}_", char.IsAsciiLetterOrDigit)),
        new("github_pat_", c => char.IsAsciiLetterOrDigit(c) || c == '_'),
        new("AKIA", c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c), MaxBody: 15),
        new("ASIA", c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c), MaxBody: 15),
        new("eyJ", c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'),
        .. "abposr".Select(k => new ShapeStart($"xox{k}-", c => char.IsAsciiLetterOrDigit(c) || c == '-')),
    ];

    /// <summary>A value after a secret-sounding name, at the very end of the text.</summary>
    private static readonly System.Text.RegularExpressions.Regex AssignedTail = new(
        @"(?:(?<name>key|token|secret|password|passwd|pwd)[""']?[ \t]*[:=][ \t]*[""']?|\b[Bb]earer[ \t]+)(?<v>[A-Za-z0-9+/_\-.~]+={0,2})\z",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant
        | System.Text.RegularExpressions.RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    /// <summary>A value after <c>key=</c>, <c>token:</c>, <c>secret=</c> or <c>Bearer</c> this long, with digits and letters, is
    /// strong; a shorter one only held, and one under <see cref="MinAssignedPart"/> characters (or digits alone) not even that.</summary>
    private const int StrongAssignedPart = 8, MinAssignedPart = 4;

    /// <summary>
    /// The candidate secret <paramref name="joined"/> ends with, if any (M.ORCH-T982): a trailing token run (before at most a
    /// line break and its indent, which a wrap may continue) that isn't masked already and has begun a known shape past its
    /// prefix, or is a value after a secret-sounding name with a digit or a capital in it; or the first
    /// <see cref="MinLiteralPart"/> or more characters of a Vault value. The earliest-starting one is held (Hold). Null when the
    /// text can't be the start of a secret.
    /// <para>Only a strong one (Strong, the earliest of them) is masked when its hold runs out or the frame order needs it out
    /// (M.ORCH-T1038): a begun shape; a value after a password name; a value after another name once it is
    /// <see cref="StrongAssignedPart"/> characters with digits and letters; any held start of a Vault value (M.ORCH-T1049: it
    /// is a real secret's start, so it is never sent bare). A weak one (<c>apiKey = getT</c>) is held in case the rest makes it
    /// a match, then sent as it is; <c>key=1</c> isn't held at all. Literal is the earliest Vault value's start, which even a
    /// final call masks.</para>
    /// </summary>
    private (Candidate? Hold, Candidate? Strong, Candidate? Literal) FindCandidate(string joined, int[] map, bool[] marked)
    {
        var end = joined.Length;
        if (end == 0) return (null, null, null);
        var t = end;
        for (var spaces = 0; t > 0 && joined[t - 1] == ' ' && spaces < MaxWrapIndent; spaces++) t--;
        var breaks = 0;
        for (; t > 0 && joined[t - 1] == '\n' && breaks < MaxWrapBreaks; breaks++) t--;
        if (breaks == 0) t = end; // spaces alone end a run
        var runEnd = t;
        Candidate? best = null, bestStrong = null, bestLiteral = null;
        void Consider(Candidate c, bool strong)
        {
            if (best is not { } b || c.Start < b.Start) best = c;
            if (strong && (bestStrong is not { } s || c.Start < s.Start)) bestStrong = c;
        }

        if (runEnd > 0 && IsTokenChar(joined[runEnd - 1]) && !marked[map[runEnd - 1]])
        {
            var runStart = runEnd;
            while (runStart > 0 && IsTokenChar(joined[runStart - 1]) && runEnd - runStart < MaxHoldChars) runStart--;
            // A shape starts where no word character comes before it (SecretMasker's Start).
            for (var s = runStart; s < runEnd && best is null; s++)
            {
                if (s > runStart && (char.IsAsciiLetterOrDigit(joined[s - 1]) || joined[s - 1] is '_' or '-')) continue;
                var tail = joined.AsSpan(s, runEnd - s);
                foreach (var shape in ShapeStarts)
                {
                    if (tail.Length <= shape.Prefix.Length || !tail.StartsWith(shape.Prefix, StringComparison.Ordinal)) continue;
                    var body = tail[shape.Prefix.Length..];
                    if (body.Length > shape.MaxBody) continue;
                    var fits = true;
                    var generated = false;
                    var dots = 0;
                    foreach (var ch in body)
                    {
                        if (!shape.Body(ch)) { fits = false; break; }
                        if (ch == '.') dots++;
                        generated |= char.IsAsciiDigit(ch) || char.IsAsciiLetterUpper(ch);
                    }
                    if (!fits || dots > 2 || (shape.NeedsGenerated && !generated)) continue;
                    Consider(new Candidate(s, s + shape.Prefix.Length, runEnd), strong: true);
                    break;
                }
            }
            // A value after a secret-sounding name.
            var from = Math.Max(0, runStart - 48);
            try
            {
                var m = AssignedTail.Match(joined, from, runEnd - from);
                if (m.Success && m.Groups["v"] is { Length: > 0 } v
                              && v.Value.Any(ch => char.IsAsciiDigit(ch) || char.IsAsciiLetterUpper(ch)))
                {
                    var value = v.Value;
                    // A password is short and written, not generated: any start of one counts.
                    var password = m.Groups["name"] is { Success: true, Length: > 0 } name && name.Value[0] is 'p' or 'P';
                    var strong = password || (value.Length >= StrongAssignedPart && value.Any(char.IsAsciiDigit) && value.Any(char.IsAsciiLetter));
                    if (password || (value.Length >= MinAssignedPart && !value.All(char.IsAsciiDigit)))
                        Consider(new Candidate(v.Index, v.Index, runEnd), strong);
                }
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { }
        }

        // The start of a Vault value (it may hold any character, but never a line break).
        var litEnd = breaks > 0 ? runEnd : end;
        if (litEnd > 0 && !marked[map[litEnd - 1]])
        {
            var last = joined[litEnd - 1];
            var text = joined.AsSpan(0, litEnd);
            foreach (var literal in secrets.Literals)
            {
                // Long enough not to be an ordinary word or prefix ("admin", "http"), and starting at a token boundary.
                var least = Math.Min(MinLiteralPart, literal.Length - 1);
                for (var len = Math.Min(literal.Length - 1, litEnd); len >= least; len--)
                {
                    if (literal[len - 1] != last || !text.EndsWith(literal.AsSpan(0, len), StringComparison.Ordinal)) continue;
                    if (litEnd - len > 0 && IsTokenChar(joined[litEnd - len - 1]) && IsTokenChar(literal[0])) continue;
                    var found = new Candidate(litEnd - len, litEnd - len, litEnd);
                    Consider(found, strong: true);
                    if (bestLiteral is not { } bl || found.Start < bl.Start) bestLiteral = found;
                    break;
                }
            }
        }
        return (best, bestStrong, bestLiteral);
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

    /// <summary>
    /// Writes one escape sequence (M.ORCH-T980). A string sequence (OSC, DCS, APC, PM, SOS) carries printable text a terminal
    /// shows or acts on: a window title (OSC 0/2), a hyperlink's URI (OSC 8), a notification. Its payload is masked as text is,
    /// on its own, one bullet per secret character, with the introducer and terminator kept, so it stays a well-formed
    /// sequence. A clipboard write (OSC 52) is decoded and checked, and dropped whole when what it would copy holds a secret or
    /// can't be decoded, since a bullet in base64 would be garbage. Every other sequence goes as it is.
    /// </summary>
    private void WriteEscape(ArrayBufferWriter<byte> output, ReadOnlySpan<byte> esc)
    {
        if (esc.Length < 3 || esc[1] is not ((byte)']' or (byte)'P' or (byte)'_' or (byte)'^' or (byte)'X'))
        {
            output.Write(esc);
            return;
        }
        // The terminator: BEL, CAN or SUB, or ST (ESC \); a sequence cut short or capped has none.
        var end = esc.Length;
        if (esc[^1] is 0x07 or 0x18 or 0x1A) end--;
        else if (esc.Length >= 4 && esc[^2] == 0x1B && esc[^1] == (byte)'\\') end -= 2;
        else if (esc[^1] == 0x1B) end--;
        var payload = esc[2..end];
        if (payload.IsEmpty)
        {
            output.Write(esc);
            return;
        }
        if (esc[1] == (byte)']' && payload.StartsWith("52;"u8))
        {
            if (ClipboardHoldsSecret(payload)) return;
            output.Write(esc);
            return;
        }
        var masked = MaskPayload(payload);
        output.Write(esc[..2]);
        output.Write(masked);
        output.Write(esc[end..]);
    }

    /// <summary>A string sequence's payload masked on its own (it holds no escape: the parser ended the sequence at one).</summary>
    private byte[] MaskPayload(ReadOnlySpan<byte> payload) => new StreamMasker(secrets).Process(payload, final: true);

    /// <summary>Whether an OSC 52 payload (<c>52;Pc;Pd</c>, Pd base64) would put a secret on the viewer's clipboard: true when
    /// the decoded text masks to anything else, or Pd isn't base64. A query (<c>?</c>) or an empty Pd (clear) holds none.</summary>
    private bool ClipboardHoldsSecret(ReadOnlySpan<byte> payload)
    {
        var rest = payload[3..];
        var semi = rest.IndexOf((byte)';');
        if (semi < 0) return false;
        var data = rest[(semi + 1)..];
        if (data.IsEmpty || data.SequenceEqual("?"u8)) return false;
        byte[] decoded;
        try { decoded = Convert.FromBase64String(Encoding.ASCII.GetString(data)); }
        catch (FormatException) { return true; }
        return !MaskPayload(decoded).AsSpan().SequenceEqual(decoded);
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
