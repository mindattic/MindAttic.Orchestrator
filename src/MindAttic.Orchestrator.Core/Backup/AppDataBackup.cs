using System.Diagnostics;
using System.IO.Compression;

namespace MindAttic.Orchestrator.Backup;

public sealed record AppDataBackupResult(bool Ok, string ZipPath, long Bytes, TimeSpan Elapsed, string Output = "");

/// <summary>
/// Zips <c>%APPDATA%\MindAttic</c> (MindAttic.Vault's settings and credential tree) into
/// <c>{targetFolder}\AppData.zip</c>, and restores it. A copy of the workspace and its databases never touches
/// this tree, so without it a restore comes back with no Vault settings or credentials. Ported from
/// MindAttic.Launcher's AppDataBackupService (M.ORCH-T2046).
/// </summary>
public sealed class AppDataBackup
{
    public const string ZipFileName = "AppData.zip";

    public static readonly string DefaultSource = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic");

    public string Source { get; }
    private readonly Func<string, bool> exists;

    public AppDataBackup() : this(DefaultSource) { }

    public AppDataBackup(string source, Func<string, bool>? exists = null)
    {
        Source = source;
        this.exists = exists ?? Directory.Exists;
    }

    public static string ResolveZipPath(string targetFolder) => Path.Combine(targetFolder, ZipFileName);

    /// <summary>
    /// Zips <see cref="Source"/> into <c>{targetFolder}\AppData.zip</c>. A missing source (nothing ever seeded
    /// Vault settings here) is OK with a note, not a failure. A zip that dies midway is deleted so it can't pass
    /// for a backup.
    /// </summary>
    public AppDataBackupResult Zip(string targetFolder)
    {
        var zipPath = ResolveZipPath(targetFolder);
        if (!exists(Source))
            return new AppDataBackupResult(true, zipPath, 0, TimeSpan.Zero, $"AppData folder not found, skipped: {Source}");

        var sw = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(targetFolder);
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(Source, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            sw.Stop();
            return new AppDataBackupResult(true, zipPath, new FileInfo(zipPath).Length, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* best-effort */ }
            return new AppDataBackupResult(false, zipPath, 0, sw.Elapsed, ex.Message);
        }
    }

    /// <summary>Extracts a previously made AppData.zip onto <paramref name="destDir"/>, overwriting in place.</summary>
    public AppDataBackupResult Restore(string zipPath, string destDir)
    {
        if (!File.Exists(zipPath))
            return new AppDataBackupResult(false, zipPath, 0, TimeSpan.Zero, $"Zip not found: {zipPath}");

        var sw = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(destDir);
            ZipFile.ExtractToDirectory(zipPath, destDir, overwriteFiles: true);
            sw.Stop();
            return new AppDataBackupResult(true, zipPath, new FileInfo(zipPath).Length, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new AppDataBackupResult(false, zipPath, 0, sw.Elapsed, ex.Message);
        }
    }
}
