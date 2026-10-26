using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Status;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The status bus (M.ORCH-T872): the envelope (kind, version, payload) round-trips at full precision, a subscriber reads
/// only the kinds and versions it knows and is never thrown into, a snapshot's figures are pushed, kept for connecting dashboards
/// and laid over the snapshot shown, and the inbox takes the hooks' lines.</summary>
public sealed class StatusBusTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private static Pace P(double recent) => new(PaceMode.Open, 4, 1.0 / 3, recent, null, null, null, "note");

    private static Snapshot S(DateTime taken, double recent = 123.456789012345) =>
        new(taken, [], ["a warning"]) { Pace = P(recent), GlobalEmail = "a@x.com", Account = "acct" };

    [Test]
    public void An_envelope_round_trips_its_kind_version_and_payload_exactly()
    {
        var sent = StatusEnvelope.Of(SnapshotFigures.Kind, SnapshotFigures.Of(S(Now, 0.1 + 0.2)), SnapshotFigures.Version);
        var back = StatusStream.Deserialize(StatusStream.Serialize(sent))!;
        Assert.That(back.Kind, Is.EqualTo(SnapshotFigures.Kind));
        Assert.That(back.V, Is.EqualTo(SnapshotFigures.Version));
        Assert.That(back.Pid, Is.EqualTo(Environment.ProcessId));
        var figures = back.PayloadAs<SnapshotFigures>()!;
        Assert.That(figures.Pace!.RecentPerHour, Is.EqualTo(0.1 + 0.2));
        Assert.That(figures.Pace.SustainablePerHour, Is.EqualTo(1.0 / 3));
        Assert.That(figures.TakenUtc, Is.EqualTo(Now));
    }

    [Test]
    public void The_wire_names_are_the_envelopes_own()
    {
        var line = StatusStream.Serialize(StatusEnvelope.Of("poke", new { Source = "x" }));
        Assert.That(line, Does.Contain("\"kind\":\"poke\"").And.Contain("\"v\":1").And.Contain("\"payload\":"));
    }

    [Test]
    public void A_hooks_line_reads_as_an_envelope()
    {
        var e = StatusStream.Deserialize("""{"kind": "poke", "v": 1, "at": "2026-10-25T12:00:00Z", "pid": 5, "payload": {"source": "context-gate"}}""")!;
        Assert.That(e.Kind, Is.EqualTo("poke"));
        Assert.That(e.Pid, Is.EqualTo(5));
        Assert.That(e.Payload.GetProperty("source").GetString(), Is.EqualTo("context-gate"));
    }

    [Test]
    public void A_bus_reads_only_the_kinds_and_versions_it_knows()
    {
        var got = new List<StatusMessage>();
        var bus = new StatusBus().On<StatusMessage>(StatusMessage.Usage, (e, m) => got.Add(StatusMessage.From(e, m)), StatusMessage.Version);
        var usage = new StatusMessage { Kind = StatusMessage.Usage, AtUtc = Now }.ToEnvelope();
        bus.Dispatch(usage with { Kind = "ticket-event" });
        bus.Dispatch(usage with { V = 2 });
        Assert.That(got, Is.Empty, "another kind, or a version past the one read, is skipped");
        bus.Dispatch(usage);
        Assert.That(got.Single().Kind, Is.EqualTo(StatusMessage.Usage));
    }

    [Test]
    public void A_handler_that_throws_or_a_payload_that_wont_read_never_reaches_the_caller()
    {
        var bus = new StatusBus().On<StatusMessage>("boom", (_, _) => throw new InvalidOperationException("boom"));
        Assert.DoesNotThrow(() => bus.Dispatch(StatusEnvelope.Of("boom", new StatusMessage())));
        Assert.DoesNotThrow(() => bus.Dispatch(StatusEnvelope.Of("boom", 42)));
    }

    [Test]
    public void Figures_laid_over_a_snapshot_take_its_figures_and_time_and_keep_the_rest()
    {
        var shown = S(Now) with { Approved = new HashSet<string>(["X-T1"], StringComparer.OrdinalIgnoreCase) };
        var pushed = SnapshotFigures.Of(S(Now.AddSeconds(15), 99.5) with { Warnings = [] });
        var over = pushed.Over(shown);
        Assert.That(over.TakenUtc, Is.EqualTo(Now.AddSeconds(15)));
        Assert.That(over.Pace!.RecentPerHour, Is.EqualTo(99.5));
        Assert.That(over.Warnings, Is.Empty);
        Assert.That(over.Approved, Does.Contain("X-T1"));
    }

    [Test]
    public void The_rest_digest_ignores_the_figures_and_moves_with_the_rest()
    {
        var s = S(Now);
        Assert.That(SnapshotFigures.RestOf(s with { TakenUtc = Now.AddMinutes(1), Pace = P(5), Warnings = [] }), Is.EqualTo(SnapshotFigures.RestOf(s)));
        Assert.That(SnapshotFigures.RestOf(s with { Approved = new HashSet<string>(["X-T1"]) }), Is.Not.EqualTo(SnapshotFigures.RestOf(s)));
    }

    [Test]
    public void The_server_keeps_the_newest_figures_for_dashboards_that_connect_later()
    {
        using var server = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        server.PublishFigures(S(Now, 7.25));
        server.PublishFigures(S(Now.AddSeconds(-5), 1));
        var kept = StatusStream.Deserialize(server.Retained(SnapshotFigures.Kind)!)!.PayloadAs<SnapshotFigures>()!;
        Assert.That(kept.Pace!.RecentPerHour, Is.EqualTo(7.25));
        Assert.That(server.Current().SnapshotTakenUtc, Is.EqualTo(Now));
    }

    [Test]
    public async Task A_dashboard_gets_the_state_then_the_kept_figures_on_connect_then_each_kind_published()
    {
        var pipe = "mindattic-orchestrator-status-test-" + Guid.NewGuid().ToString("N");
        using var server = new StatusStreamServer { PipeName = pipe };
        server.PublishFigures(S(Now));
        server.Start();
        var got = new List<StatusEnvelope>();
        var gotThree = new TaskCompletionSource();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var client = StatusStreamClient.RunAsync((StatusEnvelope e) =>
        {
            lock (got)
            {
                got.Add(e);
                if (got.Count == 2) server.Publish(StatusEnvelope.Of("ticket-event", new { Id = "M.ORCH-T879" }));
                if (got.Count >= 3) gotThree.TrySetResult();
            }
        }, _ => { }, stop.Token, pipe);
        await gotThree.Task.WaitAsync(stop.Token);
        stop.Cancel();
        await client;
        Assert.That(got.Select(e => e.Kind), Is.EqualTo(new[] { StatusMessage.State, SnapshotFigures.Kind, "ticket-event" }));
    }
}
