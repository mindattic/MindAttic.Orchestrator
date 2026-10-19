using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class CliRosterTests
{
    private static readonly DateTime Turn = new(2026, 10, 18, 20, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Every_open_cli_is_listed_with_whether_autopilot_is_on_in_it_and_the_repos_it_works()
    {
        var clis = new[]
        {
            new CliBilling(100, "s-orch", @"D:\Projects\MindAttic\MindAttic.Orchestrator", ContextCycler.SubscriptionAuth, null),
            new CliBilling(200, "s-prose", @"D:\Projects\MindAttic\Prose", "api-key", new HostSession("claude", 9, "pipe", null)),
        };
        var autopilot = new Dictionary<int, IReadOnlyList<string>> { [100] = ["MindAttic.Orchestrator", "GTFO.*"], [999] = ["*"] };
        var sessions = new[] { new SessionContext("s-orch", null, 40, Idle: false, Turn), new SessionContext("s-prose", null, 10, Idle: true, Turn.AddMinutes(-5)) };

        var rows = CliRoster.Build(clis, autopilot, sessions);

        Assert.That(rows, Has.Count.EqualTo(2), "a mark whose CLI isn't open isn't a CLI");
        var orch = rows.Single(r => r.Pid == 100);
        Assert.That((orch.Repo, orch.Autopilot, orch.Bills, orch.InLauncher, orch.Idle, orch.LastTurnUtc),
            Is.EqualTo(("MindAttic.Orchestrator", true, BillingModes.Team, false, false, (DateTime?)Turn)));
        Assert.That(orch.AutopilotRepos, Is.EqualTo(new[] { "MindAttic.Orchestrator", "GTFO.*" }));
        var prose = rows.Single(r => r.Pid == 200);
        Assert.That((prose.Repo, prose.Autopilot, prose.Bills, prose.InLauncher, prose.Idle), Is.EqualTo(("Prose", false, BillingModes.Api, true, true)));
        Assert.That(prose.AutopilotRepos, Is.Empty);
    }

    [Test]
    public void A_cli_whose_gate_has_not_recorded_a_turn_has_no_last_turn()
    {
        var rows = CliRoster.Build([new CliBilling(1, "s", null, null, null)], new Dictionary<int, IReadOnlyList<string>>(), []);
        Assert.That(rows.Single().LastTurnUtc, Is.Null);
    }

    [Test]
    public void The_roster_reads_back_as_written_and_a_missing_or_torn_one_reads_as_none()
    {
        var dir = Directory.CreateTempSubdirectory("orch-roster-").FullName;
        try
        {
            Assert.That(CliRoster.Read(dir), Is.Null);
            var row = new CliRow(7, "s", "Prose", @"D:\Prose", BillingModes.Team, true, true, ["*"], false, Turn);
            CliRoster.Write(dir, [row], Turn);
            var back = CliRoster.Read(dir)!;
            Assert.That(back.WrittenUtc, Is.EqualTo(Turn));
            Assert.That(back.Clis.Single() with { AutopilotRepos = row.AutopilotRepos }, Is.EqualTo(row));
            Assert.That(back.Clis.Single().AutopilotRepos, Is.EqualTo(new[] { "*" }));
            Assert.That(Directory.GetFiles(dir), Has.Length.EqualTo(1), "no temp file left beside it");

            File.WriteAllText(Path.Combine(dir, CliRoster.FileName), "{\"WrittenUtc\":");
            Assert.That(CliRoster.Read(dir), Is.Null);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
