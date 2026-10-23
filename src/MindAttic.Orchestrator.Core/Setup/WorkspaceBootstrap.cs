using System.Diagnostics;
using System.Text.Json;

namespace MindAttic.Orchestrator.Setup;

/// <summary>One repo of the workspace: the folder it lives in under the root, and where to clone it from.</summary>
public sealed record ManifestRepo(string Name, string Remote);

/// <summary>
/// The workspace as a list: the root repo (which versions the shared agent standard) and every repo
/// that sits in it. It lives in the root repo itself, at <see cref="RelativePath"/>, so cloning the
/// root is enough to know what else to clone.
/// </summary>
public sealed record WorkspaceManifest(string Root, IReadOnlyList<ManifestRepo> Repos)
{
    public const string RelativePath = @"docs\workspace-repos.json";
}

public sealed record BootstrapResult(int Cloned, int AlreadyThere, IReadOnlyList<string> Failed);

/// <summary>
/// Builds a workspace on a machine that has none: clone the root repo into an empty folder, read its
/// manifest, clone every repo it lists. Run it again on a partial workspace and it only clones what's
/// missing. <see cref="Export"/> writes the manifest from a live workspace.
/// </summary>
public static class WorkspaceBootstrap
{
    /// <summary>The MindAttic workspace root repo.</summary>
    public const string DefaultRootRemote = "https://github.com/mindattic/codex-standard.git";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>True when there's nothing at <paramref name="folder"/> yet: no folder, or an empty one.</summary>
    public static bool IsEmpty(string folder) => !Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any();

