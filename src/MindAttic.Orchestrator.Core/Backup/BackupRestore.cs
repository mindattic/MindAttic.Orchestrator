namespace MindAttic.Orchestrator.Backup;

/// <summary>A dump the restore found but had nowhere to put back (no matching project in the current roster).</summary>
public sealed record SqliteRestoreSkip(string Project, string DumpFile);

/// <summary>What one restore produced: the AppData unzip, each SQLite replay, the skipped dumps and the Azure SQL exports found.</summary>
public sealed record BackupRestoreResult(
    string Folder,
    AppDataBackupResult? AppData,
    IReadOnlyList<SqliteRestoreResult> Sqlite,
    IReadOnlyList<SqliteRestoreSkip> Skipped,
    IReadOnlyList<string> AzureSqlExports,
    string? Error = null)
{
    /// <summary>
    /// True when the folder existed, the AppData zip (if any) extracted and every matched dump replayed. Skipped dumps
    /// and the Azure SQL exports, which are never applied, don't fail it.
    /// </summary>
    public bool Ok => Error is null && (AppData?.Ok ?? true) && Sqlite.All(r => r.Ok);
}

/// <summary>
/// Restores a dated folder made by <see cref="BackupService"/>: extracts <c>AppData.zip</c> over
/// <c>%APPDATA%\MindAttic</c> and replays each <c>Databases\SQLite\{project}\{db}.sql</c> into the live database the
/// current roster names for that project. The Azure SQL exports (<c>Databases\{server}\{db}.sql</c>, and any pre-Azure
/// <c>.bak</c>) are only listed, never applied: an export goes into an empty database the person picks, by hand
/// (<c>sqlcmd -S &lt;server&gt; -d &lt;empty db&gt; -G -i &lt;file&gt;</c>), never the live one. Recovery onto a running
/// workspace, not a bootstrap. Ported from MindAttic.Launcher's RestoreCommand (M.ORCH-T2049).
/// </summary>
public sealed class BackupRestore
{
    private readonly SqliteRestore sqlite;
    private readonly AppDataBackup appData;

    public BackupRestore(SqliteRestore? sqlite = null, AppDataBackup? appData = null)
    {
        this.sqlite = sqlite ?? new SqliteRestore();
        this.appData = appData ?? new AppDataBackup();
    }

    /// <summary>Where the AppData zip extracts to.</summary>
    public string AppDataTarget => appData.Source;

    /// <summary>
    /// Restores <paramref name="folder"/>. <paramref name="databases"/> is the current roster's SQLite files (as
    /// <see cref="SqliteBackup.CollectTargets"/> builds them), which say where each dump goes back to.
    /// </summary>
    public BackupRestoreResult Run(
        string folder,
        IReadOnlyList<SqliteBackupTarget> databases,
        Action<SqliteRestoreResult>? onDatabaseDone = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return new BackupRestoreResult(folder ?? "", null, [], [], [], $"Backup folder not found: {folder}");

        var zip = AppDataBackup.ResolveZipPath(folder);
        var appDataResult = File.Exists(zip) ? appData.Restore(zip, appData.Source) : null;

        var restored = new List<SqliteRestoreResult>();
        var skipped = new List<SqliteRestoreSkip>();
        foreach (var (project, dump, dbFile) in MatchDumps(folder, databases))
        {
            if (ct.IsCancellationRequested) break;
            if (dbFile is null)
            {
                skipped.Add(new SqliteRestoreSkip(project, dump));
                continue;
            }
            var r = sqlite.RestoreOne(project, dbFile, dump, ct);
            restored.Add(r);
            onDatabaseDone?.Invoke(r);
        }

        return new BackupRestoreResult(folder, appDataResult, restored, skipped, FindAzureSqlExports(folder));
    }

    /// <summary>
    /// Pairs each <c>{folder}\Databases\SQLite\{project}\{db}.sql</c> with the live file it restores to: the roster
    /// entry of that project whose file stem matches the dump's, or the project's only file. A project can list
    /// several files, so matching on the project alone would replay every dump over its first. No match is null.
    /// </summary>
    public static IReadOnlyList<(string Project, string Dump, string? DatabaseFile)> MatchDumps(
        string folder, IReadOnlyList<SqliteBackupTarget> databases)
    {
        var root = Path.Combine(folder, SqliteBackup.DatabasesSubfolder, SqliteBackup.SqliteSubfolder);
        if (!Directory.Exists(root)) return [];

        var byProject = databases
            .GroupBy(t => t.Project, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var pairs = new List<(string, string, string?)>();
        foreach (var projectDir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var project = Path.GetFileName(projectDir);
            foreach (var dump in Directory.GetFiles(projectDir, "*.sql").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string? dbFile = null;
                if (byProject.TryGetValue(project, out var dbs))
                {
                    var stem = Path.GetFileNameWithoutExtension(dump);
                    dbFile = dbs.FirstOrDefault(t => string.Equals(Path.GetFileNameWithoutExtension(t.DatabaseFile), stem, StringComparison.OrdinalIgnoreCase)).DatabaseFile
                        ?? (dbs.Count == 1 ? dbs[0].DatabaseFile : null);
                }
                pairs.Add((project, dump, dbFile));
            }
        }
        return pairs;
    }

    /// <summary>
    /// The Azure SQL exports in the folder: every <c>.sql</c> or <c>.bak</c> under <c>Databases</c> but the SQLite
    /// dumps. Listed for the person to apply by hand; never applied here.
    /// </summary>
    public static IReadOnlyList<string> FindAzureSqlExports(string folder)
    {
        var root = Path.Combine(folder, AzureSqlExport.DatabasesSubfolder);
        if (!Directory.Exists(root)) return [];
        var sqliteRoot = Path.Combine(root, SqliteBackup.SqliteSubfolder) + Path.DirectorySeparatorChar;
        return Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.StartsWith(sqliteRoot, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
