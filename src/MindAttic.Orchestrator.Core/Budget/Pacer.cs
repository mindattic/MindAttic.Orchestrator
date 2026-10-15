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

    public string Key => Mode.ToString().ToLowerInvariant();

    /// <summary>Running agents should conserve: the orchestrator can't slow a session, only ask it to.</summary>
    public bool Advise => Mode is PaceMode.Over or PaceMode.Coast || (IsPaced && RecentPerHour > SustainablePerHour);
}

/// <summary>
/// Steers spend so usage reaches <see cref="OrchestratorSettings.PaceTargetPercent"/> of each limit
/// as the window resets, instead of hitting the wall early and waiting hours. For every window with a
/// real limit (the 5-hour block, and the week once it has a limit and a reset), the sustainable rate
/// is what's left of the target divided by the hours until reset; the tightest window governs. The
/// session cap is how many sessions at today's per-session burn fit in that rate. Near a reset with
/// budget left the sustainable rate climbs, so the cap opens back up and the leftover gets used.
/// </summary>
public static class Pacer
{
    private static readonly TimeSpan Bucket = TimeSpan.FromMinutes(10);

    public static Pace Compute(BudgetStatus b, int activeSessions, double? typicalPerSession, OrchestratorSettings s, DateTime nowUtc)
    {
        var max = s.MaxConcurrentSessions;
        var recent = b.RecentFreshPerHour;
        if (!s.Pacing) return new Pace(PaceMode.Unpaced, max, 0, recent, null, null, null, "Pacing is off.");

        if (b.AtLimitUntilUtc is { } until)
        {
            var name = b.AtLimitKind == LimitKind.Weekly ? "weekly" : "5-hour";
            return new Pace(PaceMode.Over, 0, 0, recent, null, name, until,
                $"At the {name} limit until {Clock(until)}.",
                $"At the {name} limit until {Clock(until)}. Finish the current step and stop at a clean point.");
        }

        var windows = new List<(string Name, long Limit, long Used, DateTime ResetUtc)>();
        if (!b.BlockLimitIsPeak && b.BlockLimit > 0)
            windows.Add(b.CurrentBlock is { } block
                ? ("5-hour", b.BlockLimit, block.Usage.Fresh, block.EndUtc)
                : ("5-hour", b.BlockLimit, 0, nowUtc + BudgetCalculator.BlockLength));
        if (b.WeeklyLimit is > 0 && b.WeekResetUtc is { } weekReset)
            windows.Add(("weekly", b.WeeklyLimit.Value, b.Week.Fresh, weekReset));
        if (windows.Count == 0)
            return new Pace(PaceMode.Unpaced, max, 0, recent, null, null, null,
                "Nothing to pace against yet: set a budget, or one is learned the first time you hit a limit.");

        if (windows.FirstOrDefault(w => w.Used >= w.Limit) is { Name: not null } spent)
            return new Pace(PaceMode.Over, 0, 0, recent, null, spent.Name, spent.ResetUtc,
                $"Past the {spent.Name} limit until {Clock(spent.ResetUtc)}.",
                $"Past the {spent.Name} limit until {Clock(spent.ResetUtc)}. Finish the current step and stop at a clean point.");

        var target = Math.Clamp(s.PaceTargetPercent, 1, 100) / 100.0;
        var (gov, sustainable) = windows
            .Select(w => (w, Rate: Math.Max(0, w.Limit * target - w.Used) / Math.Max((w.ResetUtc - nowUtc).TotalHours, 1.0 / 60)))
            .MinBy(x => x.Rate);

        // What one more session costs: today's sessions if any are running, else your usual session.
        double? perSession = activeSessions > 0 && recent > 0 ? recent / activeSessions : typicalPerSession;
        var cap = sustainable <= 0 ? 0
            : perSession is > 0 and var p ? (int)Math.Clamp(Math.Floor(sustainable / p), 0, max)
            : Math.Min(1, max); // no history at all: start one and measure it

        var mode = cap == 0 ? PaceMode.Coast : cap < max ? PaceMode.Throttled : PaceMode.Open;
        var rates = $"{Formatting.Tokens((long)recent)}/h now, {Formatting.Tokens((long)sustainable)}/h fits the {gov.Name} window until {Clock(gov.ResetUtc)}";
        var note = mode switch
        {
            PaceMode.Open => $"On pace: {rates}.",
            PaceMode.Throttled => $"Pacing: {rates}; capped at {cap} of {max} sessions.",
            _ => sustainable <= 0
                ? $"Coasting: the {gov.Name} window is at its {s.PaceTargetPercent}% target until {Clock(gov.ResetUtc)}; no new sessions."
                : $"Coasting: {rates}; not even one more session fits.",
        };
        var agentNote = $"Usage is outrunning the {gov.Name} limit (resets {Clock(gov.ResetUtc)}). Work in smaller steps, " +
                        "don't spawn subagents or parallel agents, and skip optional verification passes.";
        var room = windows.Min(w => Math.Max(0, w.Limit * target - w.Used));
        return new Pace(mode, cap, sustainable, recent, perSession, gov.Name, gov.ResetUtc, note, agentNote) { RoomTokens = room };
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

    private static string Clock(DateTime utc) => utc.ToLocalTime().ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
}
