namespace MindAttic.Orchestrator.Activity;

/// <summary>A drive's free space, as the watch reads it.</summary>
public sealed record DriveFree(string Root, double FreeGb);

/// <summary>A folder or file taking a lot of space, its path as it's shown.</summary>
public sealed record SpaceHog(string Path, long Bytes);

/// <summary>What one look at the drives found: <see cref="Alert"/> says it's worth pushing; otherwise only a level change to log.</summary>
public sealed record DiskEvent(HealthLevel Level, DriveFree Drive, bool Alert, bool Changed);

/// <summary>
/// Watches free space on the system drive and the worktree root's (M.ORCH-T471). Found live 2026-10-18: the drive filled to
/// about 1 MB free overnight while autopilot ran, and whatever wrote it was deleted before anyone could see what it was. Below
/// <see cref="Settings.OrchestratorSettings.DiskWarnGb"/> you're alerted, urgently below
/// <see cref="Settings.OrchestratorSettings.DiskCriticalGb"/>, with the biggest folders in %TEMP% and the biggest files under
/// %TEMP%\claude (background commands' output, scratchpad builds), so a runaway writer is named while it's still growing. One
/// alert per step down (warn, then critical), and again each time free space halves since the last one: a drive still
/// filling keeps saying so, one sitting still at 15 GB doesn't repeat itself. It comes back to normal only once clear of the
/// warn line by a margin, so a drive hovering on it doesn't alert every few minutes.
/// </summary>
public sealed class DiskWatch
{
    public HealthLevel Level { get; private set; }
    private double? alertedAtGb;
    private HealthLevel alertedLevel;

