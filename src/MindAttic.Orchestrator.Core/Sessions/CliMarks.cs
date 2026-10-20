using Microsoft.Data.Sqlite;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Each CLI's /allocate, /allocate off and /crossrepo marks (M.ORCH-T528): rows of the orchestrator database's
/// <c>cli_marks</c> (schema step 6), one per kind and CLI process, which the hooks write through stdlib sqlite3. While the
/// database is held, missing or from before the table a hook writes the old <c>&lt;kind&gt;-&lt;pid&gt;</c> file instead
/// (a removal leaves <c>&lt;kind&gt;-&lt;pid&gt;.cleared</c>); every reader takes such a file over the row, and the daemon
/// reads them in (<see cref="ReadIn"/>), keeping each as .bak.
/// </summary>
public static class CliMarks
{
    public const string Autopilot = "autopilot";
    public const string NoAutopilot = "noautopilot";
    public const string Crossrepo = "crossrepo";
    public static readonly string[] Kinds = [Autopilot, NoAutopilot, Crossrepo];

    public const string ClearedSuffix = ".cleared";

    /// <summary>The database the marks are kept in; replaceable for tests.</summary>
    internal static Func<OrchestratorDb> Db { get; set; } = () => new OrchestratorDb(OrchestratorDb.StatePath(JsonStore.LiveDirectory));

    /// <summary>Every CLI's mark of <paramref name="kind"/> (by process id, its text): the rows, with the files in
    /// <paramref name="directory"/> over them. The files are listed first, so one read in meanwhile is in the rows.</summary>
    public static IReadOnlyDictionary<int, string> Read(string directory, string kind)
    {
        var files = new Dictionary<int, string>();
        var cleared = new HashSet<int>();
        if (Directory.Exists(directory))
            foreach (var f in Directory.EnumerateFiles(directory, kind + "-*"))
            {
                var rest = Path.GetFileName(f)[(kind.Length + 1)..];
                if (IsPid(rest, out var pid)) { if (ReadText(f) is { } text) files[pid] = text; }
                else if (rest.EndsWith(ClearedSuffix, StringComparison.Ordinal) && IsPid(rest[..^ClearedSuffix.Length], out pid)) cleared.Add(pid);
            }
        var marks = new Dictionary<int, string>();
        foreach (var (pid, value) in Rows(kind))
            if (!cleared.Contains(pid)) marks[pid] = value;
        foreach (var (pid, value) in files) marks[pid] = value;
        return marks;
    }

    /// <summary>The rows of <paramref name="kind"/>; none when the database isn't there (a read never creates it), is held past
    /// its busy timeout, or is older.</summary>
    private static List<(int Pid, string Value)> Rows(string kind)
    {
        var rows = new List<(int, string)>();
        try
        {
            var db = Db();
            if (!File.Exists(db.Path)) return rows;
            using var connection = db.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pid, value FROM cli_marks WHERE kind = $kind;";
            command.Parameters.AddWithValue("$kind", kind);
            using var r = command.ExecuteReader();
            while (r.Read()) rows.Add(((int)r.GetInt64(0), r.GetString(1)));
        }
        catch (Exception ex) when (ex is SqliteException or IOException) { }
        return rows;
    }

    /// <summary>
    /// Reads every mark file in <paramref name="directory"/> into its row (a <c>.cleared</c> one removes the row), keeping each
    /// as .bak. The row is committed before the file is moved aside (M.ORCH-T540), so a reader always finds one or the other;
    /// a file a hook rewrote between the read and the move is put back for the next time. While the database can't take it,
    /// the file stays where it is. Nothing happens without a database: a read never creates it.
    /// </summary>
    public static void ReadIn(string directory)
    {
        if (!Directory.Exists(directory)) return;
        var db = Db();
        if (!File.Exists(db.Path)) return;
        foreach (var kind in Kinds)
            foreach (var f in Directory.EnumerateFiles(directory, kind + "-*").ToList())
            {
                var rest = Path.GetFileName(f)[(kind.Length + 1)..];
                var clear = rest.EndsWith(ClearedSuffix, StringComparison.Ordinal);
                if (!IsPid(clear ? rest[..^ClearedSuffix.Length] : rest, out var pid)) continue;
                var claim = $"{f}.{Environment.ProcessId}.reading";
                string text;
                try
                {
                    text = File.ReadAllText(f);
                    db.Write((connection, tx) =>
                    {
                        using var command = connection.CreateCommand();
                        command.Transaction = tx;
                        command.CommandText = clear ? "DELETE FROM cli_marks WHERE kind = $kind AND pid = $pid;"
                            : "INSERT OR REPLACE INTO cli_marks (kind, pid, value, written_at) VALUES ($kind, $pid, $value, $at);";
                        command.Parameters.AddWithValue("$kind", kind);
                        command.Parameters.AddWithValue("$pid", pid);
                        command.Parameters.AddWithValue("$value", text);
                        command.Parameters.AddWithValue("$at", (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds);
                        command.ExecuteNonQuery();
                    }, dump: false);
                }
                catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException) { continue; }
                try
                {
                    File.Move(f, claim);
                    // A hook wrote it again after the read: that newer mark goes back to be read in next time.
                    if (!clear && File.ReadAllText(claim) != text) { if (!File.Exists(f)) File.Move(claim, f); else File.Delete(claim); }
                    else if (clear) File.Delete(claim);
                    else File.Move(claim, f + ".bak", overwrite: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    try { if (File.Exists(claim)) { if (!File.Exists(f)) File.Move(claim, f); else File.Delete(claim); } }
                    catch (Exception again) when (again is IOException or UnauthorizedAccessException) { }
                }
            }
    }

    /// <summary>Drops every row of a CLI process that <paramref name="alive"/> says has closed.</summary>
    public static void Prune(Func<int, bool> alive)
    {
        try
        {
            var db = Db();
            if (!File.Exists(db.Path)) return;
            var dead = Kinds.SelectMany(k => Rows(k).Select(r => r.Pid)).Distinct().Where(p => !alive(p)).ToList();
            if (dead.Count == 0) return;
            db.Write((connection, tx) =>
            {
                foreach (var pid in dead)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = tx;
                    command.CommandText = "DELETE FROM cli_marks WHERE pid = $pid;";
                    command.Parameters.AddWithValue("$pid", pid);
                    command.ExecuteNonQuery();
                }
            }, dump: false);
        }
        catch (Exception ex) when (ex is SqliteException or IOException) { }
    }

    private static bool IsPid(string text, out int pid) =>
        int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out pid) && pid > 0;

    private static string? ReadText(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}