    /// <summary>Every git repo directly under <paramref name="root"/> that has an origin, sorted by name.</summary>
    public static WorkspaceManifest Export(string root)
    {
        var repos = new List<ManifestRepo>();
        foreach (var dir in Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(Path.Combine(dir, ".git")) && !File.Exists(Path.Combine(dir, ".git"))) continue;
            var (code, url) = Git(dir, "remote", "get-url", "origin");
            if (code == 0 && url.Trim().Length > 0) repos.Add(new ManifestRepo(Path.GetFileName(dir), url.Trim()));
        }
        var (rootCode, rootUrl) = Git(root, "remote", "get-url", "origin");
        return new WorkspaceManifest(rootCode == 0 && rootUrl.Trim().Length > 0 ? rootUrl.Trim() : DefaultRootRemote, repos);
    }

    public static void Save(WorkspaceManifest manifest, string root)
    {
        var path = Path.Combine(root, WorkspaceManifest.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, Json) + Environment.NewLine);
    }

    public static WorkspaceManifest? Load(string root)
    {
        var path = Path.Combine(root, WorkspaceManifest.RelativePath);
        if (!File.Exists(path)) return null;
        try
        {
            // Checked, not trusted: {} or "Repos": null is no manifest, and a repo missing its name or remote is
            // listed as failed (IsSafeName), not a NullReferenceException mid-build (M.ORCH-T377).
            var manifest = JsonSerializer.Deserialize<WorkspaceManifest>(File.ReadAllText(path));
            return manifest?.Repos is null ? null : manifest with { Repos = manifest.Repos.Where(r => r is not null).ToList() };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>A repo folder name that can't escape the workspace: the manifest comes from a repo, so it's checked, not trusted.</summary>
    public static bool IsSafeName(string? name) =>
        !string.IsNullOrEmpty(name) && name is not "." and not ".." && name.IndexOfAny(['\\', '/', ':']) < 0 &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// Clones the root repo into <paramref name="folder"/> (unless it already is a clone), then every
    /// repo its manifest lists that isn't there yet, <paramref name="parallel"/> at a time. Failures are
    /// collected, not thrown: one private repo you can't reach shouldn't stop the other fifty.
    /// </summary>
    public static async Task<BootstrapResult> BuildAsync(string folder, string rootRemote, IProgress<string> progress,
        int parallel = 4, CancellationToken cancel = default)
    {
        if (!Directory.Exists(Path.Combine(folder, ".git")))
        {
            if (!IsEmpty(folder)) throw new InvalidOperationException($"{folder} isn't empty and isn't the workspace repo; pick an empty folder.");
            Directory.CreateDirectory(folder);
            progress.Report($"Cloning the workspace root from {rootRemote}…");
            var (code, output) = await Task.Run(() => Git(folder, "clone", "--", rootRemote, "."), cancel);
            if (code != 0) throw new InvalidOperationException($"Couldn't clone the workspace root: {output.Trim()}");
        }

        var manifest = Load(folder)
            ?? throw new InvalidOperationException($"The workspace root has no {WorkspaceManifest.RelativePath}; run `orchestrator workspace export` on a machine that has the workspace, and push it.");

        static bool Usable(ManifestRepo r) => IsSafeName(r.Name) && !string.IsNullOrWhiteSpace(r.Remote);
        var todo = manifest.Repos.Where(r => Usable(r) && !Directory.Exists(Path.Combine(folder, r.Name))).ToList();
        var unsafeNames = manifest.Repos.Where(r => !Usable(r))
            .Select(r => IsSafeName(r.Name) ? $"{r.Name} (no remote to clone from)" : $"{r.Name ?? "(no name)"} (not a plain folder name)").ToList();
        var failed = new List<string>(unsafeNames);
        var done = 0;
        progress.Report($"{manifest.Repos.Count} repos in the workspace; {todo.Count} to clone.");

        await Parallel.ForEachAsync(todo, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel), CancellationToken = cancel }, (repo, _) =>
        {
            var (code, output) = Git(folder, "clone", "--", repo.Remote, repo.Name);
            lock (failed)
            {
                done++;
                if (code != 0) failed.Add($"{repo.Name}: {output.Trim().Split('\n').LastOrDefault()?.Trim()}");
                progress.Report(code == 0 ? $"[{done}/{todo.Count}] {repo.Name}" : $"[{done}/{todo.Count}] {repo.Name} FAILED");
            }
            return ValueTask.CompletedTask;
        });

        return new BootstrapResult(todo.Count - (failed.Count - unsafeNames.Count()), manifest.Repos.Count - todo.Count - unsafeNames.Count(), failed);
    }

    /// <summary>The apps in a workspace: repos with a deploy.ps1 at the root (the house way to install one). The
    /// orchestrator itself is left out: it installs itself, and redeploying would stop the running copy.</summary>
    public static IReadOnlyList<string> Apps(string folder) =>
        [.. Directory.EnumerateDirectories(folder)
            .Where(d => File.Exists(Path.Combine(d, "deploy.ps1")) && !Path.GetFileName(d).Equals("MindAttic.Orchestrator", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Order(StringComparer.OrdinalIgnoreCase)!];

    /// <summary>How long a failed app's output is waited for once it has exited (M.ORCH-T354).</summary>
    internal static TimeSpan OutputGrace { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs each app's deploy.ps1 (no -Launch, so nothing opens), one at a time since several build
    /// shared packages, with a time limit per app. Returns the apps that failed.
    /// </summary>
    public static async Task<IReadOnlyList<string>> InstallAppsAsync(string folder, IProgress<string> progress, TimeSpan? perApp = null, CancellationToken cancel = default)
    {
        var failed = new List<string>();
        var apps = Apps(folder);
        for (var i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            progress.Report($"[{i + 1}/{apps.Count}] installing {app}…");
            var psi = new ProcessStartInfo("powershell")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Path.Combine(folder, app),
            };
            foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(folder, app, "deploy.ps1") }) psi.ArgumentList.Add(a);
            try
            {
                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEndAsync(cancel);
                var errors = p.StandardError.ReadToEndAsync(cancel);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(perApp ?? TimeSpan.FromMinutes(15));
                try { await p.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException)
                {
                    try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException) { } // exited, or part of the tree couldn't be ended
                    // Kill returns before the process is gone, and it holds its working folder (the app's) until it is.
                    try { await p.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) { }
                    if (cancel.IsCancellationRequested) throw; // you cancelled: stop, rather than "time out" every app left
                    failed.Add($"{app}: timed out");
                    continue;
                }
                if (p.ExitCode == 0) progress.Report($"    {app} installed");
                else
                {
                    // The pipes end at EOF, not at its exit: a build server it started (MSBuild nodes, VBCSCompiler) inherits
                    // them and outlives it by minutes, so what's said comes from what arrived within a few seconds (M.ORCH-T354).
                    var read = Task.WhenAll(output, errors);
                    var last = await Task.WhenAny(read, Task.Delay(OutputGrace, cancel)) != read ? $"exit code {p.ExitCode}"
                        : ((await errors).Trim() is { Length: > 0 } e ? e : (await output).Trim()).Split('\n').LastOrDefault()?.Trim();
                    failed.Add($"{app}: {last}");
                    progress.Report($"    {app} FAILED");
                }
            }
            catch (System.ComponentModel.Win32Exception ex) { failed.Add($"{app}: {ex.Message}"); }
        }
        return failed;
    }

    private static (int Code, string Output) Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(dir);
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, stdout.Result + stderr);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, "git isn't installed (https://git-scm.com/download/win)");
        }
    }
}
