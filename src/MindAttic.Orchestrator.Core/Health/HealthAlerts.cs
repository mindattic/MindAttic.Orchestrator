using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Health;

/// <summary>A check instance that went unhealthy (or came back from it): what to tell the owner (alert and Psst text).</summary>
public sealed record HealthAlert(string Probe, string CheckId, string? ComponentId, bool Unhealthy, string Text)
{
    /// <summary>The check instance it is about, as one string (<c>probe/check[/component]</c>).</summary>
    public string Instance => HealthAlerts.InstanceKey(Probe, CheckId, ComponentId);
}

/// <summary>What the database keeps (<see cref="HealthAlerts.StateName"/>): the check instances last alerted unhealthy and not yet
/// recovered, so a daemon restart neither repeats the alert nor loses the recovery.</summary>
public sealed class HealthAlertBook
{
    /// <summary>Gets or sets the instances alerted unhealthy (<see cref="HealthAlerts.InstanceKey"/>) and when.</summary>
    public Dictionary<string, DateTimeOffset> Alerted { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Change-only health alerts (M.ORCH-T2039, docs/HEALTH.md section 5): a check instance alerts unhealthy only after
/// <see cref="OrchestratorSettings.HealthFailureThreshold"/> unhealthy results in a row, and alerts its recovery after
/// <see cref="OrchestratorSettings.HealthSuccessThreshold"/> non-unhealthy ones in a row (the hysteresis that keeps a check
/// hovering at its line from flapping). One alert per transition, never one per run; degraded never alerts. A result no newer
/// than the last one taken for its instance (a report seen twice) doesn't count. The alerted set is kept in the database.
/// </summary>
public sealed class HealthAlerts(JsonStore? store)
{
    /// <summary>The state name constant.</summary>
    public const string StateName = "health-alerts";

    private sealed class Run
    {
        public int Unhealthy;
        public int Fine;
        public DateTimeOffset Last = DateTimeOffset.MinValue;
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Run> runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HealthAlertBook book = store is null ? new() : Safe(store);

    private static HealthAlertBook Safe(JsonStore s)
    {
        try { return s.Load<HealthAlertBook>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException) { return new(); }
    }

    /// <summary>One check instance as a key: <c>probe/check</c>, with <c>/component</c> when it has one.</summary>
    public static string InstanceKey(string probe, string checkId, string? componentId) =>
        $"{probe.Trim().ToLowerInvariant()}/{checkId.Trim()}" + (string.IsNullOrWhiteSpace(componentId) ? "" : "/" + componentId.Trim());

    /// <summary>Whether <paramref name="instance"/> is alerted unhealthy and not yet recovered.</summary>
    public bool IsAlerted(string instance)
    {
        lock (gate) return book.Alerted.ContainsKey(instance);
    }

    /// <summary>Takes every result of <paramref name="report"/>; returns the transitions it made (unhealthy, or recovered).</summary>
    public IReadOnlyList<HealthAlert> Take(HealthReport report, int failureThreshold, int successThreshold)
    {
        var fail = Math.Max(1, failureThreshold);
        var succeed = Math.Max(1, successThreshold);
        var found = new List<HealthAlert>();
        lock (gate)
        {
            foreach (var r in report.Results)
            {
                var key = InstanceKey(report.Probe, r.CheckId, r.ComponentId);
                if (!runs.TryGetValue(key, out var run)) runs[key] = run = new Run();
                if (r.Time <= run.Last) continue;
                run.Last = r.Time;
                var alerted = book.Alerted.ContainsKey(key);
                if (r.Grade == HealthGrade.Unhealthy)
                {
                    run.Fine = 0;
                    run.Unhealthy++;
                    if (alerted || run.Unhealthy < fail) continue;
                    book.Alerted[key] = r.Time;
                    Save(key, r.Time);
                    found.Add(new(report.Probe, r.CheckId, r.ComponentId, true, UnhealthyText(report, r, run.Unhealthy)));
                }
                else
                {
                    run.Unhealthy = 0;
                    run.Fine++;
                    if (!alerted || run.Fine < succeed) continue;
                    book.Alerted.Remove(key);
                    Save(key, null);
                    found.Add(new(report.Probe, r.CheckId, r.ComponentId, false, RecoveredText(report, r)));
                }
            }
        }
        return found;
    }

    private void Save(string key, DateTimeOffset? alerted)
    {
        if (store is null) return;
        try
        {
            store.Update<HealthAlertBook>(StateName, b =>
            {
                if (alerted is { } at) b.Alerted[key] = at;
                else b.Alerted.Remove(key);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException) { }
    }

    private static string What(HealthReport report, HealthResult r) =>
        r.CheckId + (string.IsNullOrWhiteSpace(r.ComponentId) ? "" : $" ({r.ComponentId})") + $" on {report.Host}";

    private static string Reading(HealthResult r) =>
        r.ObservedValue is { } v ? Num(v) + (string.IsNullOrEmpty(r.ObservedUnit) ? "" : " " + r.ObservedUnit)
        : r.ObservedState ?? "no reading";

    /// <summary>The unhealthy text: the check, its reading, the line it was graded against and the probe's output.</summary>
    internal static string UnhealthyText(HealthReport report, HealthResult r, int inARow)
    {
        var line = r.UnhealthyAt is { } u ? $", unhealthy line {Num(u)}{(string.IsNullOrEmpty(r.ObservedUnit) ? "" : " " + r.ObservedUnit)}" : "";
        var output = string.IsNullOrWhiteSpace(r.Output) ? "" : $": {r.Output.Trim()}";
        return $"Health: {What(report, r)} is unhealthy ({Reading(r)}{line}; {inARow} in a row){output}.";
    }

    /// <summary>The one-line recovery text.</summary>
    internal static string RecoveredText(HealthReport report, HealthResult r) =>
        $"Health: {What(report, r)} recovered ({HealthGrades.ToWire(r.Grade)}, {Reading(r)}).";

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
