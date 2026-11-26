using System.Diagnostics;

namespace MindAttic.Orchestrator.Backup;

/// <summary>One SQLite database's <c>.dump</c> outcome.</summary>
public sealed record SqliteBackupResult(
    string Project, string DatabaseFile, bool Ok, int ExitCode, TimeSpan Elapsed, string BackupFile, string Output = "");

/// <summary>A (project, database file) pair to back up. Value-equal so duplicates dedupe.</summary>
public readonly record struct SqliteBackupTarget(string Project, string DatabaseFile);

/// <summary>
/// A full <c>sqlite3 .dump</c> (schema and data as SQL text) of each database into
/// <c>{targetFolder}\Databases\SQLite\{project}\{db}.sql</c>: an engine-verified snapshot rather than the raw
/// <c>.db</c> file, which can be mid-write or WAL-split. Ported from MindAttic.Launcher's SqliteBackupService
/// (M.ORCH-T2046); the path, argument and exit-code rules are pure and the process runner is injectable, so
/// tests never need sqlite3 or a real database.
/// </summary>
public sealed class SqliteBackup
{
    public const string DatabasesSubfolder = "Databases";
    public const string SqliteSubfolder = "SQLite";

    /// <summary>
    /// <c>{targetFolder}\Databases\SQLite\sources.tsv</c>: one <c>{dump path relative to the SQLite folder}\t{database
    /// file}</c> line per dump written, so a restore puts a numbered dump (<c>app-2.sql</c>) back where it came from.
    /// </summary>
    public const string SourcesFileName = "sources.tsv";

    public static string ResolveSourcesFilePath(string targetFolder) =>
        Path.Combine(targetFolder, DatabasesSubfolder, SqliteSubfolder, SourcesFileName);

