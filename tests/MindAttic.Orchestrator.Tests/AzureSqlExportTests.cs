using MindAttic.Orchestrator.Azure;
using MindAttic.Orchestrator.Backup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2047: the Azure SQL export ported from MindAttic.Launcher, without ever reaching Azure.</summary>
public sealed class AzureSqlExportTests
{
    private string temp = "";

    [SetUp]
    public void SetUp()
    {
        temp = Path.Combine(Path.GetTempPath(), "MindAtticOrchAzureSqlTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
    }

    [Test]
    public void Export_file_is_Databases_server_db_sql_with_illegal_characters_scrubbed()
    {
        Assert.That(AzureSqlExport.ResolveExportFilePath(@"R:\B\2026-11-22", "sql-x", "MindAttic.Tickets"),
            Is.EqualTo(@"R:\B\2026-11-22\Databases\sql-x\MindAttic.Tickets.sql"));
        Assert.That(AzureSqlExport.ResolveExportFilePath(@"R:\B", "a|b", "c:d"), Is.EqualTo(@"R:\B\Databases\a_b\c_d.sql"));
    }

    [Test]
    public void Target_takes_a_short_server_name_or_its_fqdn()
    {
        Assert.That(AzureSqlTarget.On("sql-x", "Db"), Is.EqualTo(new AzureSqlTarget("sql-x", "sql-x.database.windows.net", "Db")));
        Assert.That(AzureSqlTarget.On("sql-x.database.windows.net", " Db "), Is.EqualTo(new AzureSqlTarget("sql-x", "sql-x.database.windows.net", "Db")));
    }

    [Test]
    public void Targets_come_from_kickstart_rows_once_each_and_only_the_named_ones()
    {
        var db = new KickstartDatabase("sql-x", "sql-x.database.windows.net", "Shared", "Online", "GP_S");
        var apps = new[]
        {
            new KickstartApp("a", "Running", "rg", "u", db),
            new KickstartApp("b", "Running", "rg", "u", db),
            new KickstartApp("c", "Running", "rg", "u", new KickstartDatabase("sql-x", "", "Other", "Paused", "")),
            new KickstartApp("d", "Running", "rg", "u", null),
        };
        Assert.That(AzureSqlExport.CollectTargets(apps).Select(t => t.Database), Is.EqualTo(new[] { "Shared", "Other" }));
        Assert.That(AzureSqlExport.CollectTargets(apps, ["other"]).Single().Fqdn, Is.EqualTo("sql-x.database.windows.net"));
    }

    [Test]
    public void Inserts_and_identity_switches_share_a_batch_everything_else_gets_GO()
    {
        Assert.That(AzureSqlExport.NeedsBatchSeparator("INSERT [dbo].[T] ([a]) VALUES (1)"), Is.False);
        Assert.That(AzureSqlExport.NeedsBatchSeparator("  SET IDENTITY_INSERT [dbo].[T] ON"), Is.False);
        Assert.That(AzureSqlExport.NeedsBatchSeparator("CREATE TABLE [dbo].[T] ([a] int)"), Is.True);
    }

    [Test]
    public void Service_principal_needs_all_three_values()
    {
        var file = Path.Combine(temp, "providers.json");
        File.WriteAllText(file, """{ "AzureSql": { "TenantId": "t", "ClientId": "c", "ClientSecret": "s" } }""");
        Assert.That(AzureSqlExport.ReadServicePrincipal(file), Is.EqualTo(("t", "c", "s")));
        File.WriteAllText(file, """{ "AzureSql": { "TenantId": "t", "ClientId": "c" } }""");
        Assert.That(AzureSqlExport.ReadServicePrincipal(file), Is.Null);
        Assert.That(AzureSqlExport.ReadServicePrincipal(Path.Combine(temp, "missing.json")), Is.Null);
    }

    [Test]
    public void A_failing_database_is_its_result_and_the_rest_still_export()
    {
        var export = new AzureSqlExport((t, folder, _, _) => t.Database == "Bad"
            ? throw new InvalidOperationException("boom")
            : new AzureSqlExportResult(t.Server, t.Database, true, TimeSpan.Zero, AzureSqlExport.ResolveExportFilePath(folder, t.Server, t.Database)));
        var results = export.Export([AzureSqlTarget.On("s", "Bad"), AzureSqlTarget.On("s", "Good"), AzureSqlTarget.On("s", "")], temp);
        Assert.That(results.Select(r => r.Ok), Is.EqualTo(new[] { false, true, false }));
        Assert.That(results[0].Output, Does.Contain("boom"));
    }

    [Test]
    public void Backup_run_exports_azure_databases_into_its_dated_folder()
    {
        var exported = new List<string>();
        var service = new BackupService(temp,
            sqlite: new SqliteBackup("sqlite3", (_, _) => (0, "")),
            appData: new AppDataBackup(Path.Combine(temp, "no-appdata")),
            now: () => new DateTime(2026, 11, 22),
            azureSql: new AzureSqlExport((t, folder, _, _) =>
            {
                exported.Add(folder);
                return new AzureSqlExportResult(t.Server, t.Database, true, TimeSpan.Zero, AzureSqlExport.ResolveExportFilePath(folder, t.Server, t.Database));
            }));
        var run = service.Run([], azureDatabases: [AzureSqlTarget.On("s", "Db")]);
        Assert.That(exported, Is.EqualTo(new[] { Path.Combine(temp, "2026-11-22") }));
        Assert.That(run.AzureSql.Single().ExportFile, Is.EqualTo(Path.Combine(temp, "2026-11-22", "Databases", "s", "Db.sql")));
        Assert.That(run.Ok, Is.EqualTo(run.AppData.Ok));
    }
}
