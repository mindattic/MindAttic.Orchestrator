using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Azure;
using MindAttic.Orchestrator.Backup;

namespace MindAttic.Orchestrator.Health;

/// <summary>
/// One Azure SQL database as <c>az sql db list</c> describes it: its server (short name, resource group, FQDN), name,
/// <see cref="Status"/> (<c>Online</c>, <c>Paused</c>, <c>Resuming</c>, ...), sku (<c>GP_S_Gen5_1</c>, tier, vCores) and,
/// for a serverless database, its auto-pause delay in minutes (-1 when auto-pause is off; null when the database has none,
/// a provisioned one). <see cref="ResourceId"/> is its ARM id (<c>/subscriptions/.../databases/name</c>), what
/// <c>az monitor metrics list</c> reads its metrics by (M.ORCH-T2027).
/// </summary>
public sealed record AzureSqlDatabase(
    string Server,
    string ResourceGroup,
    string Fqdn,
    string Name,
    string Status,
    string Sku,
    string? Tier = null,
    int? Capacity = null,
    int? AutoPauseDelayMinutes = null,
    string? ResourceId = null)
{
    /// <summary>The health instance id, <c>server/database</c> (docs/HEALTH.md section 3).</summary>
    public string ComponentId => $"{Server}/{Name}";

    /// <summary>Serverless with auto-pause on.</summary>
    public bool AutoPauses => AutoPauseDelayMinutes is > 0;

    /// <summary>The DTU purchasing model (Basic, Standard, Premium): its load is <c>dtu_consumption_percent</c>; a vCore
    /// database's is <c>cpu_percent</c>. Read from the tier, or from the sku name (<c>Basic</c>, <c>S0</c>, <c>P1</c>) when
    /// az gave no tier.</summary>
    public bool IsDtuModel => Tier is { } tier
        ? tier.Trim().ToLowerInvariant() is "basic" or "standard" or "premium"
        : Sku.Equals("Basic", StringComparison.OrdinalIgnoreCase)
            || (Sku.Length > 1 && (Sku[0] is 'S' or 'P') && Sku.Skip(1).All(char.IsDigit));

    /// <summary>Serverless compute (a <c>GP_S_</c>/<c>HS_S_</c> sku), billed by <c>app_cpu_billed</c>.</summary>
    public bool IsServerless => Sku.Contains("_S_", StringComparison.OrdinalIgnoreCase);

    /// <summary>Asleep: a paused serverless database has no compute and emits no metrics.</summary>
    public bool IsPaused => Status.Equals("Paused", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("AutoClosed", StringComparison.OrdinalIgnoreCase);

    /// <summary>The Azure Monitor metrics this database's load and storage are read from (M.ORCH-T2027):
    /// <c>dtu_consumption_percent</c> (DTU model) or <c>cpu_percent</c> (vCore), <c>storage_percent</c>, and
    /// <c>app_cpu_billed</c> for serverless.</summary>
    public IReadOnlyList<string> MetricNames => IsServerless
        ? [IsDtuModel ? AzureSqlMetrics.DtuMetric : AzureSqlMetrics.CpuMetric, AzureSqlMetrics.StorageMetric, AzureSqlMetrics.AppCpuBilledMetric]
        : [IsDtuModel ? AzureSqlMetrics.DtuMetric : AzureSqlMetrics.CpuMetric, AzureSqlMetrics.StorageMetric];
}

/// <summary>
/// One database's latest Azure Monitor readings (M.ORCH-T2027): the newest point with a value of each metric over the
/// probe's window, the percents by their maximum and <c>app_cpu_billed</c> (vCore-seconds) by its total. A metric az had no
/// point for is null; <see cref="Error"/> says why none could be read at all.
/// </summary>
public sealed record AzureSqlMetrics(
    double? DtuPercent = null,
    double? CpuPercent = null,
    double? StoragePercent = null,
    double? AppCpuBilled = null,
    string? Error = null)
{
    /// <summary>The DTU model's load metric.</summary>
    public const string DtuMetric = "dtu_consumption_percent";
    /// <summary>The vCore model's load metric.</summary>
    public const string CpuMetric = "cpu_percent";
    /// <summary>Data space used, percent of the max size.</summary>
    public const string StorageMetric = "storage_percent";
    /// <summary>Serverless compute billed, vCore-seconds.</summary>
    public const string AppCpuBilledMetric = "app_cpu_billed";

    /// <summary>The load <c>sqldb:dtu</c> grades: DTU percent where there is one, else CPU percent.</summary>
    public double? LoadPercent => DtuPercent ?? CpuPercent;

    /// <summary>Metrics that couldn't be read.</summary>
    public static AzureSqlMetrics Failed(string error) => new(Error: error);
}

/// <summary>What a listing came back with: every database, or why there are none (<see cref="Error"/>), with
/// <see cref="NeedsLogin"/> set when az isn't signed in or installed.</summary>
public sealed record AzureSqlListing(IReadOnlyList<AzureSqlDatabase> Databases, string? Error = null, bool NeedsLogin = false)
{
    /// <summary>Whether the listing worked. A server whose db list failed is noted in <see cref="Error"/> while the
    /// others' databases are still listed.</summary>
    public bool Ok => Error is null;

    /// <summary>A failed listing.</summary>
    public static AzureSqlListing Failed(string error, bool needsLogin = false) => new([], error, needsLogin);
}

/// <summary>
/// The Azure SQL probe's lister (M.ORCH-T2025, docs/HEALTH.md, story M.ORCH-T2015): the C# port of
/// <c>kickstart.ps1 -ListDatabases</c>. <c>az sql server list</c>, then <c>az sql db list</c> per server (master skipped),
/// run directly through <see cref="IProcessRunner"/> rather than through the script, because the script's document carries
/// no auto-pause delay and a PowerShell hop per probe run is cost for nothing. It shares the existing az plumbing instead of
/// adding its own: <see cref="AzureBlobUpload.ResolveAz"/> finds az.cmd and <see cref="Kickstart.NeedsLogin"/> recognises a
/// missing sign-in. Read-only in Azure; blocks for as long as az takes (seconds per server), so run it off the UI thread.
/// Never throws.
/// </summary>
public sealed class AzureSqlProbe(IProcessRunner? runner = null, Func<string?>? resolveAz = null)
{
    private readonly IProcessRunner runner = runner ?? ProcessRunner.Default;
    private readonly Func<string?> resolveAz = resolveAz ?? AzureBlobUpload.ResolveAz;

    /// <summary>How long each az call may run.</summary>
    public TimeSpan AzTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Every Azure SQL database az can see, sorted by server then name.</summary>
    public AzureSqlListing List()
    {
        var az = resolveAz();
        if (az is null) return AzureSqlListing.Failed("az (the Azure CLI) isn't installed or on PATH.", needsLogin: true);

        var (servers, serverError) = Az(az, ["sql", "server", "list", "-o", "json"]);
        if (servers is null) return AzureSqlListing.Failed(serverError ?? "az sql server list failed.", Kickstart.NeedsLogin(serverError));
        List<(string Name, string Rg, string Fqdn)> parsed;
        try { parsed = ParseServers(servers); }
        catch (JsonException ex) { return AzureSqlListing.Failed($"az sql server list's output couldn't be read: {ex.Message}"); }

        var dbs = new List<AzureSqlDatabase>();
        var errors = new List<string>();
        foreach (var s in parsed)
        {
            var (json, error) = Az(az, ["sql", "db", "list", "-g", s.Rg, "-s", s.Name, "-o", "json"]);
            if (json is null) { errors.Add($"{s.Name}: {error ?? "az sql db list failed"}"); continue; }
            try { dbs.AddRange(ParseDatabases(json, s.Name, s.Rg, s.Fqdn)); }
            catch (JsonException ex) { errors.Add($"{s.Name}: az sql db list's output couldn't be read: {ex.Message}"); }
        }
        var sorted = dbs.OrderBy(d => d.Server, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return errors.Count == 0
            ? new AzureSqlListing(sorted)
            : new AzureSqlListing(sorted, string.Join("; ", errors), errors.Any(Kickstart.NeedsLogin));
    }

    /// <summary>One <c>sqldb:state</c> result per database, graded by rule (docs/HEALTH.md section 3): <c>Online</c> and
    /// <c>Paused</c> (a serverless database asleep, as designed) pass; a transition (<c>Resuming</c>, <c>Pausing</c>,
    /// <c>Scaling</c>, <c>Creating</c>, ...) warns; anything else (<c>Offline</c>, <c>Disabled</c>, <c>Suspect</c>, ...) fails.</summary>
    public static IReadOnlyList<HealthResult> StateResults(IEnumerable<AzureSqlDatabase> dbs, DateTimeOffset time) =>
        [.. dbs.Select(d => new HealthResult("sqldb:state", d.ComponentId, "datastore", null, "state", GradeState(d.Status), time,
            Output: $"{d.Status} ({d.Sku}{(d.AutoPauses ? $", auto-pause {d.AutoPauseDelayMinutes} min" : "")})",
            ObservedState: d.Status))];

    /// <summary>How far back <see cref="Metrics"/> looks for the newest point; Azure SQL emits a point a minute, a few
    /// minutes late.</summary>
    public TimeSpan MetricsWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// <paramref name="db"/>'s latest load, storage and (serverless) billed-CPU readings via <c>az monitor metrics list</c>
    /// (M.ORCH-T2027): one call for all of its <see cref="AzureSqlDatabase.MetricNames"/> at 1-minute grain. When az refuses
    /// the set (a tier missing one of them, e.g. Hyperscale without <c>storage_percent</c>) each is asked for alone, so one
    /// unsupported metric never hides the others. A paused database isn't asked at all: it has no compute and emits nothing.
    /// Never throws.
    /// </summary>
    public AzureSqlMetrics Metrics(AzureSqlDatabase db, DateTimeOffset? now = null)
    {
        if (db.IsPaused) return new AzureSqlMetrics();
        if (string.IsNullOrWhiteSpace(db.ResourceId)) return AzureSqlMetrics.Failed("az sql db list gave no resource id.");
        var az = resolveAz();
        if (az is null) return AzureSqlMetrics.Failed("az (the Azure CLI) isn't installed or on PATH.");

        var end = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var window = new List<string> { "--interval", "PT1M", "--aggregation", "Maximum", "Total",
            "--start-time", Iso(end - MetricsWindow), "--end-time", Iso(end), "-o", "json" };

        var readings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        string? lastError = null;
        bool Read(IEnumerable<string> metrics)
        {
            var args = new List<string> { "monitor", "metrics", "list", "--resource", db.ResourceId!, "--metrics" };
            args.AddRange(metrics);
            args.AddRange(window);
            var (json, error) = Az(az, args);
            if (json is null) { lastError = error; return false; }
            try { foreach (var (k, v) in ParseMetrics(json)) readings[k] = v; return true; }
            catch (JsonException ex) { lastError = $"az monitor metrics list's output couldn't be read: {ex.Message}"; return false; }
        }

        var names = db.MetricNames;
        if (!Read(names))
        {
            if (Kickstart.NeedsLogin(lastError)) return AzureSqlMetrics.Failed(lastError ?? "az isn't signed in.");
            bool any = false;
            foreach (var n in names) any |= Read([n]);
            if (!any) return AzureSqlMetrics.Failed(lastError ?? "az monitor metrics list failed.");
        }
        double? Get(string n) => readings.TryGetValue(n, out var v) ? v : null;
        return new AzureSqlMetrics(Get(AzureSqlMetrics.DtuMetric), Get(AzureSqlMetrics.CpuMetric), Get(AzureSqlMetrics.StorageMetric),
            Get(AzureSqlMetrics.AppCpuBilledMetric));
    }

    /// <summary>Every database's <c>sqldb:state</c>, <c>sqldb:dtu</c> and <c>sqldb:storage</c> results (M.ORCH-T2027): the
    /// state by rule, the two percents graded against <paramref name="thresholds"/> (default
    /// <see cref="HealthThresholds.Default"/>). One az metrics call (a few when az refuses the set) per awake database.</summary>
    public IReadOnlyList<HealthResult> Results(AzureSqlListing listing, HealthThresholds? thresholds = null, DateTimeOffset? time = null)
    {
        var t = thresholds ?? HealthThresholds.Default;
        var at = time ?? DateTimeOffset.UtcNow;
        var results = new List<HealthResult>(StateResults(listing.Databases, at));
        foreach (var db in listing.Databases) results.AddRange(MetricResults(db, Metrics(db, at), t, at));
        return results;
    }

    /// <summary>
    /// <paramref name="db"/>'s <c>sqldb:dtu</c> and <c>sqldb:storage</c> results from <paramref name="m"/>, graded on
    /// <see cref="HealthCheck.DtuPercent"/> and <see cref="HealthCheck.StoragePercent"/> with the line stamped in. The load is
    /// DTU percent, else CPU percent; a serverless database's billed vCore-seconds ride in the load's output. A paused
    /// database reads 0% load (healthy: asleep as designed) and has no storage result, since it emits no metrics; a missing
    /// reading on an awake one is degraded ("no reading", with az's error when there was one).
    /// </summary>
    public static IReadOnlyList<HealthResult> MetricResults(AzureSqlDatabase db, AzureSqlMetrics m, HealthThresholds t, DateTimeOffset time)
    {
        if (db.IsPaused)
            return [HealthResult.Graded(HealthCheck.DtuPercent, t, 0, time, db.ComponentId, $"{db.Status}: no compute running")];

        static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        string NoReading(string what) => m.Error is { } e ? $"no {what} reading: {e}" : $"no {what} reading in the last window";

        var loadName = db.IsDtuModel ? "DTU" : "CPU";
        var loadOutput = m.LoadPercent is { } load ? $"{loadName} {Pct(load)} (max, latest minute)" : NoReading(loadName);
        if (m.AppCpuBilled is { } billed)
            loadOutput += $"; app CPU billed {billed.ToString("0.##", CultureInfo.InvariantCulture)} vCore-s";
        return
        [
            HealthResult.Graded(HealthCheck.DtuPercent, t, m.LoadPercent ?? double.NaN, time, db.ComponentId, loadOutput),
            HealthResult.Graded(HealthCheck.StoragePercent, t, m.StoragePercent ?? double.NaN, time, db.ComponentId,
                m.StoragePercent is { } st ? $"storage {Pct(st)} of max size" : NoReading("storage")),
        ];
    }

    /// <summary>Parses <c>az monitor metrics list -o json</c>: each metric's newest point that has a value, its maximum
    /// (percent metrics) or total (<c>app_cpu_billed</c>), falling back to whichever aggregation is present.</summary>
    internal static Dictionary<string, double> ParseMetrics(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var metrics = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var v) ? v : root;
        var found = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in Items(metrics))
        {
            var name = !metric.TryGetProperty("name", out var n) ? ""
                : n.ValueKind == JsonValueKind.Object ? Str(n, "value")
                : n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            if (name.Length == 0 || !metric.TryGetProperty("timeseries", out var series)) continue;
            string[] order = name.Equals(AzureSqlMetrics.AppCpuBilledMetric, StringComparison.OrdinalIgnoreCase)
                ? ["total", "maximum", "average"] : ["maximum", "average", "total"];
            (DateTimeOffset At, double Value)? best = null;
            foreach (var ts in Items(series))
            {
                if (!ts.TryGetProperty("data", out var data)) continue;
                foreach (var point in Items(data))
                {
                    double? value = null;
                    foreach (var agg in order)
                        if (point.TryGetProperty(agg, out var a) && a.ValueKind == JsonValueKind.Number) { value = a.GetDouble(); break; }
                    if (value is null) continue;
                    var at = StrOrNull(point, "timeStamp") is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var p) ? p : DateTimeOffset.MinValue;
                    if (best is null || at >= best.Value.At) best = (at, value.Value);
                }
            }
            if (best is { } b) found[name] = b.Value;
        }
        return found;
    }

    /// <summary>The rule grade of a database status word.</summary>
    public static HealthGrade GradeState(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "online" or "paused" or "autoclosed" => HealthGrade.Healthy,
        "resuming" or "pausing" or "scaling" or "creating" or "copying" or "restoring" or "recovering" or "standby"
            or "onlinechangingdwperformancetiers" or "starting" or "stopping" => HealthGrade.Degraded,
        _ => HealthGrade.Unhealthy,
    };

    /// <summary>Parses <c>az sql server list -o json</c>: each server's name, resource group and FQDN.</summary>
    internal static List<(string Name, string Rg, string Fqdn)> ParseServers(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return [.. Items(doc.RootElement)
            .Select(e => (Name: Str(e, "name"), Rg: Str(e, "resourceGroup"), Fqdn: Str(e, "fullyQualifiedDomainName")))
            .Where(s => s.Name.Length > 0 && s.Rg.Length > 0)
            .Select(s => s.Fqdn.Length > 0 ? s : s with { Fqdn = $"{s.Name}.database.windows.net" })];
    }

    /// <summary>Parses <c>az sql db list -o json</c> for one server; <c>master</c> is skipped, as the script does.</summary>
    internal static List<AzureSqlDatabase> ParseDatabases(string json, string server, string resourceGroup, string fqdn)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<AzureSqlDatabase>();
        foreach (var e in Items(doc.RootElement))
        {
            var name = Str(e, "name");
            if (name.Length == 0 || name.Equals("master", StringComparison.OrdinalIgnoreCase)) continue;
            var sku = e.TryGetProperty("currentSku", out var cs) && cs.ValueKind == JsonValueKind.Object ? cs
                : e.TryGetProperty("sku", out var s) && s.ValueKind == JsonValueKind.Object ? s : default;
            var skuName = sku.ValueKind == JsonValueKind.Object ? Str(sku, "name") : "";
            if (skuName.Length == 0) skuName = Str(e, "currentServiceObjectiveName");
            list.Add(new AzureSqlDatabase(server, resourceGroup, fqdn, name, Str(e, "status"), skuName,
                sku.ValueKind == JsonValueKind.Object ? StrOrNull(sku, "tier") : null,
                sku.ValueKind == JsonValueKind.Object ? Int(sku, "capacity") : null,
                Int(e, "autoPauseDelay"), StrOrNull(e, "id")));
        }
        return list;
    }

    // Runs one az call: its stdout on success, else the first meaningful error line.
    private (string? Json, string? Error) Az(string az, IReadOnlyList<string> args)
    {
        ProcessResult r;
        try { r = runner.Run(az, args, AzTimeout); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return (null, $"Couldn't run az: {ex.Message}"); }
        if (r.TimedOut && !r.Exited) return (null, $"az {string.Join(' ', args.Take(3))} timed out.");
        if (r.ExitCode != 0 || string.IsNullOrWhiteSpace(r.Output))
            return (null, FirstLine(r.Error) ?? FirstLine(r.Output) ?? $"az {string.Join(' ', args.Take(3))} exited {r.ExitCode} with no output.");
        return (r.Output, null);
    }

    private static IEnumerable<JsonElement> Items(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Array => e.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList(),
        JsonValueKind.Object => [e],
        _ => [],
    };

    private static string Str(JsonElement e, string name) => StrOrNull(e, name) ?? "";

    private static string? StrOrNull(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => v.ToString(),
        } : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => null,
        } : null;

    private static string? FirstLine(string? text) =>
        text?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("WARNING:", StringComparison.Ordinal));
}
