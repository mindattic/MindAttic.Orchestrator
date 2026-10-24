namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The single-file exe unpacks its native libraries into &lt;base&gt;\&lt;app&gt;\&lt;build hash&gt; on its first run, and .NET never
/// removes an old build's folder: about 10.5 MB per deploy, a gigabyte in ten days of allocate (M.ORCH-T469).
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
            try
            {
                var files = new DirectoryInfo(dir).GetFiles("*", SearchOption.AllDirectories);
                if (files.Any(InUse)) continue;
                Directory.Delete(dir, recursive: true);
                folders++;
                bytes += files.Sum(f => f.Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (folders, bytes);
    }

    // A loaded DLL is mapped with delete sharing, so its folder can still be renamed or partly deleted; it can't be opened for writing.
    private static bool InUse(FileInfo file)
    {
        try
        {
            using var _ = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }
}
