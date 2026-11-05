namespace MindAttic.Orchestrator.Activity;

/// <summary>One thing directly under a Claude session's temp folder (or its scratchpad), measured as a whole.</summary>
public sealed record TempEntry(string Path, string Name, long Bytes, DateTime NewestWriteUtc);

/// <summary>A Claude session's folder in %TEMP%\claude\&lt;project&gt;\&lt;session&gt;: whether its CLI still runs, when anything in it
/// was last written, what sits directly in it and what sits directly in its scratchpad.</summary>
public sealed record SessionTemp(string Path, string SessionId, bool Live, DateTime NewestWriteUtc,
    IReadOnlyList<TempEntry> Entries, IReadOnlyList<TempEntry> Scratch);

/// <summary>Something the cleanup would delete, and why.</summary>
public sealed record CleanupItem(string Path, long Bytes, DateTime NewestWriteUtc, string Reason);

/// <summary>What a cleanup run did: what it freed, what it deleted, what it couldn't, and the big things it only names.</summary>
public sealed record CleanupResult(double FreeGbBefore, long FreedBytes, IReadOnlyList<CleanupItem> Deleted,
    IReadOnlyList<string> Errors, IReadOnlyList<SpaceHog> NotOurs);

/// <summary>
/// Disk self-cleanup (M.ORCH-T1449). Found live 2026-11-05: C: filled to 1.2 MB free, 27.6 GB of it one session's scratch
/// copies of an Unreal project under %TEMP%\claude. Below <see cref="Settings.OrchestratorSettings.DiskCleanupGb"/> it deletes
/// agent-made temp only, oldest first, until there's that line plus <see cref="MarginGb"/> free:
/// (1) the folders of sessions whose CLI no longer runs and that nothing has written for <see cref="EndedQuiet"/>, all but
/// their <c>tasks</c> output; (2) in running sessions, scratchpad entries of at least <see cref="MinScratchBytes"/> nothing has
/// written for <see cref="ScratchQuiet"/> (copied build trees, snapshots). Nothing written in the last <see cref="Recent"/>,
/// nothing under <c>tasks</c> and nothing outside %TEMP%\claude's session folders is touched; the rest of %TEMP%, the page
/// file and the hibernation file are only named. Tool worktrees are never deleted: they're git checkouts that can hold work.
/// </summary>
public static class DiskCleanup
{
    public const double MarginGb = 10;
    public static readonly TimeSpan EndedQuiet = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ScratchQuiet = TimeSpan.FromHours(2);
    public static readonly TimeSpan Recent = TimeSpan.FromMinutes(15);
    public const long MinScratchBytes = 64L * 1024 * 1024;
    public const string TasksFolder = "tasks", ScratchFolder = "scratchpad";

    private const double Gb = 1024.0 * 1024 * 1024;

    /// <summary>Everything that may go, oldest first. Pure: the folders are described, not read.</summary>
    public static IReadOnlyList<CleanupItem> Candidates(IEnumerable<SessionTemp> sessions, DateTime nowUtc)
    {
        var items = new List<CleanupItem>();
        foreach (var s in sessions)
        {
            if (!s.Live && nowUtc - s.NewestWriteUtc >= EndedQuiet)
                items.AddRange(s.Entries
                    .Where(e => !e.Name.Equals(TasksFolder, StringComparison.OrdinalIgnoreCase) && nowUtc - e.NewestWriteUtc >= Recent)
                    .Select(e => new CleanupItem(e.Path, e.Bytes, e.NewestWriteUtc, $"ended session {s.SessionId}")));
            else
                items.AddRange(s.Scratch
                    .Where(e => e.Bytes >= MinScratchBytes && nowUtc - e.NewestWriteUtc >= ScratchQuiet)
                    .Select(e => new CleanupItem(e.Path, e.Bytes, e.NewestWriteUtc, $"stale scratch in {(s.Live ? "running" : "recent")} session {s.SessionId}")));
        }
        return [.. items.Where(i => i.Bytes > 0).OrderBy(i => i.NewestWriteUtc)];
    }

    /// <summary>The oldest of <paramref name="candidates"/> that together free <paramref name="needBytes"/> (all of them when
    /// they don't reach it); nothing when nothing is needed.</summary>
    public static IReadOnlyList<CleanupItem> Plan(IReadOnlyList<CleanupItem> candidates, long needBytes)
    {
        var plan = new List<CleanupItem>();
        long sum = 0;
        foreach (var c in candidates)
        {
            if (sum >= needBytes) break;
            plan.Add(c);
            sum += c.Bytes;
        }
        return plan;
    }

    /// <summary>Bytes to free so <paramref name="freeGb"/> reaches <paramref name="lineGb"/> + <see cref="MarginGb"/>; 0 when the
    /// drive is at or over the line, or the line is 0 (off).</summary>
    public static long Need(double freeGb, double lineGb) =>
        lineGb <= 0 || freeGb >= lineGb ? 0 : (long)((lineGb + MarginGb - freeGb) * Gb);

