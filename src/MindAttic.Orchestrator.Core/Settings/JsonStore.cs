using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MindAttic.Vault.Settings;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's settings and small state, kept in <see cref="OrchestratorDb"/> (M.ORCH-T524) where they used to be JSON
/// files under <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>. Each is still named as its file was and handled as a JSON
/// value, so callers load and save the same types: <c>settings</c> goes into the <c>settings</c> table one row per setting,
/// <c>assignments</c> into <c>assignments</c> one row per assignment, and every other name (limits, reminders, audit, the pace
/// alarms, limit wakes, allocate holds, stale claims, dashboard-seen) is one row of <c>state</c>. A change is one write
/// transaction, so the daemon, a dashboard and the CLI changing the same thing at once queue on SQLite's lock instead of a
/// <c>.lock</c> file, and none saves over what another added. A JSON file still in <see cref="Directory"/> is read in the first
/// time its name is used (unless the database already holds it) and kept beside it as <c>.bak</c>.
/// <para>LAMP (M.ORCH-T1308): the app-level settings live in MindAttic Vault (<see cref="OrchestratorVaultSettings"/>, edited in
/// MindAttic.Vault.Desktop). For the live store, reading the settings overlays the Vault's values on the row, and writing them
/// writes the Vault's share back to the Vault file as well as the row, which the hooks keep reading;
/// <see cref="SyncVaultSettings"/> copies an edit made in Vault.Desktop into the row.</para>
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

    /// <summary>The state row holding the Vault values as the database and the Vault last agreed on them (LAMP, M.ORCH-T1308):
    /// the common ancestor <see cref="OrchestratorVaultSettings.Merge"/> tells a row change from a Vault change by.</summary>
    public const string VaultSyncedName = "vault-settings";

    /// <param name="directory">The store's folder; null is <see cref="LiveDirectory"/>.</param>
    /// <param name="db">The database; null finds it from <paramref name="directory"/>.</param>
    /// <param name="vault">The MindAttic Vault settings file the app-level settings are overlaid from. Null picks the user's real
    /// one (<c>%LOCALAPPDATA%\MindAttic\Orchestrator\settings.json</c>) for the live store alone; a scratch store (tests, a
    /// folder of its own) has none unless one is given, so it reads and writes its database row exactly as before.</param>
    public JsonStore(string? directory = null, OrchestratorDb? db = null, JsonSettingsStore<OrchestratorVaultSettings>? vault = null)
    {
        Directory = directory ?? LiveDirectory;
        Db = db ?? new OrchestratorDb(OrchestratorDb.StatePath(Directory));
        Vault = vault ?? (IsLiveStore() ? JsonSettingsStore<OrchestratorVaultSettings>.ForSettingsApp() : null);
    }

    /// <summary>The MindAttic Vault settings file the orchestrator's app-level settings live in (LAMP, M.ORCH-T1308), overlaid on
    /// the database's settings row; null for a scratch store.</summary>
    public JsonSettingsStore<OrchestratorVaultSettings>? Vault { get; }

    private bool IsLiveStore()
    {
        try
        {
            return Db.IsLive && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Directory)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(LiveDirectory)), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
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
            JsonObject? importedVault = null;
            using (var connection = Connect(name))
            {
                if (Read(connection, null, name) is { } node) value = node.Deserialize<T>(Options) ?? new T();
                else if (!imported) return false;
                // LAMP: the Vault's values over the row's (M.ORCH-T1308).
                if (name == SettingsName && Vault is not null && value is OrchestratorSettings row)
                    value = (T)(object)Overlay(row, ReadSynced(connection, null), out importedVault);
            }
            // Imported just now: the row and the Vault agree, which is what a later three-way merge starts from.
            if (importedVault is not null) TryRecordSynced(importedVault);
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
            WriteTo(name, (connection, tx) =>
            {
                Write(connection, tx, name, node);
                if (name == SettingsName && value is OrchestratorSettings s) WriteVault(connection, tx, s);
            });
            Retire(name);
        });
        // Committed: the status bus pushes it to every dashboard (M.ORCH-T891).
        StoreChanges.Note(this, name);
    }

    /// <summary>
    /// Loads, changes and saves <paramref name="name"/> in one write transaction, so a change made at once by another process
    /// (the daemon's allocate, a dashboard's Dispatch, the CLI) waits for this one and is never dropped. A value that isn't
    /// readable JSON, or an old file that can't be read in, throws rather than having an empty one saved over it.
    /// </summary>
    public void Update<T>(string name, Action<T> change) where T : new()
    {
        Guard(() =>
        {
            if (!Import(name))
                throw new InvalidDataException($"{Path.Combine(Directory, name + ".json")} couldn't be read in; left as it is rather than saved over");
            WriteTo(name, (connection, tx) =>
            {
                T value;
                try { value = Read(connection, tx, name) is { } node ? node.Deserialize<T>(Options) ?? new T() : new T(); }
                catch (JsonException ex) { throw new InvalidDataException($"{Describe(name)} isn't readable JSON; left as it is rather than saved over", ex); }
                // LAMP (M.ORCH-T1308): the change is made to the settings as they read, the Vault's values over the row's.
                if (name == SettingsName && Vault is not null && value is OrchestratorSettings row)
                    value = (T)(object)Overlay(row, ReadSynced(connection, tx), out _);
                change(value);
                Write(connection, tx, name, JsonSerializer.SerializeToNode(value, Options));
                if (name == SettingsName && value is OrchestratorSettings changed) WriteVault(connection, tx, changed);
            });
        });
        // Committed: the status bus pushes it to every dashboard (M.ORCH-T891).
        StoreChanges.Note(this, name);
    }

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

    /// <summary>Loads, changes and saves the assignments in one transaction every process takes (the daemon's allocate, a
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
        // The settings' write also records the Vault values they were synced at (LAMP), copied for the hooks alike.
        else if (name == SettingsName && Vault is not null) Db.WriteData(change, true, SettingsName, "state:" + VaultSyncedName);
        else Db.WriteData(change, true, name is SettingsName or AssignmentsName ? name : "state:" + name);
    }

    // ---- LAMP: the app-level settings live in MindAttic Vault (M.ORCH-T1308) ----

    private (string? Text, JsonObject? Values) vaultCache;
    private bool manifestWritten;
    private readonly System.Threading.Lock vaultGate = new();

    private enum VaultFile { Missing, Unreadable, Read }

    /// <summary>The Vault file's values (normalized through <see cref="OrchestratorVaultSettings"/>): the small file is read every
    /// time (a timestamp can miss two writes in one clock tick) and parsed again only when its text changed.</summary>
    private VaultFile ReadVault(out JsonObject? values)
    {
        values = null;
        var path = Vault!.FilePath;
        try
        {
            if (!File.Exists(path)) return VaultFile.Missing;
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return VaultFile.Unreadable;
            lock (vaultGate)
                if (vaultCache.Values is { } cached && vaultCache.Text == text)
                {
                    values = (JsonObject)cached.DeepClone();
                    return VaultFile.Read;
                }
            var parsed = OrchestratorVaultSettings.FromJson(JsonNode.Parse(text) ?? throw new JsonException("empty")).ToJson();
            lock (vaultGate) vaultCache = (text, parsed);
            values = (JsonObject)parsed.DeepClone();
            return VaultFile.Read;
        }
        catch (FileNotFoundException) { return VaultFile.Missing; }
        catch (DirectoryNotFoundException) { return VaultFile.Missing; }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return VaultFile.Unreadable;
        }
    }

    /// <summary>The Vault values the row and the Vault last agreed on, or null before they ever have.</summary>
    private static JsonObject? ReadSynced(DbConnection connection, DbTransaction? tx)
    {
        try { return Read(connection, tx, VaultSyncedName) is JsonObject o ? OrchestratorVaultSettings.FromJson(o).ToJson() : null; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>
    /// <paramref name="row"/> with the Vault's values over its own: merged three ways (<see cref="OrchestratorVaultSettings.Merge"/>)
    /// against what they were last synced at. With no Vault file yet, today's row is imported into it once (the row itself is
    /// left as it is, the rollback) and <paramref name="imported"/> says what was imported. A Vault file that can't be read (a
    /// hand edit's typo, held right now) leaves the row's values in force rather than putting every setting back to its default.
    /// </summary>
    private OrchestratorSettings Overlay(OrchestratorSettings row, JsonObject? synced, out JsonObject? imported)
    {
        imported = null;
        var rowValues = OrchestratorVaultSettings.From(row).ToJson();
        switch (ReadVault(out var vault))
        {
            case VaultFile.Missing:
                try
                {
                    Vault!.Save(OrchestratorVaultSettings.FromJson(rowValues));
                    EnsureManifest();
                    imported = rowValues;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                return row;
            case VaultFile.Unreadable:
                return row;
        }
        EnsureManifest();
        var merged = OrchestratorVaultSettings.Merge(synced, rowValues, vault!);
        return OrchestratorVaultSettings.FromJson(merged).ApplyTo(row);
    }

    /// <summary>Inside a settings write: records <paramref name="s"/>'s Vault values as synced and writes them to the Vault file
    /// when they differ from it. An IO failure throws, rolling the database write back, so the two never part.</summary>
    private void WriteVault(DbConnection connection, DbTransaction tx, OrchestratorSettings s)
    {
        if (Vault is null) return;
        var values = OrchestratorVaultSettings.From(s);
        var json = values.ToJson();
        Write(connection, tx, VaultSyncedName, json);
        if (ReadVault(out var onDisk) == VaultFile.Read && JsonNode.DeepEquals(onDisk, json)) return;
        Vault.Save(values);
        EnsureManifest();
    }

    /// <summary>Writes <c>settings.manifest.json</c> beside the Vault file (once a process, and only when it changed), so
    /// Vault.Desktop shows the Orchestrator tab.</summary>
    private void EnsureManifest()
    {
        if (manifestWritten || Vault is null) return;
        try
        {
            MindAttic.Vault.Settings.Manifest.SettingsManifestFile.WriteIfChanged(Vault.Directory,
                MindAttic.Vault.Settings.Manifest.SettingsManifestBuilder.Build<OrchestratorVaultSettings>());
            manifestWritten = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Records what was just imported as synced; best effort (a later settings write records it anyway).</summary>
    private void TryRecordSynced(JsonObject values)
    {
        try { Guard(() => WriteTo(VaultSyncedName, (connection, tx) => Write(connection, tx, VaultSyncedName, values))); }
        catch (IOException) { }
    }

    /// <summary>
    /// Brings the database's settings row (what the hooks read) and the Vault file into agreement (LAMP, M.ORCH-T1308): after an
    /// edit in MindAttic.Vault.Desktop, or a write that reached only the row, the merged settings are written to both, and the
    /// status bus pushes them to every dashboard. Nothing is written when they already agree, or when the row can't be read.
    /// Returns whether anything was written. The daemon runs it every tick, and on every change to the Vault file.
    /// </summary>
    public bool SyncVaultSettings()
    {
        if (Vault is null) return false;
        if (!InSync(out var inSync) || inSync) return false;
        Update<OrchestratorSettings>(SettingsName, _ => { });
        return true;
    }

    /// <summary>Calls <paramref name="changed"/> (on a worker thread, 300 ms after the last of a burst) whenever the Vault file
    /// is written, e.g. a Save in MindAttic.Vault.Desktop; null for a store with no Vault. Dispose to stop.</summary>
    public IDisposable? WatchVault(Action changed)
    {
        if (Vault is null) return null;
        System.IO.Directory.CreateDirectory(Vault.Directory);
        var debounce = new Timer(_ =>
        {
            try { changed(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Settings").Warning(ex, "Applying a MindAttic Vault settings change failed"); }
        });
        var watcher = new FileSystemWatcher(Vault.Directory, Vault.FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
        };
        FileSystemEventHandler handler = (_, _) => debounce.Change(300, Timeout.Infinite);
        watcher.Changed += handler;
        watcher.Created += handler;
        watcher.Renamed += (s, e) => handler(s, e);
        watcher.EnableRaisingEvents = true;
        return new VaultWatch(watcher, debounce);
    }

    private sealed class VaultWatch(FileSystemWatcher watcher, Timer debounce) : IDisposable
    {
        public void Dispose()
        {
            watcher.Dispose();
            debounce.Dispose();
        }
    }

    /// <summary>Whether the row, the synced record and the Vault file all hold the same Vault values; false (unknown) when the
    /// row or the Vault file can't be read now.</summary>
    private bool InSync(out bool inSync)
    {
        inSync = false;
        try
        {
            if (!Import(SettingsName)) return false;
            using var connection = Connect(SettingsName);
            var row = Read(connection, null, SettingsName) is { } node ? node.Deserialize<OrchestratorSettings>(Options) ?? new() : new();
            var rowValues = OrchestratorVaultSettings.From(row).ToJson();
            var synced = ReadSynced(connection, null);
            var file = ReadVault(out var vault);
            if (file == VaultFile.Unreadable) return false;
            if (file == VaultFile.Missing) return true; // written by the next write
            var merged = OrchestratorVaultSettings.Merge(synced, rowValues, vault!);
            inSync = JsonNode.DeepEquals(merged, rowValues) && JsonNode.DeepEquals(merged, synced) && JsonNode.DeepEquals(merged, vault);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or DbException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
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
