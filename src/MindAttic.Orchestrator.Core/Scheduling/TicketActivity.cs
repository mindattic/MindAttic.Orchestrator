using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// What is working a dispatched ticket (M.ORCH-T1122). Every ticket is worked in its repo's own checkout on main now, which
/// other agents (and you) share, so a session or tab being in that folder says nothing about this ticket: there it's judged by
/// the ticket itself, by repo and ticket id (a session or subagent started on it, a tab titled for it). Only an assignment
/// made before that, in a worktree of its own, is still judged by its folder.
/// </summary>
internal static class TicketActivity
{
    /// <summary>Whether <paramref name="a"/> works in a worktree of its own (judged by its folder): its repo's checkout as the
    /// snapshot has it decides; with its repo not in the snapshot it's judged by its folder, as before.</summary>
    public static bool ByFolder(Assignment a, Snapshot snapshot) =>
        snapshot.Repos.FirstOrDefault(r => r.Path.Length > 0 && Same(r.Name, a.Repo)) is not { } repo || a.InOwnWorktree(repo.Path);

    /// <summary>Whether Launcher tab <paramref name="h"/> runs <paramref name="a"/>: any tab in its old worktree; in the shared
    /// checkout only a tab titled for its ticket (the <see cref="Dispatcher"/> titles each "&lt;repo&gt; &lt;ticket&gt;").</summary>
    public static bool Hosts(HostSession h, Assignment a, bool byFolder) =>
        IsUnder(h.WorkingDirectory, a.Directory) && (byFolder || NamesTicket(h.Title, a.TicketId));

    /// <summary>Whether a transcript (a session's, at <paramref name="cwd"/>, started on <paramref name="ticketId"/>) is
    /// <paramref name="a"/>'s.</summary>
    public static bool Works(string? cwd, string? ticketId, Assignment a, bool byFolder) =>
        byFolder ? IsUnder(cwd, a.Directory) : ticketId is not null && Same(ticketId, a.TicketId);

    /// <summary>A tab running <paramref name="a"/>, or a session or ticketed subagent working it with a write that
    /// <paramref name="recent"/> accepts.</summary>
    public static bool Active(Assignment a, Snapshot snapshot, Func<DateTime, bool> recent)
    {
        var byFolder = ByFolder(a, snapshot);
        foreach (var repo in snapshot.Repos)
        {
            if (repo.Hosts.Any(h => Hosts(h, a, byFolder))) return true;
            foreach (var s in repo.Sessions)
            {
                if (recent(s.LastWriteUtc) && Works(s.Cwd, s.TicketId, a, byFolder)) return true;
                if (s.TicketedSubagents.Any(t => t.Events.Count > 0 && recent(t.Events[^1].AtUtc) && Works(t.Cwd, t.TicketId, a, byFolder))) return true;
            }
        }
        return false;
    }

    /// <summary>Whether a tab's title names ticket <paramref name="id"/> as a word of its own (not M.ORCH-T12 in M.ORCH-T123).</summary>
    public static bool NamesTicket(string? title, string id) =>
        !string.IsNullOrEmpty(title) && Regex.IsMatch(title, $@"(?<![\w.-]){Regex.Escape(id)}(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsUnder(string? path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;
        var p = Path.TrimEndingDirectorySeparator(path.Replace('/', '\\'));
        var d = Path.TrimEndingDirectorySeparator(directory.Replace('/', '\\'));
        return p.Equals(d, StringComparison.OrdinalIgnoreCase) || p.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
