using System.Diagnostics;

namespace MindAttic.Orchestrator.Backup;

/// <summary>One SQLite dump's restore outcome.</summary>
public sealed record SqliteRestoreResult(
    string Project, string DatabaseFile, bool Ok, int ExitCode, TimeSpan Elapsed, string DumpFile, string Output = "");

/// <summary>
/// Replays a <c>.sql</c> dump made by <see cref="SqliteBackup"/> into its database through sqlite3. A dump is a full
/// schema+data script, so the restore goes into a fresh file: the existing database is moved aside to
/// <c>{db}.pre-restore</c> first and put back if the restore fails or is cancelled, so a bad run never leaves neither.
/// Ported from MindAttic.Launcher's SqliteRestoreService (M.ORCH-T2049); the runner is injectable so tests never need
/// sqlite3.
/// </summary>
public sealed class SqliteRestore
{
    /// <summary>Runs sqlite3 against <c>dbFile</c> with <c>sql</c> piped to stdin; returns its exit code and output.</summary>
    public delegate (int ExitCode, string Output) SqliteRunner(string dbFile, string sql, CancellationToken ct);

    public string Executable { get; }
    private readonly SqliteRunner run;

    public SqliteRestore() : this("sqlite3") { }

    public SqliteRestore(string executable, SqliteRunner? runner = null)
    {
        Executable = executable;
        run = runner ?? DefaultRun;
    }

    public SqliteRestoreResult RestoreOne(string project, string dbFile, string dumpFile, CancellationToken ct = default)
    {
        if (!File.Exists(dumpFile))
            return new SqliteRestoreResult(project, dbFile, false, -1, TimeSpan.Zero, dumpFile, $"Dump file not found: {dumpFile}");

        var sw = Stopwatch.StartNew();
        var aside = dbFile + ".pre-restore";
        var movedAside = false;
        try
        {
            var sql = File.ReadAllText(dumpFile);
            if (File.Exists(dbFile))
            {
                File.Move(dbFile, aside, overwrite: true);
                movedAside = true;
            }
            var dir = Path.GetDirectoryName(dbFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var (code, output) = run(dbFile, sql, ct);
            sw.Stop();

            var ok = SqliteBackup.ComputeOk(code, ct.IsCancellationRequested);
            if (!ok)
            {
                // A failed replay can leave a half-built file; drop it so it can't pass for a restored database.
                try { if (File.Exists(dbFile)) File.Delete(dbFile); } catch { /* best-effort */ }
                PutBack(aside, dbFile, ref movedAside);
            }
            else if (movedAside)
            {
                try { File.Delete(aside); } catch { /* best-effort */ }
            }
            return new SqliteRestoreResult(project, dbFile, ok, code, sw.Elapsed, dumpFile, ok ? "" : BackupFolders.Tail(output, 2000));
        }
        catch (Exception ex)
        {
            sw.Stop();
            try { if (movedAside && File.Exists(dbFile)) File.Delete(dbFile); } catch { /* best-effort */ }
            PutBack(aside, dbFile, ref movedAside);
            var message = ex is System.ComponentModel.Win32Exception { NativeErrorCode: 2 }
                ? $"'{Executable}' not found on PATH; install the sqlite3 command-line tool."
                : ex.Message;
            return new SqliteRestoreResult(project, dbFile, false, -1, sw.Elapsed, dumpFile, message);
        }
    }

    private static void PutBack(string aside, string dbFile, ref bool movedAside)
    {
        if (!movedAside) return;
        try { File.Move(aside, dbFile, overwrite: true); movedAside = false; } catch { /* best-effort */ }
    }

    private (int, string) DefaultRun(string dbFile, string sql, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(dbFile);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {Executable}.");
        var stdoutTask = Task.Run(() => p.StandardOutput.ReadToEnd(), CancellationToken.None);
        var stderrTask = Task.Run(() => p.StandardError.ReadToEnd(), CancellationToken.None);
        p.StandardInput.Write(sql);
        p.StandardInput.Close();

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
        try { Task.WaitAll([stdoutTask, stderrTask], TimeSpan.FromSeconds(5)); } catch { }

        int code;
        try { code = p.ExitCode; }
        catch { code = -1; }

        var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "";
        var stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "";
        return (code, string.Join("\n", stderr, stdout).Trim());
    }
}
