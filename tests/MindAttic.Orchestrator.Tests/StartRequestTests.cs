using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class StartRequestTests
{
    [Test]
    public void Start_names_the_repo_after_it()
    {
        // M.ORCH-T767: the Tickets desktop's Repos tab Start link runs MindAttic.Orchestrator.exe --start <repo>.
        Assert.That(StartRequest.Repo(["--start", "MindAttic.Tickets"]), Is.EqualTo("MindAttic.Tickets"));
        Assert.That(StartRequest.Repo(["--high-contrast", "--START", "GTFO.Core"]), Is.EqualTo("GTFO.Core"));
        Assert.That(StartRequest.Repo([]), Is.Null);
        Assert.That(StartRequest.HandOff(["--start", "A"]), Is.EqualTo(new[] { "--start", "A" }));
        Assert.That(StartRequest.HandOff(["--high-contrast"]), Is.Empty, "a plain launch only comes forward");
    }

    [TestCase("")]
    [TestCase(@"..\Other")]
    [TestCase("C:\\Windows")]
    [TestCase("A B")]
    [TestCase("A\" & calc")]
    [TestCase("A;rm")]
    [TestCase(".hidden")]
    [TestCase("A|B")]
    public void Anything_but_a_plain_repo_name_is_refused(string name)
    {
        Assert.That(StartRequest.Repo(["--start", name], out var refused), Is.Null);
        Assert.That(refused, Is.EqualTo(name.Trim()));
        Assert.That(StartRequest.HandOff(["--start", name]), Is.Empty);
    }

    [Test]
    public void Start_with_nothing_after_it_starts_nothing()
    {
        Assert.That(StartRequest.Repo(["--start"], out var refused), Is.Null);
        Assert.That(refused, Is.EqualTo(""));
    }
}
