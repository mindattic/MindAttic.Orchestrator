using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One check instance on the health dashboard (M.ORCH-T2033, docs/HEALTH.md): a probe's check on one component, its latest
/// result and the results heard since this window connected, oldest first, at most <see cref="AppBusHub.HealthHistoryLength"/>
/// (the hub's own ring, so the heartbeat strip and the per-login self-correction, M.ORCH-T2057, read the same window). It
/// goes grey "stale" once its probe's report is older than three of the probe's periods (section 4).
/// </summary>
public sealed class HealthCheckRow(string probe, string checkId, string? componentId) : ObservableObject
{
    /// <summary>How many of its probe's periods a check may go unheard before it reads stale (docs/HEALTH.md section 4).</summary>
    public const int StalePeriods = 3;

    /// <summary>The period assumed for a probe that sends none (the 30 s default sample).</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);

    private readonly List<HealthResult> history = [];
    private HealthResult? latest;
    private DateTimeOffset reportedAt;
    private TimeSpan interval = DefaultInterval;
    private bool stale;
    private bool? learned;

    /// <summary>The probe that reports it (<c>machine</c>, <c>azure-sql</c>, <c>links</c>...).</summary>
    public string Probe { get; } = probe;
    /// <summary>The health+json check id, <c>"&lt;component&gt;:&lt;measurement&gt;"</c> (<c>disk:free</c>).</summary>
    public string CheckId { get; } = checkId;
    /// <summary>The instance (<c>C:</c>, <c>server/database</c>), or null when the check has only one.</summary>
    public string? ComponentId { get; } = componentId;

    /// <summary>What the row is called: the check id, with its instance when it has one ("disk:free C:").</summary>
    public string Name => ComponentId is { Length: > 0 } c ? $"{CheckId} {c}" : CheckId;

    /// <summary>The newest result; null before the first.</summary>
    public HealthResult? Latest => latest;

    /// <summary>The results heard, oldest first, the last <see cref="Latest"/>.</summary>
    public IReadOnlyList<HealthResult> History => history;

    /// <summary>The latest result's grade, whatever its age.</summary>
    public HealthGrade Grade => latest?.Grade ?? HealthGrade.Degraded;

    /// <summary>Whether its probe hasn't reported it for <see cref="StalePeriods"/> periods: shown grey, never alerting.</summary>
    public bool IsStale { get => stale; private set { if (Set(ref stale, value)) RaiseStatus(); } }

    /// <summary>When it goes stale unless a newer report comes.</summary>
    public DateTimeOffset StaleAt => reportedAt + TimeSpan.FromTicks(interval.Ticks * StalePeriods);

    /// <summary>"healthy", "degraded", "unhealthy" or "stale".</summary>
    public string StatusText => IsStale ? "stale" : Grade switch
    {
        HealthGrade.Healthy => "healthy",
        HealthGrade.Degraded => "degraded",
        _ => "unhealthy",
    };

    /// <summary>The status colour: green, amber, red, or grey when stale.</summary>
    public Brush StatusBrush => IsStale ? Palette.Quiet : BrushOf(Grade);

    /// <summary>The latest reading: "16.45 GB", a state word ("Paused"), or "no reading".</summary>
    public string ValueText => latest switch
    {
        null => "",
        { ObservedState: { } s } => s,
        { ObservedValue: { } v } => $"{v.ToString("0.##", CultureInfo.InvariantCulture)}{(latest.ObservedUnit is { Length: > 0 } u ? (u == "%" ? u : " " + u) : "")}",
        _ => "no reading",
    };

    /// <summary>The latest result's own line (why it reads as it does, or why there's no reading); empty when none.</summary>
    public string Output => latest?.Output ?? "";

    /// <summary>When the latest result was taken, local time ("14:02:31"); empty before the first.</summary>
    public string TimeText => latest is null ? "" : latest.Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>The line the latest reading was graded against (M.ORCH-T2034): "degraded under 10 GB, unhealthy under 5 GB",
    /// "degraded at 80%, unhealthy at 95%", or empty when the result carries none (a state check).</summary>
    public string ThresholdText
    {
        get
        {
            if (latest is not { DegradedAt: { } d, UnhealthyAt: { } u }) return "";
            var past = d > u ? "under" : "at";
            return $"degraded {past} {Amount(d)}, unhealthy {past} {Amount(u)}";
        }
    }

