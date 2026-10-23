using Microsoft.Data.Sqlite;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Each session's state as the context gate keeps it (M.ORCH-T528): rows of the orchestrator database's <c>session_states</c>
/// (schema step 7), still exported as <c>&lt;session&gt;.state.json</c>, which the Launcher's ClaudeRestartGate and
/// <see cref="ContextCycler"/> read. The context gate writes the row and then the file, and the file alone while the
/// database is held, so the file is never older than the row; the daemon reads in a file whose row is missing or older
/// (<see cref="ReadIn"/>), the first time keeping a copy as .bak.
/// </summary>
public static class SessionStates
{
    public const string Suffix = ".state.json";

    /// <summary>The database the states are kept in; replaceable for tests.</summary>
    internal static Func<OrchestratorDb> Db { get; set; } = () => new OrchestratorDb(OrchestratorDb.StatePath(JsonStore.LiveDirectory));

    /// <summary>The rows: each session's state and when it was written.</summary>
    public static IReadOnlyDictionary<string, (string Value, DateTime WrittenUtc)> Rows()
    {
        var rows = new Dictionary<string, (string, DateTime)>(StringComparer.Ordinal);
        try
        {
            var db = Db();
            if (!File.Exists(db.Path)) return rows;
            using var connection = db.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT session_id, value, written_at FROM session_states;";
            using var r = command.ExecuteReader();
            while (r.Read()) rows[r.GetString(0)] = (r.GetString(1), DateTime.UnixEpoch.AddTicks((long)(r.GetDouble(2) * TimeSpan.TicksPerSecond)));
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException) { }
        return rows;
    }

    /// <summary>Reads every <c>.state.json</c> in <paramref name="directory"/> whose row is missing or older into its row (one
    /// write transaction), a copy of a file read in for the first time kept as .bak. Nothing happens without a database.</summary>
    public static void ReadIn(string directory)
    {
        if (!Directory.Exists(directory)) return;
        var db = Db();
        if (!File.Exists(db.Path)) return;
        var rows = Rows();
        var due = new List<(string Session, string Text, DateTime At, bool First)>();
        foreach (var f in Directory.EnumerateFiles(directory, "*" + Suffix))
        {
            var session = Path.GetFileName(f)[..^Suffix.Length];
            try
            {
                var at = File.GetLastWriteTimeUtc(f);
                var had = rows.TryGetValue(session, out var row);
                if (had && row.WrittenUtc >= at) continue;
                // The gate's export, a moment after its row: the same state, nothing to read in.
                var text = File.ReadAllText(f);
                if (had && row.Value == text) continue;
                due.Add((session, text, at, !had));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        if (due.Count == 0) return;
        try
        {
            db.Write((connection, tx) =>
            {
                foreach (var (session, text, at, _) in due)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = tx;
                    command.CommandText = "INSERT OR REPLACE INTO session_states (session_id, value, written_at) VALUES ($id, $value, $at);";
                    command.Parameters.AddWithValue("$id", session);
                    command.Parameters.AddWithValue("$value", text);
                    command.Parameters.AddWithValue("$at", (at - DateTime.UnixEpoch).TotalSeconds);
                    command.ExecuteNonQuery();
                }
            }, dump: false);
        }
        catch (System.Data.Common.DbException) { return; } // held: the next tick
        foreach (var (session, _, _, first) in due.Where(d => d.First))
        {
            var f = Path.Combine(directory, session + Suffix);
            try { if (!File.Exists(f + ".bak")) File.Copy(f, f + ".bak"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Drops the rows of sessions not heard from since <paramref name="beforeUtc"/>, as their files are.</summary>
    public static void Prune(DateTime beforeUtc)
    {
        try
        {
            var db = Db();
            if (!File.Exists(db.Path)) return;
            db.Write((connection, tx) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = tx;
                command.CommandText = "DELETE FROM session_states WHERE written_at < $before;";
                command.Parameters.AddWithValue("$before", (beforeUtc - DateTime.UnixEpoch).TotalSeconds);
                command.ExecuteNonQuery();
            }, dump: false);
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException) { }
    }
}
