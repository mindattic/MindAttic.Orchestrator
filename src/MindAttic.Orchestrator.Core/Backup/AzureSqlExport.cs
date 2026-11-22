using System.Diagnostics;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;
using MindAttic.Orchestrator.Azure;

namespace MindAttic.Orchestrator.Backup;

/// <summary>One Azure SQL database's export outcome.</summary>
public sealed record AzureSqlExportResult(
    string Server, string Database, bool Ok, TimeSpan Elapsed, string ExportFile,
    long Bytes = 0, int Tables = 0, long InsertStatements = 0, string Output = "");

/// <summary>An Azure SQL database to export: its server's short name, its FQDN and the database's name. Value-equal so duplicates dedupe.</summary>
public readonly record struct AzureSqlTarget(string Server, string Fqdn, string Database)
{
    /// <summary>A target on the given server; <paramref name="server"/> may be the short name or the FQDN.</summary>
    public static AzureSqlTarget On(string server, string database)
    {
        var s = (server ?? "").Trim();
        var fqdn = s.Contains('.') ? s : s + ".database.windows.net";
        return new AzureSqlTarget(fqdn.Split('.')[0], fqdn, (database ?? "").Trim());
    }
}

/// <summary>
/// The Azure SQL half of the backup (M.ORCH-T2047, ported from MindAttic.Launcher's AzureSqlExportService): each
/// database is exported to a <c>.sql</c> script, schema and data, at <c>{datedFolder}\Databases\{server}\{db}.sql</c>.
/// SMO, over a SqlClient connection, writes the script the way SSMS's "Generate Scripts" does (tables with their keys,
/// constraints, indexes and triggers, views, procedures, functions, sequences, then the rows as INSERTs). It signs in
/// the way docs/ops/azure-migration's Backup-AzureDatabase.ps1 does: the service principal in
/// <c>%APPDATA%\MindAttic\Database\providers.json</c> (<c>AzureSql.TenantId/ClientId/ClientSecret</c>), else the
/// signed-in az account; no password is stored anywhere, and the token is renewed during a long export. A paused
/// serverless database is woken by connecting, retried until <see cref="WakeTimeoutSeconds"/>. Read-only against
/// Azure: it only SELECTs. One database's failure is its result, never an exception, so it doesn't stop the rest.
/// </summary>
public sealed class AzureSqlExport
{
    public const string DatabasesSubfolder = SqliteBackup.DatabasesSubfolder;

    /// <summary>How long a paused serverless database gets to come online before its export fails.</summary>
    public const int DefaultWakeTimeoutSeconds = 180;

    public const string SqlResource = "https://database.windows.net/";