    /// <summary>Whether the threshold is one the daemon learned (M.ORCH-T2057) rather than the configured one; null when unknown.</summary>
    public bool? IsLearned => learned;

    /// <summary>"learned" or "configured" for the threshold's source, empty when unknown or there's no threshold.</summary>
    public string LineSourceText => ThresholdText.Length == 0 || learned is not { } l ? "" : l ? "learned" : "configured";

    /// <summary>The sparkline's width and height in device-independent pixels.</summary>
    public const double SparkWidth = 120, SparkHeight = 18;

    /// <summary>The numeric readings in <see cref="History"/> as a polyline in a <see cref="SparkWidth"/> x
    /// <see cref="SparkHeight"/> box, oldest on the left, higher values up; empty with fewer than two readings.</summary>
    public PointCollection SparkPoints
    {
        get
        {
            var values = history.Where(r => r.ObservedValue is { } v && double.IsFinite(v)).Select(r => r.ObservedValue!.Value).ToList();
            var points = new PointCollection();
            if (values.Count < 2) { points.Freeze(); return points; }
            double min = values.Min(), max = values.Max(), span = max - min;
            for (var i = 0; i < values.Count; i++)
            {
                var x = SparkWidth * i / (values.Count - 1);
                var y = span <= 0 ? SparkHeight / 2 : SparkHeight - 1 - (SparkHeight - 2) * (values[i] - min) / span;
                points.Add(new System.Windows.Point(x, y));
            }
            points.Freeze();
            return points;
        }
    }

    /// <summary><see cref="SparkPoints"/> as a geometry the card's Path draws; empty with fewer than two readings.</summary>
    public Geometry SparkLine
    {
        get
        {
            var points = SparkPoints;
            if (points.Count < 2) return Geometry.Empty;
            var line = new PathGeometry([new PathFigure(points[0], [new PolyLineSegment(points.Skip(1), true)], false)]);
            line.Freeze();
            return line;
        }
    }

    private string Amount(double v) =>
        $"{v.ToString("0.##", CultureInfo.InvariantCulture)}{(latest?.ObservedUnit is { Length: > 0 } u ? (u == "%" ? u : " " + u) : "")}";

    /// <summary>The history's grades as colours, oldest first: the heartbeat strip (docs/HEALTH.md, Uptime Kuma's bar).</summary>
    public IReadOnlyList<Brush> HistoryBrushes => [.. history.Select(r => BrushOf(r.Grade))];

    /// <summary>A health grade's colour: healthy the working colour, degraded the waiting one, anything else the over
    /// one.</summary>
    public static Brush BrushOf(HealthGrade grade) => grade switch
    {
        HealthGrade.Healthy => Palette.Working,
        HealthGrade.Degraded => Palette.Waiting,
        _ => Palette.Over,
    };

    /// <summary>Takes one result of a report taken at <paramref name="reportTime"/> with period <paramref name="period"/>; one
    /// older than the newest held is dropped (the hub relays out-of-order reports). True when it was taken.</summary>
    internal bool Take(HealthResult result, DateTimeOffset reportTime, TimeSpan period, DateTimeOffset now, bool? learnedLine = null)
    {
        if (latest is not null && reportTime < reportedAt) return false;
        history.Add(result);
        if (history.Count > AppBusHub.HealthHistoryLength) history.RemoveRange(0, history.Count - AppBusHub.HealthHistoryLength);
        latest = result;
        learned = learnedLine;
        reportedAt = reportTime == default ? now : reportTime;
        interval = period > TimeSpan.Zero ? period : DefaultInterval;
        stale = now >= StaleAt;
        RaiseStatus();
        Raise(nameof(Latest)); Raise(nameof(History)); Raise(nameof(HistoryBrushes)); Raise(nameof(ValueText));
        Raise(nameof(Output)); Raise(nameof(TimeText)); Raise(nameof(Grade)); Raise(nameof(IsStale));
        Raise(nameof(ThresholdText)); Raise(nameof(IsLearned)); Raise(nameof(LineSourceText)); Raise(nameof(SparkPoints)); Raise(nameof(SparkLine));
        return true;
    }

