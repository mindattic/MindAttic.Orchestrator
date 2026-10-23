using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's settings and small state, kept in <see cref="OrchestratorDb"/> (M.ORCH-T524) where they used to be JSON
/// files under <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>. Each is still named as its file was and handled as a JSON
/// value, so callers load and save the same types: <c>settings</c> goes into the <c>settings</c> table one row per setting,
/// <c>assignments</c> into <c>assignments</c> one row per assignment, and every other name (limits, reminders, audit, the pace
/// alarms, limit wakes, autopilot holds, stale claims, dashboard-seen) is one row of <c>state</c>. A change is one write
/// transaction, so the daemon, a dashboard and the CLI changing the same thing at once queue on SQLite's lock instead of a
/// <c>.lock</c> file, and none saves over what another added. A JSON file still in <see cref="Directory"/> is read in the first
/// time its name is used (unless the database already holds it) and kept beside it as <c>.bak</c>.
/// </summary>
public sealed class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A setting's value as its row keeps it: one line.</summary>
    private static readonly JsonSerializerOptions Compact = new();

    public const string SettingsName = "settings", AssignmentsName = "assignments", LimitsName = "limits";

    /// <summary>The store's own folder under <c>%APPDATA%</c>, where the JSON files were: logs, prompts and leases stay there.</summary>
    public static string LiveDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Orchestrator");

    public JsonStore(string? directory = null, OrchestratorDb? db = null)
    {
        Directory = directory ?? LiveDirectory;
        Db = db ?? new OrchestratorDb(OrchestratorDb.StatePath(Directory));
    }

    /// <summary>The folder the old JSON files are read in from (and the logs, prompts and leases kept beside them).</summary>
    public string Directory { get; }

    public OrchestratorDb Db { get; }

    /// <summary>Where <paramref name="name"/> is kept, for messages: the database and its table.</summary>
    public string Describe(string name) => name switch
    {
        SettingsName => $"{Db.Path} (settings)",
        AssignmentsName => $"{Db.Path} (assignments)",
        _ => $"{Db.Path} (state '{name}')",
    };

    /// <summary>The value, or defaults when it's not kept yet or couldn't be read now.</summary>
    public T Load<T>(string name) where T : new()
    {
        TryLoad<T>(name, out var value);
        return value;
    }

    /// <summary>Like <see cref="Load{T}"/>, but says whether it was read: false (with defaults) when the database couldn't be
    /// read now, its value isn't readable JSON, or an old file still waiting to be read in can't be (held, or a hand edit's
    /// typo), so a caller holding an earlier copy keeps that rather than taking an empty one for the truth.</summary>
    public bool TryLoad<T>(string name, out T value) where T : new()
    {
        value = new T();
        try
        {
            var imported = Import(name);
            using var connection = Connect(name);
            if (Read(connection, null, name) is { } node) value = node.Deserialize<T>(Options) ?? new T();
            else if (!imported) return false;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or DbException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            value = new T();
            return false;
        }
    }

    /// <summary>Whether <paramref name="name"/> is kept at all (a value saved, even an empty one).</summary>
    public bool Has(string name)
    {
        try
        {
            Import(name);
            using var connection = Connect(name);
            return Read(connection, null, name) is not null;
        }
        catch (Exception ex) when (ex is JsonException or DbException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>When a <c>state</c> value was last written (null when it isn't kept or couldn't be read), so a reader rereads
    /// it only once it changed, as it once watched the file's time.</summary>
    public DateTime? WrittenUtc(string name)
    {
        try
        {
            Import(name);
            using var connection = Connect(name);
            return Sql.Scalar(connection, null, "SELECT written_at FROM state WHERE name = @name;", ("@name", name)) is { } at
                ? DateTime.UnixEpoch.AddTicks((long)(Convert.ToDouble(at, System.Globalization.CultureInfo.InvariantCulture) * TimeSpan.TicksPerSecond)) : null;
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Replaces <paramref name="name"/>'s value. A database that stays busy past its timeout is an <see cref="IOException"/>,
    /// as a held file was.</summary>
    public void Save<T>(string name, T value)
    {
        var node = JsonSerializer.SerializeToNode(value, Options);
        Guard(() =>
        {
            Import(name);
            WriteTo(name, (connection, tx) => Write(connection, tx, name, node));
            Retire(name);
        });
    }

    /// <summary>
    /// Loads, changes and saves <paramref name="name"/> in one write transaction, so a change made at once by another process
    /// (the daemon's autopilot, a dashboard's Dispatch, the CLI) waits for this one and is never dropped. A value that isn't
    /// readable JSON, or an old file that can't be read in, throws rather than having an empty one saved over it.
    /// </summary>
    public void Update<T>(string name, Action<T> change) where T : new() => Guard(() =>
    {
        if (!Import(name))
            throw new InvalidDataException($"{Path.Combine(Directory, name + ".json")} couldn't be read in; left as it is rather than saved over");
        WriteTo(name, (connection, tx) =>
        {
            T value;
            try { value = Read(connection, tx, name) is { } node ? node.Deserialize<T>(Options) ?? new T() : new T(); }
            catch (JsonException ex) { throw new InvalidDataException($"{Describe(name)} isn't readable JSON; left as it is rather than saved over", ex); }
            change(value);
            Write(connection, tx, name, JsonSerializer.SerializeToNode(value, Options));
        });
    });

    public OrchestratorSettings LoadSettings() => Load<OrchestratorSettings>(SettingsName);

    /// <summary>The settings, and whether they were read: false (with defaults) when they're there but couldn't be (a bad
    /// settings row, the database held past its timeout), so a caller acting on them skips that work rather than running it on
    /// defaults (M.ORCH-T720). None saved yet reads as the defaults.</summary>
    public bool TryLoadSettings(out OrchestratorSettings s) => TryLoad(SettingsName, out s);

    /// <summary>The settings to change and save back: throws when they're there but can't be read (an old settings.json with a
    /// hand edit's typo, the database held past its timeout), where <see cref="LoadSettings"/>'s defaults would be saved over
    /// every budget, block and reservation in it.</summary>
    public OrchestratorSettings LoadSettingsToChange() => TryLoad<OrchestratorSettings>(SettingsName, out var s) ? s
        : throw new InvalidDataException($"The settings in {Describe(SettingsName)} couldn't be read; left as they are rather than saved over with defaults. Fix or remove {Path.Combine(Directory, "settings.json")} if it's still there, and try again.");
    public void SaveSettings(OrchestratorSettings s) => Save(SettingsName, s);
    public List<Assignment> LoadAssignments() => Load<List<Assignment>>(AssignmentsName);
    public void SaveAssignments(List<Assignment> a) => Save(AssignmentsName, a);

    /// <summary>Loads, changes and saves the assignments in one transaction every process takes (the daemon's autopilot, a
    /// dashboard's Dispatch, the CLI), so one's save never drops what another added in between.</summary>
    public void UpdateAssignments(Action<List<Assignment>> change) => Update(AssignmentsName, change);

    public bool HasLimits => Has(LimitsName);
    public List<Budget.LimitObservation> LoadLimits() => Load<List<Budget.LimitObservation>>(LimitsName);

    // For a load, merge and save: false when they're there but couldn't be read, so the merge isn't saved over them.
    public bool TryLoadLimits(out List<Budget.LimitObservation> o) => TryLoad(LimitsName, out o);
    public void SaveLimits(List<Budget.LimitObservation> o) => Save(LimitsName, o);

    private static bool IsVolatile(string name) => OrchestratorDb.Volatile.Contains(name);

    /// <summary>Where <paramref name="name"/> is read from: a volatile row from this machine's file, the rest from the store of the
    /// orchestrator's data (SQL Server once configured, M.ORCH-T658).</summary>
    private DbConnection Connect(string name)
    {
        if (IsVolatile(name)) return Db.Open();
        // SQL Server down: read this machine's copy (written on every change, so at worst a minute behind) rather than take
        // the defaults for the truth, which would put every budget, block and pace setting back to its default.
        try { return Db.OpenData(); }
        catch (Exception ex) when (Db.Server is not null && (ex is DbException or InvalidOperationException))
        {
            return Db.Open();
        }
    }

    /// <summary>One write transaction on <paramref name="name"/>'s store; a change to the settings, the assignments or a saved
    /// state row is copied into this machine's file for the hooks.</summary>
    private void WriteTo(string name, Action<DbConnection, DbTransaction> change)
    {
        if (IsVolatile(name)) Db.Write((connection, tx) => change(connection, tx), dump: false);
        else Db.WriteData(change, true, name is SettingsName or AssignmentsName ? name : "state:" + name);
    }

    private static void Guard(Action write)
    {
        try { write(); }
        catch (DbException ex) { throw new IOException($"The orchestrator database couldn't be written: {ex.Message}", ex); }
    }

    // ---- the tables ----

    /// <summary>The assignment columns, with the <see cref="Assignment"/> property each holds. A property added to it needs a
    /// column here (a new schema step), or it isn't kept.</summary>
    private static readonly (string Column, string Property, Type Kind)[] AssignmentColumns =
    [
        ("ticket_id", nameof(Assignment.TicketId), typeof(string)),
        ("repo", nameof(Assignment.Repo), typeof(string)),
        ("directory", nameof(Assignment.Directory), typeof(string)),
        ("provider", nameof(Assignment.Provider), typeof(string)),
        ("started_utc", nameof(Assignment.StartedUtc), typeof(string)),
        ("worktree", nameof(Assignment.Worktree), typeof(bool)),
        ("pilot", nameof(Assignment.Pilot), typeof(long)),
        ("pilot_started_utc", nameof(Assignment.PilotStartedUtc), typeof(string)),
        ("log", nameof(Assignment.Log), typeof(string)),
        ("cli", nameof(Assignment.Cli), typeof(long)),
        ("cli_session", nameof(Assignment.CliSession), typeof(string)),
        ("model", nameof(Assignment.Model), typeof(string)),
        ("reviewed_utc", nameof(Assignment.ReviewedUtc), typeof(string)),
        ("account", nameof(Assignment.Account), typeof(string)),
    ];

    /// <summary><paramref name="name"/>'s value as JSON, or null when nothing is kept for it.</summary>
    private static JsonNode? Read(DbConnection connection, DbTransaction? tx, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        switch (name)
        {
            case SettingsName:
            {
                command.CommandText = "SELECT name, value FROM settings ORDER BY name;";
                using var r = command.ExecuteReader();
                JsonObject? settings = null;
                while (r.Read()) (settings ??= new JsonObject())[r.GetString(0)] = JsonNode.Parse(r.GetString(1));
                return settings;
            }
            case AssignmentsName:
            {
                command.CommandText = $"SELECT {string.Join(", ", AssignmentColumns.Select(c => c.Column))} FROM assignments ORDER BY position;";
                using var r = command.ExecuteReader();
                JsonArray? list = null;
                while (r.Read())
                {
                    var row = new JsonObject();
                    for (var i = 0; i < AssignmentColumns.Length; i++)
                    {
                        if (r.IsDBNull(i)) continue;
                        var (_, property, kind) = AssignmentColumns[i];
                        row[property] = kind == typeof(bool) ? JsonValue.Create(Sql.Long(r, i) != 0)
                            : kind == typeof(long) ? JsonValue.Create(Sql.Long(r, i))
                            : JsonValue.Create(r.GetString(i));
                    }
                    (list ??= new JsonArray()).Add(row);
                }
                return list;
            }
            default:
                return Sql.Scalar(connection, tx, "SELECT value FROM state WHERE name = @name;", ("@name", name)) is string json ? JsonNode.Parse(json) : null;
        }
    }

    /// <summary>Replaces <paramref name="name"/>'s value with <paramref name="node"/> (null keeps nothing for it).</summary>
    private static void Write(DbConnection connection, DbTransaction tx, string name, JsonNode? node)
    {
        switch (name)
        {
            case SettingsName:
                Sql.Run(connection, tx, "DELETE FROM settings;");
                if (node is not JsonObject settings) break;
                foreach (var (key, value) in settings)
                    if (value is not null) // left out, as the file left nulls out
                        Sql.Run(connection, tx, "INSERT INTO settings (name, value) VALUES (@name, @value);",
                            ("@name", key), ("@value", value.ToJsonString(Compact)));
                break;
            case AssignmentsName:
                Sql.Run(connection, tx, "DELETE FROM assignments;");
                if (node is not JsonArray list) break;
                var position = 0L;
                foreach (var item in list.OfType<JsonObject>())
                {
                    var values = AssignmentColumns.Select(c => (Name: "@" + c.Column, Value: Cell(item[c.Property]))).ToArray();
                    Sql.Run(connection, tx,
                        $"INSERT INTO assignments (position, {string.Join(", ", AssignmentColumns.Select(c => c.Column))}) " +
                        $"VALUES (@position, {string.Join(", ", values.Select(v => v.Name))});",
                        [("@position", position++), .. values]);
                }
                break;
            default:
                if (node is null)
                {
                    Sql.Run(connection, tx, "DELETE FROM state WHERE name = @name;", ("@name", name));
                    break;
                }
                Sql.Upsert(connection, tx, "state", ["name"],
                    ("name", name), ("value", node.ToJsonString(Options)),
                    ("written_at", (DateTime.UtcNow - DateTime.UnixEpoch).Ticks / (double)TimeSpan.TicksPerSecond));
                break;
        }
    }

    /// <summary>A JSON value as a cell: text, an integer, 0/1 for a bool, NULL for null.</summary>
    private static object? Cell(JsonNode? node) => node is not JsonValue v ? null : v.GetValueKind() switch
    {
        JsonValueKind.String => v.GetValue<string>(),
        JsonValueKind.True => 1L,
        JsonValueKind.False => 0L,
        JsonValueKind.Number => v.TryGetValue<long>(out var n) ? n : v.GetValue<double>(),
        JsonValueKind.Null => null,
        _ => v.ToJsonString(),
    };

    // ---- the one-time import ----

    /// <summary>
    /// Reads <c>&lt;name&gt;.json</c> in <see cref="Directory"/> into the database when it's there and the database holds nothing
    /// for <paramref name="name"/> yet, then renames it <c>.bak</c>: once a value is kept, the file is only renamed, never read
    /// over it (a process from before the move saving its file again changes nothing). False when the file is there and
    /// couldn't be read in: held right now (tried again next time), or not readable JSON, a hand edit's typo, left where it is
    /// for you to fix rather than replaced by defaults. A scratch database (<see cref="OrchestratorDb.EnvVar"/> set: tests)
    /// never takes the live files under <c>%APPDATA%</c>.
    /// </summary>
    private bool Import(string name)
    {
        var file = Path.Combine(Directory, name + ".json");
        if (!File.Exists(file)) return true;
        if (Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar) is { Length: > 0 } &&
            string.Equals(Path.GetFullPath(Directory), Path.GetFullPath(LiveDirectory), StringComparison.OrdinalIgnoreCase)) return true;
        var taken = false;
        try
        {
            WriteTo(name, (connection, tx) =>
            {
                // Read under the write lock: a process that got here first already took it in.
                if (Read(connection, tx, name) is not null) { taken = true; return; }
                string text;
                try { text = File.ReadAllText(file); }
                catch (FileNotFoundException) { taken = true; return; } // renamed since the check: taken in by then
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
                JsonNode? node;
                try { node = JsonNode.Parse(text, documentOptions: new() { CommentHandling = JsonCommentHandling.Skip }); }
                catch (JsonException) { return; }
                Write(connection, tx, name, node);
                taken = true;
            });
        }
        catch (DbException) { return false; }
        if (taken) Retire(name);
        return taken;
    }

    /// <summary>Renames a taken-in (or saved over) file <c>.bak</c>; one held right now is renamed next time.</summary>
    private void Retire(string name)
    {
        var file = Path.Combine(Directory, name + ".json");
        if (!File.Exists(file)) return;
        if (Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar) is { Length: > 0 } &&
            string.Equals(Path.GetFullPath(Directory), Path.GetFullPath(LiveDirectory), StringComparison.OrdinalIgnoreCase)) return;
        try { File.Move(file, file + ".bak", overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
