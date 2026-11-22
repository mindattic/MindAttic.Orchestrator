using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Azure;
using MindAttic.Orchestrator.Backup;

namespace MindAttic.Orchestrator.Health;

/// <summary>
/// One Azure SQL database as <c>az sql db list</c> describes it: its server (short name, resource group, FQDN), name,
/// <see cref="Status"/> (<c>Online</c>, <c>Paused</c>, <c>Resuming</c>, ...), sku (<c>GP_S_Gen5_1</c>, tier, vCores) and,
/// for a serverless database, its auto-pause delay in minutes (-1 when auto-pause is off; null when the database has none,
/// a provisioned one).
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
    int? AutoPauseDelayMinutes = null)
{
    /// <summary>The health instance id, <c>server/database</c> (docs/HEALTH.md section 3).</summary>
    public string ComponentId => $"{Server}/{Name}";

    /// <summary>Serverless with auto-pause on.</summary>
    public bool AutoPauses => AutoPauseDelayMinutes is > 0;
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
                Int(e, "autoPauseDelay")));
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
