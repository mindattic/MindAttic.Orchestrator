using System.Text;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// Finds secrets in printable terminal text (M.ORCH-T935, spike section 5, mitigation 2): known token shapes and the exact
/// values of the owner's Vault-held secrets. It only finds; <see cref="StreamMasker"/> works out which bytes of a VT stream
/// those characters are and replaces them. The literal values live in memory only and are never logged.
/// <para>Shapes: <c>sk-ant-…</c>, <c>sk-…</c> (OpenAI), <c>ghp_</c>/<c>gho_</c>/<c>ghu_</c>/<c>ghs_</c>/<c>ghr_</c> and
/// <c>github_pat_…</c>, AWS access key ids (<c>AKIA…</c>, <c>ASIA…</c>), JWTs (three base64url segments, the first <c>eyJ</c>),
/// Slack <c>xox[abposr]-</c> tokens, and long high-entropy hex or base64 after <c>key=</c>, <c>token=</c>, <c>secret=</c>,
/// <c>password=</c> (also <c>:</c>, quoted, any prefix such as <c>API_KEY</c>) or <c>Bearer</c>. PEM private-key blocks are
/// stateful across chunks, so <see cref="StreamMasker"/> handles them with <see cref="PemBegin"/> and <see cref="PemEnd"/>.</para>
/// <para>False positives are guarded: a token must start at a word boundary, the bare <c>sk-</c> shape needs digits and letters
/// and some entropy, and a value after <c>key=</c> needs mixed case and digits (or to be long hex) and high entropy, so prose,
/// identifiers, commit hashes and UUIDs go through untouched.</para>
/// </summary>
internal sealed class SecretMasker
{
    /// <summary>A Vault value shorter than this is not masked as a literal: it would hit ordinary words.</summary>
    public const int MinLiteral = 8;

    /// <summary>No literal secrets: shapes only.</summary>
    public static SecretMasker ShapesOnly { get; } = new();

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // A token starts where no token character comes before it.
    private const string Start = @"(?<![A-Za-z0-9_\-])";

    private sealed record Shape(Regex Pattern, Func<string, bool>? Valid = null);

    private static readonly Shape[] Shapes =
    [
        new(new($@"{Start}sk-ant-[A-Za-z0-9_\-]{{20,}}", Options, RegexTimeout), v => HasDigit(v) && HasLetter(v)),
        new(new($@"{Start}sk-(?:proj-|svcacct-|admin-)?[A-Za-z0-9_\-]{{20,}}", Options, RegexTimeout),
            v => HasDigit(v[3..]) && HasLetter(v[3..]) && Entropy(v[3..]) >= 3.5),
        new(new($@"{Start}gh[pousr]_[A-Za-z0-9]{{30,}}", Options, RegexTimeout), v => HasDigit(v)),
        new(new($@"{Start}github_pat_[A-Za-z0-9_]{{40,}}", Options, RegexTimeout)),
        new(new($@"{Start}(?:AKIA|ASIA)[A-Z0-9]{{16}}(?![A-Za-z0-9])", Options, RegexTimeout)),
        new(new($@"{Start}eyJ[A-Za-z0-9_\-]{{5,}}\.[A-Za-z0-9_\-]{{5,}}\.[A-Za-z0-9_\-]{{10,}}", Options, RegexTimeout)),
        new(new($@"{Start}xox[abposr]-[A-Za-z0-9\-]{{10,}}", Options, RegexTimeout), v => HasDigit(v)),
    ];

    /// <summary>The value after a secret-sounding name: <c>API_KEY=</c>, <c>token: "</c>, <c>"password":"</c>, <c>Bearer </c>.</summary>
    private static readonly Regex Assigned = new(
        @"(?:(?:key|token|secret|password|passwd|pwd)[""']?[ \t]*[:=][ \t]*[""']?|\b[Bb]earer[ \t]+)(?<v>[A-Za-z0-9+/_\-.~]{16,}={0,2})",
        Options | RegexOptions.IgnoreCase, RegexTimeout);

    /// <summary>The line that opens a PEM private key block.</summary>
    internal static readonly Regex PemBegin = new(@"-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY-----", Options, RegexTimeout);

