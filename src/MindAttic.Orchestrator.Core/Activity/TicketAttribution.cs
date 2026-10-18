using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Activity;

/// <summary>Which ticket a session works on: named in the dispatch prompt, or implied by the ticket's worktree folder.</summary>
public static partial class TicketAttribution
{
    /// <summary>How <see cref="Scheduling.Scheduler.PromptFor"/> opens every dispatched session.</summary>
    public const string PromptMarker = "Work on ticket ";

    [GeneratedRegex(@"Work on ticket (?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+) \((?<type>[a-z]+)\)")]
    private static partial Regex Prompt();

    [GeneratedRegex(@"[\\/]MindAttic\.worktrees[\\/][^\\/]+[\\/](?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)(?:[\\/]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Worktree();

    public static (string Id, string Type)? FromPrompt(string text) =>
        Prompt().Match(text) is { Success: true } m ? (m.Groups["id"].Value, m.Groups["type"].Value) : null;

    [GeneratedRegex(@"^(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex TicketId();

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