    /// <summary>Reads the session folders under <paramref name="claudeTemp"/> (%TEMP%\claude\&lt;project&gt;\&lt;session&gt;); only
    /// folders named as a session id count, so Claude Code's own caches beside them (bundled skills, edit diffs) are never
    /// touched. <paramref name="liveSessions"/> are the sessions whose CLI runs.</summary>
    public static IReadOnlyList<SessionTemp> Scan(string claudeTemp, IReadOnlySet<string> liveSessions)
    {
        var found = new List<SessionTemp>();
        if (!Directory.Exists(claudeTemp)) return found;
        foreach (var project in Safe(() => Directory.EnumerateDirectories(claudeTemp, "*", Shallow)))
            foreach (var dir in Safe(() => Directory.EnumerateDirectories(project, "*", Shallow)))
            {
                var id = Path.GetFileName(dir);
                if (!Guid.TryParse(id, out _)) continue;
                var entries = Measure(dir);
                var scratch = Measure(Path.Combine(dir, ScratchFolder));
                var newest = entries.Count > 0 ? entries.Max(e => e.NewestWriteUtc) : Directory.GetLastWriteTimeUtc(dir);
                found.Add(new SessionTemp(dir, id, liveSessions.Contains(id), newest, entries, scratch));
            }
        return found;
    }

    /// <summary>
    /// Runs the cleanup on %TEMP%'s drive when it's under <paramref name="lineGb"/> (or always plans, with
    /// <paramref name="dryRun"/>, which deletes nothing). Every deletion is appended to <paramref name="logPath"/>.
    /// </summary>
    public static CleanupResult Run(double lineGb, IReadOnlySet<string> liveSessions, DateTime nowUtc, bool dryRun,
        string? logPath, string? tempDir = null)
    {
        var temp = tempDir ?? Path.GetTempPath();
        var freeGb = FreeGb(temp);
        var need = Need(freeGb, lineGb);
        var candidates = Candidates(Scan(Path.Combine(temp, "claude"), liveSessions), nowUtc);
        var plan = dryRun ? Plan(candidates, need > 0 ? need : long.MaxValue) : Plan(candidates, need);
        var deleted = new List<CleanupItem>();
        var errors = new List<string>();
        long freed = 0;
        if (!dryRun)
            foreach (var item in plan)
                try
                {
                    if (Directory.Exists(item.Path)) Directory.Delete(item.Path, recursive: true);
                    else if (File.Exists(item.Path)) File.Delete(item.Path);
                    deleted.Add(item);
                    freed += item.Bytes;
                    Log(logPath, nowUtc, item);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{item.Path}: {ex.Message}");
                }
        return new CleanupResult(freeGb, dryRun ? plan.Sum(p => p.Bytes) : freed, dryRun ? plan : deleted, errors, NotOurs(temp));
    }

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
        var line = $"{verb} {DiskWatch.Size(r.FreedBytes)} of agent temp ({r.Deleted.Count} item{(r.Deleted.Count == 1 ? "" : "s")}; " +
            $"{r.FreeGbBefore:0.0} GB was free).";
        if (r.Deleted.Count > 0)
            line += " Biggest: " + string.Join(", ", r.Deleted.OrderByDescending(d => d.Bytes).Take(3).Select(d => $"{Path.GetFileName(d.Path)} {DiskWatch.Size(d.Bytes)}")) + ".";
        if (r.Errors.Count > 0) line += $" {r.Errors.Count} couldn't be deleted (in use).";
        if (r.NotOurs.Count > 0)
            line += " Not agent-made, left alone: " + string.Join(", ", r.NotOurs.Select(h => $"{h.Path} {DiskWatch.Size(h.Bytes)}")) + ".";
        return line;
    }

    /// <summary>The sessions whose Claude CLI runs, on the global login and every login of its own (each keeps its context
    /// gate's marks in its own session-context).</summary>
    public static IReadOnlySet<string> LiveSessionIds(DateTime nowUtc)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new List<string> { new Sessions.ContextCycler().Directory };
        try { dirs.AddRange(new Sessions.AccountDirs().All().Select(a => a.MarksDirectory)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
            try { ids.UnionWith(new Sessions.ContextCycler(dir).LiveSessions(nowUtc).Keys); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return ids;
    }

    /// <summary>Where every deletion is written down: beside the orchestrator's database.</summary>
    public static string LogPath => Path.Combine(Path.GetDirectoryName(Settings.OrchestratorDb.LivePath) ?? ".", "disk-cleanup.log");

    public static double FreeGb(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace / Gb; }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { return double.MaxValue; }
    }

    private static void Log(string? logPath, DateTime nowUtc, CleanupItem item)
    {
        if (logPath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{nowUtc:yyyy-MM-ddTHH:mm:ssZ}\t{item.Bytes}\t{item.Reason}\t{item.Path}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static IReadOnlyList<TempEntry> Measure(string dir)
    {
        if (!Directory.Exists(dir)) return [];
        var list = new List<TempEntry>();
        foreach (var path in Safe(() => Directory.EnumerateFileSystemEntries(dir, "*", Shallow)))
        {
            long bytes = 0;
            var newest = DateTime.MinValue;
            if (Directory.Exists(path))
            {
                newest = Directory.GetLastWriteTimeUtc(path);
                foreach (var f in Safe(() => new DirectoryInfo(path).EnumerateFiles("*", Deep)))
                    try
                    {
                        bytes += f.Length;
                        if (f.LastWriteTimeUtc > newest) newest = f.LastWriteTimeUtc;
                    }
                    catch (IOException) { }
            }
            else
                try
                {
                    var f = new FileInfo(path);
                    bytes = f.Length;
                    newest = f.LastWriteTimeUtc;
                }
                catch (IOException) { continue; }
            list.Add(new TempEntry(path, Path.GetFileName(path), bytes, newest));
        }
        return list;
    }

    private static readonly EnumerationOptions Shallow = new() { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
    private static readonly EnumerationOptions Deep = new() { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };

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
