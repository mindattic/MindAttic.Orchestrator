using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Health;

/// <summary>
/// One element of a health report (M.ORCH-T2020, docs/HEALTH.md section 3/7): one check on one instance, graded. The check
/// id is the health+json <c>checks</c> key (<c>"&lt;component&gt;:&lt;measurement&gt;"</c>, e.g. <c>disk:free</c>),
/// <see cref="ComponentId"/> the instance (<c>C:</c>, <c>server/database</c>, <c>dashboard-&gt;daemon</c>) or null when there
/// is only ever one. The observed value is a number (<see cref="ObservedValue"/>) or, for an enumerated check such as
/// <c>sqldb:state</c>, a word (<see cref="ObservedState"/>); both null means the probe couldn't take a reading.
/// <see cref="DegradedAt"/>/<see cref="UnhealthyAt"/> are the line the value was graded against, copied in at grading time.
/// </summary>
public sealed record HealthResult(
    string CheckId,
    string? ComponentId,
    string ComponentType,
    double? ObservedValue,
    string? ObservedUnit,
    HealthGrade Grade,
    DateTimeOffset Time,
    double? DegradedAt = null,
    double? UnhealthyAt = null,
    string? Output = null,
    string? ObservedState = null)
{
    /// <summary>A result for <paramref name="value"/> on <paramref name="check"/>, graded against <paramref name="t"/>'s line
    /// for it, with that line stamped in. NaN (no reading) is reported degraded with a null value, never healthy: not being
    /// able to see a thing is worth a look (docs/HEALTH.md section 2); <paramref name="output"/> should say why.</summary>
    public static HealthResult Graded(HealthCheck check, HealthThresholds t, double value, DateTimeOffset time,
        string? componentId = null, string? output = null)
    {
        var line = t[check];
        var (id, type, unit) = Describe(check);
        bool none = double.IsNaN(value);
        return new HealthResult(id, componentId, type, none ? null : value, unit,
            none ? HealthGrade.Degraded : line.Grade(value), time, line.Degraded, line.Unhealthy,
            output ?? (none ? "no reading" : null));
    }

    /// <summary>The check id, component type and unit a <see cref="HealthCheck"/> reports under (docs/HEALTH.md section 3).</summary>
    public static (string CheckId, string ComponentType, string Unit) Describe(HealthCheck check) => check switch
    {
        HealthCheck.RamFreeGb => ("memory:free", "system", "GB"),
        HealthCheck.CommitPercent => ("memory:commit", "system", "%"),
        HealthCheck.DiskFreeGb => ("disk:free", "system", "GB"),
        HealthCheck.DtuPercent => ("sqldb:dtu", "datastore", "%"),
        HealthCheck.StoragePercent => ("sqldb:storage", "datastore", "%"),
        HealthCheck.LinkLatencyMs => ("link:latency", "component", "ms"),
        _ => throw new ArgumentOutOfRangeException(nameof(check), check, null),
    };
}

