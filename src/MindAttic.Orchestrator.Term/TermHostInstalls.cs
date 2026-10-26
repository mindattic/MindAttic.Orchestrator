using System.Diagnostics;
using System.Globalization;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The side-by-side TermHost installs (M.ORCH-T932, spike docs/spikes/cli-host-viewer.md section 7): each build lives in its own
/// folder, <c>&lt;install dir&gt;\TermHost\&lt;version&gt;\</c>, and an install or redeploy never stops or overwrites a folder.
/// Running CLIs keep the host binary they started on; new sessions start on the newest (<see cref="Newest"/>); a folder older
/// than the newest is deleted once no process runs from it (<see cref="Cleanup(string)"/>).
/// <para>A folder is named <c>&lt;major&gt;.&lt;minor&gt;.&lt;patch&gt;-&lt;yyyyMMddHHmmss&gt;</c>: the assembly version, then the exe's
/// build time (UTC), so two builds of one version still order. The stamp is optional, <c>1.0.0</c> alone is valid.</para>
/// </summary>
public static class TermHostInstalls
{
    public const string FolderName = "TermHost";
    public const string ExeName = "MindAttic.Orchestrator.TermHost.exe";
    public const string StampFormat = "yyyyMMddHHmmss";

    /// <summary>One installed version folder.</summary>
    public sealed record Installed(string Name, Version Version, string Stamp, string Directory, string ExeFile = ExeName)
    {
        public string Exe => Path.Combine(Directory, ExeFile);
    }

    /// <summary>The folder that holds the versions, under the app's install dir (<c>C:\Apps\MindAttic.Orchestrator</c>).</summary>
    public static string Root(string installDir) => Path.Combine(installDir, FolderName);

    /// <summary>The folder name for a build: <c>1.0.0-20261026153000</c>.</summary>
    public static string VersionFolderName(Version version, DateTime builtUtc) =>
        $"{version.ToString(3)}-{builtUtc.ToString(StampFormat, CultureInfo.InvariantCulture)}";

    /// <summary>Parses a version folder's name; null for anything else (a staging folder, a stray name).</summary>
    public static Installed? Parse(string name, string directory, string exeFile = ExeName)
    {
        var dash = name.IndexOf('-');
        var versionText = dash < 0 ? name : name[..dash];
        var stamp = dash < 0 ? "" : name[(dash + 1)..];
        if (!Version.TryParse(versionText, out var version) || version.Major < 0 || version.Minor < 0) return null;
        if (stamp.Length > 0 && (stamp.Length != StampFormat.Length || !stamp.All(char.IsAsciiDigit))) return null;
        return new Installed(name, version, stamp, directory, exeFile);
    }

    /// <summary>The installed versions that hold a TermHost exe, oldest first. Empty when the root is missing.</summary>
    public static IReadOnlyList<Installed> List(string root, string exeFile = ExeName)
    {
        if (!Directory.Exists(root)) return [];
        var found = new List<Installed>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
                if (Parse(Path.GetFileName(dir), dir, exeFile) is { } v && File.Exists(v.Exe)) found.Add(v);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return Sort(found);
    }

    /// <summary>Oldest first: by version, then build stamp, then name (so the order is total).</summary>
    public static List<Installed> Sort(IEnumerable<Installed> versions) =>
        [.. versions.OrderBy(v => v.Version).ThenBy(v => v.Stamp, StringComparer.Ordinal).ThenBy(v => v.Name, StringComparer.Ordinal)];

    /// <summary>The newest installed TermHost under <paramref name="root"/>, or null when none is installed.
    /// Whatever starts a host (TermLauncher.Start, M.ORCH-T925) takes its exe from here.</summary>
    public static Installed? Newest(string root, string exeFile = ExeName) => List(root, exeFile) is { Count: > 0 } all ? all[^1] : null;

    /// <summary>The newest TermHost exe under the install dir, or null.</summary>
    public static string? ResolveExe(string installDir) => Newest(Root(installDir))?.Exe;

