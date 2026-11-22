using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Health;

/// <summary>One check's learned lines: what <see cref="HealthLearning"/> last moved them to, why, when, and from how many
/// readings.</summary>
public sealed record LearnedHealthLine(double Degraded, double Unhealthy, string Reason, DateTimeOffset At, int Basis);

/// <summary>One move of a check's lines, kept so every change can be read back with its reason (M.ORCH-T2057).</summary>
public sealed record HealthLineChange(DateTimeOffset At, string Check, double FromDegraded, double FromUnhealthy,
    double ToDegraded, double ToUnhealthy, string Reason);

/// <summary>
/// The health lines the daemon has learned from the probes' history (M.ORCH-T2057), kept in the store under
/// <see cref="HealthLearning.StoreName"/>: each check's current learned line by <see cref="HealthCheck"/> name, and the last
/// <see cref="HealthLearning.ChangesKept"/> moves with their reasons. A check with no entry grades on its configured line.
/// </summary>
public sealed class LearnedHealthLines
{
    /// <summary>Each check's learned line, by <see cref="HealthCheck"/> name.</summary>
    public Dictionary<string, LearnedHealthLine> Lines { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The moves made, oldest first.</summary>
    public List<HealthLineChange> Changes { get; set; } = [];

    /// <summary>The learned line for <paramref name="check"/>, or null.</summary>
    public LearnedHealthLine? Of(HealthCheck check) => Lines.TryGetValue(check.ToString(), out var l) ? l : null;
}

/// <summary>
/// Self-correction of the health lines (M.ORCH-T2057, the house rule that every number a user gives is a setting that learns
/// within the user's bounds; the pacer's <see cref="Budget.LimitCalibration"/> is the same idea for usage limits). The
/// configured line (<see cref="OrchestratorSettings"/> Health*) is the tightest a line goes; the bound
/// (<see cref="OrchestratorSettings"/> Health*Learn*) the loosest. When the machine's normal reading (the
/// <see cref="NormalPercentile"/>th worst of the history the daemon keeps, <see cref="Status.AppBusHub.HealthHistory"/>) sits
/// past the configured degraded line, that line only cries wolf, so it moves just past the normal reading
/// (<see cref="OrchestratorSettings.HealthLearnMarginPercent"/>) and the unhealthy line moves with it, both clamped to the
/// bound; once the normal reading is back inside the configured line, both go back to it. Each move is logged with its reason.
/// The history is the daemon's, which is the machine's, so the lines learned are the machine's, shared by every login on it.
/// </summary>
public static class HealthLearning
{
    /// <summary>The store name the learned lines are kept under.</summary>
    public const string StoreName = "health-lines";

    /// <summary>How many line moves are kept to read back.</summary>
    public const int ChangesKept = 200;

    /// <summary>Which reading counts as normal: the 90th worst percentile (the 10th for a lower-is-worse line), so an odd spike
    /// never moves a line, but a reading the machine sits at most of the time does.</summary>
    public const double NormalPercentile = 90;

    /// <summary>The loosest <paramref name="check"/>'s lines may be learned to, as <paramref name="s"/> sets it.</summary>
    public static double Bound(OrchestratorSettings s, HealthCheck check) => check switch
    {
        HealthCheck.RamFreeGb => s.HealthRamLearnFloorGb,
        HealthCheck.CommitPercent => s.HealthCommitLearnCeilingPercent,
        HealthCheck.DiskFreeGb => s.HealthDiskFreeLearnFloorGb,
        HealthCheck.DtuPercent => s.HealthDtuLearnCeilingPercent,
        HealthCheck.StoragePercent => s.HealthStorageLearnCeilingPercent,
        HealthCheck.LinkLatencyMs => s.HealthLinkLatencyLearnCeilingMs,
        _ => throw new ArgumentOutOfRangeException(nameof(check), check, null),
    };

    /// <summary><paramref name="learned"/> held between <paramref name="configured"/> (tightest) and <paramref name="bound"/>
    /// (loosest), the unhealthy line never better than the degraded one. A bound tighter than the configured line pins the
    /// line to the configured one: the user's own line always wins.</summary>
    public static HealthLine Clamp(HealthLine configured, double bound, double degraded, double unhealthy)
    {
        double Hold(double value, double tightest) => configured.LowerIsWorse
            ? Math.Min(tightest, Math.Max(value, Math.Min(bound, tightest)))
            : Math.Max(tightest, Math.Min(value, Math.Max(bound, tightest)));
        var d = Hold(degraded, configured.Degraded);
        var u = Hold(unhealthy, configured.Unhealthy);
        u = configured.LowerIsWorse ? Math.Min(u, d) : Math.Max(u, d);
        return configured with { Degraded = d, Unhealthy = u };
    }

    /// <summary>The line to grade <paramref name="check"/> on: the learned one held to the current settings' configured line
    /// and bound (so a setting changed since the daemon learned applies at once), or the configured one when learning is off
    /// or nothing is learned.</summary>
    public static HealthLine Effective(OrchestratorSettings s, HealthCheck check, HealthLine configured, LearnedHealthLines? learned) =>
        s.HealthLearn && learned?.Of(check) is { } l ? Clamp(configured, Bound(s, check), l.Degraded, l.Unhealthy) : configured;