    /// <summary>The drives to watch: the system drive and <paramref name="worktreeRoot"/>'s, once each.</summary>
    public static IReadOnlyList<string> Roots(string? worktreeRoot)
    {
        var roots = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (Path.GetPathRoot(Path.GetFullPath(path)) is { Length: > 0 } r && !roots.Contains(r, StringComparer.OrdinalIgnoreCase)) roots.Add(r);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows) is { Length: > 0 } win ? win : Environment.GetEnvironmentVariable("SystemDrive") + "\\");
        Add(worktreeRoot);
        return roots;
    }

    /// <summary>Free space on each of <paramref name="roots"/> that's a ready drive; one that can't be read is left out.</summary>
    public static IReadOnlyList<DriveFree> Read(IEnumerable<string> roots)
    {
        var drives = new List<DriveFree>();
        foreach (var root in roots)
            try
            {
                var d = new DriveInfo(root);
                if (d.IsReady) drives.Add(new DriveFree(d.RootDirectory.FullName, d.AvailableFreeSpace / Gb));
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { }
        return drives;
    }

    /// <summary>The level for <paramref name="freeGb"/>. Coming down from <paramref name="previous"/> it needs to be clear of the
    /// line it crossed by a tenth of the warn line (at least 1 GB).</summary>
    public static HealthLevel Judge(double freeGb, double warnGb, double criticalGb, HealthLevel previous = HealthLevel.Ok)
    {
        if (warnGb <= 0) return HealthLevel.Ok;
        var pad = Math.Max(1, warnGb / 10);
        var level = freeGb < criticalGb ? HealthLevel.Danger : freeGb < warnGb ? HealthLevel.Warn : HealthLevel.Ok;
        if (level >= previous) return level;
        var held = freeGb < criticalGb + pad ? HealthLevel.Danger : freeGb < warnGb + pad ? HealthLevel.Warn : HealthLevel.Ok;
        return (HealthLevel)Math.Max((int)level, Math.Min((int)held, (int)previous));
    }

    /// <summary>Takes in one look at <paramref name="drives"/> (the fullest one counts); null when there's nothing to read.</summary>
    public DiskEvent? Observe(IReadOnlyList<DriveFree> drives, double warnGb, double criticalGb)
    {
        if (drives.Count == 0) return null;
        var drive = drives.MinBy(d => d.FreeGb)!;
        var level = Judge(drive.FreeGb, warnGb, criticalGb, Level);
        var changed = level != Level;
        Level = level;
        if (level == HealthLevel.Ok)
        {
            alertedAtGb = null;
            alertedLevel = HealthLevel.Ok;
            return new DiskEvent(level, drive, false, changed);
        }
        // Halved since the last alert: still filling. A floor of 10 MB keeps a drive at 0 bytes from alerting every tick.
        var alert = level > alertedLevel || (alertedAtGb is { } at && drive.FreeGb <= at / 2 && at > 0.01);
        if (alert)
        {
            alertedAtGb = drive.FreeGb;
            alertedLevel = (HealthLevel)Math.Max((int)alertedLevel, (int)level);
        }
        return new DiskEvent(level, drive, alert, changed);
    }

    /// <summary>The <paramref name="count"/> biggest folders directly in <paramref name="temp"/>, by everything under each.</summary>
    public static IReadOnlyList<SpaceHog> LargestFolders(string temp, int count = 5)
    {
        if (!Directory.Exists(temp)) return [];
        var sizes = new List<SpaceHog>();
        foreach (var dir in Safe(() => Directory.EnumerateDirectories(temp, "*", Shallow)))
            sizes.Add(new SpaceHog(Path.GetFileName(dir), Safe(() => new DirectoryInfo(dir).EnumerateFiles("*", Deep)).Sum(Length)));
        return [.. sizes.Where(s => s.Bytes > 0).OrderByDescending(s => s.Bytes).Take(count)];
    }

    /// <summary>The <paramref name="count"/> biggest files anywhere under <paramref name="root"/> (%TEMP%\claude: each project's
    /// sessions, their background-command output and scratchpads), their paths relative to it.</summary>
    public static IReadOnlyList<SpaceHog> LargestFiles(string root, int count = 5)
    {
        if (!Directory.Exists(root)) return [];
        return [.. Safe(() => new DirectoryInfo(root).EnumerateFiles("*", Deep))
            .Select(f => new SpaceHog(Path.GetRelativePath(root, f.FullName), Length(f)))
            .Where(s => s.Bytes > 0).OrderByDescending(s => s.Bytes).Take(count)];
    }

    /// <summary>The alert's text: which drive, how low, and what's taking the space.</summary>
    public static string Describe(DiskEvent e, double warnGb, double criticalGb, IReadOnlyList<SpaceHog> folders, IReadOnlyList<SpaceHog> files)
    {
        var line = $"{e.Drive.Root} has {Size((long)(e.Drive.FreeGb * Gb))} free " +
            (e.Level == HealthLevel.Danger ? $"(critical under {criticalGb:0.#} GB)" : $"(warn under {warnGb:0.#} GB)") + ".";
        line += folders.Count > 0 ? " Biggest in %TEMP%: " + string.Join(", ", folders.Select(f => $"{f.Path} {Size(f.Bytes)}")) + "." : " Nothing big in %TEMP%.";
        if (files.Count > 0) line += @" Biggest under %TEMP%\claude: " + string.Join(", ", files.Select(f => $"{f.Path} {Size(f.Bytes)}")) + ".";
        return line;
    }

    public static string Size(long bytes) => bytes >= Gb ? $"{bytes / Gb:0.0} GB" : $"{bytes / (1024.0 * 1024):0} MB";

    private const double Gb = 1024.0 * 1024 * 1024;

    private static readonly EnumerationOptions Shallow = new() { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };

    // Junctions aren't followed: one pointing back up would count a folder twice, or forever.
    private static readonly EnumerationOptions Deep = new() { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };

    // A file deleted between being listed and being measured counts as nothing.
    private static long Length(FileInfo f)
    {
        try { return f.Length; }
        catch (IOException) { return 0; }
    }

    // A folder that vanishes or is locked mid-walk ends that walk with what it found, not the tick.
    private static IEnumerable<T> Safe<T>(Func<IEnumerable<T>> list)
    {
        IEnumerator<T> e;
        try { e = list().GetEnumerator(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }
        using (e)
            while (true)
            {
                try { if (!e.MoveNext()) yield break; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }
                yield return e.Current;
            }
    }
}
