namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Temp folders a test makes, deleted when it's done (M.ORCH-T470): the suite runs many times a night, so a folder left
/// behind each run fills %TEMP%. A delete that still fails after a few tries fails the test instead of passing quietly.
/// </summary>
public sealed class TempDirs
{
    private readonly List<string> made = [];

    /// <summary>A new, empty temp folder that <see cref="DeleteAll"/> removes.</summary>
    public string New()
    {
        var dir = Directory.CreateTempSubdirectory("orch-test-").FullName;
        made.Add(dir);
        return dir;
    }

    /// <summary>Deletes every folder <see cref="New"/> made; throws if one can't be.</summary>
    public void DeleteAll()
    {
        var failed = new List<string>();
        foreach (var dir in made)
            if (!Delete(dir)) failed.Add(dir);
        made.Clear();
        if (failed.Count > 0) throw new IOException("Couldn't delete the test's temp folders: " + string.Join(", ", failed));
    }

    /// <summary>
    /// Deletes <paramref name="dir"/> and everything in it, clearing read-only marks (git writes its objects read-only)
    /// and retrying while a just-exited process still holds a file. True once it's gone.
    /// </summary>
    public static bool Delete(string dir)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!Directory.Exists(dir)) return true;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(dir, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20) return false;
                Thread.Sleep(100);
            }
        }
    }
}
