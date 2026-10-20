using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The daemon's side of /handoff (M.ORCH-T481), once a tick while the mark is winding down: returns claims no subagent started on
/// (their worktrees removed), asks each idle Launcher-tab CLI that hasn't quicksaved to do so (it types /handoff there, once), and
/// once the roster has had nothing working for two ticks running marks the handoff quiet and says <see cref="Handoff.QuietLine"/>
/// once. A resumed mark is dropped a day on.
/// </summary>
public sealed class HandoffWatch(ContextCycler cycler, JsonStore store)
{
    /// <summary>Removes a claim's worktree and returns the ticket to the queue (seam); the message, or null when it couldn't.</summary>
    internal Func<Assignment, string, string?> Return { get; init; } = (a, repoPath) =>
    {
        // Only one with nothing in it: no change, and nothing of its own past the main line, so no work is lost.
        if (Directory.Exists(a.Directory))
        {
            if (Dispatcher.Git(a.Directory, "status", "--porcelain").Output.Trim().Length > 0) return null;
            var main = Dispatcher.Mainline(repoPath);
            if (Dispatcher.Git(a.Directory, "merge-base", "--is-ancestor", "HEAD", $"origin/{main}").Code != 0 &&
                Dispatcher.Git(a.Directory, "merge-base", "--is-ancestor", "HEAD", main).Code != 0) return null;
            if (Dispatcher.Git(repoPath, "worktree", "remove", a.Directory).Code != 0) return null;
        }
        store.UpdateAssignments(all => all.RemoveAll(x => x.TicketId == a.TicketId));
        return "its worktree removed";
    };

    /// <summary>Types into a Launcher tab and presses Enter (seam).</summary>
    internal Func<string, string, Task<bool>> Submit { get; init; } = (pipe, text) => SessionControl.SubmitAsync(pipe, text);

    internal Func<int, IReadOnlyList<HostSession>, HostSession?> HostOf { get; init; } =
        (pid, hosts) => hosts.FirstOrDefault(h => ProcessTree.IsAncestor(h.ProcessId, pid));

    /// <summary>The prompt typed into an idle tab that hasn't saved its handoff.</summary>
    public const string Prompt = "/handoff";

    private readonly HashSet<string> asked = new(StringComparer.Ordinal);
    private readonly HashSet<string> leftAlone = new(StringComparer.OrdinalIgnoreCase);
    private int quietTicks;
    private string? started;

    /// <summary>What happened this tick, and whether it just went quiet (the caller alerts once).</summary>
    public (IReadOnlyList<string> Lines, bool WentQuiet) Tick(IReadOnlyList<CliRow> roster, IReadOnlyList<HostSession> hosts,
        IReadOnlyList<WorkedTicket> worked, IReadOnlyDictionary<string, string> repoPaths, DateTime nowUtc)
    {
        var lines = new List<string>();
        var mark = Handoff.Read(cycler.Directory);
        if (mark?.Started != started) { asked.Clear(); leftAlone.Clear(); quietTicks = 0; started = mark?.Started; }
        if (mark is null) return (lines, false);
        if (mark.State == Handoff.Resumed)
        {
            if (mark.ResumedUtc is { } at && nowUtc - at > Handoff.ResumedKept)
            {
                Handoff.Clear(cycler.Directory);
                lines.Add("handoff: done a day after the switch; its marks are tidied away");
            }
            return (lines, false);
        }
        if (mark.State != Handoff.Winding) return (lines, false);

        var workedIds = worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var a in store.LoadAssignments().Where(a => Handoff.Unstarted(a, workedIds, nowUtc) && !leftAlone.Contains(a.TicketId)))
        {
            var found = repoPaths.TryGetValue(a.Repo, out var path);
            var said = found ? Return(a, path!) : null;
            // Left as it is once, and said once: nothing works it, so it doesn't keep the handoff from going quiet either; its
            // worktree stays on disk through the switch (M.ORCH-T509).
            if (said is null) leftAlone.Add(a.TicketId);
            lines.Add(said is not null ? $"handoff: {a.TicketId} was claimed but no subagent started on it: it's back in the queue ({said})"
                : found ? $"handoff: {a.TicketId} was claimed but no subagent started on it, and its worktree {a.Directory} has work in it, so it's left as it is"
                : $"handoff: {a.TicketId} was claimed but no subagent started on it, and its repo {a.Repo} isn't in the workspace, so it's left as it is");
        }

        // An idle tab that hasn't saved is asked to, once; one typed into is busy by the next tick.
        var saved = Handoff.Saved(cycler.Directory);
        var typed = false;
        foreach (var c in roster.Where(c => c.Idle && c.InLauncher && !saved.ContainsKey(c.Pid) && asked.Add(c.SessionId)))
        {
            if (HostOf(c.Pid, hosts) is not { } host) continue;
            bool sent;
            try { sent = Submit(host.PipeName, Prompt).GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException) { sent = false; }
            if (sent) { typed = true; lines.Add($"handoff: asked the idle CLI in {c.Repo} (pid {c.Pid}) to quicksave its handoff"); }
        }

        var work = Handoff.Working(roster, cycler.AliveSubagents(nowUtc), store.LoadAssignments().Where(a => !leftAlone.Contains(a.TicketId)), workedIds);
        quietTicks = work.Any || typed ? 0 : quietTicks + 1;
        if (quietTicks < 2) return (lines, false);
        Handoff.Write(cycler.Directory, mark with { State = Handoff.Quiet, QuietAt = Handoff.Stamp(nowUtc) });
        lines.Add($"handoff: {Handoff.QuietLine} ({saved.Count} CLI handoff{(saved.Count == 1 ? "" : "s")} saved)");
        return (lines, true);
    }
}
