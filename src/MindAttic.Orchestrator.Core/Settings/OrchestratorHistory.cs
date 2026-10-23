using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's growing history in <see cref="OrchestratorDb"/> (M.ORCH-T494), where it used to be JSON files under
/// <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>: Claude's meter readings (<c>usage_samples</c>, was <c>usage-samples.json</c>),
/// one row per 5-hour window (<c>pace_ledger</c>, was <c>pace-ledger.jsonl</c>), tokens per day per repo (<c>usage_history</c>,
/// was <c>usage-history.json</c>) and tokens per ticket (<c>ticket_costs</c>, was <c>ticket-costs.json</c>). Each write is one
/// transaction, so the daemon, the dashboard and a CLI run writing at once queue on SQLite's lock instead of saving over each
/// other's file. The old files are read in once by <see cref="ImportJson"/> and kept beside it as <c>.bak</c>.
/// </summary>
public static class OrchestratorHistory
{
    /// <summary>The JSON files the tables replace, as <see cref="ImportJson"/> finds them.</summary>
    public const string SamplesFile = "usage-samples.json", LedgerFile = "pace-ledger.jsonl", HistoryFile = "usage-history.json", CostsFile = "ticket-costs.json";

    /// <summary>How long meter readings are kept: a week, as the JSON file kept them (<see cref="ReportedLimit.Record"/> trims
    /// to it too). Enough to grade any window a daemon was down for; the ledger keeps each window's grade for good.</summary>
    public static readonly TimeSpan KeepSamples = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Json = new();

    // ---- usage_samples ----

    /// <summary>Every meter reading kept, oldest first; false (and none) when the database couldn't be read now.</summary>
    public static bool TryLoadUsageSamples(this OrchestratorDb db, out List<UsageSample> samples) =>
        Try(() => db.LoadUsageSamples(), out samples);

    public static List<UsageSample> LoadUsageSamples(this OrchestratorDb db)
    {
        using var connection = db.OpenData();
        return Sql.Read(connection, null, "SELECT at, \"percent\", reset, window_fresh FROM usage_samples ORDER BY at;",
            r => new UsageSample(Time(r.GetString(0)), Sql.Real(r, 1), Time(r.GetString(2)), Sql.Long(r, 3)))
            .OrderBy(s => s.AtUtc).ToList();
    }

    /// <summary>What the samples table holds now, cheaply (its row count and newest reading), so a reader rereads it only once
    /// another process has added or trimmed some; false when the database couldn't be read now.</summary>
    public static bool TryUsageSamplesStamp(this OrchestratorDb db, out (long Count, string Last) stamp) =>
        Try(() =>
        {
            using var connection = db.OpenData();
            return Sql.Read(connection, null, "SELECT count(*), COALESCE(max(at), '') FROM usage_samples;", r => (Sql.Long(r, 0), r.GetString(1))).Single();
        }, out stamp);

    /// <summary>Adds the readings not kept yet (a reading is keyed by its time) and drops those older than <paramref name="keepFromUtc"/>.</summary>
    public static void AddUsageSamples(this OrchestratorDb db, IEnumerable<UsageSample> samples, DateTime keepFromUtc) =>
        db.WriteData((connection, tx) =>
        {
            foreach (var s in samples) InsertSample(connection, tx, s);
            Sql.Run(connection, tx, "DELETE FROM usage_samples WHERE at < @from;", ("@from", Text(keepFromUtc)));
        });

    private static void InsertSample(DbConnection connection, DbTransaction tx, UsageSample s) =>
        Sql.InsertIfMissing(connection, tx, "usage_samples", ["at"],
            ("at", Text(s.AtUtc)), ("percent", s.Percent), ("reset", Text(s.ResetUtc)), ("window_fresh", s.WindowFresh));

    // ---- pace_ledger ----

