using System.Diagnostics;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>The global pause as the store keeps it: on or off, since when, and who turned it on.</summary>
public sealed record PauseState
{
    public bool Paused { get; init; }
    public DateTime? SinceUtc { get; init; }
    public string? By { get; init; }
}

/// <summary>A Claude CLI process the pause ended: its id and executable, when it could be read.</summary>
public sealed record PausedCli(int Pid, string? Path);

/// <summary>What a pause did: the CLIs it ended, the tickets it put back in the queue, and anything it couldn't do.</summary>
public sealed record PauseResult(IReadOnlyList<PausedCli> Killed, IReadOnlyList<string> Requeued, IReadOnlyList<string> Failed);

/// <summary>
/// The global Pause/Resume toggle (M.ORCH-T808, the user, 2026-10-24: the machine becomes unusable from RAM and API-call volume;
/// it has to actually stop spending, not politely finish at the next safe point as the M.ORCH-T786 abort lever does). Pausing
/// (<see cref="Pause"/>, after the dashboard's confirm) records the pause first, so nothing claims or dispatches meanwhile, then
/// ends every Claude CLI process on the machine outright, each with its whole tree (its subagents, shells, MCP servers), so RAM
/// is freed and API calls stop at once, and puts each ticket a Claude CLI held back in the queue, first for the next round
/// (the same safety net <see cref="AgentAbort.Requeue"/> gives an aborted subagent): only in-flight uncommitted work is lost,
/// and that stays in its worktree. While paused (<see cref="Active"/>) nothing claims (<c>orchestrator next --claim</c>,
/// <see cref="Allocate"/>), dispatches (<see cref="Dispatcher"/>), wakes an /allocate CLI or redispatches an orphan, org-wide.
/// <see cref="Resume"/> only lifts it: the ended processes stay ended, and new subagents pick the requeued tickets back up.
/// </summary>
public static class GlobalPause
{
    /// <summary>The <c>state</c> row holding the pause.</summary>
    public const string StateName = "global-pause";

    /// <summary>Why nothing is claimed or dispatched while paused, as <c>orchestrator next</c> and the daemon say it.</summary>
    public const string Why = "the orchestrator is paused (the dashboard's pause button): nothing is claimed or dispatched until it's resumed";

    /// <summary>The pause as kept; off when it isn't kept or can't be read.</summary>
    public static PauseState Load(JsonStore store) => store.Load<PauseState>(StateName);

    /// <summary>Whether the pause is on.</summary>
    public static bool Active(JsonStore store) => Load(store).Paused;

    /// <summary>
    /// Pauses: records it (so a claim or dispatch racing the kill is refused), ends every Claude CLI process
    /// (<paramref name="clis"/>, each ended with <paramref name="kill"/>), then releases each Claude ticket's assignment and records it
    /// requeued. <paramref name="clis"/> and <paramref name="kill"/> are seams; by default every Claude CLI process on the machine
    /// (not the Claude Desktop app) is ended with its whole tree.
    /// </summary>
    public static PauseResult Pause(JsonStore store, string? by, DateTime nowUtc,
        Func<IReadOnlyList<PausedCli>>? clis = null, Func<PausedCli, bool>? kill = null)
    {
        store.Save(StateName, new PauseState { Paused = true, SinceUtc = nowUtc, By = by });
        var failed = new List<string>();
        var killed = new List<PausedCli>();
        foreach (var cli in (clis ?? ClaudeClis)())
            if ((kill ?? Kill)(cli)) killed.Add(cli);
            else failed.Add($"couldn't end the Claude CLI pid {cli.Pid}");

        var requeued = new List<string>();
        try
        {
            store.UpdateAssignments(all =>
            {
                var gone = all.Where(a => a.Provider.Equals(Providers.Claude, StringComparison.OrdinalIgnoreCase)).ToList();
                requeued.AddRange(gone.Select(a => a.TicketId).Distinct(StringComparer.OrdinalIgnoreCase));
                all.RemoveAll(gone.Contains);
            });
            foreach (var id in requeued) AgentAbort.Requeue(store, id, nowUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            failed.Add($"couldn't put the tickets back in the queue: {ex.Message}");
        }
        return new PauseResult(killed, requeued, failed);
    }

    /// <summary>Lifts the pause: /allocate and dispatch may start new work again. No confirm: it's always the safe direction.</summary>
    public static void Resume(JsonStore store) => store.Save(StateName, new PauseState());

    /// <summary>Whether a <c>claude</c> process at <paramref name="path"/> is a CLI rather than the Claude Desktop app (installed
    /// under <c>AnthropicClaude</c>, or as a Store app under <c>WindowsApps</c>). A path that couldn't be read counts as a CLI.</summary>
    public static bool IsCli(string? path) => path is null ||
        (path.IndexOf("AnthropicClaude", StringComparison.OrdinalIgnoreCase) < 0 &&
         path.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) < 0);

    /// <summary>Every Claude CLI process on the machine, as <see cref="Providers.IsClaudeProcess"/> names them.</summary>
    private static IReadOnlyList<PausedCli> ClaudeClis()
    {
        var found = new List<PausedCli>();
        foreach (var p in Process.GetProcesses())
            using (p)
            {
                string name;
                string? path = null;
                try { name = p.ProcessName; }
                catch (InvalidOperationException) { continue; }
                if (!Providers.IsClaudeProcess(name)) continue;
                try { path = p.MainModule?.FileName; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                if (IsCli(path)) found.Add(new PausedCli(p.Id, path));
            }
        return found;
    }

    private static bool Kill(PausedCli cli)
    {
        try
        {
            using var p = Process.GetProcessById(cli.Pid);
            // A dashboard or CLI command started from inside a CLI is in its tree: that CLI alone is ended, not this process too.
            p.Kill(entireProcessTree: !Sessions.ProcessTree.IsAncestor(cli.Pid, Environment.ProcessId));
            return true;
        }
        catch (ArgumentException) { return true; } // exited meanwhile
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException) { return false; }
    }
}
