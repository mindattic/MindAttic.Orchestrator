using System.Diagnostics;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class WorkspaceBootstrapTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-boot-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        // A killed install's process can still hold its working folder for a moment after it is gone (M.ORCH-T738).
        for (var attempt = 1; ; attempt++)
        {
            try { Directory.Delete(dir, recursive: true); return; }
            catch (IOException) when (attempt < 50) { Thread.Sleep(200); }
        }
    }

    // Local repos stand in for GitHub: a "remote" is just a path git can clone.
    private string Repo(string name, Action<string>? fill = null)
    {
        var path = Path.Combine(dir, "remotes", name);
        Directory.CreateDirectory(path);
        Git(path, "init", "-q");
        File.WriteAllText(Path.Combine(path, "README.md"), name);
        fill?.Invoke(path);
        Git(path, "add", ".");
        Git(path, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init");
        return path;
    }

    private string RootWith(params ManifestRepo[] repos) =>
        Repo("root", path => WorkspaceBootstrap.Save(new WorkspaceManifest("unused", repos), path));

    [Test]
    public void Builds_a_workspace_from_the_root_and_then_only_fills_gaps()
    {
        var a = Repo("A");
        var b = Repo("B");
        var root = RootWith(new ManifestRepo("A", a), new ManifestRepo("B", b),
            new ManifestRepo("..", a), new ManifestRepo("Gone", Path.Combine(dir, "remotes", "nope")));
        var target = Path.Combine(dir, "ws");
        var lines = new List<string>();

        var first = WorkspaceBootstrap.BuildAsync(target, root, new SyncProgress(lines)).GetAwaiter().GetResult();

        Assert.That(File.Exists(Path.Combine(target, "A", "README.md")) && File.Exists(Path.Combine(target, "B", "README.md")));
        Assert.That(File.Exists(Path.Combine(target, WorkspaceManifest.RelativePath)), "the root itself is cloned into the folder");
        Assert.That(first.Cloned, Is.EqualTo(2));
        Assert.That(first.Failed, Has.Count.EqualTo(2));
        Assert.That(first.Failed, Has.Some.StartsWith(".."), "a name that would escape the workspace is refused");
        Assert.That(first.Failed, Has.Some.StartsWith("Gone:"), "one unreachable repo doesn't stop the rest");
        Assert.That(Directory.Exists(Path.Combine(dir, "A")), Is.False);

        var again = WorkspaceBootstrap.BuildAsync(target, root, new SyncProgress(lines)).GetAwaiter().GetResult();
        Assert.That((again.Cloned, again.AlreadyThere), Is.EqualTo((0, 2)));
    }

    [Test]
    public void A_manifest_without_repos_or_with_a_repo_missing_its_name_or_remote_is_said_not_a_crash()
    {
        var target = Path.Combine(dir, "ws");
        var empty = Repo("empty", path =>
        {
            Directory.CreateDirectory(Path.Combine(path, "docs"));
            File.WriteAllText(Path.Combine(path, WorkspaceManifest.RelativePath), "{}");
        });
        Assert.ThrowsAsync<InvalidOperationException>(() => WorkspaceBootstrap.BuildAsync(target, empty, new SyncProgress([])),
            "no repo list is the clear has-no-manifest error");

        var a = Repo("A");
        var partial = Repo("partial", path =>
        {
            Directory.CreateDirectory(Path.Combine(path, "docs"));
            File.WriteAllText(Path.Combine(path, WorkspaceManifest.RelativePath),
                $$"""{"Root":"x","Repos":[{"Name":"A","Remote":{{System.Text.Json.JsonSerializer.Serialize(a)}}},{"Remote":"x"},{"Name":"NoRemote"},null]}""");
        });
        var built = WorkspaceBootstrap.BuildAsync(Path.Combine(dir, "ws2"), partial, new SyncProgress([])).GetAwaiter().GetResult();

        Assert.That(built.Cloned, Is.EqualTo(1));
        Assert.That(built.Failed, Has.Count.EqualTo(2));
        Assert.That(built.Failed, Has.Some.StartsWith("(no name)").And.Some.StartsWith("NoRemote (no remote"));
    }

    [Test]
    public void A_folder_with_other_things_in_it_is_left_alone()
    {
        var target = Path.Combine(dir, "busy");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "mine.txt"), "x");

        Assert.ThrowsAsync<InvalidOperationException>(() => WorkspaceBootstrap.BuildAsync(target, RootWith(), new SyncProgress([])));
        Assert.That(File.Exists(Path.Combine(target, "mine.txt")));
    }

    [Test]
    public void Export_lists_the_repos_on_disk_with_their_remotes()
    {
        var a = Repo("A");
        var target = Path.Combine(dir, "ws");
        WorkspaceBootstrap.BuildAsync(target, RootWith(new ManifestRepo("A", a)), new SyncProgress([])).GetAwaiter().GetResult();
        Directory.CreateDirectory(Path.Combine(target, "not-a-repo"));

        var manifest = WorkspaceBootstrap.Export(target);

        Assert.That(manifest.Repos, Is.EqualTo(new[] { new ManifestRepo("A", a) }));
        Assert.That(manifest.Root, Is.EqualTo(Path.Combine(dir, "remotes", "root")));
    }

    [Test]
    public void Installs_every_app_with_a_deploy_script_except_the_orchestrator_itself()
    {
        var ws = Path.Combine(dir, "ws");
        foreach (var (app, script) in new[]
        {
            ("Good", "Set-Content -Path installed.txt -Value ok"),
            ("Bad", "Write-Error 'no SDK'; exit 1"),
            ("MindAttic.Orchestrator", "Set-Content -Path should-not-run.txt -Value x"),
        })
        {
            Directory.CreateDirectory(Path.Combine(ws, app));
            File.WriteAllText(Path.Combine(ws, app, "run.ps1"), script);
        }
        Directory.CreateDirectory(Path.Combine(ws, "Library"));

        Assert.That(WorkspaceBootstrap.Apps(ws), Is.EqualTo(new[] { "Bad", "Good" }));
        var failed = WorkspaceBootstrap.InstallAppsAsync(ws, new SyncProgress([])).GetAwaiter().GetResult();

        Assert.That(File.Exists(Path.Combine(ws, "Good", "installed.txt")));
        Assert.That(failed.Single(), Does.StartWith("Bad:"));
        Assert.That(File.Exists(Path.Combine(ws, "MindAttic.Orchestrator", "should-not-run.txt")), Is.False);
    }

    [Test]
    public void One_slow_app_is_reported_as_timed_out_and_the_rest_still_install()
    {
        var ws = Path.Combine(dir, "ws");
        App(ws, "Fast", "Set-Content -Path installed.txt -Value ok");
        App(ws, "Slow", "Start-Sleep -Seconds 60");

        var failed = WorkspaceBootstrap.InstallAppsAsync(ws, new SyncProgress([]), perApp: TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();

        Assert.That(failed, Is.EqualTo(new[] { "Slow: timed out" }));
        Assert.That(File.Exists(Path.Combine(ws, "Fast", "installed.txt")));
    }

    [Test]
    public void A_failed_app_whose_child_still_holds_its_output_is_reported_without_waiting_for_the_child()
    {
        // M.ORCH-T354: a build server the deploy started inherits its pipes; the failure was reported only once it exited.
        var ws = Path.Combine(dir, "ws");
        App(ws, "Bad", "cmd /c \"start /b /d %TEMP% powershell -NoProfile -Command Start-Sleep 25\"; exit 1");
        var timer = Stopwatch.StartNew();

        var failed = WorkspaceBootstrap.InstallAppsAsync(ws, new SyncProgress([])).GetAwaiter().GetResult();

        Assert.That(failed.Single(), Does.StartWith("Bad:"));
        Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(20)), "not held until the child exits");
    }

    [Test]
    public void Cancelling_stops_the_install_instead_of_reporting_every_app_timed_out()
    {
        var ws = Path.Combine(dir, "ws");
        App(ws, "A", "Start-Sleep -Seconds 60");
        App(ws, "B", "Set-Content -Path ran.txt -Value x");
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var timer = Stopwatch.StartNew();

        Assert.That(() => WorkspaceBootstrap.InstallAppsAsync(ws, new SyncProgress([]), cancel: cancel.Token).GetAwaiter().GetResult(),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(timer.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)), "stopped at the cancel, not the 15 minute limit");
        Assert.That(File.Exists(Path.Combine(ws, "B", "ran.txt")), Is.False, "nothing after the cancel runs");
    }

    private static void App(string ws, string name, string script)
    {
        Directory.CreateDirectory(Path.Combine(ws, name));
        File.WriteAllText(Path.Combine(ws, name, "run.ps1"), script);
    }

    private sealed class SyncProgress(List<string> lines) : IProgress<string>
    {
        public void Report(string value) { lock (lines) lines.Add(value); }
    }

    private static void Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-C"); psi.ArgumentList.Add(dir);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
    }
}
