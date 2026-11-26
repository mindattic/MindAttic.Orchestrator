using System.Security.Cryptography;
using System.Text;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The copy of the deployed Launcher that the dashboard's tabs run from (M.ORCH-T2285), so a Launcher redeploy never stops a
/// live agent tab.
/// <para>MindAttic.Launcher's run.ps1 -Redeploy (deploy-lib's Publish-AppConfig) stops every process running from the folder it
/// replaces, <c>R:\MindAttic\Apps\MindAttic.Launcher\Release</c>. Every hosted agent tab is a <c>MindAttic.Launcher host ...</c>
/// process, so tabs started straight from <see cref="OrchestratorOptions.LauncherExe"/> would all die on each redeploy. Instead,
/// every Launcher the dashboard starts (menus, host tabs, the hidden commit) runs from a snapshot of that folder under
/// <c>&lt;Launcher&gt;\Run\&lt;build&gt;</c>, one per build: a redeploy replaces Release only, the tabs already open keep running
/// from their snapshot, and tabs started after it pick up the new build in a fresh snapshot. A Launcher menu passes its own exe
/// path to the host tabs it opens, so those run from the snapshot too.</para>
/// <para>A build is named by a fingerprint of every file in the deploy folder (name, length, write time), taken before and after
/// the copy, so a copy made while a redeploy was writing files is thrown away rather than kept as a mix of two builds. Once any
/// snapshot exists, a new build's copy is made in the background and the newest snapshot runs meanwhile, so no caller (the UI
/// thread among them) waits on a 100 MB copy; only the very first snapshot is made inline.</para>
/// <para>Old snapshots are deleted once nothing runs from them and none was handed out in the last
/// <see cref="PickedGrace"/> (each one handed out is touched, across processes): the exe goes first, and Windows refuses to
/// delete a running exe, so a snapshot still in use is left whole.</para>
/// </summary>
public static class LauncherRunCopy
{
    /// <summary>The folder beside the deploy configs (Debug, Release) that holds the snapshots.</summary>
    public const string FolderName = "Run";

    /// <summary>How long after a snapshot was last handed out it's kept even with nothing running from it, so a process that
    /// picked it but hasn't started its exe yet (another process's prune in between) still finds it.</summary>
    public static TimeSpan PickedGrace { get; set; } = TimeSpan.FromMinutes(10);

    private static readonly Lock Gate = new();
    private static readonly HashSet<string> InFlight = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The exe to start for <paramref name="deployedExe"/>: this build's snapshot (made inline only when there's no
    /// snapshot at all yet; otherwise made in the background while the newest existing one is answered), else the newest snapshot
    /// when the deployed exe is missing (mid-redeploy), else <paramref name="deployedExe"/> itself, so a tab still opens.
    /// Never throws.</summary>
    public static string Resolve(string deployedExe)
    {
        try
        {
            var deployDir = Path.GetDirectoryName(Path.GetFullPath(deployedExe));
            var appDir = deployDir is null ? null : Path.GetDirectoryName(deployDir);
            if (deployDir is null || appDir is null) return deployedExe;
            var runRoot = Path.Combine(appDir, FolderName);
            var exeName = Path.GetFileName(deployedExe);
            var exe = new FileInfo(deployedExe);
            if (!exe.Exists) return Picked(Newest(runRoot, exeName)) ?? deployedExe;
            var name = $"{exe.LastWriteTimeUtc:yyyyMMdd-HHmmss}-{Fingerprint(deployDir)}";
            var targetExe = Path.Combine(runRoot, name, exeName);
            if (File.Exists(targetExe))
            {
                Picked(targetExe);
                Prune(runRoot, keep: name, exeName);
                return targetExe;
            }
            if (Newest(runRoot, exeName) is { } newest)
            {
                lock (Gate)
                    if (InFlight.Add(targetExe))
                        _ = Task.Run(() =>
                        {
                            try { Snapshot(deployDir, runRoot, name, exeName); }
                            finally { lock (Gate) InFlight.Remove(targetExe); }
                        });
                return Picked(newest)!;
            }
            return Picked(Snapshot(deployDir, runRoot, name, exeName)) ?? deployedExe;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            OrchestratorLog.For("Launcher").Warning(ex, "Couldn't snapshot the Launcher at {Exe}; starting it in place", deployedExe);
            return deployedExe;
        }
    }

    /// <summary>Copies <paramref name="deployDir"/> to <paramref name="runRoot"/>\<paramref name="name"/>; the snapshot's exe, or
    /// null when the folder changed during the copy (a redeploy mid-copy) or the copy failed. Never throws.</summary>
    private static string? Snapshot(string deployDir, string runRoot, string name, string exeName)
    {
        var target = Path.Combine(runRoot, name);
        var targetExe = Path.Combine(target, exeName);
        var staging = Path.Combine(runRoot, $".{name}.{Guid.NewGuid():N}");
        try
        {
            CopyTree(deployDir, staging);
            if (!name.EndsWith("-" + Fingerprint(deployDir), StringComparison.Ordinal)) return null;
            try { Directory.Move(staging, target); }
            catch (IOException) when (File.Exists(targetExe)) { } // another process made the same snapshot first
            return File.Exists(targetExe) ? targetExe : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            OrchestratorLog.For("Launcher").Warning(ex, "Couldn't snapshot the Launcher from {Folder}", deployDir);
            return null;
        }
        finally { TryDelete(staging); }
    }

    /// <summary>A short hash of every file under <paramref name="dir"/>: its relative name, length and write time.</summary>
    internal static string Fingerprint(string dir)
    {
        var text = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            text.Append(Path.GetRelativePath(dir, file).ToLowerInvariant()).Append('|').Append(info.Length).Append('|')
                .Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..12].ToLowerInvariant();
    }

    /// <summary>Marks <paramref name="exe"/>'s snapshot as just handed out (its folder's write time), so no prune takes it from
    /// under a start that hasn't run it yet; answers <paramref name="exe"/>.</summary>
    private static string? Picked(string? exe)
    {
        if (exe is not null && Path.GetDirectoryName(exe) is { } dir)
            try { Directory.SetLastWriteTimeUtc(dir, DateTime.UtcNow); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return exe;
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

    /// <summary>Deletes every snapshot but <paramref name="keep"/> that nothing runs from and nobody picked lately, and staging
    /// folders left by a crash.</summary>
    private static void Prune(string runRoot, string keep, string exeName)
    {
        var now = DateTime.UtcNow;
        foreach (var dir in new DirectoryInfo(runRoot).EnumerateDirectories())
        {
            if (dir.Name == keep) continue;
            if (dir.Name.StartsWith('.'))
            {
                // A staging folder another process may still be filling: only one left from well before is a crash's.
                if (dir.CreationTimeUtc < now.AddHours(-1)) TryDelete(dir.FullName);
                continue;
            }
            if (dir.LastWriteTimeUtc > now - PickedGrace) continue;
            // A running exe can't be deleted, so a snapshot a live tab runs from fails here and is left whole.
            try { File.Delete(Path.Combine(dir.FullName, exeName)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            TryDelete(dir.FullName);
        }
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from))
        {
            var dest = Path.Combine(to, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
            File.SetLastWriteTimeUtc(dest, File.GetLastWriteTimeUtc(file));
        }
        foreach (var dir in Directory.EnumerateDirectories(from)) CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
