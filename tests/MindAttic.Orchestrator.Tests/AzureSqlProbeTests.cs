using MindAttic.Orchestrator.Health;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2025: the Azure SQL lister (kickstart.ps1 -ListDatabases in C#) over canned az JSON, never reaching Azure.</summary>
public sealed class AzureSqlProbeTests
{
    private const string Servers = """
        [
          { "name": "sql-b", "resourceGroup": "rg-b", "fullyQualifiedDomainName": "sql-b.database.windows.net", "location": "eastus" },
          { "name": "sql-a", "resourceGroup": "rg-a", "fullyQualifiedDomainName": null }
        ]
        """;

    private const string DbsA = """
        [
          { "name": "master", "status": "Online", "currentSku": { "name": "System", "tier": "System", "capacity": 0 } },
          { "name": "Tickets", "status": "Paused", "autoPauseDelay": 60,
            "currentSku": { "name": "GP_S_Gen5", "tier": "GeneralPurpose", "family": "Gen5", "capacity": 1 } },
          { "name": "Gadfly", "status": "Resuming", "autoPauseDelay": -1,
            "currentSku": { "name": "GP_S_Gen5", "tier": "GeneralPurpose", "capacity": 2 } }
        ]
        """;

    private const string DbsB = """
        [ { "name": "Launcher", "status": "Online", "autoPauseDelay": null, "currentServiceObjectiveName": "Basic",
            "sku": { "name": "Basic", "tier": "Basic", "capacity": 5 } } ]
        """;

    private sealed class FakeAz(Func<IReadOnlyList<string>, ProcessResult> run) : IProcessRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];
        public ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout) { Calls.Add(args); return run(args); }
    }

    private static ProcessResult Ok(string json) => new(0, json, "", false) { Exited = true };

    private static ProcessResult Answer(IReadOnlyList<string> a) =>
        a[1] == "server" ? Ok(Servers)
        : a.Contains("sql-a") ? Ok(DbsA)
        : Ok(DbsB);

    [Test]
    public void Lists_every_database_on_every_server_without_master_sorted_with_sku_and_auto_pause()
    {
        var az = new FakeAz(Answer);
        var listing = new AzureSqlProbe(az, () => "az.cmd").List();

        Assert.That(listing.Ok, Is.True, listing.Error);
        Assert.That(listing.Databases.Select(d => d.ComponentId), Is.EqualTo(new[] { "sql-a/Gadfly", "sql-a/Tickets", "sql-b/Launcher" }));

        var tickets = listing.Databases.Single(d => d.Name == "Tickets");
        Assert.That(tickets, Is.EqualTo(new AzureSqlDatabase("sql-a", "rg-a", "sql-a.database.windows.net", "Tickets", "Paused",
            "GP_S_Gen5", "GeneralPurpose", 1, 60)));
        Assert.That(tickets.AutoPauses, Is.True);

        var gadfly = listing.Databases.Single(d => d.Name == "Gadfly");
        Assert.That((gadfly.Status, gadfly.AutoPauseDelayMinutes, gadfly.AutoPauses), Is.EqualTo(("Resuming", (int?)-1, false)));

        var launcher = listing.Databases.Single(d => d.Name == "Launcher");
        Assert.That((launcher.Sku, launcher.Capacity, launcher.AutoPauseDelayMinutes, launcher.Fqdn),
            Is.EqualTo(("Basic", (int?)5, (int?)null, "sql-b.database.windows.net")));

        // One server list, then one db list per server with its own resource group.
        Assert.That(az.Calls, Has.Count.EqualTo(3));
        Assert.That(az.Calls.Skip(1).Select(c => string.Join(' ', c)),
            Is.EquivalentTo(new[] { "sql db list -g rg-b -s sql-b -o json", "sql db list -g rg-a -s sql-a -o json" }));
    }

    [Test]
    public void A_server_whose_db_list_fails_is_reported_while_the_rest_are_listed()
    {
        var listing = new AzureSqlProbe(new FakeAz(a =>
            a.Contains("sql-b") ? new ProcessResult(1, "", "ERROR: (AuthorizationFailed) no access", false) { Exited = true } : Answer(a)),
            () => "az.cmd").List();

        Assert.That(listing.Ok, Is.False);
        Assert.That(listing.Error, Does.Contain("sql-b").And.Contain("AuthorizationFailed"));
        Assert.That(listing.Databases.Select(d => d.Name), Is.EqualTo(new[] { "Gadfly", "Tickets" }));
    }

    [Test]
    public void Not_signed_in_or_not_installed_needs_a_login()
    {
        var signedOut = new AzureSqlProbe(new FakeAz(_ =>
            new ProcessResult(1, "", "ERROR: Please run 'az login' to setup account.", false) { Exited = true }), () => "az.cmd").List();
        Assert.That((signedOut.Ok, signedOut.NeedsLogin, signedOut.Databases.Count), Is.EqualTo((false, true, 0)));

        var missing = new AzureSqlProbe(new FakeAz(_ => throw new InvalidOperationException("never run")), () => null).List();
        Assert.That((missing.Ok, missing.NeedsLogin), Is.EqualTo((false, true)));
    }

    [Test]
    public void State_is_graded_by_rule_paused_passes_resuming_warns_offline_fails()
    {
        Assert.That(AzureSqlProbe.GradeState("Online"), Is.EqualTo(HealthGrade.Healthy));
        Assert.That(AzureSqlProbe.GradeState("Paused"), Is.EqualTo(HealthGrade.Healthy));
        Assert.That(AzureSqlProbe.GradeState("Resuming"), Is.EqualTo(HealthGrade.Degraded));
        Assert.That(AzureSqlProbe.GradeState("Offline"), Is.EqualTo(HealthGrade.Unhealthy));
        Assert.That(AzureSqlProbe.GradeState(null), Is.EqualTo(HealthGrade.Unhealthy));

        var at = new DateTimeOffset(2026, 11, 22, 21, 0, 0, TimeSpan.Zero);
        var r = AzureSqlProbe.StateResults([new AzureSqlDatabase("sql-a", "rg-a", "f", "Tickets", "Paused", "GP_S_Gen5", AutoPauseDelayMinutes: 60)], at).Single();
        Assert.That((r.CheckId, r.ComponentId, r.ObservedState, r.Grade, r.Time), Is.EqualTo(("sqldb:state", "sql-a/Tickets", "Paused", HealthGrade.Healthy, at)));
        Assert.That(r.Output, Is.EqualTo("Paused (GP_S_Gen5, auto-pause 60 min)"));
    }
}
