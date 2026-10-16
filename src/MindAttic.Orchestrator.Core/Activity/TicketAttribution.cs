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

    public static string? FromWorktree(string? cwd) =>
        cwd is not null && Worktree().Match(cwd) is { Success: true } m ? m.Groups["id"].Value.ToUpperInvariant() : null;
}
