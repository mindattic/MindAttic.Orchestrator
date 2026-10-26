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
    public sealed record Installed(string Name, Version Version, string Stamp, string Directory)
    {
        public string Exe => Path.Combine(Directory, ExeName);
    }

    /// <summary>The folder that holds the versions, under the app's install dir (<c>C:\Apps\MindAttic.Orchestrator</c>).</summary>
    public static string Root(string installDir) => Path.Combine(installDir, FolderName);

    /// <summary>The folder name for a build: <c>1.0.0-20261026153000</c>.</summary>
    public static string VersionFolderName(Version version, DateTime builtUtc) =>
        $"{version.ToString(3)}-{builtUtc.ToString(StampFormat, CultureInfo.InvariantCulture)}";

    /// <summary>Parses a version folder's name; null for anything else (a staging folder, a stray name).</summary>
    public static Installed? Parse(string name, string directory)
    {
        var dash = name.IndexOf('-');
        var versionText = dash < 0 ? name : name[..dash];
        var stamp = dash < 0 ? "" : name[(dash + 1)..];
        if (!Version.TryParse(versionText, out var version) || version.Major < 0 || version.Minor < 0) return null;
        if (stamp.Length > 0 && (stamp.Length != StampFormat.Length || !stamp.All(char.IsAsciiDigit))) return null;
        return new Installed(name, version, stamp, directory);
    }

    /// <summary>The installed versions that hold a TermHost exe, oldest first. Empty when the root is missing.</summary>
    public static IReadOnlyList<Installed> List(string root)
    {
        if (!Directory.Exists(root)) return [];
        var found = new List<Installed>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
                if (Parse(Path.GetFileName(dir), dir) is { } v && File.Exists(v.Exe)) found.Add(v);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return Sort(found);
    }

    /// <summary>Oldest first: by version, then build stamp, then name (so the order is total).</summary>
    public static List<Installed> Sort(IEnumerable<Installed> versions) =>
        [.. versions.OrderBy(v => v.Version).ThenBy(v => v.Stamp, StringComparer.Ordinal).ThenBy(v => v.Name, StringComparer.Ordinal)];

    /// <summary>The newest installed TermHost under <paramref name="root"/>, or null when none is installed.
    /// Whatever starts a host (TermLauncher.Start, M.ORCH-T925) takes its exe from here.</summary>
    public static Installed? Newest(string root) => List(root) is { Count: > 0 } all ? all[^1] : null;

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
    public static IReadOnlyList<string>? RunningExePaths()
    {
        var paths = new List<string>();
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
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

    /// <summary>Deletes the removable version folders under <paramref name="root"/>; returns the names deleted. Never throws:
    /// a folder that can't be deleted (something still holds a file) stays for the next pass.</summary>
    public static IReadOnlyList<string> Cleanup(string root) => Cleanup(root, RunningExePaths());

    internal static IReadOnlyList<string> Cleanup(string root, IReadOnlyList<string>? running)
    {
        if (running is null) return [];
        var deleted = new List<string>();
        foreach (var v in SelectRemovable(List(root), running))
        {
            try { Directory.Delete(v.Directory, recursive: true); deleted.Add(v.Name); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return deleted;
    }
}