    /// <summary>Marks it stale once <paramref name="now"/> is past <see cref="StaleAt"/>.</summary>
    internal void Age(DateTimeOffset now) => IsStale = latest is not null && now >= StaleAt;

    private void RaiseStatus()
    {
        Raise(nameof(StatusText));
        Raise(nameof(StatusBrush));
    }
}

/// <summary>
/// The health dashboard's rows (M.ORCH-T2033): each <see cref="AppBus.Health"/> envelope on the status stream (a probe run's
/// health+json report, docs/HEALTH.md section 3) is read into one <see cref="HealthCheckRow"/> per check instance, with its
/// status and recent history. Kept on the root tab only; a one-shot timer greys a row the moment it goes stale (not a poll).
/// </summary>
public sealed partial class MainViewModel
{
    private readonly ObservableCollection<HealthCheckRow> healthChecks = [];
    private readonly Dictionary<(string Probe, string CheckId, string ComponentId), HealthCheckRow> healthRows = [];
    private System.Windows.Threading.DispatcherTimer? healthStaleTimer;

    /// <summary>Every check instance heard on the bus, ordered by probe, check and instance.</summary>
    public ObservableCollection<HealthCheckRow> HealthChecks => Root.healthChecks;

    /// <summary>The worst status across the fresh rows (stale ones don't count, as they don't alert); healthy when none.</summary>
    public HealthGrade HealthStatus => Root.healthStatus;

    // Worked out once when the rows change (RaiseHealthStatus, M.ORCH-T2211), not by every binding that reads it.
    private HealthGrade healthStatus = HealthGrade.Healthy;

    private static HealthGrade WorstFresh(IReadOnlyList<HealthCheckRow> rows)
    {
        var worst = HealthGrade.Healthy;
        for (var i = 0; i < rows.Count; i++)
            if (!rows[i].IsStale && rows[i].Grade > worst) worst = rows[i].Grade;
        return worst;
    }

    /// <summary>The overall status as a word for the Health view (M.ORCH-T2034): "healthy", "degraded" or "unhealthy", or
    /// "no reports yet" before any probe has been heard.</summary>
    public string HealthStatusText => Root.healthChecks.Count == 0 ? "no reports yet" : HealthStatus switch
    {
        HealthGrade.Healthy => "healthy",
        HealthGrade.Degraded => "degraded",
        _ => "unhealthy",
    };

    /// <summary>The overall status's colour; grey before any report.</summary>
    public Brush HealthStatusBrush => Root.healthChecks.Count == 0 ? Palette.Quiet : HealthCheckRow.BrushOf(HealthStatus);

    /// <summary>Whether the Health view shows (the Misc area of the global tab).</summary>
    public bool ShowsHealth => IsGlobalTab;

    /// <summary>Whether the Health view says no probe has reported yet.</summary>
    public bool ShowsHealthEmpty => IsGlobalTab && healthChecks.Count == 0;