    /// <summary>The <paramref name="percentile"/>th value of <paramref name="sorted"/> (ascending), nearest rank.</summary>
    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile / 100 * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>What <paramref name="readings"/> say <paramref name="check"/>'s lines should be, and why; null when there are
    /// fewer than <see cref="OrchestratorSettings.HealthLearnMinSamples"/> readings to go on (the line stays as it is).</summary>
    public static (HealthLine Line, string Reason)? Learn(OrchestratorSettings s, HealthCheck check, IEnumerable<double> readings)
    {
        var configured = HealthThresholds.Of(s)[check];
        var sorted = readings.Where(double.IsFinite).Order().ToList();
        if (sorted.Count == 0 || sorted.Count < Math.Max(1, s.HealthLearnMinSamples)) return null;
        var unit = HealthResult.Describe(check).Unit;
        var bound = Bound(s, check);
        var lower = configured.LowerIsWorse;
        var normal = Percentile(sorted, lower ? 100 - NormalPercentile : NormalPercentile);
        var pct = lower ? 100 - NormalPercentile : NormalPercentile;
        var past = lower ? normal < configured.Degraded : normal >= configured.Degraded;
        if (!past)
            return (configured, $"p{pct:0} of {sorted.Count} readings is {F(normal)} {unit}, inside the configured degraded line {F(configured.Degraded)} {unit}");
        var margin = Math.Max(0, s.HealthLearnMarginPercent) / 100;
        var degraded = lower ? normal * (1 - margin) : normal * (1 + margin);
        var shift = degraded - configured.Degraded;
        var line = Clamp(configured, bound, degraded, configured.Unhealthy + shift);
        var held = lower ? line.Degraded > degraded : line.Degraded < degraded;
        return (line, $"p{pct:0} of {sorted.Count} readings is {F(normal)} {unit}, past the configured degraded line {F(configured.Degraded)} {unit}; " +
            $"moved {F(s.HealthLearnMarginPercent)}% past it" + (held ? $", held at the bound {F(bound)} {unit}" : ""));
    }

    /// <summary>
    /// One learning pass: each check's lines relearned from <paramref name="readings"/> (every finite reading the history holds
    /// for that check, all of its instances), and each one that moved noted in <paramref name="book"/> and returned. With
    /// learning off every learned line is dropped (noted as a move back to the configured line). A check without enough readings
    /// keeps what it has.
    /// </summary>
    public static IReadOnlyList<HealthLineChange> Pass(LearnedHealthLines book, OrchestratorSettings s,
        Func<HealthCheck, IEnumerable<double>> readings, DateTimeOffset now)
    {
        var changes = new List<HealthLineChange>();
        foreach (var check in Enum.GetValues<HealthCheck>())
        {
            var configured = HealthThresholds.Of(s)[check];
            var had = book.Of(check);
            var from = had is null ? configured : Clamp(configured, Bound(s, check), had.Degraded, had.Unhealthy);
            HealthLine to;
            string reason;
            int basis;
            if (!s.HealthLearn)
            {
                if (had is null) continue;
                (to, reason, basis) = (configured, "learning is off (--health-learn false)", 0);
            }
            else if (Learn(s, check, readings(check)) is { } learned)
            {
                (to, reason) = learned;
                basis = readings(check).Count(double.IsFinite);
            }
            else continue;

            if (to == configured) book.Lines.Remove(check.ToString());
            else book.Lines[check.ToString()] = new LearnedHealthLine(to.Degraded, to.Unhealthy, reason, now, basis);
            if (Same(from.Degraded, to.Degraded) && Same(from.Unhealthy, to.Unhealthy)) continue;
            var change = new HealthLineChange(now, check.ToString(), from.Degraded, from.Unhealthy, to.Degraded, to.Unhealthy, reason);
            changes.Add(change);
            book.Changes.Add(change);
        }
        if (book.Changes.Count > ChangesKept) book.Changes.RemoveRange(0, book.Changes.Count - ChangesKept);
        return changes;
    }

    // A move under a hundredth of a unit (or a part in ten thousand) isn't worth a line in the log.
    private static bool Same(double a, double b) => Math.Abs(a - b) < Math.Max(0.01, Math.Abs(b) * 1e-4);

    /// <summary>The line read back for a human: "check: degraded/unhealthy unit" with what it was configured as when learned.</summary>
    public static string Describe(OrchestratorSettings s, HealthCheck check, LearnedHealthLines? learned)
    {
        var configured = HealthThresholds.Of(s)[check];
        var effective = Effective(s, check, configured, learned);
        var unit = HealthResult.Describe(check).Unit;
        var name = HealthResult.Describe(check).CheckId;
        return effective == configured
            ? $"{name} {F(configured.Degraded)}/{F(configured.Unhealthy)} {unit} (configured)"
            : $"{name} {F(effective.Degraded)}/{F(effective.Unhealthy)} {unit} learned (configured {F(configured.Degraded)}/{F(configured.Unhealthy)}, bound {F(Bound(s, check))})";
    }
}
