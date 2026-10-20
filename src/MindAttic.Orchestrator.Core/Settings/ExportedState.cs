using System.Text;
using Microsoft.Data.Sqlite;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// A mark kept as a <c>state</c> row of the orchestrator's database and still exported as its old JSON file, because
/// something outside the orchestrator reads that file (M.ORCH-T528): the billing mode (the Launcher) and the account record
/// (the shared statusline). The hooks keep the same pair through stdlib sqlite3. A write puts the row, then the file; while
/// the database is held past its busy timeout only the file is written, so a read takes whichever of the two was written
/// later. The first read with no row reads the file in (a copy kept as .bak); a read never creates the database.
/// </summary>
public static class ExportedState
{
    /// <summary>The newer of <paramref name="name"/>'s row and <paramref name="path"/>'s text; null when there's neither.</summary>
    public static string? Read(OrchestratorDb db, string name, string path)
    {
        (string Value, DateTime WrittenUtc)? row = null;
        var dbThere = File.Exists(db.Path);
        if (dbThere)
            try { row = db.LoadState(name); }
            catch (SqliteException) { } // held past the busy timeout: the file stands in
        string? file = null;
        var fileUtc = DateTime.MinValue;
        try
        {
            if (File.Exists(path))
            {
                fileUtc = File.GetLastWriteTimeUtc(path);
                file = ReadShared(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (row is null && file is not null && dbThere)
            try
            {
                db.SaveState(name, file);
                if (!File.Exists(path + ".bak")) File.Copy(path, path + ".bak");
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException) { }
        if (row is { } r && (file is null || r.WrittenUtc > fileUtc)) return r.Value;
        return file;
    }

    /// <summary>Writes <paramref name="json"/> to <paramref name="name"/>'s row (when the database takes it), then exports it
    /// to <paramref name="path"/>.</summary>
    public static void Write(OrchestratorDb db, string name, string path, string json)
    {
        try { db.SaveState(name, json); }
        catch (SqliteException) { } // held past the busy timeout: the file alone, which reads newer
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmp, json, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