    /// <summary>Whether <paramref name="result"/>'s line is a learned one (M.ORCH-T2057): it differs from the configured line for
    /// its check while learning is on. Null when its check isn't a graded one or the settings can't be read. Never throws.</summary>
    private bool? IsLearnedLine(HealthResult result)
    {
        try
        {
            if (result.DegradedAt is not { } d || result.UnhealthyAt is not { } u) return null;
            if (!(checksById ??= ChecksById()).TryGetValue(result.CheckId.Trim(), out var found)) return null;
            var s = shownSettings;
            if (s is null) return null;
            if (!s.HealthLearn) return false;
            var line = HealthThresholds.Of(s)[found];
            return Math.Abs(line.Degraded - d) > 1e-9 || Math.Abs(line.Unhealthy - u) > 1e-9;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    // Each graded check by its check id, built once on first use (M.ORCH-T2211); the first check with an id wins, as the scan did.
    private static Dictionary<string, HealthCheck>? checksById;

    private static Dictionary<string, HealthCheck> ChecksById()
    {
        var byId = new Dictionary<string, HealthCheck>(StringComparer.Ordinal);
        foreach (var c in Enum.GetValues<HealthCheck>()) byId.TryAdd(HealthResult.Describe(c).CheckId, c);
        return byId;
    }

    /// <summary>Reads one <see cref="AppBus.Health"/> envelope into the rows. One this build can't read is skipped. Never throws.</summary>
    internal void OnHealthEnvelope(StatusEnvelope envelope)
    {
        try
        {
            if (envelope.V > AppBus.HealthVersion || envelope.Payload.ValueKind != System.Text.Json.JsonValueKind.Object) return;
            if (HealthReport.FromJson(envelope.Payload.GetRawText()) is { Probe.Length: > 0 } report) OnHealthReport(report);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Health").Warning(ex, "Reading a health report off the bus failed; skipped");
        }
    }

    /// <summary>Adds each of <paramref name="report"/>'s results to its check instance's row, creating the row in order the
    /// first time it's heard.</summary>
    internal void OnHealthReport(HealthReport report)
    {
        if (!IsGlobalTab) { Root.OnHealthReport(report); return; }
        var now = DateTimeOffset.UtcNow;
        var probe = report.Probe.Trim();
        var period = TimeSpan.FromSeconds(Math.Max(0, report.IntervalSeconds));
        foreach (var result in report.Results)
        {
            var key = (probe.ToLowerInvariant(), result.CheckId.Trim(), result.ComponentId?.Trim() ?? "");
            if (!healthRows.TryGetValue(key, out var row))
            {
                healthRows[key] = row = new HealthCheckRow(probe, result.CheckId.Trim(), result.ComponentId?.Trim() is { Length: > 0 } c ? c : null);
                var at = 0;
                while (at < healthChecks.Count && CompareRows(healthChecks[at], row) <= 0) at++;
                healthChecks.Insert(at, row);
            }
            row.Take(result, report.Time, period, now, IsLearnedLine(result));
        }
        RaiseHealthStatus();
        ArmHealthStale();
    }

    private void RaiseHealthStatus()
    {
        healthStatus = WorstFresh(healthChecks);
        Raise(nameof(HealthStatus)); Raise(nameof(HealthStatusText)); Raise(nameof(HealthStatusBrush)); Raise(nameof(ShowsHealthEmpty));
    }

    private static int CompareRows(HealthCheckRow a, HealthCheckRow b)
    {
        var c = string.Compare(a.Probe, b.Probe, StringComparison.OrdinalIgnoreCase);
        if (c == 0) c = string.Compare(a.CheckId, b.CheckId, StringComparison.Ordinal);
        return c != 0 ? c : string.Compare(a.ComponentId ?? "", b.ComponentId ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Arms the one-shot timer for the earliest moment a fresh row goes stale; none when every row already is.</summary>
    private void ArmHealthStale()
    {
        if (System.Windows.Application.Current?.Dispatcher is null) return;
        healthStaleTimer ??= CreateHealthStaleTimer();
        healthStaleTimer.Stop();
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? next = null;
        foreach (var row in healthChecks)
            if (!row.IsStale && (next is null || row.StaleAt < next)) next = row.StaleAt;
        if (next is not { } when) return;
        var wait = when - now + TimeSpan.FromMilliseconds(250);
        if (wait < TimeSpan.FromSeconds(1)) wait = TimeSpan.FromSeconds(1);
        if (wait > TimeSpan.FromDays(1)) wait = TimeSpan.FromDays(1);
        healthStaleTimer.Interval = wait;
        healthStaleTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer CreateHealthStaleTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var now = DateTimeOffset.UtcNow;
            foreach (var row in healthChecks) row.Age(now);
            RaiseHealthStatus();
            ArmHealthStale();
        };
        return timer;
    }
}
