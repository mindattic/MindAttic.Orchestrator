using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Health;

/// <summary>How a health check reads, worst last (the Kubernetes/ASP.NET Core health-check grades).</summary>
public enum HealthGrade
{
    /// <summary>Within its lines.</summary>
    Healthy,
    /// <summary>Past its degraded line: worth a look, nothing is stopped by it.</summary>
    Degraded,
    /// <summary>Past its unhealthy line: what an alert fires on.</summary>
    Unhealthy,
}

/// <summary>The checks a health probe grades against <see cref="HealthThresholds"/> (M.ORCH-T2035).</summary>
public enum HealthCheck
{
    /// <summary>Free physical RAM (GB); lower is worse.</summary>
    RamFreeGb,
    /// <summary>Commit charge (percent of the commit limit); higher is worse.</summary>
    CommitPercent,
    /// <summary>Free space on one drive (GB); lower is worse.</summary>
    DiskFreeGb,
    /// <summary>An Azure SQL database's DTU (or vCore CPU) percent; higher is worse.</summary>
    DtuPercent,
    /// <summary>An Azure SQL database's storage percent of its max size; higher is worse.</summary>
    StoragePercent,
    /// <summary>A link's round trip in milliseconds; higher is worse.</summary>
    LinkLatencyMs,
}

/// <summary>One check's two lines: past <paramref name="Degraded"/> it reads degraded, past <paramref name="Unhealthy"/>
/// unhealthy. <paramref name="LowerIsWorse"/> is true for free-space checks (RAM, disk), where "past" means under.</summary>
public sealed record HealthLine(double Degraded, double Unhealthy, bool LowerIsWorse)
{
    /// <summary>The grade of <paramref name="value"/>: unhealthy at or past the unhealthy line, degraded at or past the
    /// degraded one (strictly under, for a lower-is-worse line), healthy otherwise. A line out of order (unhealthy before
    /// degraded) still grades the worse of the two first, so a misordered setting never hides an unhealthy reading.
    /// NaN (no reading) is healthy: a probe that couldn't read reports that itself.</summary>
    public HealthGrade Grade(double value)
    {
        if (double.IsNaN(value)) return HealthGrade.Healthy;
        bool Past(double line) => LowerIsWorse ? value < line : value >= line;
        if (Past(Unhealthy)) return HealthGrade.Unhealthy;
        return Past(Degraded) ? HealthGrade.Degraded : HealthGrade.Healthy;
    }
}

