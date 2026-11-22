using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A git repo found under the workspace root that isn't on the Launcher's roster yet.</summary>
public sealed record DiscoveredRepo(string Name, string Path);

/// <summary>
/// The write side of the Launcher's roster (M.ORCH-T1987): <see cref="ProjectRoster"/> only reads it. <see cref="FindUnregistered"/>
/// scans the workspace root for git repos the roster doesn't know about yet, the same way the Launcher's own
/// Services.ProjectDiscovery/Menus.DiscoverProjectsMenu offer new repos at startup; <see cref="Append"/> writes the ones picked
/// back into the same settings.json, as the {name, path, repoUrl} entries <see cref="ProjectRoster.Load"/> reads, leaving every
/// other entry and field in the file untouched. The Launcher's write side also writes a Windows Terminal colour scheme for the
/// new project's tab; that has nothing to plug into here, since Orchestrator hosts CLIs as embedded TermHost panes, never `wt`
/// tabs, and already has its own login colour concept (LoginLook/MainWindow.ChangeColor) for the thing a tab's colour actually
/// is in this app.
/// </summary>
public static class ProjectDiscovery
{
    /// <summary>
    /// Immediate subdirectories of <paramref name="root"/> that contain a git repo (a <c>.git</c> directory, or a <c>.git</c>
    /// file for a worktree) and aren't already in <paramref name="roster"/> by path (case-insensitive, trailing separators
    /// ignored). Empty when the root is missing or unreadable.
    /// </summary>
    public static IReadOnlyList<DiscoveredRepo> FindUnregistered(IReadOnlyList<RosterProject> roster, string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return [];

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in roster)
            if (!string.IsNullOrWhiteSpace(p.Path))
                known.Add(Normalize(p.Path));

        // Materialize the listing inside the guard: an unreadable root (a permission-denied mount, a disconnected network
        // drive) would otherwise throw straight out of the menu click; degrade to "nothing found" instead.
        string[] subdirs;
        try { subdirs = Directory.GetDirectories(root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }

        var found = new List<DiscoveredRepo>();
        foreach (var dir in subdirs)
        {
            if (!IsGitRepo(dir)) continue;
            if (known.Contains(Normalize(dir))) continue;
            found.Add(new DiscoveredRepo(Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)), dir));
        }

        return found.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsGitRepo(string dir)
    {
        var dotGit = Path.Combine(dir, ".git");
        return Directory.Exists(dotGit) || File.Exists(dotGit);
    }

    /// <summary>Canonical comparison key for a path: full path, trailing separators stripped, the same normalization
    /// <see cref="ProjectRoster"/>'s own callers expect so a settings entry written as <c>D:\…\Foo\</c> matches a scanned
    /// <c>D:\…\Foo</c>.</summary>
    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Un-normalizable junk in settings shouldn't crash discovery; compare the raw string instead so it just
            // won't match a real scanned path.
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>The git remote URL (origin) for <paramref name="repoPath"/> — the same <c>git -C &lt;path&gt; remote
    /// get-url origin</c> read <see cref="ProjectCommit"/>'s own git calls use — or null when it can't be read (no
    /// remote, not a git checkout, or git isn't on PATH).</summary>
    public static string? RemoteUrl(string repoPath, IProcessRunner? runner = null)
    {
        try
        {
            var r = (runner ?? ProcessRunner.Default).Run("git", ["-C", repoPath, "remote", "get-url", "origin"], ProjectCommit.GitTimeout);
            if (r.ExitCode != 0 || r.TimedOut) return null;
            var url = r.Output.Trim();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    /// <summary>
    /// Appends <paramref name="repos"/> to the roster's settings.json (<see cref="ProjectRoster.SettingsPath"/> by default) as
    /// <c>{name, path, repoUrl}</c> entries — the same shape <see cref="ProjectRoster.Load"/> reads — leaving every other
    /// entry and field already in the file untouched. <paramref name="remoteUrl"/> reads each repo's origin, defaulting to
    /// <see cref="RemoteUrl"/>; a missing file is created with just a "projects" array. Returns how many were written; 0 when
    /// the file couldn't be parsed or written (a locked file, bad JSON already in it).
    /// </summary>
    public static int Append(IReadOnlyList<DiscoveredRepo> repos, string? settingsPath = null, Func<string, string?>? remoteUrl = null)
    {
        if (repos.Count == 0) return 0;
        var file = settingsPath ?? ProjectRoster.SettingsPath;
        remoteUrl ??= path => RemoteUrl(path);
        try
        {
            JsonObject root;
            if (File.Exists(file))
            {
                var text = File.ReadAllText(file);
                root = (string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text) as JsonObject) ?? new JsonObject();
            }
            else root = new JsonObject();

            if (root["projects"] is not JsonArray projects)
            {
                projects = new JsonArray();
                root["projects"] = projects;
            }

            foreach (var repo in repos)
                projects.Add(new JsonObject
                {
                    ["name"] = repo.Name,
                    ["path"] = repo.Path,
                    ["repoUrl"] = remoteUrl(repo.Path),
                });

            var dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(file, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return repos.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return 0; }
    }
}
