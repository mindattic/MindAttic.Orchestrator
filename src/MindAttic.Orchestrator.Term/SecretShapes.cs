using System.Text;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The one source of the secret shapes MindAttic masks (M.ORCH-T963): the gateway's broadcast masker (<c>SecretMasker</c>,
/// M.ORCH-T935), the dashboard's activity text (<c>ToolCallText</c>) and the context gate's Python activity records
/// (<c>AgentGate</c>, M.ORCH-T964) all read these pattern strings, so a shape added here reaches all three.
/// <para>Each pattern is written in the subset .NET and Python's <c>re</c> both read the same way: no named groups, no
/// variable-width lookbehind, case-insensitivity only as a scoped <c>(?i:…)</c>. <see cref="ActivityPattern"/> is pasted
/// verbatim into the generated hook script.</para>
/// <para>The broadcast masker keeps its own false-positive checks (entropy, digits and letters) since it masks a whole
/// terminal screen; an activity target is a few words that are never the point, so <see cref="MaskActivity"/> masks every
/// match outright, and also any long run of token characters holding a digit.</para>
/// </summary>
public static class SecretShapes
{
    /// <summary>A token starts where no token character comes before it.</summary>
    public const string Start = @"(?<![A-Za-z0-9_\-])";

    /// <summary>Anthropic keys, <c>sk-ant-…</c>.</summary>
    public const string Anthropic = Start + @"sk-ant-[A-Za-z0-9_\-]{20,}";

    /// <summary>OpenAI keys, <c>sk-…</c>, <c>sk-proj-…</c>.</summary>
    public const string OpenAi = Start + @"sk-(?:proj-|svcacct-|admin-)?[A-Za-z0-9_\-]{20,}";

    /// <summary>GitHub tokens, <c>ghp_</c>, <c>gho_</c>, <c>ghu_</c>, <c>ghs_</c>, <c>ghr_</c>.</summary>
    public const string GitHub = Start + @"gh[pousr]_[A-Za-z0-9]{30,}";

    /// <summary>GitHub fine-grained tokens, <c>github_pat_…</c>.</summary>
    public const string GitHubPat = Start + @"github_pat_[A-Za-z0-9_]{40,}";

    /// <summary>AWS access key ids, <c>AKIA…</c> and <c>ASIA…</c>.</summary>
    public const string AwsKeyId = Start + @"(?:AKIA|ASIA)[A-Z0-9]{16}(?![A-Za-z0-9])";

    /// <summary>JWTs: three base64url segments, the first <c>eyJ</c>.</summary>
    public const string Jwt = Start + @"eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{10,}";

    /// <summary>Slack tokens, <c>xox[abposr]-…</c>.</summary>
    public const string Slack = Start + @"xox[abposr]-[A-Za-z0-9\-]{10,}";

    /// <summary>Google API keys, <c>AIza…</c> (35 characters after it; a longer run is masked whole).</summary>
    public const string Google = Start + @"AIza[A-Za-z0-9_\-]{35,}";

    /// <summary>Stripe keys, <c>sk_live_…</c>, <c>pk_test_…</c>, <c>rk_live_…</c>.</summary>
    public const string Stripe = Start + @"[spr]k_(?:live|test)_[A-Za-z0-9]{16,}";

    /// <summary>The token shapes, in the order the broadcast masker checks them.</summary>
    public static IReadOnlyList<string> Tokens { get; } = [Anthropic, OpenAi, GitHub, GitHubPat, AwsKeyId, Jwt, Slack, Google, Stripe];

    /// <summary>The value after a secret-sounding name: <c>API_KEY=</c>, <c>token: "</c>, <c>"password":"</c>, <c>Bearer </c>.
    /// Group 1 is the name part (kept), group 2 the value.</summary>
    public const string Assigned =
        @"((?i:(?:key|token|secret|password|passwd|pwd)[""']?[ \t]*[:=][ \t]*[""']?|\bbearer[ \t]+))([A-Za-z0-9+/_\-.~]{16,}={0,2})";

    /// <summary>A long run of token characters holding a digit (base64, hex, an unknown key): activity text only.</summary>
    public const string LongRun = @"(?<![A-Za-z0-9+/=_\-])(?=[A-Za-z0-9+/=_\-]*[0-9])[A-Za-z0-9+/=_\-]{32,}";

    /// <summary>What a masked activity secret reads as.</summary>
    public const string ActivityMask = "***";

    /// <summary>Every activity shape as one alternation: <see cref="Assigned"/> first (its group 1, the name, is kept), then the
    /// tokens, then <see cref="LongRun"/>. Replace a match with group 1 plus <see cref="ActivityMask"/>.</summary>
    public static string ActivityPattern { get; } = string.Join("|", new[] { Assigned }.Concat(Tokens).Append(LongRun));

    private static readonly Regex Activity = new(ActivityPattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    /// <summary><paramref name="text"/> with every secret shape replaced by <see cref="ActivityMask"/> (a name before an
    /// assigned value is kept: <c>token=***</c>). On a regex timeout the whole text is masked rather than risk one.</summary>
    public static string MaskActivity(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        try { return Activity.Replace(text, m => m.Groups[1].Value + ActivityMask); }
        catch (RegexMatchTimeoutException) { return ActivityMask; }
    }

    /// <summary><paramref name="value"/> as a Python string literal (double-quoted, backslashes and quotes escaped), for pasting
    /// a pattern into a generated hook script.</summary>
    public static string PythonLiteral(string value)
    {
        var sb = new StringBuilder("\"", value.Length + 8);
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                '\n' => @"\n",
                '\r' => @"\r",
                '\t' => @"\t",
                _ => c.ToString(),
            });
        }
        return sb.Append('"').ToString();
    }
}
