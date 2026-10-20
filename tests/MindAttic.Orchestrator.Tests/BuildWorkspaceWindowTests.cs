using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The first-run workspace builder (M.ORCH-T555): its window is never shown here; the build is a stand-in, so
/// nothing is cloned.</summary>
[Apartment(ApartmentState.STA)]
public sealed class BuildWorkspaceWindowTests
{
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    [Test]
    public void Closing_the_window_at_the_install_the_apps_question_is_no_and_keeps_the_workspace()
    {
        // The clone is done and the window asks 'Also install the apps?'; X then left the question unanswered and the
        // build not succeeded, so WorkspaceRoot wasn't saved and the next start asked for a folder again.
        var folder = temps.New();
        Directory.CreateDirectory(Path.Combine(folder, "SomeApp"));
        File.WriteAllText(Path.Combine(folder, "SomeApp", "deploy.ps1"), "");
        var window = OffScreenWindow.BuildWorkspace(folder, _ => Task.FromResult(new BootstrapResult(1, 0, [])));

        var build = window.BuildAsync();
        Assert.That(window.Asking, Is.True, "it asks about the apps");

        window.Close();

        Assert.That(window.Succeeded, Is.True, "the workspace is there: closing at the question doesn't throw it away");
        Assert.That(build.IsCompleted, Is.True, "closing answers the question no");
    }
}
