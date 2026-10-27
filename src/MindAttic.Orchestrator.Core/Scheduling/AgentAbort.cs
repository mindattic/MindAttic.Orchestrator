using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>One in-flight subagent the abort may pick: the ticket it works, what it burns of its login's 5-hour window
/// (percent of the window per hour), and whether it's landing (committing, rebasing, pushing), so not at a safe stop.</summary>
public sealed record AbortCandidate(string AgentId, string TicketId, double PercentPerHour, bool Landing)
{
    /// <summary>The CLI that runs it (its subagent mark's process id); null when not recorded.</summary>
    public int? Cli { get; init; }
}

/// <summary>An abort the orchestrator sent: the login (its email; null for the global login), the subagent, its ticket and burn.</summary>
public sealed record AbortedAgent(string? Login, string AgentId, string TicketId, double PercentPerHour, double ProjectedPercent, double TargetPercent);

/// <summary>An abort mark still settling into the login's trend: when it was sent and what the subagent was burning.</summary>
public sealed record AbortMark(string AgentId, DateTime AtUtc, double PercentPerHour);

/// <summary>
/// The orchestrator's last-resort cool-off lever (M.ORCH-T786, the user, 2026-10-24: drop extra weight off a plane that can't
/// stay in the air; losing tokens is fine, hitting the ceiling has no fix). When a login's live aggregated 5-hour trend
/// (M.ORCH-T783) is still on course past its pace line with the new-start cool-off (M.ORCH-T785, <see cref="Pacer.CoolOff"/>)
/// already on for <see cref="OrchestratorSettings.AgentAbortAfterMinutes"/>, the orchestrator itself picks just enough of that
/// login's in-flight ticket subagents to pull the trend back under (<see cref="Choose"/>: the ones not landing first, the fastest
/// burning first) and aborts them: an abort mark in the login's <c>session-context\subagent-aborts</c> that the pace gate reads
/// before each of that subagent's tool calls and refuses it with "stop now" (so it stops between tool calls, never mid-write or
/// mid-commit, its worktree and uncommitted work left as they are); its claim is released and its ticket goes to the front of
/// the queue for the next round (<see cref="Requeued"/>). Only subagents working a ticket are ever picked: a CLI itself, and a
/// manual session's own subagents, are never touched.
/// </summary>
public static partial class AgentAbort
{
    /// <summary>The abort marks' folder in a login's <c>session-context</c>: one file per aborted subagent, named as its mark is.</summary>
    public const string DirName = "subagent-aborts";

    /// <summary>The <c>state</c> row holding each ticket an abort put back (ticket → when), to sort it first in the queue.</summary>
    public const string RequeuedStateName = "requeued";

    /// <summary>How long an aborted ticket stays at the front of the queue unclaimed.</summary>
    public static readonly TimeSpan RequeueLife = TimeSpan.FromDays(1);

    /// <summary>How long an abort takes to leave the trend: the 10-minute rate the projection carries (M.ORCH-T783) still holds
    /// the aborted subagent's spend, less each minute, until it has rolled out.</summary>
    public static TimeSpan Settle => BudgetCalculator.ControlWindow;

    /// <summary>An abort mark older than this is dropped whatever became of its subagent.</summary>
    public static readonly TimeSpan MarkLife = TimeSpan.FromHours(6);

    /// <summary>A mark's file name, as the context gate names a subagent's own mark.</summary>
    public static string MarkName(string agentId) => SafeName().Replace(agentId, "_");

    /// <summary>A transcript's agent id (<c>agent-&lt;id&gt;</c>, its file name) as the marks name it: the bare <c>&lt;id&gt;</c>
    /// (bughunt 2026-10-27).</summary>
    public static string BareId(string agentId) => agentId.StartsWith("agent-", StringComparison.Ordinal) ? agentId["agent-".Length..] : agentId;

    [GeneratedRegex(@"[^A-Za-z0-9_.-]")]
    private static partial Regex SafeName();

