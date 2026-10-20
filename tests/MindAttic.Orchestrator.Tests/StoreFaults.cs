using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Faults in the store's tables (M.ORCH-T524), where tests once held or garbled a JSON file: a value that isn't readable
/// JSON (half a write), and a value that can't be saved right now (the database held).</summary>
internal static class StoreFaults
{
    /// <summary>Puts <paramref name="text"/> in as <paramref name="name"/>'s state value as it stands, readable JSON or not.</summary>
    public static void Garble(JsonStore store, string name, string text) =>
        Sql(store, "INSERT OR REPLACE INTO state (name, value, written_at) VALUES ($name, $text, 1760533200);", ("$name", name), ("$text", text));

    /// <summary>Until disposed, saving <paramref name="name"/> fails as a held database does.</summary>
    public static IDisposable Unwritable(JsonStore store, string name)
    {
        var trigger = "held_" + Guid.NewGuid().ToString("N");
        // The settings are rewritten whole (delete, then insert); a state value is one row.
        var settings = name == JsonStore.SettingsName;
        string[] ons = settings ? ["INSERT", "DELETE"] : ["INSERT", "UPDATE"];
        foreach (var on in ons)
            Sql(store, settings
                ? $"CREATE TRIGGER {trigger}_{on} BEFORE {on} ON settings BEGIN SELECT RAISE(ABORT, 'database is locked'); END;"
                : $"CREATE TRIGGER {trigger}_{on} BEFORE {on} ON state WHEN NEW.name = '{name.Replace("'", "''")}' BEGIN SELECT RAISE(ABORT, 'database is locked'); END;");
        return new Undo(() =>
        {
            foreach (var on in ons) Sql(store, $"DROP TRIGGER {trigger}_{on};");
        });
    }

    /// <summary>The settings as their rows hold them, one <c>"name": value</c> line each (nulls are left out, as the file left them).</summary>
    public static string SettingsText(JsonStore store)
    {
        using var connection = store.Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, value FROM settings ORDER BY name;";
        using var r = command.ExecuteReader();
        var lines = new List<string>();
        while (r.Read()) lines.Add($"\"{r.GetString(0)}\": {r.GetString(1)}");
        return string.Join("\n", lines);
    }

    private static void Sql(JsonStore store, string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = store.Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (n, v) in parameters) command.Parameters.AddWithValue(n, v);
        command.ExecuteNonQuery();
    }

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