/// <summary>
/// What one probe run produced (M.ORCH-T2020, docs/HEALTH.md section 3): every result it took, the run's time, duration
/// and period. <see cref="Status"/> is the worst grade in it (ASP.NET <c>HealthReport.Status</c>). <see cref="ToJson"/> and
/// <see cref="FromJson"/> write and read the <c>application/health+json</c> shape, results grouped under <c>checks</c> by
/// check id. System.Text.Json only, so it can be copied like AppBus.cs.
/// </summary>
public sealed record HealthReport(
    string Probe,
    string Host,
    string? Description,
    DateTimeOffset Time,
    long DurationMs,
    int IntervalSeconds,
    IReadOnlyList<HealthResult> Results)
{
    /// <summary>The worst grade of every result; healthy when there are none.</summary>
    public HealthGrade Status => Results.Count == 0 ? HealthGrade.Healthy : Results.Max(r => r.Grade);

    /// <summary>The report as health+json (docs/HEALTH.md section 3).</summary>
    public string ToJson(bool indented = false)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = indented }))
        {
            w.WriteStartObject();
            w.WriteString("status", HealthGrades.ToWire(Status));
            w.WriteString("serviceId", Probe);
            if (Description is not null) w.WriteString("description", Description);
            w.WriteString("probe", Probe);
            w.WriteString("host", Host);
            w.WriteString("time", HealthGrades.FormatTime(Time));
            w.WriteNumber("durationMs", DurationMs);
            w.WriteNumber("intervalSeconds", IntervalSeconds);
            w.WriteStartObject("checks");
            foreach (var group in Results.GroupBy(r => r.CheckId, StringComparer.Ordinal))
            {
                w.WriteStartArray(group.Key);
                foreach (var r in group) WriteResult(w, r);
                w.WriteEndArray();
            }
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteResult(Utf8JsonWriter w, HealthResult r)
    {
        w.WriteStartObject();
        if (r.ComponentId is not null) w.WriteString("componentId", r.ComponentId);
        w.WriteString("componentType", r.ComponentType);
        if (r.ObservedState is not null) w.WriteString("observedValue", r.ObservedState);
        else if (r.ObservedValue is { } v && double.IsFinite(v)) w.WriteNumber("observedValue", v);
        else w.WriteNull("observedValue");
        if (r.ObservedUnit is not null) w.WriteString("observedUnit", r.ObservedUnit);
        w.WriteString("status", HealthGrades.ToWire(r.Grade));
        if (r.DegradedAt is { } d) w.WriteNumber("degradedAt", d);
        if (r.UnhealthyAt is { } u) w.WriteNumber("unhealthyAt", u);
        w.WriteString("time", HealthGrades.FormatTime(r.Time));
        if (r.Output is not null) w.WriteString("output", r.Output);
        w.WriteEndObject();
    }

    /// <summary>Reads a health+json report; field names case-insensitively, unknown fields ignored, <c>ok</c>/<c>up</c> and
    /// <c>error</c>/<c>down</c> accepted as statuses. Null when <paramref name="json"/> isn't a report object.</summary>
    public static HealthReport? FromJson(string json)
    {
        JsonObject? root;
        try { root = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject; }
        catch (JsonException) { return null; }
        return root is null ? null : FromNode(root);
    }

    /// <summary>Reads a report already parsed (e.g. an app-bus envelope's payload). Null when it isn't one.</summary>
    public static HealthReport? FromNode(JsonObject root)
    {
        string? Str(JsonObject o, string name) =>
            o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        double? Num(JsonObject o, string name) =>
            o[name] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
        DateTimeOffset Time(JsonObject o) =>
            Str(o, "time") is { } t && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : default;

        var results = new List<HealthResult>();
        if (root["checks"] is JsonObject checks)
        {
            foreach (var (checkId, node) in checks)
            {
                if (node is not JsonArray items) continue;
                foreach (var item in items.OfType<JsonObject>())
                {
                    double? value = null;
                    string? state = null;
                    if (item["observedValue"] is JsonValue ov)
                    {
                        if (ov.TryGetValue<double>(out var d)) value = d;
                        else if (ov.TryGetValue<string>(out var s)) state = s;
                    }
                    results.Add(new HealthResult(checkId, Str(item, "componentId"), Str(item, "componentType") ?? "component",
                        value, Str(item, "observedUnit"), HealthGrades.FromWire(Str(item, "status")), Time(item),
                        Num(item, "degradedAt"), Num(item, "unhealthyAt"), Str(item, "output"), state));
                }
            }
        }
        var probe = Str(root, "probe") ?? Str(root, "serviceId");
        if (probe is null) return null;
        return new HealthReport(probe, Str(root, "host") ?? "", Str(root, "description"), Time(root),
            (long)(Num(root, "durationMs") ?? 0), (int)(Num(root, "intervalSeconds") ?? 0), results);
    }

    /// <summary>Equal when every field and every result (in order) are; records compare lists by reference otherwise.</summary>
    public bool Equals(HealthReport? other) => other is not null && Probe == other.Probe && Host == other.Host
        && Description == other.Description && Time == other.Time && DurationMs == other.DurationMs
        && IntervalSeconds == other.IntervalSeconds && Results.SequenceEqual(other.Results);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Probe, Host, Time, Results.Count);
}

/// <summary>Conversions between <see cref="HealthGrade"/>, the health+json wire status, the older
/// <see cref="HealthLevel"/> and Nagios exit codes (docs/HEALTH.md section 2).</summary>
public static class HealthGrades
{
    /// <summary>MachineHealth's level as a grade: Ok healthy, Warn degraded, Danger unhealthy.</summary>
    public static HealthGrade From(HealthLevel level) => level switch
    {
        HealthLevel.Ok => HealthGrade.Healthy,
        HealthLevel.Warn => HealthGrade.Degraded,
        _ => HealthGrade.Unhealthy,
    };

    /// <summary>The wire status: <c>pass</c>, <c>warn</c> or <c>fail</c>.</summary>
    public static string ToWire(HealthGrade grade) => grade switch
    {
        HealthGrade.Healthy => "pass",
        HealthGrade.Degraded => "warn",
        _ => "fail",
    };

    /// <summary>A wire status as a grade, accepting <c>ok</c>/<c>up</c> for pass and <c>error</c>/<c>down</c> for fail; an
    /// unknown or missing status reads degraded (worth a look, never silently healthy).</summary>
    public static HealthGrade FromWire(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "pass" or "ok" or "up" or "healthy" => HealthGrade.Healthy,
        "fail" or "error" or "down" or "unhealthy" => HealthGrade.Unhealthy,
        _ => HealthGrade.Degraded,
    };

    /// <summary>The Nagios exit code of a grade: 0, 1 or 2 (3 is for a probe that couldn't run at all).</summary>
    public static int NagiosExit(HealthGrade grade) => (int)grade;

    /// <summary>A time as UTC ISO 8601, sub-seconds only when present.</summary>
    internal static string FormatTime(DateTimeOffset t) =>
        t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);
}