    /// <summary>Every window the ledger holds, oldest first.</summary>
    public static List<LedgerWindow> LoadLedger(this OrchestratorDb db)
    {
        using var connection = db.OpenData();
        return Sql.Read(connection, null,
            "SELECT start_utc, end_utc, grade, why, peak_percent, final_percent, fresh, samples, minutes_at_target, limit_hit_utc, weekly_hit_utc, " +
            "held, held_minutes, refused, prompts_answered, gaps, exempt, account FROM pace_ledger ORDER BY end_utc;",
            r => new LedgerWindow(Time(r.GetString(0)), Time(r.GetString(1)), Enum.Parse<WindowGrade>(r.GetString(2)), r.GetString(3),
                Sql.OptionalReal(r, 4), Sql.OptionalReal(r, 5), Sql.Long(r, 6), Sql.Int(r, 7), Sql.Real(r, 8), OptionalTime(r, 9), OptionalTime(r, 10),
                Sql.Int(r, 11), Sql.Real(r, 12), Sql.Int(r, 13), Sql.Int(r, 14),
                JsonSerializer.Deserialize<List<MeterGap>>(r.GetString(15), Json) ?? [], Sql.Int(r, 16), Sql.OptionalText(r, 17)))
            .OrderBy(w => w.EndUtc).ToList();
    }

    /// <summary>Records <paramref name="windows"/> for good; a window already recorded (same end, same account) is left as it was.</summary>
    public static void AppendLedger(this OrchestratorDb db, IEnumerable<LedgerWindow> windows) =>
        db.WriteData((connection, tx) => { foreach (var w in windows) InsertWindow(connection, tx, w); });

    private static void InsertWindow(DbConnection connection, DbTransaction tx, LedgerWindow w) =>
        Sql.InsertIfMissing(connection, tx, "pace_ledger", ["end_utc", "account"],
            ("end_utc", Text(w.EndUtc)), ("account", w.Account), ("start_utc", Text(w.StartUtc)), ("grade", w.Grade.ToString()), ("why", w.Why),
            ("peak_percent", w.PeakPercent), ("final_percent", w.FinalPercent), ("fresh", w.Fresh), ("samples", w.Samples), ("minutes_at_target", w.MinutesAtTarget),
            ("limit_hit_utc", w.LimitHitUtc is { } l ? Text(l) : null), ("weekly_hit_utc", w.WeeklyHitUtc is { } h ? Text(h) : null), ("held", w.Held),
            ("held_minutes", w.HeldMinutes), ("refused", w.Refused), ("prompts_answered", w.PromptsAnswered), ("exempt", w.Exempt),
            ("gaps", JsonSerializer.Serialize(w.Gaps, Json)));

    // ---- usage_history ----

    /// <summary>The kept history in <see cref="UsageHistory.Order"/>; false (and none) when the database couldn't be read now, so a
    /// merge isn't saved over a year of it.</summary>
    public static bool TryLoadUsageHistory(this OrchestratorDb db, out List<DayUsage> days) => Try(() => db.LoadUsageHistory(), out days);