    /// <summary>Reads <see cref="SourcesFileName"/> into dump path (full) to database file; empty when it's missing (an older backup).</summary>
    public static IReadOnlyDictionary<string, string> ReadSources(string targetFolder)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var file = ResolveSourcesFilePath(targetFolder);
        if (!File.Exists(file)) return map;
        var root = Path.GetDirectoryName(file)!;
        foreach (var line in File.ReadAllLines(file))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0 || tab == line.Length - 1) continue;
            map[Path.GetFullPath(Path.Combine(root, line[..tab]))] = line[(tab + 1)..];
        }
        return map;
    }

    /// <summary>Runs sqlite3 with the given arguments; returns its exit code and, on success, the dump text.</summary>
    public delegate (int ExitCode, string Output) SqliteRunner(IReadOnlyList<string> args, CancellationToken ct);

    public string Executable { get; }
    private readonly SqliteRunner run;

    public SqliteBackup() : this("sqlite3") { }

    public SqliteBackup(string executable, SqliteRunner? runner = null)
    {
        Executable = executable;
        run = runner ?? DefaultRun;
    }

    /// <summary>
    /// Flattens (project, project folder, database entries) into the distinct database files to back up: blank
    /// entries drop, relative ones resolve against the project folder, and one file named twice backs up once.
    /// </summary>
    public static IReadOnlyList<SqliteBackupTarget> CollectTargets(
        IEnumerable<(string Project, string ProjectPath, IEnumerable<string>? Databases)> projects)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<SqliteBackupTarget>();
        foreach (var (project, projectPath, databases) in projects)
            foreach (var raw in databases ?? [])
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var resolved = ResolveDatabasePath(projectPath, raw);
                if (seen.Add(resolved)) targets.Add(new SqliteBackupTarget(project, resolved));
            }
        return targets;
    }

    /// <summary>Absolute paths pass through; a relative one resolves against the project's folder.</summary>
    public static string ResolveDatabasePath(string projectPath, string raw)
    {
        var trimmed = raw.Trim();
        return Path.IsPathRooted(trimmed) ? trimmed : Path.GetFullPath(Path.Combine(projectPath, trimmed));
    }

    /// <summary>
    /// The .sql path for a (project, database) pair under the dated folder. The project is part of the path so
    /// two projects' same-named <c>app.db</c> land in distinct files; path-illegal characters become <c>_</c>.
    /// </summary>
    public static string ResolveBackupFilePath(string targetFolder, string project, string databaseFile) =>
        Path.Combine(targetFolder, DatabasesSubfolder, SqliteSubfolder, SanitizeFileName(project),
            Path.GetFileNameWithoutExtension(databaseFile) + ".sql");

    private static string SanitizeFileName(string name)
    {
        var trimmed = name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            trimmed = trimmed.Replace(c, '_');
        return string.IsNullOrEmpty(trimmed) ? "project" : trimmed;
    }

    /// <summary><c>-readonly</c> so sqlite3 never creates or modifies the source (a bad path would otherwise make an empty db).</summary>
    public static IReadOnlyList<string> BuildArguments(string databaseFile) => ["-readonly", databaseFile, ".dump"];

    /// <summary>sqlite3 exits 0 on success; non-zero or a cancellation is a failure.</summary>
    public static bool ComputeOk(int exitCode, bool cancelled) => !cancelled && exitCode == 0;

    /// <summary>
    /// Backs up each target in order into <paramref name="targetFolder"/>. One bad database is recorded in its
    /// result and doesn't stop the rest; cancellation stops before the next. Two databases of one project that
    /// map to the same .sql get <c>-2</c>, <c>-3</c>... rather than overwriting each other; <see cref="SourcesFileName"/>
    /// records which database each dump came from.
    /// </summary>
    public IReadOnlyList<SqliteBackupResult> Backup(
        IReadOnlyList<SqliteBackupTarget> targets,
        string targetFolder,
        Action<SqliteBackupResult>? onDone = null,
        CancellationToken ct = default)
    {
        var results = new List<SqliteBackupResult>();
        var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in targets)
        {
            if (ct.IsCancellationRequested) break;
            var first = ResolveBackupFilePath(targetFolder, t.Project, t.DatabaseFile);
            var file = first;
            for (var n = 2; !usedFiles.Add(file); n++)
                file = Path.ChangeExtension(first, null) + $"-{n}.sql";
            var r = BackupOne(t, targetFolder, ct, file);
            results.Add(r);
            onDone?.Invoke(r);
        }
        var written = results.Where(r => r.Ok).ToList();
        if (written.Count > 0)
        {
            var sources = ResolveSourcesFilePath(targetFolder);
            var root = Path.GetDirectoryName(sources)!;
            try { File.WriteAllLines(sources, written.Select(r => Path.GetRelativePath(root, r.BackupFile) + "\t" + r.DatabaseFile)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* restore falls back to matching by file name */ }
        }
        return results;
    }

    public SqliteBackupResult BackupOne(SqliteBackupTarget target, string targetFolder, CancellationToken ct = default, string? backupFile = null)
    {
        var file = backupFile ?? ResolveBackupFilePath(targetFolder, target.Project, target.DatabaseFile);

        if (!File.Exists(target.DatabaseFile))
            return new SqliteBackupResult(target.Project, target.DatabaseFile, false, -1, TimeSpan.Zero, file,
                $"Database file not found: {target.DatabaseFile}");

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var sw = Stopwatch.StartNew();
        int code;
        string output;
        var cancelled = false;
        try
        {
            (code, output) = run(BuildArguments(target.DatabaseFile), ct);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            code = -1;
            output = "cancelled";
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            code = -1;
            output = $"'{Executable}' not found on PATH; install the sqlite3 command-line tool.";
        }
        catch (Exception ex)
        {
            code = -1;
            output = ex.Message;
        }
        sw.Stop();

        var ok = ComputeOk(code, cancelled || ct.IsCancellationRequested);
        if (ok && string.IsNullOrWhiteSpace(output))
        {
            // Even an empty database dumps BEGIN/COMMIT; nothing at all means the dump was lost (M.ORCH-T2296).
            ok = false;
            code = -1;
            output = "sqlite3 exited 0 but produced an empty dump.";
        }
        if (ok)
        {
            try { File.WriteAllText(file, output); }
            catch (Exception ex)
            {
                ok = false;
                code = -1;
                output = ex.Message;
            }
        }
        if (!ok)
        {
            // A failed or cancelled dump can leave a partial .sql; don't let it pass for a restorable backup.
            try { if (File.Exists(file)) File.Delete(file); } catch { /* best-effort */ }
        }

        return new SqliteBackupResult(
            target.Project, target.DatabaseFile, ok, code, sw.Elapsed, file, ok ? "" : BackupFolders.Tail(output, 2000));
    }

    private (int, string) DefaultRun(IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {Executable}.");
        var stdoutTask = Task.Run(() => p.StandardOutput.ReadToEnd(), CancellationToken.None);
        var stderrTask = Task.Run(() => p.StandardError.ReadToEnd(), CancellationToken.None);

        while (!p.HasExited)
        {
            if (ct.IsCancellationRequested)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                break;
            }
            p.WaitForExit(250);
        }

        try { p.WaitForExit(5000); } catch { }
        if (ct.IsCancellationRequested)
        {
            try { Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(5)); } catch { }
            throw new OperationCanceledException(ct);
        }
        // The dump is all of stdout: wait for the reader to finish, however long a big database takes (M.ORCH-T2296).
        try { p.WaitForExit(); Task.WaitAll(stdoutTask, stderrTask); } catch { }

        int code;
        try { code = p.ExitCode; }
        catch { code = -1; }

        var stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "";
        var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "";
        // On success stdout is the dump itself; keep stderr out of it so a stray warning isn't written into the .sql.
        return (code, code == 0 ? stdout : string.Join("\n", stderr, stdout).Trim());
    }
}
