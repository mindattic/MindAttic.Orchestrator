using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2031: the links probe, run on fake presences, sessions and CLI pids.</summary>
public sealed class LinkProbeTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 22, 20, 40, 0, TimeSpan.Zero);

    private static HealthResult Of(HealthReport report, string checkId, string componentId) =>
        report.Results.Single(r => r.CheckId == checkId && r.ComponentId == componentId);

    [Test]
    public async Task Each_link_is_a_result_graded_by_its_rule()
    {
        // The dashboard's process runs (pid 8); Tickets Desktop said it was there from pid 7, which is gone.
        var presences = new AppPresences { StartOf = pid => pid == 8 ? At.UtcDateTime.AddHours(-1) : null };
        presences.Note(new AppPresence { App = AppBus.OrchestratorDesktop, Pid = 8, Handles = [AppBus.ShowWorker], AtUtc = At.UtcDateTime });
        presences.Note(new AppPresence { App = AppBus.TicketsDesktop, Pid = 7, Handles = [AppBus.OpenTicket], AtUtc = At.UtcDateTime });
        var probe = new LinkProbe
        {
            ReachDaemon = _ => Task.FromResult(new DaemonReach(12)),
            Presences = presences,
            TermSessions = () => [new TermSessionRecord { Id = "a", HostPid = 100 }, new TermSessionRecord { Id = "b", HostPid = 200 }],
            HostAlive = s => s.HostPid == 100,
            CliPids = () => [("s1", 300), ("s2", 300), ("s3", 400)],
            CliAlive = pid => pid == 300,
            Host = "TEST",
            Clock = () => At,
        };

        var report = await probe.RunAsync();

        Assert.That(report.Probe, Is.EqualTo(LinkProbe.Name));
        var daemon = Of(report, LinkProbe.LatencyCheck, LinkProbe.DaemonLink);
        Assert.That(daemon.ObservedValue, Is.EqualTo(12));
        Assert.That(daemon.ObservedUnit, Is.EqualTo("ms"));
        Assert.That(daemon.Grade, Is.EqualTo(HealthGrade.Healthy));
        Assert.That(Of(report, LinkProbe.UpCheck, "daemon->dashboard").ObservedState, Is.EqualTo(LinkProbe.Up));
        var tickets = Of(report, LinkProbe.UpCheck, "daemon->tickets-desktop");
        Assert.That((tickets.ObservedState, tickets.Grade), Is.EqualTo((LinkProbe.Gone, HealthGrade.Healthy)));
        Assert.That(Of(report, LinkProbe.UpCheck, "daemon->termhost:100").Grade, Is.EqualTo(HealthGrade.Healthy));
        Assert.That(Of(report, LinkProbe.UpCheck, "daemon->termhost:200").Grade, Is.EqualTo(HealthGrade.Degraded));
        Assert.That(report.Results.Count(r => r.ComponentId == "daemon->cli:300"), Is.EqualTo(1), "one result per CLI process");
        Assert.That(Of(report, LinkProbe.UpCheck, "daemon->cli:400").ObservedState, Is.EqualTo(LinkProbe.Gone));
        Assert.That(report.Status, Is.EqualTo(HealthGrade.Degraded));
        Assert.That(HealthReport.FromJson(report.ToJson()), Is.EqualTo(report));
    }

    [Test]
    public async Task An_app_never_heard_is_absent_and_healthy()
    {
        var probe = new LinkProbe { ReachDaemon = _ => Task.FromResult(new DaemonReach(5)), Presences = new AppPresences(), Clock = () => At };
        var report = await probe.RunAsync();
        var dashboard = Of(report, LinkProbe.UpCheck, "daemon->dashboard");
        Assert.That((dashboard.ObservedState, dashboard.Grade), Is.EqualTo((LinkProbe.Absent, HealthGrade.Healthy)));
        Assert.That(report.Status, Is.EqualTo(HealthGrade.Healthy));
    }

    [Test]
    public async Task A_daemon_that_does_not_answer_is_unhealthy_and_a_slow_one_is_graded_by_its_line()
    {
        var down = await new LinkProbe { ReachDaemon = _ => Task.FromResult(new DaemonReach(null, "the daemon isn't listening")), Clock = () => At }.RunAsync();
        var link = Of(down, LinkProbe.LatencyCheck, LinkProbe.DaemonLink);
        Assert.That(link.Grade, Is.EqualTo(HealthGrade.Unhealthy));
        Assert.That(link.ObservedValue, Is.Null);
        Assert.That(link.Output, Is.EqualTo("the daemon isn't listening"));
        Assert.That(HealthGrades.NagiosExit(down.Status), Is.EqualTo(2));

        var slow = await new LinkProbe { ReachDaemon = _ => Task.FromResult(new DaemonReach(1500)), Clock = () => At }.RunAsync();
        Assert.That(Of(slow, LinkProbe.LatencyCheck, LinkProbe.DaemonLink).Grade, Is.EqualTo(HealthGrade.Degraded));
    }

    [Test]
    public async Task A_source_that_throws_is_no_reading_not_a_crash()
    {
        var probe = new LinkProbe
        {
            ReachDaemon = _ => throw new InvalidOperationException("boom"),
            TermSessions = () => throw new IOException("unreadable"),
            Clock = () => At,
        };
        var report = await probe.RunAsync();
        var link = Of(report, LinkProbe.LatencyCheck, LinkProbe.DaemonLink);
        Assert.That((link.Grade, link.ObservedValue), Is.EqualTo((HealthGrade.Degraded, (double?)null)));
        Assert.That(report.Results, Has.Count.EqualTo(1));
    }

    [Test]
    public void The_hub_keeps_the_presences_it_takes_for_the_probe()
    {
        using var stream = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        var hub = new AppBusHub(stream, new Tickets.TicketActivityFeed());
        hub.Take(StatusEnvelope.Of(AppBus.Presence, new AppPresence { App = AppBus.TicketsDesktop, Pid = 7, Handles = [AppBus.OpenTicket] }));
        Assert.That(hub.Presences.Heard(AppBus.TicketsDesktop)?.Pid, Is.EqualTo(7));
    }
}
