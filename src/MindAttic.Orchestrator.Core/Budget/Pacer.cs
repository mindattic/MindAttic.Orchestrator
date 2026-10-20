using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

public enum PaceMode
{
    /// <summary>No limit to pace against, or pacing is off: the warn line decides, as before.</summary>
    Unpaced,
    /// <summary>The burn rate fits: every session slot is available.</summary>
    Open,
    /// <summary>Burning faster than the window allows: fewer sessions until it fits again.</summary>
    Throttled,
    /// <summary>Under the limit, but not even one more session fits before the reset.</summary>
    Coast,
    /// <summary>At or past a limit.</summary>
    Over,
}

/// <summary>How fast you can spend and still reach the reset without hitting the wall, and how many sessions that allows.</summary>
public sealed record Pace(
    PaceMode Mode,
    int SessionCap,
    double SustainablePerHour,
    double RecentPerHour,
    double? PerSessionPerHour,
    string? Governing,
    DateTime? ResetUtc,
    string Note,
    string AgentNote = "")
{
    public bool IsPaced => Mode != PaceMode.Unpaced;

    /// <summary>Uncached tokens left before the tightest window reaches its pace target; null when unpaced.</summary>
    public double? RoomTokens { get; init; }

    /// <summary>Seconds running agents wait before each tool call so the burn lasts until the reset; 0 runs them free.</summary>
    public double AgentDelaySeconds { get; init; }

    /// <summary>How many subagents may run at once across every CLI right now (the gate holds a new one past it);
    /// null when unpaced, so nothing is held.</summary>
    public int? SubagentAllowance { get; init; }

    /// <summary>How many more sessions the spare rate pays for right now (what fits the target minus what's being drawn,
    /// over what one session draws); negative when the draw is already over. Autopilot starts at most this many.</summary>
    public int SpareSessions { get; init; }

    /// <summary>Subagents running when the allowance was worked out.</summary>
    public int Subagents { get; init; }

    /// <summary>The last-write time of the newest subagent mark <see cref="Subagents"/> was counted from (null: it saw
    /// none); a mark newer than it started since the count.</summary>
    public DateTime? SubagentsSeenThroughUtc { get; init; }

    /// <summary><see cref="Subagents"/> by the CLI (process id) running each, for each CLI's fair share of the allowance
    /// (<see cref="Scheduling.SubagentShare"/>, M.ORCH-T495); a subagent whose mark names no CLI isn't in it.</summary>
    public IReadOnlyDictionary<int, int> SubagentsByCli { get; init; } = new Dictionary<int, int>();

    /// <summary>Where the 5-hour window is on course to end, when <see cref="Pacer.UnderTarget"/> raised the allowance for
    /// being under <see cref="TargetPercent"/> (M.ORCH-T529); null when it didn't.</summary>
    public double? UnderTargetPercent { get; init; }

    /// <summary>The pace target <see cref="UnderTargetPercent"/> is short of.</summary>
    public double TargetPercent { get; init; }

    /// <summary>The window is at its target or a limit: nothing more may be spent before the reset. The pace gate then
    /// refuses tool calls (every CLI, /pacing off included) rather than letting a held call through, so usage never
    /// passes the limit, whatever the hold settings.</summary>
    public bool Stop { get; init; }

    /// <summary>The window is past <see cref="WindDownAt"/> of its limit: agents start only what they can finish
    /// before the stop, and file what they can't as a ticket; autopilot starts no new ticket until the window resets
    /// (M.ORCH-T254: tickets whose estimate fit what was left still went out, five at once as the reset neared).</summary>
    public bool WindDown { get; init; }

    /// <summary>The share of the limit the wind-down starts at: <see cref="Pacer.WindDownShare"/>, or
    /// <see cref="Pacer.FrontLoadedWindDownShare"/> on the front-loaded profile (which also starts no new subagent past it).</summary>
    public double WindDownAt { get; init; } = Pacer.WindDownShare;

    /// <summary>The profile the pace was worked out on (<see cref="OrchestratorSettings.PaceProfile"/>).</summary>
    public string Profile { get; init; } = OrchestratorSettings.LinearProfile;

    /// <summary>Why no new ticket starts past the wind-down line; empty when it isn't passed (or the window is stopped).</summary>
    public string WindDownHold => !WindDown || Stop ? "" :
        $"the {Governing ?? "5-hour"} window is past {WindDownPercent} of its limit: no new ticket starts before it resets" +
        (ResetUtc is { } r ? $" at {Formatting.ResetTime(r, DateTime.UtcNow)}" : "");

    // Written the same on any Windows language, as the rest of the notes are (M.ORCH-T404).
    private string WindDownPercent => (WindDownAt * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    public string WindDownNote => !WindDown || Stop ? "" :
        $"The {Governing ?? "5-hour"} window is past {WindDownPercent} of its limit" + (ResetUtc is { } r ? $" (resets {Formatting.ResetTime(r, DateTime.UtcNow)})" : "") +
        ", and work stops outright at the target. Don't start anything you can't finish before then. If the task you're on " +
        "won't finish, commit what's done and file the rest as a ticket (workflow ticket new --type <type> --summary \"...\").";

    /// <summary>What agents are told about the allowance at the start of each turn.</summary>
    public string SubagentNote => SubagentAllowance switch
    {
        null => "",
        0 => $"Subagents: none new right now ({(WindDown && !Stop && Profile == OrchestratorSettings.FrontLoadedProfile ? $"the {Governing ?? "5-hour"} window is past its {WindDownPercent} wind-down line" : Note)}). Finish with the {Subagents} running; a new one waits until there's room.",
        var a when a > Subagents && UnderTargetPercent is { } projected => $"Subagents: up to {a} may run at once across all agents ({Subagents} running now). " +
            $"The 5-hour window is on course to end near {projected:0}%, under its {TargetPercent:0}% target, so start more now: a subagent on every ticket " +
            "you've claimed or been handed, all at once, and parallel subagents for independent pieces. Don't hold one back because its files overlap " +
            "another's: each rebases onto main as it lands.",
        var a when a > Subagents => $"Subagents: up to {a} may run at once across all agents ({Subagents} running now). There's room under the 5-hour pace, " +
            "so hand independent pieces (searches, separate files, test runs, reviews) to parallel subagents rather than doing them one after another.",
        var a => $"Subagents: {a} at once across all agents ({Subagents} running now). Usage is ahead of the 5-hour pace, so don't start extra subagents; a new one waits for a free slot.",
    };

    public string Key => Mode.ToString().ToLowerInvariant();

    /// <summary>Running agents should conserve: past a limit, out of room, or burning faster than fits.
    /// Coasting only means no room for another session; the ones running are told nothing.</summary>
    public bool Advise => Mode is PaceMode.Over || (IsPaced && (SustainablePerHour <= 0 || RecentPerHour > SustainablePerHour));
}

/// <summary>
/// Steers spend so usage reaches <see cref="OrchestratorSettings.PaceTargetPercent"/> of each limit
/// as the window resets, instead of hitting the wall early and waiting hours. For every window with a
/// real limit (the 5-hour block, and the week once it has a limit and a reset), the sustainable rate
/// is what's left of the target divided by the hours until reset, or on the front-loaded profile what
/// keeps it on its curve (<see cref="SustainableRate"/>); the tightest window governs. The
/// session cap is how many sessions at today's per-session burn fit in that rate. Near a reset with
/// budget left the sustainable rate climbs, so the cap opens back up and the leftover gets used.
/// </summary>
public static class Pacer
{
    private static readonly TimeSpan Bucket = TimeSpan.FromMinutes(10);

    /// <summary>Past this share of the 5-hour limit, only work that can finish before the stop starts.</summary>
    public const double WindDownShare = 0.90;

    /// <summary>Why nothing is paced while the sign-in is an API key: it's billed per token, with no window to run into.</summary>
    public const string ApiKeyNote = "API key: no 5-hour or weekly windows, so nothing is paced or stopped.";

    /// <param name="recentDelaySeconds">The agent delay averaged over the recent-rate window (see
    /// <see cref="RateLimitStatusWriter.AgentDelay"/>): what the measured rate was slowed by.</param>
    /// <param name="subagents">Subagents running now across every CLI (the context gate's SubagentStart/Stop marks).</param>
    public static Pace Compute(BudgetStatus b, int activeSessions, double? typicalPerSession, OrchestratorSettings s, DateTime nowUtc,
        double recentDelaySeconds = 0, int subagents = 0)
    {
        var max = s.MaxConcurrentSessions;
        var recent = b.RecentFreshPerHour;

        // The stop never depends on pacing being on: a wall Claude reported, or a known 5-hour limit spent to its target,
        // stops everything either way (found in review: with pacing off nothing ever said stop).
        if (!s.Pacing && b.AtLimitUntilUtc is null && !b.WeeklyStopped(nowUtc))
        {
            var spentOut = !b.BlockLimitIsPeak && b.BlockLimit > 0 && b.CurrentBlock is { } blk &&
                blk.Usage.Fresh >= b.BlockLimit * Math.Clamp(s.PaceTargetPercent, 1, 100) / 100.0;
            // Said when it's so: "Pacing is off." alone read as nothing held while the gate refused every call (M.ORCH-T466).
            var offNote = spentOut && b.CurrentBlock is { } end
                ? $"Pacing is off, but the 5-hour window is at its target: tool calls are refused until it resets at {Formatting.ResetTime(end.EndUtc, nowUtc)}."
                : "Pacing is off.";
            return new Pace(PaceMode.Unpaced, max, 0, recent, null, "5-hour", b.CurrentBlock?.EndUtc, offNote) { Stop = spentOut };
        }

        if (b.AtLimitUntilUtc is { } until)
        {
            var name = b.AtLimitKind == LimitKind.Weekly ? "weekly" : "5-hour";
            return new Pace(PaceMode.Over, 0, 0, recent, null, name, until,
                $"At the {name} limit until {Formatting.ResetTime(until, nowUtc)}.",
                $"At the {name} limit until {Formatting.ResetTime(until, nowUtc)}. Finish the current step and stop at a clean point.")
            { SubagentAllowance = 0, Subagents = subagents, Stop = true };
        }

        // Claude reports the week at the stop line: the hooks refuse every tool call from here to the weekly reset, so nothing
        // may be handed out or said to be on pace (M.ORCH-T438). A hard stop only, at the hooks' own line; below it the week
        // is never paced.
        if (b.WeeklyStopped(nowUtc) && b.WeeklyStop is { } weekStop)
        {
            var at = $"Claude reports the week at {weekStop.Percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%";
            return new Pace(PaceMode.Over, 0, 0, recent, null, "weekly", weekStop.ResetUtc,
                $"{at}: stopped at the weekly limit until {Formatting.ResetTime(weekStop.ResetUtc, nowUtc)}.",
                $"{at}: at the weekly limit until {Formatting.ResetTime(weekStop.ResetUtc, nowUtc)}. Finish the current step and stop at a clean point.")
            { SubagentAllowance = 0, Subagents = subagents, Stop = true };
        }

        var windows = new List<(string Name, long Limit, long Used, DateTime StartUtc, DateTime ResetUtc)>();
        if (!b.BlockLimitIsPeak && b.BlockLimit > 0)
            windows.Add(b.CurrentBlock is { } block
                ? ("5-hour", b.BlockLimit, block.Usage.Fresh, block.StartUtc, block.EndUtc)
                : ("5-hour", b.BlockLimit, 0, nowUtc, nowUtc + BudgetCalculator.BlockLength));
        // The week is shown, never paced: spending each 5-hour window to its target is the goal, and a
        // week-long stretch would hold agents for days. Only a real weekly wall, or Claude's week at the hooks' stop line
        // (both above), stops new work.
        if (windows.Count == 0)
            return new Pace(PaceMode.Unpaced, max, 0, recent, null, null, null,
                "Nothing to pace against yet: set a budget, or one is learned the first time you hit a limit.");

        // Past a limit only as far as the orchestrator can tell (no wall from Claude): keep going at the
        // slowest pace; if the limit was wrong, the learner retires it.
        if (windows.FirstOrDefault(w => w.Used >= w.Limit) is { Name: not null } spent)
            return new Pace(PaceMode.Over, 0, 0, recent, null, spent.Name, spent.ResetUtc,
                $"Past the {spent.Name} limit until {Clock(spent.ResetUtc, nowUtc)}.",
                $"Past the {spent.Name} limit until {Clock(spent.ResetUtc, nowUtc)}. Finish the current step and stop at a clean point.")
            { AgentDelaySeconds = AgentDelay(b.ControlFreshPerHour, 0, recentDelaySeconds, s), SubagentAllowance = 0, Subagents = subagents, Stop = true };

        var target = Math.Clamp(s.PaceTargetPercent, 1, 100) / 100.0;
        var k = s.PaceExponent;
        var (gov, sustainable) = windows
            .Select(w => (w, Rate: SustainableRate(w.Limit, w.Used, target, w.StartUtc, w.ResetUtc, nowUtc, k)))
            .MinBy(x => x.Rate);
        // At the target: nothing more is spent before the reset. Under the front-loaded curve the rate is also 0 while usage is
        // above the curve, but that only holds agents until the curve catches up; it never stops them.
        var atTarget = windows.Any(w => w.Used >= w.Limit * target);
        var windDownAt = s.FrontLoaded ? FrontLoadedWindDownShare : WindDownShare;
        var windDown = windows.Any(w => w.Used >= w.Limit * windDownAt);

        // What one more session costs: today's sessions if any are running, else your usual session. A
        // held session burns less than it will once let go, so judge it by what it would draw unheld.
        // Never below the historical typical: a lull right after a batch of sessions ends (recent draw
        // momentarily near zero) must not make the next session look free, or spare/perSession explodes
        // into a burst of new sessions sized for a cost that isn't real (found live: autopilot read a
        // post-batch lull as near-zero cost and started 24 sessions in one tick, well past the 5-hour
        // target before its reset, M.ORCH-T111).
        double? perSession = activeSessions > 0 && recent > 0
            ? Math.Max(Unheld(recent, recentDelaySeconds) / activeSessions, typicalPerSession ?? 0)
            : typicalPerSession;
        var cap = sustainable <= 0 ? 0
            : perSession is > 0 and var p ? (int)Math.Clamp(Math.Floor(sustainable / p), 0, max)
            : Math.Min(1, max); // no history at all: start one and measure it

        var mode = cap == 0 ? PaceMode.Coast : cap < max ? PaceMode.Throttled : PaceMode.Open;
        var rates = $"{Formatting.Tokens((long)recent)}/h now, {Formatting.Tokens((long)sustainable)}/h fits the {gov.Name} window until {Clock(gov.ResetUtc, nowUtc)}";
        var note = mode switch
        {
            PaceMode.Open => $"On pace: {rates}.",
            PaceMode.Throttled => $"Pacing: {rates}; capped at {cap} of {max} sessions.",
            _ => atTarget
                ? $"Coasting: the {gov.Name} window is at its {s.PaceTargetPercent}% target until {Clock(gov.ResetUtc, nowUtc)}; no new sessions."
                : sustainable <= 0
                ? $"Coasting: the {gov.Name} window is ahead of its front-loaded curve; held until the curve catches up (resets {Clock(gov.ResetUtc, nowUtc)})."
                : $"Coasting: {rates}; not even one more session fits.",
        };
        var delay = AgentDelay(b.ControlFreshPerHour, sustainable, recentDelaySeconds, s);
        var agentNote = $"Usage is running ahead of the {gov.Name} limit (resets {Clock(gov.ResetUtc, nowUtc)}); the orchestrator is stretching it" +
                        (delay > 0 ? $" by holding each tool call about {delay:0}s" : "") +
                        ". Keep working normally; the subagent allowance says how many subagents may run at once.";
        if (delay > 0) note += $" Agents held {delay:0}s per tool call.";
        var room = windows.Min(w => Math.Max(0, w.Limit * target - w.Used));
        var demand = Unheld(b.ControlFreshPerHour, recentDelaySeconds);
        // Front-loaded: past its wind-down line no new subagent starts at all (the ones running finish), however much the
        // curve's rate would still pay for, so the last stretch to the target isn't overrun by a burst started late.
        var allowance = s.FrontLoaded && windDown ? 0 : SubagentAllowance(sustainable, demand, perSession, subagents, s.MaxSubagents);
        return new Pace(mode, cap, sustainable, recent, perSession, gov.Name, gov.ResetUtc, note, agentNote)
        {
            RoomTokens = room, AgentDelaySeconds = delay, SubagentAllowance = allowance, Subagents = subagents,
            SpareSessions = SpareSessions(sustainable, demand, perSession),
            Stop = atTarget,
            WindDown = windDown, WindDownAt = windDownAt,
            Profile = s.FrontLoaded ? OrchestratorSettings.FrontLoadedProfile : OrchestratorSettings.LinearProfile,
        };
    }

    /// <summary>
    /// The uncached tokens per hour that keep a window on its pace curve. Linear (<paramref name="k"/> = 1): what's left of the
    /// target spread evenly over the hours to the reset. Front-loaded (<paramref name="k"/> &lt; 1): the rate that reaches the
    /// curve target × elapsed^k at <see cref="CurveHorizon"/> from now (the reset, once it's nearer), so it spends fast early and
    /// eases off; the curve never passes the target, and over the last stretch it is the linear rate. 0 at the target, or while
    /// usage is above the curve (held until the curve catches up).
    /// </summary>
    internal static double SustainableRate(double limit, double used, double target, DateTime startUtc, DateTime resetUtc, DateTime nowUtc, double k)
    {
        var left = Math.Max((resetUtc - nowUtc).TotalHours, 1.0 / 60);
        var length = (resetUtc - startUtc).TotalHours;
        if (k >= 1 || length <= 0) return Math.Max(0, limit * target - used) / left;
        var horizon = Math.Min(CurveHorizon.TotalHours, left);
        var at = Math.Clamp(((nowUtc - startUtc).TotalHours + horizon) / length, 0, 1);
        return Math.Max(0, limit * target * Math.Pow(at, k) - used) / horizon;
    }

    /// <summary>How far ahead the front-loaded rate aims along its curve: short enough to follow the bend, long enough not to
    /// swing with every reading.</summary>
    public static readonly TimeSpan CurveHorizon = TimeSpan.FromMinutes(30);

    /// <summary>On the front-loaded profile, past this share of the 5-hour limit no new subagent starts and no new ticket
    /// either (the wind-down), since the curve leaves less slack near the target than the straight line does.</summary>
    public const double FrontLoadedWindDownShare = 0.85;

    /// <summary>
    /// Sessions the spare rate pays for: (<paramref name="sustainable"/> − <paramref name="demand"/>) ÷ one session's
    /// draw, rounded down; negative when over. With no session history yet, one while there's any room, to measure it.
    /// </summary>
    internal static int SpareSessions(double sustainable, double demand, double? perSession)
    {
        if (sustainable <= 0) return 0;
        var spare = sustainable - demand;
        return perSession is > 0 and var p ? (int)Math.Clamp(Math.Floor(spare / p), -100, 100) : spare > 0 ? 1 : 0;
    }

    /// <summary>
    /// Raises the subagent allowance while the 5-hour window is on course to end under its target (M.ORCH-T529: it projected ~65%
    /// of a 98% target while claimed tickets sat unstarted): to the ones running plus one more for every 10 points it's short
    /// (at least one), never past <paramref name="max"/> and never lower than it was. Left as it is at the stop, past the
    /// wind-down line, when the pace says none new (0), or when the window isn't under its target.
    /// </summary>
    public static Pace UnderTarget(Pace pace, Utilization? window, int max)
    {
        if (pace is not { IsPaced: true, Stop: false, WindDown: false, SubagentAllowance: { } allowed and > 0 } || max <= 0) return pace;
        if (window is not { Ended: false } u || u.ProjectedEndPercent >= u.TargetPercent) return pace;
        var shortBy = u.TargetPercent - Math.Max(0, u.ProjectedEndPercent);
        var raised = (int)Math.Min(max, Math.Max(allowed, (long)pace.Subagents + Math.Max(1, (int)Math.Ceiling(shortBy / 10))));
        return pace with { SubagentAllowance = raised, UnderTargetPercent = Math.Max(0, u.ProjectedEndPercent), TargetPercent = u.TargetPercent };
    }

    /// <summary>
    /// How many subagents may run at once: the ones running plus as many more as the spare rate
    /// (<paramref name="sustainable"/> minus the unheld <paramref name="demand"/>) pays for, one
    /// <paramref name="perAgent"/> each; fewer when demand is over, so the excess drains as they finish.
    /// Never below 1 while there's anything left (a held spawn waits, it isn't refused), 0 once the
    /// window is at its target, and never above <paramref name="max"/>.
    /// </summary>
    internal static int SubagentAllowance(double sustainable, double demand, double? perAgent, int running, int max)
    {
        if (max <= 0 || sustainable <= 0) return 0;
        var spare = sustainable - demand;
        var more = perAgent is > 0 and var p ? (int)Math.Clamp(Math.Floor(spare / p), -1000, 1000) : spare > 0 ? 2 : -1;
        return (int)Math.Clamp((long)running + more, 1, max);
    }

    /// <summary>What a draw measured under a hold of <paramref name="heldSeconds"/> per tool call would be
    /// unheld: a <see cref="StepSeconds"/> step plus the wait burns what the step alone would.</summary>
    internal static double Unheld(double measured, double heldSeconds) =>
        measured * (StepSeconds + Math.Max(0, heldSeconds)) / StepSeconds;

    /// <summary>The working time between an agent's tool calls the delay is added to.</summary>
    internal const double StepSeconds = 20;

    /// <summary>
    /// How long each tool call waits so the burn fits <paramref name="sustainable"/>. The measured rate
    /// already reflects the delay agents were under, so the demand they'd burn unheld is estimated first
    /// (a step of <see cref="StepSeconds"/> plus the wait burns what <see cref="StepSeconds"/> alone
    /// would); judging the held rate instead would release the hold the moment it worked, and oscillate.
    /// Zero as soon as that demand fits.
    /// </summary>
    public static double AgentDelay(double recent, double sustainable, double recentDelaySeconds, OrchestratorSettings s)
    {
        var max = Math.Max(0, s.MaxAgentDelaySeconds);
        if (!s.ThrottleAgents || max == 0) return 0;
        if (sustainable <= 0) return recent > 0 ? max : 0;
        var demand = Unheld(recent, recentDelaySeconds);
        if (demand <= sustainable) return 0;
        return Math.Round(Math.Min(max, StepSeconds * (demand / sustainable - 1)));
    }

    /// <summary>
    /// Your usual burn for one session while it's working: the median uncached tokens per hour over
    /// every 10-minute stretch in which a session (subagents included) was active.
    /// </summary>
    public static double? TypicalPerSession(IEnumerable<IReadOnlyList<UsageEvent>> sessions, DateTime sinceUtc)
    {
        var rates = new List<double>();
        foreach (var events in sessions)
            foreach (var stretch in events.Where(e => e.AtUtc >= sinceUtc).GroupBy(e => e.AtUtc.Ticks / Bucket.Ticks))
                rates.Add(stretch.Sum(e => e.Usage.Fresh) / Bucket.TotalHours);
        if (rates.Count == 0) return null;
        rates.Sort();
        return rates[rates.Count / 2];
    }

    // With the weekday when it falls on another day: an 11:30 PM "until 2:00 AM" reads as today's (M.ORCH-T410).
    private static string Clock(DateTime utc, DateTime nowUtc) => Formatting.ResetTime(utc, nowUtc);
}
