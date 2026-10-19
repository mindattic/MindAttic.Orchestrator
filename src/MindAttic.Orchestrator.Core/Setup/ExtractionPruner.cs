namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The single-file exe unpacks its native libraries into &lt;base&gt;\&lt;app&gt;\&lt;build hash&gt; on its first run, and .NET never
/// removes an old build's folder: about 10.5 MB per deploy, a gigabyte in ten days of autopilot (M.ORCH-T469).
/// </summary>
public static class ExtractionPruner
{
    public static string BaseDir() =>
        Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Path.GetTempPath(), ".net");

    /// <summary>Deletes every build folder of <paramref name="app"/> no running copy uses; returns how many and their bytes.</summary>
    public static (int Folders, long Bytes) Prune(string baseDir, string app)
    {
        var appDir = Path.Combine(baseDir, app);
        if (!Directory.Exists(appDir)) return (0, 0);
        var (folders, bytes) = (0, 0L);
        foreach (var dir in Directory.GetDirectories(appDir))
        {
            // A running build holds its DLLs open, and Windows won't rename a folder with an open file in it: a folder
            // that moves is one nothing is using, and deleting it can't pull a library out from under a live process.
            var doomed = dir.EndsWith(".prune", StringComparison.OrdinalIgnoreCase) ? dir : dir + ".prune";
            try
            {
                if (doomed != dir) Directory.Move(dir, doomed);
                var size = new DirectoryInfo(doomed).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                Directory.Delete(doomed, recursive: true);
                folders++;
                bytes += size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (folders, bytes);
    }
}
