using Microsoft.Data.Sqlite;
using MindAttic.Log;
using MindAttic.Log.Extensions;
using MindAttic.Log.Migration;
using MindAttic.Vault.Paths;
using Serilog;
using Serilog.Core;

namespace MindAttic.Orchestrator;

/// <summary>
/// Wires MindAttic.Log, the workspace's one logging schema, for every orchestrator process: its own
/// standalone <c>MindAttic.Log.db</c> under Vault's shared roaming root (Phase 2, MindAttic.Log
/// 3.0.0 — see its docs/BIBLE.md §1.1), since the orchestrator has no database of its own. The
/// daemon, the dashboard and one-off CLI runs share the file (the sink waits out other writers'
/// locks), and each entry is tagged with its <c>Component</c>. Browse it with MindAttic.Log.Reader.
/// </summary>
public static class OrchestratorLog
{
    public const string Application = "MindAttic.Orchestrator";

    /// <summary>Phase 1's rolled-file directory — kept only so <see cref="Start"/> can migrate any
    /// history left there into the new standalone database.</summary>
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", Application, "logs");

    /// <summary>Points the static <see cref="Serilog.Log"/> at MindAttic.Log. Call once, at the entry point.</summary>
    /// <summary>The log database every orchestrator process writes (open it with MindAttic.Log.Reader).</summary>
    public static string DbPath => VaultPaths.LoggingDbPath(Application);

    public static void Start(string component)
    {
        var dbPath = DbPath;

        // Phase 2: carry forward any rows left in Phase 1's rolled MindAttic.Log.<yyyy-MM>.db
        // files (LogFileRoller is retired), then remove the now-empty rolled files. Idempotent and
        // a clean no-op on every later run.
        if (System.IO.Directory.Exists(Directory))
        {
            foreach (var rolledFile in System.IO.Directory.GetFiles(Directory, "MindAttic.Log.*.db"))
            {
                MindAtticLogMigrator.MigrateFromSqliteAsync(rolledFile, dbPath, dropSource: true)
                    .GetAwaiter().GetResult();
                SqliteConnection.ClearAllPools();
                try { File.Delete(rolledFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        // Fully qualified: inside MindAttic.Orchestrator, a bare "Log" means the MindAttic.Log namespace.
        Serilog.Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Component", component)
            .WriteToMindAtticLog(new MindAtticLogOptions
            {
                Application = Application,
                DbPath = dbPath,
            })
            .CreateLogger();
    }

    /// <summary>A logger whose entries land in the log's Category column as <paramref name="category"/>.</summary>
    public static ILogger For(string category) => Serilog.Log.ForContext(Constants.SourceContextPropertyName, category);

    /// <summary>Deletes log entries older than <paramref name="keepDays"/> (the library's own retention is a fixed 365 days, so
    /// the Vault setting <c>LogKeepDays</c> trims it shorter; M.ORCH-T1611). Returns rows deleted; 0 when keepDays is 0 or the
    /// database is busy or missing. Freed pages are reused by later inserts, so the file stops growing.</summary>
    public static int Prune(int keepDays, DateTime nowUtc)
    {
        if (keepDays <= 0 || !File.Exists(DbPath)) return 0;
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath};Pooling=False");
            connection.Open();
            using var busy = connection.CreateCommand();
            busy.CommandText = "PRAGMA busy_timeout = 5000;";
            busy.ExecuteNonQuery();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM MindAttic_Log WHERE TimestampUtc < $cut;";
            cmd.Parameters.AddWithValue("$cut", nowUtc.AddDays(-Math.Min(keepDays, 36500)).ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException) { return 0; }
    }

    public static void Stop() => Serilog.Log.CloseAndFlush();
}
