using System.Globalization;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// A milestone's forecast (M.ORCH-T1688, roadmap Phase 9): what is left on it, when it should finish and what it should cost,
/// worked out from its open tickets' estimates against measured throughput. Pure: everything it reads is handed in.
/// <list type="bullet">
/// <item>The date is the remaining tickets over the tickets closed per day in the measuring window
/// (<see cref="WindowDays"/>). With nothing closed in the window the date is unknown, never a guess.</item>
/// <item>The cost is the remaining tokens: each open ticket's estimate, and the measured average tokens per completed ticket
/// for one with no estimate. With no measured average either, those tickets are left out of the sum and counted.</item>
/// <item>At risk when the date is later than the milestone's own first forecast by more than <see cref="SlipMargin"/> of the
/// original span (activation to the first projected date; never less than <see cref="MinSlip"/>), or when more tickets have
/// joined it since activation than were closed on it.</item>
/// </list>
/// </summary>
public static class MilestoneForecast
{
    /// <summary>The days of closings throughput is measured over, today included.</summary>
    public const int WindowDays = 14;

    /// <summary>How much later than the first forecast a projection may be, as a share of the original span, before the
    /// milestone is at risk.</summary>
    public const double SlipMargin = 0.2;

    /// <summary>The least slip that counts, so a short span doesn't flag a milestone over a few hours.</summary>
    public static readonly TimeSpan MinSlip = TimeSpan.FromDays(1);

    /// <summary>An open ticket on the milestone and its estimate in tokens; null (or zero) when it has none.</summary>
    public sealed record OpenTicket(string Id, long? Estimate);

    /// <summary>What was measured: tickets <paramref name="Closed"/> over <paramref name="Days"/>, and the average tokens a
    /// completed ticket cost (null when no completed ticket has a measured cost).</summary>
    public sealed record Throughput(int Closed, double Days, long? AverageTokens)
    {
        /// <summary>Tickets closed per day; null when none closed in the window.</summary>
        public double? TicketsPerDay => Closed > 0 && Days > 0 ? Closed / Days : null;
    }

    /// <summary>The milestone since activation: tickets that joined it, and tickets closed on it.</summary>
    public sealed record Growth(int Added, int Closed);

    /// <summary>The milestone's own first forecast: the date it projected, and when that was worked out.</summary>
    public sealed record Baseline(DateTime FirstProjectedUtc, DateTime FirstAtUtc);

    /// <summary>The forecast. <see cref="ProjectedUtc"/> is null when the date is unknown (no throughput) or nothing is left.</summary>
    public sealed record Result(int RemainingTickets, long RemainingTokens, int FromAverage, int Unvalued, double? TicketsPerDay,
        DateTime? ProjectedUtc, string Basis, bool AtRisk, IReadOnlyList<string> Reasons)
    {
        /// <summary>Whether every ticket on it is closed: what's left is its acceptance.</summary>
        public bool Complete => RemainingTickets == 0;

        /// <summary>Why it's at risk, in one line; empty when it isn't.</summary>
        public string Reason => string.Join(' ', Reasons);
    }

    /// <summary>The forecast of a milestone with <paramref name="open"/> tickets left, now.</summary>
    public static Result Compute(IReadOnlyList<OpenTicket> open, Throughput measured, DateTime nowUtc, DateTime? activatedUtc = null,
        Baseline? baseline = null, Growth? growth = null)
    {
        var missing = open.Count(t => t.Estimate is not > 0);
        var average = measured.AverageTokens is > 0 ? measured.AverageTokens : null;
        var tokens = open.Sum(t => t.Estimate is > 0 ? t.Estimate.Value : average ?? 0);
        var rate = measured.TicketsPerDay;
        DateTime? projected = open.Count > 0 && rate is { } perDay ? nowUtc.AddDays(open.Count / perDay) : null;
        var days = measured.Days.ToString("0.#", CultureInfo.InvariantCulture);
        var basis = open.Count == 0 ? "Every ticket on it is closed."
            : rate is { } r ? $"{open.Count} open tickets at {r.ToString("0.##", CultureInfo.InvariantCulture)} tickets closed a day ({measured.Closed} closed in its repos in the last {days} days)."
            : $"No ticket was closed in its repos in the last {days} days, so the date is unknown.";

        var reasons = new List<string>();
        if (projected is { } latest && baseline is not null)
        {
            var span = baseline.FirstProjectedUtc - (activatedUtc ?? baseline.FirstAtUtc);
            var allowed = TimeSpan.FromTicks(Math.Max(MinSlip.Ticks, (long)(span.Ticks * SlipMargin)));
            var slip = latest - baseline.FirstProjectedUtc;
            if (slip > allowed)
                reasons.Add($"The projected date is {Days(slip)} later than its first forecast, past the {Days(allowed)} allowed.");
        }
        if (open.Count > 0 && growth is { Added: > 0 } g && g.Added > g.Closed)
            reasons.Add($"{g.Added} tickets have joined it since activation and {g.Closed} were closed.");

        return new Result(open.Count, tokens, average is null ? 0 : missing, average is null ? missing : 0, rate, projected, basis,
            reasons.Count > 0, reasons);
    }

    private static string Days(TimeSpan span) => span.TotalDays.ToString("0.#", CultureInfo.InvariantCulture) + " days";
}
