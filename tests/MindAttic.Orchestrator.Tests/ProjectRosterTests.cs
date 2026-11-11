using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1585: the Launcher's Open Project Tab roster, read in-process for the tab right-click's project tree.</summary>
public sealed class ProjectRosterTests
{
    private static RosterProject P(string name, string? url = null) => new(name, $@"D:\Projects\{name}", url);

    [Test]
    public void Dot_named_projects_sharing_a_segment_nest_and_a_lone_one_stays_a_leaf_with_its_full_name()
    {
        var tree = ProjectRoster.Tree([P("Prose"), P("Unity.GridGame"), P("Unity.Rea.Core"), P("Unity.Rea.Editor"), P("Mods.MechWarrior5.SaveEditor")]);

        Assert.That(tree.Select(n => n.Label), Is.EqualTo(new[] { "Mods.MechWarrior5.SaveEditor", "Prose", "Unity" }));
        var unity = (ProjectBranch)tree[2];
        Assert.That(unity.Children.Select(n => n.Label), Is.EqualTo(new[] { "GridGame", "Rea" }));
        var rea = (ProjectBranch)unity.Children[1];
        Assert.That(rea.Children.Select(n => ((ProjectLeaf)n).Project.Name), Is.EqualTo(new[] { "Unity.Rea.Core", "Unity.Rea.Editor" }));
        Assert.That(rea.Children.Select(n => n.Label), Is.EqualTo(new[] { "Core", "Editor" }));
    }

    [Test]
    public void A_project_named_exactly_a_branch_sits_beside_its_children()
    {
        var tree = ProjectRoster.Tree([P("GTFO.EZ"), P("GTFO.EZ.Shared"), P("GTFO.Other")]);

        var ez = (ProjectBranch)((ProjectBranch)tree.Single()).Children.Single(n => n.Label == "EZ");
        Assert.That(ez.Children.Select(n => ((ProjectLeaf)n).Project.Name), Is.EquivalentTo(new[] { "GTFO.EZ", "GTFO.EZ.Shared" }));
    }

    [Test]
    public void The_wip_filter_keeps_tagged_remotes_and_no_filter_keeps_everything()
    {
        var projects = new[]
        {
            P("A", "https://github.com/mindattic/A.git"),
            P("B", "git@github.com:mindattic/B.git"),
            P("C", "https://github.com/mindattic/C"),
            P("D"),
        };
        var tagged = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mindattic/a", "mindattic/b" };

        Assert.That(ProjectRoster.Filter(projects, tagged).Select(p => p.Name), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(ProjectRoster.Filter(projects, null), Has.Count.EqualTo(4), "no token or GitHub down: unfiltered, never emptied");
    }

    [Test]
    public void The_launchers_settings_are_read_deduplicated_and_sorted()
    {
        var file = Path.Combine(Path.GetTempPath(), $"roster-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, """
            { "projects": [
                { "name": "Zeta", "path": "D:\\Z", "repoUrl": "https://github.com/x/zeta" },
                { "name": "alpha", "path": "D:\\A" },
                { "name": "Alpha", "path": "d:\\a" },
                { "name": "", "path": "D:\\nameless" } ] }
            """);
        try
        {
            var roster = ProjectRoster.Load(file);
            Assert.That(roster.Select(p => p.Name), Is.EqualTo(new[] { "alpha", "Zeta" }));
            Assert.That(roster[1].RepoUrl, Is.EqualTo("https://github.com/x/zeta"));
            Assert.That(ProjectRoster.Load(file + ".missing"), Is.Empty);
        }
        finally { File.Delete(file); }
    }
}
