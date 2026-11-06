using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Activity;

/// <summary>Something the cleanup deleted (or, in a dry run, would), and why.</summary>
public sealed record CleanupItem(string Path, long Bytes, string Reason);

/// <summary>What a cleanup run did: what it freed, what it deleted, what it couldn't, and the big things it only names.</summary>
public sealed record CleanupResult(double FreeGbBefore, long FreedBytes, IReadOnlyList<CleanupItem> Deleted,
    IReadOnlyList<string> Errors, IReadOnlyList<SpaceHog> NotOurs);

/// <summary>
/// Disk self-cleanup (M.ORCH-T1449). Found live 2026-11-05: C: filled to 1.2 MB free, 27.6 GB of it one session's scratch
/// copies of an Unreal project under %TEMP%\claude. Below <see cref="Settings.OrchestratorSettings.DiskCleanupGb"/> it runs the
/// ended sessions' prune right away (<see cref="SessionTempPruner"/>), rather than waiting for the daemon's next look: by
/// liveness, never by age (M.ORCH-T1467), so a running session's scratch is never deleted, however big or old; the biggest
/// of those are named instead, with the page and hibernation files and the rest of %TEMP%. Tool worktrees are never deleted:
/// they're git checkouts that can hold work.
/// </summary>
public static class DiskCleanup
{
    /// <summary>A running session's scratch entry at least this big is named in the alert (it's left alone).</summary>
    public const long NameLiveScratchBytes = 1L << 30;

    private const double Gb = 1024.0 * 1024 * 1024;

    /// <summary>
    /// Prunes every ended session's folder under %TEMP%\claude (with <paramref name="dryRun"/>, deletes nothing and lists what
    /// would go). Every deletion is appended to <paramref name="logPath"/>.
    /// </summary>
    public static CleanupResult Run(SessionLiveness liveness, bool dryRun, string? logPath, string? tempDir = null) =>
        Run(liveness, dryRun, logPath, tempDir, out _);

    /// <inheritdoc cref="Run(SessionLiveness, bool, string?, string?)"/>
    public static CleanupResult Run(SessionLiveness liveness, bool dryRun, string? logPath, string? tempDir, out TempPruneResult prune)
    {
        var temp = tempDir ?? Path.GetTempPath();
        var freeGb = FreeGb(temp);
        var p = prune = SessionTempPruner.Prune(Path.Combine(temp, "claude"), liveness, dryRun, logPath);
        var went = p.Folders.Where(f => f.Delete && (dryRun || !p.Errors.Any(e => e.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase))))
            .Select(f => new CleanupItem(f.Path, f.Bytes, f.Why)).ToList();
        var hogs = NotOurs(temp).Concat(LiveScratch(p.Folders)).ToList();
        return new CleanupResult(freeGb, dryRun ? went.Sum(w => w.Bytes) : p.Bytes, went, p.Errors, hogs);
    }

    /// <summary>The big entries in the scratchpads of sessions that are kept (running, or not known to have ended).</summary>
    internal static IEnumerable<SpaceHog> LiveScratch(IEnumerable<TempFolderVerdict> folders) =>
        folders.Where(f => !f.Delete && f.Bytes >= NameLiveScratchBytes)
            .SelectMany(f => DiskWatch.LargestFolders(Path.Combine(f.Path, "scratchpad"), 3)
                .Where(h => h.Bytes >= NameLiveScratchBytes)
                .Select(h => new SpaceHog(Path.Combine(f.Path, "scratchpad", h.Path) + " (session kept)", h.Bytes)))
            .OrderByDescending(h => h.Bytes).Take(3);

    /// <summary>The big things on the drive that aren't the agents' to delete: the page and hibernation files and the biggest
    /// folders in %TEMP% other than claude's.</summary>
    public static IReadOnlyList<SpaceHog> NotOurs(string temp)
    {
        var hogs = new List<SpaceHog>();
        if (Path.GetPathRoot(Path.GetFullPath(temp)) is { Length: > 0 } root)
            foreach (var name in new[] { "pagefile.sys", "hiberfil.sys", "swapfile.sys" })
                try
                {
                    var f = new FileInfo(Path.Combine(root, name));
                    if (f.Exists && f.Length >= Gb) hogs.Add(new SpaceHog(f.FullName, f.Length));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        hogs.AddRange(DiskWatch.LargestFolders(temp, 6).Where(h => !h.Path.Equals("claude", StringComparison.OrdinalIgnoreCase) && h.Bytes >= Gb).Take(5));
        return hogs;
    }

    /// <summary>The alert's text for <paramref name="r"/>.</summary>
    public static string Describe(CleanupResult r, bool dryRun = false)
    {
        var verb = dryRun ? "Would free" : "Freed";
        var line = $"{verb} {DiskWatch.Size(r.FreedBytes)} of ended sessions' temp ({r.Deleted.Count} folder{(r.Deleted.Count == 1 ? "" : "s")}; " +
            $"{r.FreeGbBefore:0.0} GB was free).";
        if (r.Deleted.Count > 0)
            line += " Biggest: " + string.Join(", ", r.Deleted.OrderByDescending(d => d.Bytes).Take(3).Select(d => $"{Path.GetFileName(d.Path)} {DiskWatch.Size(d.Bytes)}")) + ".";
        if (r.Errors.Count > 0) line += $" {r.Errors.Count} couldn't be deleted (in use).";
        if (r.NotOurs.Count > 0)
            line += " Left alone: " + string.Join(", ", r.NotOurs.Select(h => $"{h.Path} {DiskWatch.Size(h.Bytes)}")) + ".";
        return line;
    }

    /// <summary>Where every deletion is written down: beside the orchestrator's database.</summary>
    public static string LogPath => Path.Combine(Path.GetDirectoryName(Settings.OrchestratorDb.LivePath) ?? ".", "disk-cleanup.log");

    public static double FreeGb(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace / Gb; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { return double.MaxValue; }
    }
}