    /// <summary>The line that closes it.</summary>
    internal static readonly Regex PemEnd = new(@"-----END (?:[A-Z0-9]+ )*PRIVATE KEY-----", Options, RegexTimeout);

    private readonly string[] literals;

    /// <param name="literals">Exact secret values to mask wherever they appear (the Vault's); those under
    /// <see cref="MinLiteral"/> characters are ignored.</param>
    public SecretMasker(IEnumerable<string>? literals = null)
    {
        this.literals = (literals ?? [])
            .Select(l => l?.Trim() ?? "")
            .Where(l => l.Length >= MinLiteral && !l.Contains('\n') && !l.Contains('\r'))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(l => l.Length)
            .ToArray();
        LongestLiteral = this.literals.Length == 0 ? 0 : this.literals[0].Length;
    }

    /// <summary>How many literal values it masks.</summary>
    public int LiteralCount => literals.Length;

    /// <summary>The literal values, longest first (memory only, never logged): the stream masker holds a run that begins one.</summary>
    internal IReadOnlyList<string> Literals => literals;

    /// <summary>The longest literal's length: how much already-sent text a stream keeps to catch one split across chunks.</summary>
    public int LongestLiteral { get; }

    /// <summary>Every range of <paramref name="text"/> to mask (start, end exclusive), in no particular order; ranges may overlap.</summary>
    public IEnumerable<(int Start, int End)> Find(string text)
    {
        if (text.Length == 0) yield break;
        foreach (var shape in Shapes)
        {
            foreach (Match m in SafeMatches(shape.Pattern, text))
                if (shape.Valid is null || shape.Valid(m.Value)) yield return (m.Index, m.Index + m.Length);
        }
        foreach (Match m in SafeMatches(Assigned, text))
        {
            var v = m.Groups["v"];
            if (LooksRandom(v.Value)) yield return (v.Index, v.Index + v.Length);
        }
        foreach (var literal in literals)
        {
            for (var at = text.IndexOf(literal, StringComparison.Ordinal); at >= 0; at = text.IndexOf(literal, at + 1, StringComparison.Ordinal))
                yield return (at, at + literal.Length);
        }
    }

    /// <summary><paramref name="text"/> with each secret character replaced by <c>•</c> (a whole text, no escapes: a title).</summary>
    public string MaskText(string text)
    {
        var masked = new StreamMasker(this);
        var bytes = masked.Process(Encoding.UTF8.GetBytes(text), final: true);
        return Encoding.UTF8.GetString(bytes);
    }

    private static IEnumerable<Match> SafeMatches(Regex regex, string text)
    {
        MatchCollection matches;
        try
        {
            matches = regex.Matches(text);
            _ = matches.Count; // evaluates them all here, inside the try
        }
        catch (RegexMatchTimeoutException) { return []; }
        return matches;
    }

    /// <summary>Whether a value after <c>key=</c> looks generated rather than written: long hex, or mixed-case letters with
    /// digits and high entropy. Words, identifiers and kebab-case names don't.</summary>
    internal static bool LooksRandom(string value)
    {
        var v = value.TrimEnd('=', '.');
        if (v.Length < 16) return false;
        var hex = v.All(Uri.IsHexDigit);
        if (hex) return v.Length >= 20 && HasDigit(v) && HasLetter(v) && Entropy(v) >= 3.0;
        return HasDigit(v) && v.Any(char.IsAsciiLetterUpper) && v.Any(char.IsAsciiLetterLower) && Entropy(v) >= 3.5;
    }

    private static bool HasDigit(string s) => s.Any(char.IsAsciiDigit);

    private static bool HasLetter(string s) => s.Any(char.IsAsciiLetter);

    /// <summary>Shannon entropy in bits per character.</summary>
    internal static double Entropy(string s)
    {
        if (s.Length == 0) return 0;
        var counts = new Dictionary<char, int>();
        foreach (var c in s) counts[c] = counts.GetValueOrDefault(c) + 1;
        double h = 0;
        foreach (var n in counts.Values)
        {
            var p = (double)n / s.Length;
            h -= p * Math.Log2(p);
        }
        return h;
    }
}