    public static List<DayUsage> LoadUsageHistory(this OrchestratorDb db)
    {
        using var connection = db.OpenData();
        return UsageHistory.Order(Sql.Read(connection, null, "SELECT day, repo, provider, fresh, account, before_utc FROM usage_history;",
            r => new DayUsage(DateOnly.ParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture), r.GetString(1), r.GetString(2),
                Sql.Long(r, 3), Sql.OptionalText(r, 4), OptionalTime(r, 5))));
    }

    /// <summary>Replaces the history with <paramref name="days"/> (a merge of what was loaded and what the transcripts show).</summary>
    public static void SaveUsageHistory(this OrchestratorDb db, IEnumerable<DayUsage> days) =>
        db.WriteData((connection, tx) =>
        {
            Sql.Run(connection, tx, "DELETE FROM usage_history;");
            foreach (var d in days) InsertDay(connection, tx, d, replace: true);
        });

    private static readonly string[] DayKeys = ["day", "repo", "provider", "account", "before_utc"];

    private static void InsertDay(DbConnection connection, DbTransaction tx, DayUsage d, bool replace)
    {
        (string, object?)[] row =
        [
            ("day", d.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("repo", d.Repo), ("provider", d.Provider), ("account", d.Account),
            ("before_utc", d.BeforeUtc is { } b ? Text(b) : null), ("fresh", d.Fresh),
        ];
        if (replace) Sql.Upsert(connection, tx, "usage_history", DayKeys, row);
        else Sql.InsertIfMissing(connection, tx, "usage_history", DayKeys, row);
    }

    // ---- ticket_costs ----

    /// <summary>Every session's cost, oldest first; false (and none) when the database couldn't be read now.</summary>
    public static bool TryLoadTicketCosts(this OrchestratorDb db, out List<SessionCost> costs) => Try(() => db.LoadTicketCosts(), out costs);

    public static List<SessionCost> LoadTicketCosts(this OrchestratorDb db)
    {
        using var connection = db.OpenData();
        return Sql.Read(connection, null, "SELECT session_id, ticket_id, type, repo, fresh, last_utc, own_share, model FROM ticket_costs;",
            r => new SessionCost(r.GetString(0), r.GetString(1), Sql.OptionalText(r, 2), r.GetString(3), Sql.Long(r, 4),
                Time(r.GetString(5)), Sql.Long(r, 6) != 0, Sql.OptionalText(r, 7)))
            .OrderBy(s => s.LastUtc).ThenBy(s => s.SessionId, StringComparer.Ordinal).ToList();
    }

    /// <summary>Replaces the costs with <paramref name="costs"/> (<see cref="TicketCosts.Merge"/>'s result).</summary>
    public static void SaveTicketCosts(this OrchestratorDb db, IEnumerable<SessionCost> costs) =>
        db.WriteData((connection, tx) =>
        {
            Sql.Run(connection, tx, "DELETE FROM ticket_costs;");
            foreach (var c in costs) InsertCost(connection, tx, c, replace: true);
        });

    private static void InsertCost(DbConnection connection, DbTransaction tx, SessionCost c, bool replace)
    {
        (string, object?)[] row =
        [
            ("session_id", c.SessionId), ("ticket_id", c.TicketId), ("type", c.Type), ("repo", c.Repo), ("fresh", c.Fresh), ("last_utc", Text(c.LastUtc)),
            ("own_share", c.OwnShare ? 1L : 0L), ("model", c.Model),
        ];
        if (replace) Sql.Upsert(connection, tx, "ticket_costs", ["session_id"], row);
        else Sql.InsertIfMissing(connection, tx, "ticket_costs", ["session_id"], row);
    }

    // ---- gate_events (M.ORCH-T526) ----

    /// <summary>The pace gate's events since <paramref name="sinceUtc"/>, oldest first.</summary>
    public static List<GateEvent> LoadGateEvents(this OrchestratorDb db, DateTime sinceUtc)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT at, decision, held, tool FROM gate_events WHERE at >= $since ORDER BY at, id;";
        command.Parameters.AddWithValue("$since", Second(sinceUtc));
        using var r = command.ExecuteReader();
        var events = new List<GateEvent>();
        while (r.Read())
        {
            if (!DateTime.TryParse(r.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ||
                at < sinceUtc) continue;
            events.Add(new GateEvent(at, r.GetString(1), r.GetDouble(2), r.IsDBNull(3) ? null : r.GetString(3)));
        }
        return events;
    }

    /// <summary>Adds events as the hooks add them (tests, and the old log's lines as they're read in).</summary>
    public static void AddGateEvents(this OrchestratorDb db, IEnumerable<(GateEvent Event, double? Percent)> events) =>
        db.Write((connection, tx) => { foreach (var (e, percent) in events) InsertEvent(connection, tx, e, percent); }, dump: false);

    /// <summary>Drops the events older than <paramref name="keepFromUtc"/> once they're counted into the ledger.</summary>
    public static void TrimGateEvents(this OrchestratorDb db, DateTime keepFromUtc) =>
        db.Write((connection, tx) => Run(connection, tx, "DELETE FROM gate_events WHERE at < $from;", ("$from", Second(keepFromUtc))), dump: false);

    private static void InsertEvent(SqliteConnection connection, SqliteTransaction tx, GateEvent e, double? percent) =>
        Run(connection, tx, "INSERT INTO gate_events (at, decision, held, tool, percent) VALUES ($at, $decision, $held, $tool, $percent);",
            ("$at", Second(e.AtUtc)), ("$decision", e.Decision), ("$held", e.HeldSeconds), ("$tool", e.Tool), ("$percent", percent));

    /// <summary>A time as the hooks write it ("2026-10-15T20:00:00Z"), so the rows sort and compare as text.</summary>
    private static string Second(DateTime utc) =>
        (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the pace gate's old event log (<c>pace-gate.jsonl</c>) into <c>gate_events</c> and appends its lines to
    /// <c>pace-gate.jsonl.bak</c>. Not only once: a hook that can't write the database right now (held past its short wait, or
    /// one from before the table) appends to the file as before, so whatever is there is read in on the next tick. The file is
    /// renamed before it's read, so a line a hook appends meanwhile goes to a new file rather than being lost or read twice; a
    /// file a hook has open right now is left for next time. A rename whose rows didn't go in is read in first the next time.
    /// </summary>
    public static void ImportGateLog(this OrchestratorDb db, string path)
    {
        var taking = path + ".importing";
        if (!File.Exists(taking))
        {
            if (!File.Exists(path)) return;
            try { File.Move(path, taking); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        }
        // Read and inserted under the write lock, with the file's hash kept beside its rows (M.ORCH-T537): a second process that
        // found the same .importing while this one held the lock finds it gone or already in, and a file whose rows went in
        // but whose .bak append or delete failed is only finished next time, never inserted again.
        List<string>? lines = null;
        db.Write((connection, tx) =>
        {
            if (!File.Exists(taking)) return;
            lines = File.ReadAllLines(taking).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines))));
            using (var read = connection.CreateCommand())
            {
                read.Transaction = tx;
                read.CommandText = "SELECT value FROM state WHERE name = $name;";
                read.Parameters.AddWithValue("$name", GateLogImportedName);
                if (read.ExecuteScalar() as string == hash) return;
            }
            foreach (var line in lines)
                if (ParseEvent(line) is { } e) InsertEvent(connection, tx, e.Event, e.Percent);
            using var mark = connection.CreateCommand();
            mark.Transaction = tx;
            mark.CommandText = "INSERT OR REPLACE INTO state (name, value, written_at) VALUES ($name, $value, $at);";
            mark.Parameters.AddWithValue("$name", GateLogImportedName);
            mark.Parameters.AddWithValue("$value", hash);
            mark.Parameters.AddWithValue("$at", (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds);
            mark.ExecuteNonQuery();
        }, dump: false);
        if (lines is null) return;
        File.AppendAllLines(path + ".bak", lines);
        File.Delete(taking);
    }

    /// <summary>The <c>state</c> row holding the hash of the last <c>.importing</c> file read into <c>gate_events</c>.</summary>
    internal const string GateLogImportedName = "pace-gate-imported";

    /// <summary>One line of the old log; null when it isn't one (a torn line, a hook killed mid-write).</summary>
    private static (GateEvent Event, double? Percent)? ParseEvent(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var r = doc.RootElement;
            if (!r.TryGetProperty("at", out var a) || a.ValueKind != JsonValueKind.String || !DateTime.TryParse(a.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at)) return null;
            return (new GateEvent(at,
                r.TryGetProperty("decision", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()! : "",
                r.TryGetProperty("held", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDouble() : 0,
                r.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null),
                r.TryGetProperty("percent", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null);
        }
        catch (JsonException) { return null; }
    }

    // ---- the one-time import ----

    private static readonly ConcurrentDictionary<string, bool> Imported = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the old JSON history files in <paramref name="jsonDirectory"/> into the tables once, then renames each to
    /// <c>&lt;name&gt;.bak</c> beside it. Cheap after the first call in a process (and when there are no files). Every row goes in
    /// with INSERT OR IGNORE under its key, in one transaction, so two processes starting at once, or a rename that failed and
    /// is tried again, can't add a row twice, and a row the database already has wins over the file's. A file that can't be
    /// read now (held) is left for the next run; one that isn't readable JSON is kept as .bak with nothing taken from it.
    /// The live files under <c>%APPDATA%</c> go only into the live database: with <see cref="OrchestratorDb.EnvVar"/> set
    /// (tests, a scratch database) they're left where they are.
    /// </summary>
    public static void ImportJson(this OrchestratorDb db, string jsonDirectory)
    {
        var key = db.Path + "|" + Path.GetFullPath(jsonDirectory);
        if (Imported.ContainsKey(key)) return;
        if (Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar) is { Length: > 0 } &&
            string.Equals(Path.GetFullPath(jsonDirectory), Path.GetFullPath(JsonStore.LiveDirectory), StringComparison.OrdinalIgnoreCase))
        {
            Imported[key] = true;
            return;
        }
        string[] names = [SamplesFile, LedgerFile, HistoryFile, CostsFile];
        if (!names.Any(n => File.Exists(Path.Combine(jsonDirectory, n))))
        {
            Imported[key] = true;
            return;
        }

        var done = new List<string>();
        var held = false;
        db.WriteData((connection, tx) =>
        {
            // Read under the write lock: a process that got here first and already renamed the file leaves nothing to read.
            if (Take(SamplesFile, () => ReadList<UsageSample>(SamplesFile)) is { } samples)
                foreach (var s in samples) InsertSample(connection, tx, s);
            if (Take(LedgerFile, () => PaceLedger.Load(Path.Combine(jsonDirectory, LedgerFile))) is { } ledger)
                foreach (var w in ledger) InsertWindow(connection, tx, w);
            if (Take(HistoryFile, () => ReadList<DayUsage>(HistoryFile)) is { } history)
                foreach (var d in history) InsertDay(connection, tx, d, replace: false);
            if (Take(CostsFile, () => ReadList<SessionCost>(CostsFile)) is { } costs)
                foreach (var c in costs) InsertCost(connection, tx, c, replace: false);
        });
        foreach (var name in done)
        {
            var path = Path.Combine(jsonDirectory, name);
            try { File.Move(path, path + ".bak", overwrite: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { held = true; }
        }
        if (!held) Imported[key] = true;

        // A JSON file's rows; null when it isn't readable JSON (held, it throws).
        List<T>? ReadList<T>(string name)
        {
            try { return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(jsonDirectory, name)), Json) ?? []; }
            catch (JsonException) { return null; }
        }

        // The file's rows, or null when it isn't there (taken already) or couldn't be read now. A file whose JSON can't be
        // parsed reads as null from TryLoad too, so that's told apart by whether it can be opened at all.
        List<T>? Take<T>(string name, Func<List<T>?> load)
        {
            var path = Path.Combine(jsonDirectory, name);
            if (!File.Exists(path)) return null;
            List<T>? rows;
            try { rows = load(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { held = true; return null; }
            if (rows is null && !Unparsable(path)) { held = true; return null; }
            done.Add(name);
            return rows ?? [];
        }
    }

    /// <summary>Whether <paramref name="path"/> opens (so a failed load was its contents, not a lock).</summary>
    private static bool Unparsable(string path)
    {
        try { using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // ---- helpers ----

    /// <summary>A UTC time as the tables keep it: ISO-8601, to the tick, trailing zeros dropped ("2026-10-18T02:13:34Z").</summary>
    internal static string Text(DateTime utc) =>
        (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc).ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);

    internal static DateTime Time(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static DateTime? OptionalTime(DbDataReader r, int i) => r.IsDBNull(i) ? null : Time(r.GetString(i));


    private static bool Try<T>(Func<T> read, out T value) where T : new()
    {
        try
        {
            value = read();
            return true;
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            value = new T();
            return false;
        }
    }

    private static List<T> Read<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> row)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var r = command.ExecuteReader();
        var rows = new List<T>();
        while (r.Read()) rows.Add(row(r));
        return rows;
    }

    private static void Run(SqliteConnection connection, SqliteTransaction tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
