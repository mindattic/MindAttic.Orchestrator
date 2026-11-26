namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The copy of the deployed Launcher that the dashboard's tabs run from (M.ORCH-T2285), so a Launcher redeploy never stops a
/// live agent tab.
/// <para>MindAttic.Launcher's run.ps1 -Redeploy (deploy-lib's Publish-AppConfig) stops every process running from the folder it
/// replaces, <c>R:\MindAttic\Apps\MindAttic.Launcher\Release</c>. Every hosted agent tab is a <c>MindAttic.Launcher host ...</c>
/// process, so tabs started straight from <see cref="OrchestratorOptions.LauncherExe"/> would all die on each redeploy. Instead,
/// every Launcher the dashboard starts (menus, host tabs, the hidden commit) runs from a snapshot of that folder under
/// <c>&lt;Launcher&gt;\Run\&lt;stamp&gt;</c>, one per build: a redeploy replaces Release only, the tabs already open keep running
/// from their snapshot, and the next tab started picks up the new build in a fresh snapshot. A Launcher menu passes its own exe
/// path to the host tabs it opens, so those run from the snapshot too.</para>
/// <para>Old snapshots are deleted once nothing runs from them: the exe goes first, and Windows refuses to delete a running
/// exe, so a snapshot still in use is left whole.</para>
/// </summary>
public static class LauncherRunCopy
{
    /// <summary>The folder beside the deploy configs (Debug, Release) that holds the snapshots.</summary>
    public const string FolderName = "Run";

    private static readonly Lock Gate = new();

    /// <summary>The exe to start for <paramref name="deployedExe"/>: its snapshot (made now when this build has none yet), else the
    /// newest snapshot when the deployed exe is missing (mid-redeploy), else <paramref name="deployedExe"/> itself when no copy
    /// can be made, so a tab still opens. Never throws.</summary>
    public static string Resolve(string deployedExe)
    {
        try
        {
            var deployDir = Path.GetDirectoryName(Path.GetFullPath(deployedExe));
            var appDir = deployDir is null ? null : Path.GetDirectoryName(deployDir);
            if (deployDir is null || appDir is null) return deployedExe;
            var runRoot = Path.Combine(appDir, FolderName);
            var exeName = Path.GetFileName(deployedExe);
            lock (Gate)
            {
                var exe = new FileInfo(deployedExe);
                if (!exe.Exists) return Newest(runRoot, exeName) ?? deployedExe;
                var stamp = $"{exe.LastWriteTimeUtc:yyyyMMdd-HHmmss-fffffff}-{exe.Length:x}";
                var target = Path.Combine(runRoot, stamp);
                var targetExe = Path.Combine(target, exeName);
                if (!File.Exists(targetExe))
                {
                    var staging = Path.Combine(runRoot, $".{stamp}.{Guid.NewGuid():N}");
                    try
                    {
                        CopyTree(deployDir, staging);
                        // The build changed under the copy (a redeploy mid-copy): run what's there rather than a mixed snapshot.
                        exe.Refresh();
                        if (!exe.Exists || $"{exe.LastWriteTimeUtc:yyyyMMdd-HHmmss-fffffff}-{exe.Length:x}" != stamp)
                            return Newest(runRoot, exeName) ?? deployedExe;
                        try { Directory.Move(staging, target); }
                        catch (IOException) when (File.Exists(targetExe)) { } // another process made the same snapshot first
                    }
                    finally { TryDelete(staging); }
                }
                if (!File.Exists(targetExe)) return deployedExe;
                Prune(runRoot, keep: stamp, exeName);
                return targetExe;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            OrchestratorLog.For("Launcher").Warning(ex, "Couldn't snapshot the Launcher at {Exe}; starting it in place", deployedExe);
            return deployedExe;
        }
    }

    /// <summary>The newest snapshot's exe under <paramref name="runRoot"/>, or null when there's none.</summary>
    private static string? Newest(string runRoot, string exeName)
    {
        if (!Directory.Exists(runRoot)) return null;
        return new DirectoryInfo(runRoot).EnumerateDirectories()
            .Where(d => !d.Name.StartsWith('.') && File.Exists(Path.Combine(d.FullName, exeName)))
            .OrderByDescending(d => d.Name, StringComparer.Ordinal)
            .Select(d => Path.Combine(d.FullName, exeName))
            .FirstOrDefault();
    }

    /// <summary>Deletes every snapshot but <paramref name="keep"/> that nothing runs from, and staging folders left by a crash.</summary>
    private static void Prune(string runRoot, string keep, string exeName)
    {
        foreach (var dir in new DirectoryInfo(runRoot).EnumerateDirectories())
        {
            if (dir.Name == keep) continue;
            if (dir.Name.StartsWith('.'))
            {
                // A staging folder another process may still be filling: only one left from well before is a crash's.
                if (dir.CreationTimeUtc < DateTime.UtcNow.AddHours(-1)) TryDelete(dir.FullName);
                continue;
            }
            // A running exe can't be deleted, so a snapshot a live tab runs from fails here and is left whole.
            try { File.Delete(Path.Combine(dir.FullName, exeName)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            TryDelete(dir.FullName);
        }
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.EnumerateDirectories(from)) CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
