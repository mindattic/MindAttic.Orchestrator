using MindAttic.Orchestrator.Gateway;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1479: a client's tickets command runs on the host in the folder it was typed in, never outside the workspace.</summary>
public sealed class TicketsRunnerTests
{
    private static string Root => Path.GetFullPath(TicketsRunner.WorkspaceRoot);

    [TestCase(null)]
    [TestCase("")]
    [TestCase(@"C:\Windows")]
    [TestCase(@"..\..\Windows")]
    [TestCase(@"No.Such.Repo\src")]
    public void Anything_outside_or_missing_is_the_workspace_root(string? relative) =>
        Assert.That(TicketsRunner.Folder(relative), Is.EqualTo(Root));

    [Test]
    public void A_repo_folder_that_exists_is_kept()
    {
        var repo = Directory.EnumerateDirectories(Root).FirstOrDefault();
        if (repo is null) Assert.Ignore("No workspace on this machine.");
        Assert.That(TicketsRunner.Folder(Path.GetFileName(repo)), Is.EqualTo(Path.GetFullPath(repo)));
    }
}
