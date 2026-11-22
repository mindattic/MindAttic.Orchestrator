using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Settings;

/// <summary>
/// <c>orchestrator probe links</c> (M.ORCH-T2032, docs/HEALTH.md section 3): the latencies the daemon depends on, from outside
/// it, through <see cref="LinkProbe.ForCli"/>: the round trip to the daemon's status stream (<c>link:latency probe-&gt;daemon</c>),
/// one timed read of the ticket database (<c>link:latency probe-&gt;tickets-db</c>), both graded by the link latency lines, and the
/// daemon's tick so far or last (<c>daemon:tick</c>, seconds), degraded past the daemon's own slow-tick line and unhealthy past
/// the pace gates' stale line: the M.ORCH-T1922 stall. Reads only.
/// </summary>
internal sealed class LinksProbeCommand : ProbeCommand
{
    protected override string Probe => LinkProbe.Name;

    protected override string Description => "The daemon's status stream, the ticket database and the daemon's tick";

    protected override async Task<IReadOnlyList<HealthResult>> Check(OrchestratorSettings s, HealthThresholds t, CancellationToken ct)
    {
        var root = s.WorkspaceRoot is { Length: > 0 } r ? r : new OrchestratorOptions().WorkspaceRoot;
        var report = await LinkProbe.ForCli(root, Ctx.Store().Db, t).RunAsync(ct);
        return report.Results;
    }
}
