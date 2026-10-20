using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Sends an idle /autopilot CLI back for work once there is work it can have (M.ORCH-T523). The context gate's keep-going only
/// acts as a turn ends: a CLI whose last <c>orchestrator next --claim</c> handed out nothing (no pace room, its fair share in use,
/// a ticket held while another session's turn that filed it ran) is let stop, and nothing asked it again when the window reset,
/// a subagent finished, or a ticket was filed or approved: five CLIs sat "Needs you" all night and through the morning with
/// workable tickets and room to spare. Each tick, every live /autopilot CLI whose newest session is idle (and not waiting on a
/// window, which <see cref="LimitWaker"/> wakes, nor running a /goal) is asked <see cref="Autopilot.Next"/>, as its own
/// <c>orchestrator next</c> would be (dry run: nothing is claimed); when it would hand out a ticket, the CLI's Launcher tab is
/// typed a prompt to claim and work them. One in a plain terminal is said to you instead, once per idle stretch. A CLI is
/// woken at most once per <see cref="Cooldown"/>, so one that stops again at once (a decision only you can make) isn't
/// typed into every tick.
/// </summary>
public sealed class AutopilotWaker(ContextCycler cycler, JsonStore store)
{
    /// <summary>How long a CLI has been idle before it's woken: keep-going has already had its say at the stop, and a pause
    /// between your own prompts isn't typed over.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMinutes(2);

    /// <summary>The least time between two wakes of the same CLI.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>Whether the CLI process is still running (seam).</summary>
    internal Func<int, bool> IsAlive { get; init; } = ContextCycler.IsClaude;

    /// <summary>The Launcher tab the CLI runs in, if any (seam).</summary>
    internal Func<int, IReadOnlyList<HostSession>, HostSession?> HostOf { get; init; } =
        (pid, hosts) => hosts.FirstOrDefault(h => ProcessTree.IsAncestor(h.ProcessId, pid));

    /// <summary>Types a prompt into a tab and presses Enter (seam).</summary>
    internal Func<string, string, Task<bool>> Submit { get; init; } = (pipe, text) => SessionControl.SubmitAsync(pipe, text);

    /// <summary>The live CLIs /autopilot is on in, and their repos (seam; <see cref="ContextCycler.AutopilotClis"/>).</summary>
    internal Func<IReadOnlyDictionary<int, IReadOnlyList<string>>>? Pilots { get; init; }

    /// <summary>Whether a /handoff holds every CLI (seam).</summary>
    internal Func<bool> HandingOff { get; init; } = () => false;

    /// <param name="Typed">The prompt went into the CLI's tab; false: it's said to you to do.</param>
    public sealed record Wake(int Pid, string Where, IReadOnlyList<string> Tickets, bool Typed, string Line);

    private readonly Dictionary<int, DateTime> wokenUtc = [];
    private readonly Dictionary<int, DateTime> saidFor = [];

    /// <summary>What to wake now, woken (or said, when <paramref name="type"/> is false or it can't be typed into).</summary>
    public IReadOnlyList<Wake> Tick(SchedulePlan plan, OrchestratorSettings settings, IReadOnlyList<HostSession> hosts, bool type, DateTime nowUtc)
    {
        if (!settings.KeepGoing || !settings.Autopilot) return [];
        var pilots = Pilots?.Invoke() ?? cycler.AutopilotClis();
        foreach (var gone in wokenUtc.Keys.Concat(saidFor.Keys).Where(p => !pilots.ContainsKey(p)).ToList())
        {
            wokenUtc.Remove(gone);
            saidFor.Remove(gone);
        }
        if (pilots.Count == 0) return [];
        var newest = cycler.Load().Where(c => c.Pid is not null).GroupBy(c => c.Pid!.Value)
            .ToDictionary(g => g.Key, g => g.MaxBy(c => (Written(c.SessionId), c.TurnUtc))!);
        var handingOff = HandingOff();

        var due = new List<(int Pid, SessionContext Session, IReadOnlyList<string> Repos, IReadOnlyList<string> Ids)>();
        foreach (var (pid, repos) in pilots)
        {
            if (!newest.TryGetValue(pid, out var c) || c is not { Idle: true, LimitStop: null, Goal: null }) continue;
            if (nowUtc - c.TurnUtc < Settle) continue;
            if (wokenUtc.TryGetValue(pid, out var last) && nowUtc - last < Cooldown) continue;
            if (!IsAlive(pid)) continue;
            var (handed, _) = Autopilot.Next(plan, settings, store, repos, null, claim: false, nowUtc,
                unthrottled: cycler.IsUnthrottled(pid), self: c.SessionId, handingOff: handingOff, cli: pid, pilots: pilots);
            if (handed.Count > 0) due.Add((pid, c, repos, [.. handed.Select(h => h.Item.Id)]));
        }
        if (due.Count == 0) return [];

        var sends = due.Select(d =>
        {
            var host = type ? HostOf(d.Pid, hosts) : null;
            return (Due: d, Host: host, Sent: host is null ? Task.FromResult(false) : SafeSubmit(host.PipeName, Prompt(d.Repos, d.Ids)));
        }).ToList();
        Task.WaitAll([.. sends.Select(s => s.Sent)]);

        var wakes = new List<Wake>();
        foreach (var (d, host, sent) in sends)
        {
            var where = LimitWaker.Where(d.Session.Cwd);
            var tickets = string.Join(", ", d.Ids.Take(6)) + (d.Ids.Count > 6 ? "..." : "");
            if (sent.Result)
            {
                wokenUtc[d.Pid] = nowUtc;
                saidFor.Remove(d.Pid);
                wakes.Add(new Wake(d.Pid, where, d.Ids, true, $"autopilot: told the idle CLI in {where} to carry on: orchestrator next has {tickets} for it now"));
                continue;
            }
            // Said once per idle stretch: the same stop isn't alerted every tick.
            if (saidFor.TryGetValue(d.Pid, out var turn) && turn == d.Session.TurnUtc) continue;
            saidFor[d.Pid] = d.Session.TurnUtc;
            wakes.Add(new Wake(d.Pid, where, d.Ids, false,
                $"The /autopilot CLI in {where} is idle while orchestrator next has {tickets} for it. Tell it to carry on (" +
                (!type ? "resume-after-reset is off" : host is null ? "it isn't in a Launcher tab the orchestrator can type into" : "typing into its tab failed") + ")."));
        }
        return wakes;
    }

    /// <summary>What the CLI is typed: one line (a line break would be a new line in the prompt box, not Enter), and no double
    /// quote (wt would split on it).</summary>
    public static string Prompt(IReadOnlyList<string> repos, IReadOnlyList<string> ids) =>
        $"There's room to work again: orchestrator next has {ids.Count} workable ticket{(ids.Count == 1 ? "" : "s")} for this CLI's autopilot " +
        $"({string.Join(", ", ids.Take(6))}{(ids.Count > 6 ? "..." : "")}). Run `orchestrator next --repos '{string.Join(",", repos)}' --claim` and hand each ticket it prints " +
        "to a subagent with the prompt it prints (several at once, in one message), then repeat. End your turn when it hands out nothing.";

    /// <summary>When the context gate last wrote the session's state, its start included (1601 when it's gone since).</summary>
    private DateTime Written(string sessionId) => File.GetLastWriteTimeUtc(Path.Combine(cycler.Directory, sessionId + ".state.json"));

    private async Task<bool> SafeSubmit(string pipe, string text)
    {
        try { return await Submit(pipe, text).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { return false; }
    }
}
