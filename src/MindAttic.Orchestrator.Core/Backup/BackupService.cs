namespace MindAttic.Orchestrator.Backup;

/// <summary>What one backup run produced: the dated folder, each SQLite dump, each Azure SQL export and the AppData zip.</summary>
public sealed record BackupRunResult(
    string Folder,
    IReadOnlyList<SqliteBackupResult> Sqlite,
    AppDataBackupResult AppData,
    IReadOnlyList<AzureSqlExportResult>? AzureSql = null)
{
    /// <summary>Each Azure SQL database's export (M.ORCH-T2047); empty when the run named none.</summary>
    public IReadOnlyList<AzureSqlExportResult> AzureSql { get; init; } = AzureSql ?? [];

    /// <summary>True when every database dumped or exported and the AppData zip didn't fail (a missing AppData tree is OK).</summary>
    public bool Ok => AppData.Ok && Sqlite.All(r => r.Ok) && this.AzureSql.All(r => r.Ok);
}

/// <summary>
/// The Orchestrator's backup engine (M.ORCH-T2046, part of M.ORCH-T1983): makes the dated folder under the backup
/// base (<c>yyyy-MM-dd</c>, <c>_a</c>..<c>_z</c> on a second run that day), dumps each SQLite database into
/// <c>Databases\SQLite\{project}\{db}.sql</c>, exports each Azure SQL database into
/// <c>Databases\{server}\{db}.sql</c> (M.ORCH-T2047) and zips <c>%APPDATA%\MindAttic</c> into <c>AppData.zip</c>.
/// Every step's failure lands in the result instead of throwing, so one bad database doesn't lose the rest.
/// The clock, the sqlite3 runner and the AppData source are injectable for tests.
/// </summary>
public sealed class BackupService
{
    public string BackupBase { get; }
    private readonly SqliteBackup sqlite;
    private readonly AppDataBackup appData;
    private readonly AzureSqlExport azureSql;
    private readonly Func<DateTime> now;

    public BackupService() : this(BackupFolders.DefaultBackupBase) { }

    public BackupService(string backupBase, SqliteBackup? sqlite = null, AppDataBackup? appData = null, Func<DateTime>? now = null,
        AzureSqlExport? azureSql = null)
    {
        BackupBase = backupBase;
        this.sqlite = sqlite ?? new SqliteBackup();
        this.appData = appData ?? new AppDataBackup();
        this.azureSql = azureSql ?? new AzureSqlExport();
        this.now = now ?? (() => DateTime.Now);
    }

    /// <summary>The folder the next run would write: today's dated folder, or its next free <c>_a</c>..<c>_z</c>.</summary>
    public string ResolveTargetFolder() => BackupFolders.ResolveTarget(BackupBase, now());

    /// <summary>Runs a whole backup into a fresh dated folder and returns what it produced.</summary>
    public BackupRunResult Run(
        IReadOnlyList<SqliteBackupTarget> databases,
        Action<SqliteBackupResult>? onDatabaseDone = null,
        CancellationToken ct = default,
        IReadOnlyList<AzureSqlTarget>? azureDatabases = null,
        Action<AzureSqlExportResult>? onAzureDone = null,
        Action<string>? progress = null)
    {
        var folder = ResolveTargetFolder();
        Directory.CreateDirectory(folder);
        var dumps = sqlite.Backup(databases, folder, onDatabaseDone, ct);
        var exports = azureDatabases is { Count: > 0 } && !ct.IsCancellationRequested
            ? azureSql.Export(azureDatabases, folder, progress, onAzureDone, ct)
            : [];
        var zip = ct.IsCancellationRequested
            ? new AppDataBackupResult(false, AppDataBackup.ResolveZipPath(folder), 0, TimeSpan.Zero, "cancelled")
            : appData.Zip(folder);
        return new BackupRunResult(folder, dumps, zip, exports);
    }

    /// <summary>
    /// Deletes all but the newest <paramref name="keep"/> dated folders under the backup base (a date's
    /// <c>_a</c>..<c>_z</c> siblings go with it). Returns the folders it removed; one that can't be deleted is skipped.
    /// </summary>
    public IReadOnlyList<string> Prune(int keep)
    {
        if (!Directory.Exists(BackupBase)) return [];
        var removed = new List<string>();
        foreach (var stale in BackupFolders.ResolveStale(Directory.GetDirectories(BackupBase), keep))
        {
            try
            {
                Directory.Delete(stale, recursive: true);
                removed.Add(stale);
            }
            catch { /* locked or already gone: leave it for the next prune */ }
        }
        return removed;
    }
}