    /// <summary>
    /// The folders safe to delete: every one older than the newest that none of <paramref name="runningExePaths"/> (the main-module
    /// paths of the running processes) lies inside. The newest is never returned, nor a folder in use.
    /// </summary>
    public static IReadOnlyList<Installed> SelectRemovable(IReadOnlyList<Installed> installed, IEnumerable<string> runningExePaths)
    {
        var sorted = Sort(installed);
        if (sorted.Count < 2) return [];
        var running = runningExePaths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Normalize).ToList();
        return [.. sorted.Take(sorted.Count - 1).Where(v =>
        {
            var dir = Normalize(v.Directory) + '\\';
            return !running.Any(p => p.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
        })];
    }

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\'); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return path.TrimEnd('\\'); }
    }

    /// <summary>The main-module paths of every running TermHost. Null when one can't be read (access denied): nothing may then
    /// be deleted, since that process could be running from any folder.</summary>
    public static IReadOnlyList<string>? RunningExePaths(string exeFile = ExeName)
    {
        var paths = new List<string>();
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exeFile)))
            using (p)
            {
                try { if (p.HasExited) continue; paths.Add(p.MainModule!.FileName); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    if (!Exited(p)) return null;
                }
            }
        return paths;
    }

    private static bool Exited(Process p)
    {
        try { return p.HasExited; }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    /// <summary>How long a newer version must have existed before the one it superseded may be deleted: a host that resolved the
    /// older one just before the install is still starting from it.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    /// <summary>Deletes the removable version folders under <paramref name="root"/>; returns the names deleted. Never throws:
    /// a folder that can't be deleted (something still holds a file) stays for the next pass.</summary>
    public static IReadOnlyList<string> Cleanup(string root, string exeFile = ExeName)
    {
        // Listed before the processes are read: a host that starts in between is on a folder this pass leaves alone or
        // one it didn't list.
        var listed = List(root, exeFile);
        return Cleanup(root, listed, RunningExePaths(exeFile), Grace);
    }

    internal static IReadOnlyList<string> Cleanup(string root, IReadOnlyList<string>? running, TimeSpan grace) =>
        Cleanup(root, List(root), running, grace);

    private static IReadOnlyList<string> Cleanup(string root, IReadOnlyList<Installed> listed, IReadOnlyList<string>? running, TimeSpan grace)
    {
        if (running is null) return [];
        SweepDeleting(root);
        var sorted = Sort(listed);
        var deleted = new List<string>();
        foreach (var v in SelectRemovable(sorted, running))
        {
            // The version that superseded it must have been there a while (its folder was created then).
            var successor = sorted[sorted.FindIndex(x => x.Name == v.Name) + 1];
            try { if (DateTime.UtcNow - Directory.GetCreationTimeUtc(successor.Directory) < grace) continue; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            if (Remove(v.Directory)) deleted.Add(v.Name);
        }
        return deleted;
    }

    /// <summary>Renames the folder to a non-version name first (that fails whole when something holds a file in it), so a
    /// half-deleted folder is never listed as an install, then deletes it.</summary>
    private static bool Remove(string directory)
    {
        var parent = Path.GetDirectoryName(directory)!;
        var aside = Path.Combine(parent, "." + Path.GetFileName(directory) + DeletingSuffix);
        try
        {
            if (Directory.Exists(aside)) Directory.Delete(aside, recursive: true);
            Directory.Move(directory, aside);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        try { Directory.Delete(aside, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return true;
    }

    private const string DeletingSuffix = ".deleting";

    private static void SweepDeleting(string root)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "*" + DeletingSuffix))
                try { Directory.Delete(dir, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>
/// The side-by-side team gateway installs (M.ORCH-T967): the same <c>&lt;version&gt;-&lt;stamp&gt;</c> folders as
/// <see cref="TermHostInstalls"/>, under <c>&lt;install dir&gt;\Gateway\</c>. The daemon's GatewaySupervisor resolves the exe here;
/// a running gateway keeps its folder until it exits (a restart picks the newest), and a folder nothing runs from is cleaned up.
/// </summary>
public static class GatewayInstalls
{
    public const string FolderName = "Gateway";
    public const string ExeName = TermGateway.ExeName;

    public static string Root(string installDir) => Path.Combine(installDir, FolderName);

    public static IReadOnlyList<TermHostInstalls.Installed> List(string root) => TermHostInstalls.List(root, ExeName);

    public static TermHostInstalls.Installed? Newest(string root) => TermHostInstalls.Newest(root, ExeName);

    /// <summary>The newest Gateway exe under the install dir, or null.</summary>
    public static string? ResolveExe(string installDir) => Newest(Root(installDir))?.Exe;

    public static IReadOnlyList<string> Cleanup(string root) => TermHostInstalls.Cleanup(root, ExeName);
}