    /// <summary>The service principal file the azure-migration scripts read.</summary>
    public static readonly string DefaultProvidersFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "Database", "providers.json");

    /// <summary>Exports one database; replaceable so tests never reach Azure.</summary>
    public delegate AzureSqlExportResult Exporter(AzureSqlTarget target, string targetFolder, Action<string>? progress, CancellationToken ct);

    private readonly Exporter export;

    public int WakeTimeoutSeconds { get; init; } = DefaultWakeTimeoutSeconds;

    public string ProvidersFile { get; init; } = DefaultProvidersFile;

    public AzureSqlExport(Exporter? exporter = null) => export = exporter ?? ((t, f, p, ct) => ExportOne(t, f, p, ct));

    /// <summary>
    /// The targets for the named databases, from a kickstart list's rows (each app's database, with its server). A name
    /// no row carries is left out; one database several apps share is exported once. Null names means every database.
    /// </summary>
    public static IReadOnlyList<AzureSqlTarget> CollectTargets(IEnumerable<KickstartApp> apps, IEnumerable<string>? names = null)
    {
        var wanted = names?.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<AzureSqlTarget>();
        foreach (var db in apps.Select(a => a.Database))
        {
            if (db is null || string.IsNullOrWhiteSpace(db.Name)) continue;
            if (wanted is not null && !wanted.Contains(db.Name)) continue;
            var server = string.IsNullOrWhiteSpace(db.Fqdn) ? db.Server : db.Fqdn;
            if (string.IsNullOrWhiteSpace(server)) continue;
            var target = AzureSqlTarget.On(server, db.Name);
            if (seen.Add(target.Fqdn + "/" + target.Database)) targets.Add(target);
        }
        return targets;
    }

    /// <summary><c>{targetFolder}\Databases\{server}\{database}.sql</c>, both segments scrubbed of path-illegal characters.</summary>
    public static string ResolveExportFilePath(string targetFolder, string server, string database) =>
        Path.Combine(targetFolder, DatabasesSubfolder, SanitizeFileName(server), SanitizeFileName(database) + ".sql");

    private static string SanitizeFileName(string name)
    {
        var s = (name ?? "").Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrEmpty(s) ? "database" : s;
    }

    /// <summary>Exports each target in order into <paramref name="targetFolder"/>; cancellation stops before the next.</summary>
    public IReadOnlyList<AzureSqlExportResult> Export(
        IReadOnlyList<AzureSqlTarget> targets, string targetFolder,
        Action<string>? progress = null, Action<AzureSqlExportResult>? onDone = null, CancellationToken ct = default)
    {
        var results = new List<AzureSqlExportResult>();
        foreach (var t in targets)
        {
            if (ct.IsCancellationRequested) break;
            AzureSqlExportResult r;
            if (string.IsNullOrWhiteSpace(t.Database) || string.IsNullOrWhiteSpace(t.Fqdn))
                r = new AzureSqlExportResult(t.Server, t.Database, false, TimeSpan.Zero, "", Output: "no server or database name");
            else
            {
                progress?.Invoke($"Exporting {t.Server}/{t.Database}…");
                try { r = export(t, targetFolder, progress, ct); }
                catch (Exception ex) { r = new AzureSqlExportResult(t.Server, t.Database, false, TimeSpan.Zero, "", Output: Flatten(ex)); }
            }
            results.Add(r);
            onDone?.Invoke(r);
        }
        return results;
    }

    /// <summary>The script's opening lines: what it is, where from, when, and how to apply it.</summary>
    public static string BuildHeader(string server, string database, DateTimeOffset when) =>
        $"""
        -- {database} on {server}.database.windows.net: schema and data, exported {when:yyyy-MM-dd HH:mm:ss zzz}
        -- by MindAttic.Orchestrator's backup (M.ORCH-T2047). Apply it to an empty database, e.g.
        --   sqlcmd -S <server> -d <empty database> -G -i "{database}.sql"
        -- Never run it against the live Azure database.
        SET ANSI_NULLS ON
        GO
        SET QUOTED_IDENTIFIER ON
        GO

        """;

    /// <summary>
    /// A statement SMO hands back, and whether a <c>GO</c> follows it. Row INSERTs (and the IDENTITY_INSERT switches
    /// around them) run in one batch per table instead, so a big table isn't one batch per row.
    /// </summary>
    public static bool NeedsBatchSeparator(string statement)
    {
        var s = statement.TrimStart();
        return !(s.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase)
                 || s.StartsWith("SET IDENTITY_INSERT", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The service principal in a providers.json (<c>{ "AzureSql": { "TenantId", "ClientId", "ClientSecret" } }</c>), or
    /// null when the file, the section or any of the three values is missing or unreadable.
    /// </summary>
    public static (string TenantId, string ClientId, string ClientSecret)? ReadServicePrincipal(string providersFile)
    {
        try
        {
            if (!File.Exists(providersFile)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(providersFile));
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("AzureSql", out var sql) || sql.ValueKind != JsonValueKind.Object) return null;
            string? Get(string n) => sql.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
                ? v.GetString() : null;
            var (tenant, client, secret) = (Get("TenantId"), Get("ClientId"), Get("ClientSecret"));
            return tenant is null || client is null || secret is null ? null : (tenant, client, secret);
        }
        catch { return null; }
    }

    /// <summary>
    /// Whether a connect failure is a serverless database still waking (40613 "not currently available", 40197 / 40501
    /// service busy, 49918..49920 not enough resources) or a transient network one, so the connect is worth retrying.
    /// </summary>
    public static bool IsWaking(int sqlErrorNumber) => sqlErrorNumber is 40613 or 40197 or 40501 or 49918 or 49919 or 49920 or 4060 or 10928 or 10929 or -2 or 53;

    /// <summary>The SMO export of one database into its .sql file (written as .partial, renamed when complete).</summary>
    public AzureSqlExportResult ExportOne(AzureSqlTarget target, string targetFolder, Action<string>? progress, CancellationToken ct)
    {
        var file = ResolveExportFilePath(targetFolder, target.Server, target.Database);
        var partial = file + ".partial";
        var sw = Stopwatch.StartNew();
        var tables = 0;
        long inserts = 0;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var token = new RenewableToken(ReadServicePrincipal(ProvidersFile));
            var cs = new SqlConnectionStringBuilder
            {
                DataSource = $"tcp:{target.Fqdn},1433",
                InitialCatalog = target.Database,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                ConnectTimeout = 60,
                ApplicationName = "MindAttic.Orchestrator backup"
            }.ConnectionString;
            using var sql = OpenWaking(cs, token, target, progress, ct);
            var connection = new ServerConnection(sql, token) { StatementTimeout = 0 };
            var smo = new Server(connection);
            var db = smo.Databases[target.Database] ?? throw new InvalidOperationException($"SMO can't see database {target.Database}");

            // Fetch IsSystemObject with the collections, not one round trip per object.
            foreach (var t in new[] { typeof(Table), typeof(View), typeof(StoredProcedure), typeof(UserDefinedFunction) })
                smo.SetDefaultInitFields(t, "IsSystemObject");

            // Schemas first, on their own: SMO's dependency walker doesn't take them.
            var schemas = new List<Urn>();
            foreach (Schema s in db.Schemas) if (!s.IsSystemObject && s.Name != "dbo") schemas.Add(s.Urn);
            var urns = new List<Urn>();
            foreach (UserDefinedDataType t in db.UserDefinedDataTypes) urns.Add(t.Urn);
            foreach (UserDefinedTableType t in db.UserDefinedTableTypes) urns.Add(t.Urn);
            foreach (Sequence s in db.Sequences) urns.Add(s.Urn);
            foreach (Table t in db.Tables) if (!t.IsSystemObject) { urns.Add(t.Urn); tables++; }
            foreach (View v in db.Views) if (!v.IsSystemObject) urns.Add(v.Urn);
            foreach (UserDefinedFunction f in db.UserDefinedFunctions) if (!f.IsSystemObject) urns.Add(f.Urn);
            foreach (StoredProcedure p in db.StoredProcedures) if (!p.IsSystemObject) urns.Add(p.Urn);
            progress?.Invoke($"{target.Database}: scripting {schemas.Count + urns.Count} objects ({tables} tables) and their rows…");

            var scripter = new Scripter(smo)
            {
                Options = new ScriptingOptions
                {
                    ScriptSchema = true,
                    ScriptData = true,
                    WithDependencies = true,      // dependency order: a table before the view or FK that needs it
                    DriAll = true,                // keys, defaults, checks, foreign keys
                    Indexes = true,
                    Triggers = true,
                    ExtendedProperties = true,
                    IncludeHeaders = false,
                    ScriptBatchTerminator = false,
                    Encoding = System.Text.Encoding.UTF8
                }
            };

            using (var writer = new StreamWriter(partial, false, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(BuildHeader(target.Server, target.Database, DateTimeOffset.Now));
                if (schemas.Count > 0)
                {
                    var schemaScripter = new Scripter(smo) { Options = new ScriptingOptions { IncludeHeaders = false, ScriptBatchTerminator = false } };
                    foreach (var statement in schemaScripter.EnumScript(schemas.ToArray()))
                    {
                        writer.WriteLine(statement.TrimEnd());
                        writer.WriteLine("GO");
                    }
                }
                if (urns.Count > 0)
                {
                    var inData = false;
                    foreach (var statement in scripter.EnumScript(urns.ToArray()))
                    {
                        ct.ThrowIfCancellationRequested();
                        var separate = NeedsBatchSeparator(statement);
                        if (separate && inData) { writer.WriteLine("GO"); inData = false; }
                        writer.WriteLine(statement.TrimEnd());
                        if (separate) writer.WriteLine("GO");
                        else
                        {
                            inData = true;
                            if (statement.TrimStart().StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase)
                                && ++inserts % 50_000 == 0)
                                progress?.Invoke($"{target.Database}: {inserts:n0} rows scripted…");
                        }
                    }
                    if (inData) writer.WriteLine("GO");
                }
            }

            File.Move(partial, file, overwrite: true);
            sw.Stop();
            return new AzureSqlExportResult(target.Server, target.Database, true, sw.Elapsed, file, new FileInfo(file).Length, tables, inserts);
        }
        catch (Exception ex)
        {
            sw.Stop();
            try { if (File.Exists(partial)) File.Delete(partial); } catch { /* best-effort */ }
            var message = ex is OperationCanceledException ? "cancelled" : Flatten(ex);
            return new AzureSqlExportResult(target.Server, target.Database, false, sw.Elapsed, file, 0, tables, inserts, message);
        }
    }

    // Connecting is what wakes a paused serverless database; until it's online the connect fails with 40613, so retry.
    private SqlConnection OpenWaking(string connectionString, RenewableToken token, AzureSqlTarget target, Action<string>? progress, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(WakeTimeoutSeconds, 0));
        var announced = false;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var sql = new SqlConnection(connectionString) { AccessToken = token.GetAccessToken() };
            try
            {
                sql.Open();
                return sql;
            }
            catch (SqlException ex) when (IsWaking(ex.Number) && DateTime.UtcNow < deadline)
            {
                sql.Dispose();
                if (!announced) { progress?.Invoke($"{target.Database}: waiting for it to wake…"); announced = true; }
                ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch
            {
                sql.Dispose();
                throw;
            }
        }
    }

    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException) parts.Add(e.Message);
        return BackupFolders.Tail(string.Join(" -> ", parts.Distinct()), 2000);
    }

    /// <summary>
    /// The Azure SQL token, renewed by SMO when it nears expiry (an export can outlast the hour a token lives): the
    /// providers.json service principal when there is one, else (or when it fails) the signed-in az account.
    /// </summary>
    private sealed class RenewableToken : IRenewableToken
    {
        private static readonly TokenRequestContext Request = new([SqlResource + ".default"]);
        private readonly TokenCredential credential;
        private AccessToken current;

        public RenewableToken((string TenantId, string ClientId, string ClientSecret)? principal)
        {
            var cli = new AzureCliCredential(new AzureCliCredentialOptions { ProcessTimeout = TimeSpan.FromSeconds(60) });
            credential = principal is { } p
                ? new ChainedTokenCredential(new ClientSecretCredential(p.TenantId, p.ClientId, p.ClientSecret), cli)
                : cli;
        }

        public DateTimeOffset TokenExpiry => current.ExpiresOn;
        public string Resource => SqlResource;
        public string Tenant => "";
        public string UserId => "";

        public string GetAccessToken()
        {
            if (current.Token is null || current.ExpiresOn < DateTimeOffset.UtcNow.AddMinutes(5))
                current = credential.GetToken(Request, CancellationToken.None);
            return current.Token!;
        }
    }
}