/// <summary>
/// The lines every health probe grades its readings against (M.ORCH-T2035, part of the health dashboard M.ORCH-T1935): free
/// RAM, commit charge, each disk's free space, each Azure SQL database's DTU and storage percent, and each link's latency. Each
/// line is a setting (<see cref="OrchestratorSettings"/>, shown in Vault.Desktop's Orchestrator tab under Health and set with
/// <c>orchestrator settings --health-*</c>); probes call <see cref="Of"/> on the settings they just read, so a change applies
/// on their next run.
/// </summary>
public sealed record HealthThresholds(HealthLine RamFreeGb, HealthLine CommitPercent, HealthLine DiskFreeGb, HealthLine DtuPercent,
    HealthLine StoragePercent, HealthLine LinkLatencyMs)
{
    /// <summary>The lines as <paramref name="s"/> sets them.</summary>
    public static HealthThresholds Of(OrchestratorSettings s) => new(
        new(s.HealthRamDegradedGb, s.HealthRamUnhealthyGb, LowerIsWorse: true),
        new(s.HealthCommitDegradedPercent, s.HealthCommitUnhealthyPercent, LowerIsWorse: false),
        new(s.HealthDiskFreeDegradedGb, s.HealthDiskFreeUnhealthyGb, LowerIsWorse: true),
        new(s.HealthDtuDegradedPercent, s.HealthDtuUnhealthyPercent, LowerIsWorse: false),
        new(s.HealthStorageDegradedPercent, s.HealthStorageUnhealthyPercent, LowerIsWorse: false),
        new(s.HealthLinkLatencyDegradedMs, s.HealthLinkLatencyUnhealthyMs, LowerIsWorse: false));

    /// <summary>The lines to grade on (M.ORCH-T2057): <paramref name="s"/>'s, each moved to what the daemon has learned from the
    /// probes' history (<see cref="HealthLearning"/>) when learning is on, held between the configured line and its bound.</summary>
    public static HealthThresholds Of(OrchestratorSettings s, LearnedHealthLines? learned)
    {
        var configured = Of(s);
        if (!s.HealthLearn || learned is null || learned.Lines.Count == 0) return configured;
        HealthLine L(HealthCheck c) => HealthLearning.Effective(s, c, configured[c], learned);
        return new(L(HealthCheck.RamFreeGb), L(HealthCheck.CommitPercent), L(HealthCheck.DiskFreeGb), L(HealthCheck.DtuPercent),
            L(HealthCheck.StoragePercent), L(HealthCheck.LinkLatencyMs));
    }

    /// <summary>The lines as <paramref name="store"/>'s settings set them and as its daemon has learned them; the configured
    /// lines alone when the learned ones can't be read.</summary>
    public static HealthThresholds Of(OrchestratorSettings s, JsonStore store) =>
        Of(s, store.TryLoad<LearnedHealthLines>(HealthLearning.StoreName, out var learned) ? learned : null);

    /// <summary>What's wrong with <paramref name="s"/>'s health lines, for the CLI to refuse, or null when they're in order:
    /// none negative, percents at most 100, latencies over 0, and each check's unhealthy line no better than its degraded one.</summary>
    public static string? Problem(OrchestratorSettings s)
    {
        if (!(s.HealthRamUnhealthyGb >= 0 && s.HealthRamUnhealthyGb <= s.HealthRamDegradedGb))
            return "The RAM lines go 0 <= --health-ram-unhealthy-gb <= --health-ram-degraded-gb.";
        if (!(s.HealthDiskFreeUnhealthyGb >= 0 && s.HealthDiskFreeUnhealthyGb <= s.HealthDiskFreeDegradedGb))
            return "The disk lines go 0 <= --health-disk-unhealthy-gb <= --health-disk-degraded-gb.";
        if (!(s.HealthCommitDegradedPercent > 0 && s.HealthCommitDegradedPercent <= s.HealthCommitUnhealthyPercent && s.HealthCommitUnhealthyPercent <= 100))
            return "The commit lines go 0 < --health-commit-degraded-percent <= --health-commit-unhealthy-percent <= 100.";
        if (!(s.HealthDtuDegradedPercent > 0 && s.HealthDtuDegradedPercent <= s.HealthDtuUnhealthyPercent && s.HealthDtuUnhealthyPercent <= 100))
            return "The DTU lines go 0 < --health-dtu-degraded-percent <= --health-dtu-unhealthy-percent <= 100.";
        if (!(s.HealthStorageDegradedPercent > 0 && s.HealthStorageDegradedPercent <= s.HealthStorageUnhealthyPercent && s.HealthStorageUnhealthyPercent <= 100))
            return "The storage lines go 0 < --health-storage-degraded-percent <= --health-storage-unhealthy-percent <= 100.";
        if (!(s.HealthLinkLatencyDegradedMs > 0 && s.HealthLinkLatencyDegradedMs <= s.HealthLinkLatencyUnhealthyMs))
            return "The link lines go 0 < --health-link-degraded-ms <= --health-link-unhealthy-ms.";
        if (!(s.HealthRamLearnFloorGb >= 0 && s.HealthDiskFreeLearnFloorGb >= 0))
            return "--health-ram-learn-floor-gb and --health-disk-learn-floor-gb can't be negative.";
        if (!(s.HealthCommitLearnCeilingPercent <= 100 && s.HealthDtuLearnCeilingPercent <= 100 && s.HealthStorageLearnCeilingPercent <= 100))
            return "--health-commit-learn-ceiling-percent, --health-dtu-learn-ceiling-percent and --health-storage-learn-ceiling-percent are at most 100.";
        if (!(s.HealthLinkLatencyLearnCeilingMs > 0))
            return "--health-link-learn-ceiling-ms is over 0.";
        if (!(s.HealthLearnMinSamples >= 1 && s.HealthLearnMarginPercent >= 0 && s.HealthLearnMarginPercent <= 100))
            return "--health-learn-min-samples is at least 1 and --health-learn-margin-percent 0 to 100.";
        if (!new[] { s.HealthProbeMachineSeconds, s.HealthProbeLinksSeconds, s.HealthProbeAzureSeconds }.All(v => v >= 0 && v <= 86_400))
            return "--health-probe-machine-seconds, --health-probe-links-seconds and --health-probe-azure-seconds are 0 (off) to 86400.";
        if (!(s.HealthFailureThreshold is >= 1 and <= 100 && s.HealthSuccessThreshold is >= 1 and <= 100))
            return "--health-failure-threshold and --health-success-threshold are 1 to 100.";
        return null;
    }

    /// <summary>The defaults, for a probe run with no settings readable.</summary>
    public static HealthThresholds Default { get; } = Of(new OrchestratorSettings());

    /// <summary>The line for <paramref name="check"/>.</summary>
    public HealthLine this[HealthCheck check] => check switch
    {
        HealthCheck.RamFreeGb => RamFreeGb,
        HealthCheck.CommitPercent => CommitPercent,
        HealthCheck.DiskFreeGb => DiskFreeGb,
        HealthCheck.DtuPercent => DtuPercent,
        HealthCheck.StoragePercent => StoragePercent,
        HealthCheck.LinkLatencyMs => LinkLatencyMs,
        _ => throw new ArgumentOutOfRangeException(nameof(check), check, null),
    };

    /// <summary>The grade of <paramref name="value"/> on <paramref name="check"/>.</summary>
    public HealthGrade Grade(HealthCheck check, double value) => this[check].Grade(value);
}
