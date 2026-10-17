using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class MatchingTests
{
    private static readonly RepoMatcher Matcher = new([
        ("MindAttic.Ideas", @"D:\Projects\MindAttic\MindAttic.Ideas"),
        ("MindAttic.Ideas.Library", @"D:\Projects\MindAttic\MindAttic.Ideas.Library"),
        ("(root)", @"D:\Projects\MindAttic"),
    ]);

    [TestCase(@"D:\Projects\MindAttic\MindAttic.Ideas", "MindAttic.Ideas")]
    [TestCase(@"d:\projects\mindattic\mindattic.ideas\src\", "MindAttic.Ideas")]
    [TestCase(@"D:\Projects\MindAttic\MindAttic.Ideas.Library\docs", "MindAttic.Ideas.Library")]
    [TestCase(@"D:\Projects\MindAttic\MindAttic.IdeasX", "(root)")]
    [TestCase(@"D:\Projects\MindAttic", "(root)")]
    public void Longest_containing_repo_wins(string dir, string expected) =>
        Assert.That(Matcher.Match(dir), Is.EqualTo(expected));

    [TestCase(@"D:\", "drive")]
    [TestCase(@"D:\Anything\Deeper", "drive")]
    [TestCase(@"d:/anything", "drive")]
    [TestCase(@"E:\Other", null)]
    public void A_repo_at_a_drive_root_contains_the_whole_drive(string dir, string? expected) =>
        Assert.That(new RepoMatcher([("drive", @"D:\")]).Match(dir), Is.EqualTo(expected));

    [TestCase(@"D:\W\Repo", "Repo")]
    [TestCase(@"D:\W\Repo\", "Repo")]
    [TestCase(@"D:/W/Repo/src/", "Repo")]
    [TestCase(@"D:\W\RepoX", null)]
    public void A_trailing_slash_on_either_side_does_not_matter(string dir, string? expected)
    {
        Assert.That(new RepoMatcher([("Repo", @"D:\W\Repo\")]).Match(dir), Is.EqualTo(expected), "repo path with a trailing slash");
        Assert.That(new RepoMatcher([("Repo", @"D:/W/Repo/")]).Match(dir), Is.EqualTo(expected), "and with forward slashes");
    }

    [Test]
    public void Outside_every_repo_is_null() =>
        Assert.That(Matcher.Match(@"C:\Users\someone"), Is.Null);

    [TestCase(@"\\.\pipe\mindattic-host-claude-1234", "Claude", 1234)]
    [TestCase("mindattic-host-codex-77", "Codex", 77)]
    [TestCase("mindattic-host-newthing-5", "newthing", 5)]
    public void Pipe_names_parse(string pipe, string provider, int pid)
    {
        var parsed = HostSessionScanner.ParsePipeName(pipe);
        Assert.That(parsed?.Provider, Is.EqualTo(provider));
        Assert.That(parsed?.Pid, Is.EqualTo(pid));
    }

    [TestCase("mindattic-host-claude")]
    [TestCase("something-else-12")]
    public void Other_pipes_are_ignored(string pipe) =>
        Assert.That(HostSessionScanner.ParsePipeName(pipe), Is.Null);

    [Test]
    public void Host_command_line_prefers_path_over_name()
    {
        var scanner = new HostSessionScanner(new OrchestratorOptions { WorkspaceRoot = @"D:\W" });
        var roster = new Dictionary<string, string> { ["Vault"] = @"E:\Elsewhere\Vault" };

        Assert.That(scanner.WorkingDirectoryOf(@"""C:\Apps\L.exe"" host --name Vault --provider Claude", roster),
            Is.EqualTo(@"E:\Elsewhere\Vault"));
        Assert.That(scanner.WorkingDirectoryOf(@"L.exe host --name Unknown", roster), Is.EqualTo(@"D:\W\Unknown"));
        Assert.That(scanner.WorkingDirectoryOf(@"L.exe host --path ""D:\My Repo"" --name Vault", roster), Is.EqualTo(@"D:\My Repo"));
    }

    [Test]
    public void Classifies_by_recency_and_open_tabs()
    {
        var agg = new StatusAggregator(new OrchestratorOptions());
        var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

        Assert.That(agg.Classify(now.AddSeconds(-30), 0, now), Is.EqualTo(RepoState.Working));
        Assert.That(agg.Classify(now.AddMinutes(-10), 0, now), Is.EqualTo(RepoState.Waiting));
        Assert.That(agg.Classify(now.AddHours(-5), 1, now), Is.EqualTo(RepoState.Waiting), "an open tab is waiting on you");
        Assert.That(agg.Classify(now.AddHours(-5), 0, now), Is.EqualTo(RepoState.Dormant));
        Assert.That(agg.Classify(null, 0, now), Is.EqualTo(RepoState.Dormant));
    }

    [Test]
    public void Start_arguments_escape_semicolons_for_windows_terminal()
    {
        var control = new SessionControl(new OrchestratorOptions { LauncherExe = @"C:\L.exe" });
        var args = control.StartArguments("Repo", @"D:\Repo", "Claude", "fix a; then b").ToList();

        Assert.That(args, Does.Contain(@"fix a\; then b"));
        Assert.That(args, Does.Contain("--path"));
        Assert.That(args[args.IndexOf("--") + 1], Is.EqualTo(@"C:\L.exe"));
    }
}
