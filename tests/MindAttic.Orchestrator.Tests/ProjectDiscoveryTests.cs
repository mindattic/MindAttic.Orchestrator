using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1987: the write side of the Launcher's roster <see cref="ProjectRoster"/> only reads.</summary>
public sealed class ProjectDiscoveryTests
{
    private static string NewTempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"discover-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void MakeGitRepo(string root, string name)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
    }

    [Test]
    public void A_folder_with_a_dot_git_directory_that_isnt_on_the_roster_is_found()
    {
        var root = NewTempRoot();
        try
        {
            MakeGitRepo(root, "NewRepo");
            Directory.CreateDirectory(Path.Combine(root, "NotGit")); // no .git: not a repo

            var found = ProjectDiscovery.FindUnregistered([], root);

            Assert.That(found.Select(r => r.Name), Is.EqualTo(new[] { "NewRepo" }));
            Assert.That(found[0].Path, Is.EqualTo(Path.Combine(root, "NewRepo")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public void A_repo_already_on_the_roster_by_path_is_skipped_case_and_trailing_slash_insensitively()
    {
        var root = NewTempRoot();
        try
        {
            MakeGitRepo(root, "Known");
            MakeGitRepo(root, "Unknown");
            var roster = new[] { new RosterProject("Known", Path.Combine(root, "known") + "\\", null) };

            var found = ProjectDiscovery.FindUnregistered(roster, root);

            Assert.That(found.Select(r => r.Name), Is.EqualTo(new[] { "Unknown" }));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Test]
    public void A_missing_root_finds_nothing_rather_than_throwing()
    {
        Assert.That(ProjectDiscovery.FindUnregistered([], Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}")), Is.Empty);
    }

    [Test]
    public void Appending_adds_name_path_and_repo_url_entries_and_keeps_every_other_entry_and_field()
    {
        var file = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, """
            { "windowsTerminalSettingsPath": "C:\\wt.json", "projects": [
                { "name": "Old", "path": "D:\\Old", "repoUrl": "https://github.com/x/old", "tabColor": "#112233" } ] }
            """);
        try
        {
            var written = ProjectDiscovery.Append(
                [new DiscoveredRepo("New", @"D:\New")], file, _ => "https://github.com/x/new.git");

            Assert.That(written, Is.EqualTo(1));
            var roster = ProjectRoster.Load(file);
            Assert.That(roster.Select(p => p.Name), Is.EquivalentTo(new[] { "Old", "New" }));
            var added = roster.Single(p => p.Name == "New");
            Assert.That(added.Path, Is.EqualTo(@"D:\New"));
            Assert.That(added.RepoUrl, Is.EqualTo("https://github.com/x/new.git"));

            // The untouched project's own extra field, and the top-level sibling key, both survive the round trip.
            var text = File.ReadAllText(file);
            Assert.That(text, Does.Contain("#112233"));
            Assert.That(text, Does.Contain("windowsTerminalSettingsPath"));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void Appending_to_a_missing_file_creates_it_with_just_a_projects_array()
    {
        var file = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.json");
        try
        {
            var written = ProjectDiscovery.Append([new DiscoveredRepo("New", @"D:\New")], file, _ => null);

            Assert.That(written, Is.EqualTo(1));
            Assert.That(ProjectRoster.Load(file).Select(p => p.Name), Is.EqualTo(new[] { "New" }));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void Appending_nothing_touches_nothing_and_reports_zero()
    {
        Assert.That(ProjectDiscovery.Append([], Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")), Is.EqualTo(0));
    }
}