    [GeneratedRegex(@"\b(?:commit|push|rebase|merg(?:e|ing)|land(?:s|ing)?|cherry-pick)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LandingWords();

    /// <summary>Whether a subagent's latest tool call (<see cref="SubagentActivity.Doing"/>) is landing its work: not a safe stop.</summary>
    public static bool Landing(string? doing) => doing is not null && LandingWords().IsMatch(doing);

    /// <summary>
    /// Whether the last resort applies to a login now: the cool-off is on (<paramref name="pace"/>'s <see cref="Pace.CoolOffPercent"/>)
    /// over a running window (<paramref name="window"/>), and has been since <paramref name="coolingSinceUtc"/> for at least
    /// <paramref name="after"/>, the throttle's own chance to bend the trend.
    /// </summary>
    public static bool Due(Pace? pace, Utilization? window, DateTime? coolingSinceUtc, TimeSpan after, DateTime nowUtc) =>
        pace is { CoolOffPercent: not null, Stop: false } && window is { Ended: false, Idle: false } u && u.EndUtc > u.NowUtc &&
        coolingSinceUtc is { } since && nowUtc - since >= after;

    /// <summary>
    /// Which of <paramref name="running"/> to abort so <paramref name="window"/>'s trend lands back under its pace line: the
    /// percent per hour over (the projection past the target, spread over the hours left), less what <paramref name="recent"/>
    /// aborts still hold in the trend (each one's burn, fading out over <see cref="Settle"/>), shed in this order: the ones not
    /// landing first (nearest a safe stop), then the fastest burning, until the burn shed covers it. None when the trend is
    /// under the line once the recent aborts are taken out; all of them when even that doesn't cover it.
    /// </summary>
    public static IReadOnlyList<AbortCandidate> Choose(Utilization window, IReadOnlyList<AbortCandidate> running, IReadOnlyList<AbortMark> recent, DateTime nowUtc)
    {
        var hoursLeft = (window.EndUtc - window.NowUtc).TotalHours;
        if (hoursLeft <= 0 || running.Count == 0) return [];
        var fading = recent.Sum(m => Math.Max(0, m.PercentPerHour) * Math.Clamp(1 - (nowUtc - m.AtUtc) / Settle, 0, 1));
        var need = (window.ProjectedEndPercent - window.TargetPercent) / hoursLeft - fading;
        if (!(need > 0)) return [];
        var chosen = new List<AbortCandidate>();
        var shed = 0.0;
        foreach (var c in running.OrderBy(c => c.Landing).ThenByDescending(c => c.PercentPerHour).ThenBy(c => c.AgentId, StringComparer.Ordinal))
        {
            if (shed >= need) break;
            chosen.Add(c);
            shed += Math.Max(0, c.PercentPerHour);
        }
        return chosen;
    }

    /// <summary>A subagent's burn as a percent of its login's 5-hour window per hour: its fresh tokens over the last
    /// <see cref="Settle"/> when its transcript's responses are known (<paramref name="recentEvents"/>), else its whole run's average
    /// (<paramref name="activity"/>, over at least <see cref="Settle"/>); 0 without a limit.</summary>
    public static double PercentPerHour(IReadOnlyList<UsageEvent>? recentEvents, SubagentActivity? activity, long blockLimit, DateTime nowUtc)
    {
        if (blockLimit <= 0) return 0;
        double perHour;
        if (recentEvents is { Count: > 0 })
            perHour = recentEvents.Where(e => e.AtUtc > nowUtc - Settle && e.AtUtc <= nowUtc).Sum(e => (double)e.Usage.Fresh) / Settle.TotalHours;
        else if (activity is not null)
            perHour = activity.Usage.Fresh / Math.Max(Settle.TotalHours, (activity.LastWriteUtc - activity.StartedUtc).TotalHours);
        else return 0;
        return perHour / blockLimit * 100;
    }

    /// <summary>Writes <paramref name="c"/>'s abort mark in <paramref name="marksDirectory"/> (a login's <c>session-context</c>):
    /// what the pace gate tells it as it refuses its calls.</summary>
    public static void WriteMark(string marksDirectory, AbortCandidate c, string? login, double projected, double target, DateTime nowUtc)
    {
        var folder = Path.Combine(marksDirectory, DirName);
        Directory.CreateDirectory(folder);
        var why = $"{login ?? "this login"}'s 5-hour window is on course to end near {projected.ToString("0", CultureInfo.InvariantCulture)}%, " +
                  $"past its {target.ToString("0", CultureInfo.InvariantCulture)}% pace line, even with new subagents slowed";
        var body = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["ticket"] = c.TicketId,
            ["at"] = nowUtc.ToString("O", CultureInfo.InvariantCulture),
            ["percent_per_hour"] = Math.Round(Math.Max(0, c.PercentPerHour), 3),
            ["login"] = login,
            ["why"] = why,
        });
        var path = Path.Combine(folder, MarkName(c.AgentId));
        var temp = path + ".tmp";
        File.WriteAllText(temp, body);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The abort marks in <paramref name="marksDirectory"/>; one that can't be read counts as just sent with no burn known.</summary>
    public static IReadOnlyList<AbortMark> Marks(string marksDirectory, DateTime nowUtc)
    {
        var folder = Path.Combine(marksDirectory, DirName);
        if (!Directory.Exists(folder)) return [];
        var marks = new List<AbortMark>();
        foreach (var f in Directory.EnumerateFiles(folder).Where(f => !f.EndsWith(".tmp", StringComparison.Ordinal)))
        {
            var at = nowUtc;
            var rate = 0.0;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                var r = doc.RootElement;
                if (r.TryGetProperty("at", out var a) && a.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(a.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
                    at = when.ToUniversalTime();
                if (r.TryGetProperty("percent_per_hour", out var p) && p.ValueKind == JsonValueKind.Number) rate = p.GetDouble();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            marks.Add(new AbortMark(Path.GetFileName(f), at, rate));
        }
        return marks;
    }

    /// <summary>Drops abort marks whose subagent has stopped (not in <paramref name="alive"/>) once they've settled out of the
    /// trend, and any past <see cref="MarkLife"/>.</summary>
    public static void Prune(string marksDirectory, IReadOnlySet<string> alive, DateTime nowUtc)
    {
        foreach (var m in Marks(marksDirectory, nowUtc))
            if (nowUtc - m.AtUtc > MarkLife || (!alive.Contains(m.AgentId) && nowUtc - m.AtUtc > Settle))
                try { File.Delete(Path.Combine(marksDirectory, DirName, m.AgentId)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Releases <paramref name="ticketId"/>'s claim (the ticket returns to the queue; its worktree is never touched) and
    /// records it requeued, so it sorts first in the queue for the next round.</summary>
    public static void Requeue(JsonStore store, string ticketId, DateTime nowUtc)
    {
        store.UpdateAssignments(all => all.RemoveAll(a => a.IsClaim && a.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase)));
        store.Update<Dictionary<string, string>>(RequeuedStateName, all =>
        {
            foreach (var old in all.Where(kv => !Fresh(kv.Value, nowUtc)).Select(kv => kv.Key).ToList()) all.Remove(old);
            all[ticketId] = nowUtc.ToString("O", CultureInfo.InvariantCulture);
        });
    }

    /// <summary>The tickets an abort put back within <see cref="RequeueLife"/>: first in the queue. Empty when unreadable.</summary>
    public static IReadOnlySet<string> Requeued(JsonStore store, DateTime nowUtc)
    {
        try
        {
            return store.Load<Dictionary<string, string>>(RequeuedStateName).Where(kv => Fresh(kv.Value, nowUtc))
                .Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool Fresh(string at, DateTime nowUtc) =>
        DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when) && nowUtc - when.ToUniversalTime() < RequeueLife;
}

/// <summary>
/// Runs <see cref="AgentAbort"/> each daemon tick for the global login and every per-CLI login (M.ORCH-T786): keeps when each
/// login's cool-off came on, reads its running subagents, and aborts what <see cref="AgentAbort.Choose"/> picks. Keep one
/// instance (the daemon's, under its wake lease); not thread-safe.
/// </summary>
public sealed class AgentAborter(JsonStore store, string globalMarksDirectory, string projectsDirectory)
{
    private const string GlobalKey = "(global)";
    private readonly Dictionary<string, DateTime> coolingSince = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SubagentActivityReader> readers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AbortedAgent> Tick(Snapshot snapshot, OrchestratorSettings settings, DateTime nowUtc)
    {
        var logins = new List<(string Key, string? Email, string Marks, Pace? Pace, Utilization? Window, BudgetStatus? Budget)>
        {
            (GlobalKey, null, globalMarksDirectory, snapshot.Pace, snapshot.Utilization, snapshot.Budget),
        };
        logins.AddRange(snapshot.Accounts.Select(a => (a.Email, (string?)a.Email, Path.Combine(a.Directory, "session-context"), (Pace?)a.Pace, a.Utilization, (BudgetStatus?)a.Budget)));
        foreach (var gone in coolingSince.Keys.Where(k => logins.All(l => !l.Key.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList()) coolingSince.Remove(gone);

        // Every subagent working a ticket, as the transcripts know it: its ticket and its responses in the reader's window.
        // Keyed by the bare id the marks use: a transcript's id is its file name, "agent-<id>" (bughunt 2026-10-27).
        var ticketed = snapshot.Repos.SelectMany(r => r.Sessions).SelectMany(s => s.TicketedSubagents)
            .GroupBy(a => AgentAbort.MarkName(AgentAbort.BareId(a.AgentId)), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var aborted = new List<AbortedAgent>();
        foreach (var login in logins)
        {
            var cycler = new ContextCycler(login.Marks) { ProjectsDir = projectsDirectory };
            var alive = cycler.AliveSubagents(nowUtc);
            AgentAbort.Prune(login.Marks, alive.Select(a => a.Agent).ToHashSet(StringComparer.Ordinal), nowUtc);
            if (!settings.AgentAbort || login.Pace?.CoolOffPercent is null) { coolingSince.Remove(login.Key); continue; }
            if (!coolingSince.TryGetValue(login.Key, out var since)) coolingSince[login.Key] = since = nowUtc;
            var after = TimeSpan.FromMinutes(Math.Clamp(settings.AgentAbortAfterMinutes, 0, 60));
            if (!AgentAbort.Due(login.Pace, login.Window, since, after, nowUtc) || alive.Count == 0) continue;

            var marks = AgentAbort.Marks(login.Marks, nowUtc);
            var marked = marks.Select(m => m.AgentId).ToHashSet(StringComparer.Ordinal);
            if (!readers.TryGetValue(login.Marks, out var reader)) readers[login.Marks] = reader = new SubagentActivityReader(projectsDirectory);
            var activity = reader.Poll(cycler, nowUtc).ToDictionary(a => AgentAbort.MarkName(a.AgentId), StringComparer.Ordinal);
            var limit = login.Budget?.BlockLimit ?? 0;
            var candidates = new List<AbortCandidate>();
            foreach (var (agent, cli) in alive)
            {
                if (marked.Contains(agent)) continue;
                var known = ticketed.GetValueOrDefault(agent);
                var doing = activity.GetValueOrDefault(agent);
                // Only a subagent working a ticket: never a manual session's own subagents.
                var ticket = known?.TicketId ?? (doing?.Task is { } task ? TicketAttribution.FromPrompt(task)?.Id : null);
                if (ticket is null) continue;
                candidates.Add(new AbortCandidate(agent, ticket, AgentAbort.PercentPerHour(known?.Events, doing, limit, nowUtc), AgentAbort.Landing(doing?.Doing)) { Cli = cli });
            }
            var window = login.Window!;
            var settling = marks.Where(m => nowUtc - m.AtUtc < AgentAbort.Settle).ToList();
            foreach (var c in AgentAbort.Choose(window, candidates, settling, nowUtc))
            {
                try
                {
                    AgentAbort.WriteMark(login.Marks, c, login.Email, window.ProjectedEndPercent, window.TargetPercent, nowUtc);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                try { AgentAbort.Requeue(store, c.TicketId, nowUtc); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
                aborted.Add(new AbortedAgent(login.Email, c.AgentId, c.TicketId, c.PercentPerHour, window.ProjectedEndPercent, window.TargetPercent));
            }
        }
        return aborted;
    }
}
