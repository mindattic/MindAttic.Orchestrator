using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Activity;

/// <summary>Which ticket a session works on: named in the dispatch prompt, or implied by the ticket's worktree folder.</summary>
public static partial class TicketAttribution
{
    /// <summary>How <see cref="Scheduling.Scheduler.PromptFor"/> opens every dispatched session.</summary>
    public const string PromptMarker = "Work on ticket ";

    // The type is the first word in the parentheses and the rest is free text: an agent's prompt says "(feature, approved)",
    // "(chore, approved by the user)" as well as "(fix)", and the type may be left out (M.ORCH-T884: such a subagent was never
    // seen working its ticket, so its claim counted as pending for hours).
    [GeneratedRegex(@"Work on ticket (?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)\b(?: \((?<type>[a-z]+)[^)]*\))?")]
    private static partial Regex Prompt();

    [GeneratedRegex(@"[\\/]MindAttic\.worktrees[\\/][^\\/]+[\\/](?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)(?:[\\/]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Worktree();

    /// <summary>The ticket id and type a dispatch prompt names after <see cref="PromptMarker"/>; the type is empty when the prompt
    /// names none, and the whole is null when there's no such line.</summary>
    public static (string Id, string Type)? FromPrompt(string text) =>
        Prompt().Match(text) is { Success: true } m ? (m.Groups["id"].Value, m.Groups["type"].Value) : null; // type "" when the prompt names none

    [GeneratedRegex(@"^(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex TicketId();

    // A handoff's lead: "ticket X", "tickets X, Y and Z", "tickets: X + Y", with the type read from what follows the first one,
    // "(fix)" or "(Repo, fix: ...)" alike.
    [GeneratedRegex(@"(?i:\btickets?):?\s+(?<list>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+\b(?:\s*(?:,|&|\+|\band\b)\s*(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+\b)*)(?:\s*\((?:(?<type>[a-z]+)\b|[^,)]*,\s*(?<type>[a-z]+)\b))?")]
    private static partial Regex HandoffLead();

    [GeneratedRegex(@"(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+\b")]
    private static partial Regex AnyTicket();

    // A prompt file a coordinator wrote for the subagent to read: "...\scratchpad\M.ORCH-T2276.prompt".
    [GeneratedRegex(@"(?<![\w.-])(?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)\.prompt\b")]
    private static partial Regex PromptFile();

    /// <summary>How far into a subagent's opening prompt its lead ("ticket X ...") is looked for: a ticket named only further
    /// on is context ("another agent is working Y"), not its own.</summary>
    internal const int HandoffLeadWithin = 400;

    /// <summary>
    /// The tickets a subagent's opening prompt hands it (M.ORCH-T2276), for one that doesn't open with <see cref="PromptMarker"/>:
    /// a coordinator that writes the orchestrator's prompt to a file and tells its subagent "Your complete task prompt for ticket X
    /// (Repo, fix) is in ...\X.prompt" was never seen working X, so its claim counted as unstarted for hours. Every
    /// <see cref="PromptMarker"/> line in it (several prompts pasted into one), the ticket list its lead names
    /// (within <see cref="HandoffLeadWithin"/> characters), and every <c>&lt;ticket&gt;.prompt</c> file it names, in the order
    /// they appear; the first is its own ticket (with its type, "" when none is named), the rest it was handed too. Null when
    /// it names none of these.
    /// </summary>
    public static (string Id, string Type, IReadOnlyList<string> Also)? FromHandoff(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var found = new List<(int At, string Id, string Type)>();
        foreach (Match m in Prompt().Matches(text)) found.Add((m.Index, m.Groups["id"].Value, m.Groups["type"].Value));
        if (HandoffLead().Match(text) is { Success: true } lead && lead.Index < HandoffLeadWithin)
        {
            var type = lead.Groups["type"].Value;
            foreach (Match id in AnyTicket().Matches(lead.Groups["list"].Value))
            {
                found.Add((lead.Groups["list"].Index + id.Index, id.Value, type));
                type = ""; // the type follows the first only
            }
        }
        foreach (Match m in PromptFile().Matches(text)) found.Add((m.Index, m.Groups["id"].Value, ""));
        if (found.Count == 0) return null;
        var ordered = found.OrderBy(f => f.At).ToList();
        var ids = ordered.Select(f => f.Id.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var first = ordered[0];
        var firstType = ordered.Where(f => string.Equals(f.Id, first.Id, StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Type).FirstOrDefault(t => t.Length > 0) ?? "";
        return (ids[0], firstType, ids.Skip(1).ToList());
    }

    /// <summary>
    /// The ticket a session in <c>&lt;worktree root&gt;\&lt;repo&gt;\&lt;ticket&gt;</c> works on: under <paramref name="worktreeRoot"/>
    /// (settings.json's WorktreeRoot) when given, else under any folder named MindAttic.worktrees, the default (M.ORCH-T446).
    /// </summary>
    public static string? FromWorktree(string? cwd, string? worktreeRoot = null)
    {
        if (cwd is null) return null;
        if (!string.IsNullOrWhiteSpace(worktreeRoot))
        {
            var root = worktreeRoot.Replace('/', '\\').TrimEnd('\\') + '\\';
            var path = cwd.Replace('/', '\\');
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                path[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } parts && TicketId().IsMatch(parts[1]))
                return parts[1].ToUpperInvariant();
        }
        return Worktree().Match(cwd) is { Success: true } m ? m.Groups["id"].Value.ToUpperInvariant() : null;
    }
}
