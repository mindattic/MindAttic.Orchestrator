using MindAttic.Orchestrator.Sessions;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>The delivery of the agent mailbox (M.ORCH-T1745, on <see cref="Mailbox"/>): each live agent's unread mail handed to its
/// CLI as a <c>nudge-&lt;pid&gt;-mail.txt</c> file, which the UserPromptSubmit hook adds to its next turn; an idle agent with mail
/// also gets a wake queued (<see cref="Wakeups"/>, M.ORCH-T2067), said to you once per idle stretch.</summary>
public sealed partial class Daemon
{
    /// <summary>The nudge kind mail goes under.</summary>
    internal const string MailKind = "mail";

    /// <summary>How old a nudge file the hook drops unread (its <c>take_nudges</c>: 20 minutes); one taken younger was read.</summary>
    internal static readonly TimeSpan MailStale = TimeSpan.FromMinutes(20);

    /// <summary>How often the tick looks for mail.</summary>
    internal static readonly TimeSpan MailEvery = TimeSpan.FromSeconds(15);

    private Mailbox? mailbox;
    private DateTime mailLookedUtc;

    /// <summary>Each CLI's mail file as last written, by pid: the agent, the messages in it and when it was written.</summary>
    private readonly Dictionary<int, (string Agent, HashSet<string> Ids, DateTime WrittenUtc)> mailLeft = [];

    /// <summary>
    /// Hands every live agent its unread mail: an agent's CLI is the newest session whose <c>session_states</c> names it
    /// (<see cref="SessionContext.Agent"/>) and whose Claude process still runs. A file written earlier that is gone now was taken
    /// by the hook, and its messages are marked read when it was taken younger than <see cref="MailStale"/> (an older one the hook
    /// dropped unread, so they're left again); a file still there is rewritten when the unread set changed or it went stale.
    /// Returns the messages marked read.
    /// </summary>
    internal int DeliverMail(ContextCycler cycler, DateTime nowUtc, Func<int, bool>? isAlive = null)
    {
        if (nowUtc - mailLookedUtc < MailEvery) return 0;
        mailLookedUtc = nowUtc;
        isAlive ??= ContextCycler.IsClaude;
        mailbox ??= new Mailbox(store.Db);

        // What the hook has taken since the last look.
        var read = 0;
        foreach (var (pid, left) in mailLeft.ToList())
        {
            if (File.Exists(MailPath(cycler, pid))) continue;
            mailLeft.Remove(pid);
            if (nowUtc - left.WrittenUtc >= MailStale) continue;
            var marked = mailbox.MarkRead(left.Ids, nowUtc);
            read += marked;
            if (marked > 0) Say(LogEventLevel.Information, $"mail: {left.Agent} (pid {pid}) took {marked} message(s)");
        }

        var unread = mailbox.UnreadByAgent();
        var live = cycler.Load()
            .Where(c => c.Pid is not null && c.Agent is not null)
            .GroupBy(c => c.Pid!.Value)
            .Select(g => g.MaxBy(c => c.TurnUtc)!)
            .Where(c => isAlive(c.Pid!.Value))
            .GroupBy(c => c.Agent!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.MaxBy(c => c.TurnUtc)!, StringComparer.OrdinalIgnoreCase);

        var keep = new List<int>();
        var idle = new List<int>();
        foreach (var (agent, session) in live)
        {
            var pid = session.Pid!.Value;
            if (!unread.TryGetValue(agent, out var messages) || messages.Count == 0) continue;
            keep.Add(pid);
            // Idle (settled, not waiting on a window nor running a /goal): it only takes its mail at its next prompt, so a wake
            // is queued for it (M.ORCH-T2067), said to you once per idle stretch.
            if (session is { Idle: true, LimitStop: null, Goal: null } && nowUtc - session.TurnUtc >= AllocateWaker.Settle)
            {
                idle.Add(pid);
                WakeQueue.Enqueue(pid, session.TurnUtc, MailKind, MailWakeLine(agent, LimitWaker.Where(session.Cwd), messages.Count), nowUtc);
            }
            var ids = messages.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            if (mailLeft.TryGetValue(pid, out var left) && left.Agent.Equals(agent, StringComparison.OrdinalIgnoreCase)
                && left.Ids.SetEquals(ids) && nowUtc - left.WrittenUtc < MailStale && File.Exists(MailPath(cycler, pid))) continue;
            if (!CliNudges.Leave(cycler.Directory, pid, MailKind, Mailbox.Nudge(messages))) continue;
            mailLeft[pid] = (agent, ids, nowUtc);
            Say(LogEventLevel.Information, $"mail: left {messages.Count} message(s) for {agent} (pid {pid})");
        }
        // A CLI that ended, or whose agent has nothing unread now, keeps no mail file.
        foreach (var pid in mailLeft.Keys.Where(p => !keep.Contains(p)).ToList()) mailLeft.Remove(pid);
        CliNudges.Retain(cycler.Directory, MailKind, keep);
        // A CLI that took its mail, went busy or ended has no mail wake standing.
        WakeQueue.Retain(MailKind, idle);
        return read;
    }

    private Wakeups? wakeQueue;

    /// <summary>The wakeup queue (<see cref="Wakeups"/>) in the orchestrator's store.</summary>
    internal Wakeups WakeQueue => wakeQueue ??= new Wakeups(store);

    /// <summary>What's said when an idle agent has mail waiting (M.ORCH-T2067).</summary>
    internal static string MailWakeLine(string agent, string where, int count) =>
        $"Agent {agent} in {where} is idle with {count} unread message{(count == 1 ? "" : "s")} waiting. It takes {(count == 1 ? "it" : "them")} at its next prompt; " +
        "nothing is typed into its box, so it carries on when you next talk to it.";

    /// <summary>Says each wake the queue has not said yet (log and the Needs-you alert): an idle CLI only takes what was left for
    /// its hook at its next prompt (M.ORCH-T864).</summary>
    internal void ReportQueuedWakes()
    {
        foreach (var w in WakeQueue.Take())
        {
            Say(LogEventLevel.Warning, w.Line);
            Alert("Needs you", w.Line);
        }
    }

    private static string MailPath(ContextCycler cycler, int pid) => Path.Combine(cycler.Directory, $"{CliNudges.Prefix}{pid}-{MailKind}.txt");
}
