using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Health;

/// <summary>Whether the daemon answered on the status stream, and how fast: <see cref="Ms"/> from connecting to its first line,
/// null when it didn't answer, with <see cref="Error"/> saying why.</summary>
public sealed record DaemonReach(double? Ms, string? Error = null);

/// <summary>
/// The links probe (M.ORCH-T2031, docs/HEALTH.md section 3, part of M.ORCH-T2016): one run reports every link the daemon sits on.
/// <list type="bullet">
/// <item><c>link:latency</c> <c>probe-&gt;daemon</c>: the round trip to the daemon's first line on the status stream, graded by
/// <see cref="HealthCheck.LinkLatencyMs"/>; a daemon that doesn't answer is unhealthy.</item>
/// <item><c>link:latency</c> <c>probe-&gt;tickets-db</c> (<c>daemon-&gt;tickets-db</c> run in the daemon): one timed read of the
/// ticket database (<see cref="TicketDbPing"/>, M.ORCH-T2032), graded by the same line; no answer is unhealthy.</item>
/// <item><c>daemon:tick</c> <c>daemon</c>: the daemon's tick so far, or its last one when that was longer, in seconds
/// (<see cref="DaemonTick"/>, M.ORCH-T2032), graded by <see cref="HealthThresholds.TickSeconds"/>: the M.ORCH-T1922 stall held one tick for
/// many minutes. No tick kept, or a daemon no longer running, is degraded with no reading.</item>
/// <item><c>link:up</c> for each other end the daemon holds, a state graded by rule: <c>daemon-&gt;dashboard</c> and
/// <c>daemon-&gt;tickets-desktop</c> from their presences, <c>daemon-&gt;termhost:&lt;pid&gt;</c> for each hosted session kept and
/// <c>daemon-&gt;cli:&lt;pid&gt;</c> for each CLI whose activity it follows. <c>up</c> (its process runs) is healthy; <c>gone</c>
/// (still held, its process isn't running) is healthy for the two apps, as a cleanly closed app is no broken link (M.ORCH-T2062),
/// but degraded for a TermHost or CLI, a stale entry worth a look; <c>absent</c> (an app never heard) is healthy too.</item>
/// </list>
/// Every source is a function, so a test runs it on fakes; <see cref="ForHub"/> wires it to the daemon's own hub. Only reads.
/// </summary>
public sealed class LinkProbe
{
    /// <summary>The probe's name (its <c>probe</c>/<c>serviceId</c>).</summary>
    public const string Name = "links";

    /// <summary>The latency check id (unit <c>ms</c>).</summary>
    public const string LatencyCheck = "link:latency";

    /// <summary>The up/down check id (unit <c>state</c>).</summary>
    public const string UpCheck = "link:up";

    /// <summary>The states <see cref="UpCheck"/> reports.</summary>
    public const string Up = "up", Gone = "gone", Absent = "absent";

    /// <summary>The daemon link's instance.</summary>
    public const string DaemonLink = "probe->daemon";

    /// <summary>The apps whose presence is a link, each with the name its link goes by.</summary>
    public static readonly IReadOnlyList<(string App, string Link)> Apps =
        [(AppBus.OrchestratorDesktop, "dashboard"), (AppBus.TicketsDesktop, "tickets-desktop")];

    /// <summary>Reaches the daemon once; <see cref="ReachAsync"/> on the real stream by default.</summary>
    public Func<CancellationToken, Task<DaemonReach>> ReachDaemon { get; init; } = ct => ReachAsync(null, TimeSpan.FromSeconds(5), ct);

    /// <summary>Times one read of the ticket database; null skips the ticket-database link.</summary>
    public Func<CancellationToken, Task<TicketDbRead>>? ReadTicketDb { get; init; }

    /// <summary>The ticket-database link's instance: <c>probe-&gt;tickets-db</c>, or <c>daemon-&gt;tickets-db</c> run in the daemon.</summary>
    public string TicketDbLink { get; init; } = "probe->tickets-db";

    /// <summary>The daemon's tick timing as last kept; null skips the tick check.</summary>
    public Func<DaemonTick?>? Tick { get; init; }

    /// <summary>Whether the daemon process that kept <see cref="Tick"/> still runs.</summary>
    public Func<int, bool> DaemonAlive { get; init; } = pid => Term.TermSessions.HostStartOf(pid) is not null;

    /// <summary>The app presences heard; null skips the app links.</summary>
    public AppPresences? Presences { get; init; }

    /// <summary>The hosted sessions the daemon keeps.</summary>
    public Func<IReadOnlyList<TermSessionRecord>> TermSessions { get; init; } = () => [];

    /// <summary>Whether a hosted session's host still runs.</summary>
    public Func<TermSessionRecord, bool> HostAlive { get; init; } = Term.TermSessions.IsHostRunning;

    /// <summary>Each followed session's CLI process.</summary>
    public Func<IReadOnlyList<(string Session, int CliPid)>> CliPids { get; init; } = () => [];

    /// <summary>Whether a CLI process still runs.</summary>
    public Func<int, bool> CliAlive { get; init; } = pid => Term.TermSessions.HostStartOf(pid) is not null;

    /// <summary>The lines latency is graded against.</summary>
    public HealthThresholds Thresholds { get; init; } = HealthThresholds.Default;

    /// <summary>The machine named in the report.</summary>
    public string Host { get; init; } = Environment.MachineName;

    /// <summary>The period it runs on, sent in the report.</summary>
    public int IntervalSeconds { get; init; } = 30;

    /// <summary>The time now; tests swap it.</summary>
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>The probe wired to the daemon's own <paramref name="hub"/>: its presences, hosted sessions and CLI pids, and the
    /// stream on <paramref name="pipeName"/> (the default pipe when null).</summary>
    public static LinkProbe ForHub(AppBusHub hub, string? pipeName = null) => new()
    {
        ReachDaemon = ct => ReachAsync(pipeName, TimeSpan.FromSeconds(5), ct),
        TicketDbLink = "daemon->tickets-db",
        Presences = hub.Presences,
        TermSessions = hub.TermSessionsKept,
        CliPids = hub.Activity.CliPids,
    };

    /// <summary>The probe as <c>orchestrator probe links</c> runs it, outside the daemon: the status stream, one timed read of
    /// <paramref name="workspaceRoot"/>'s ticket database and the daemon's tick as <paramref name="db"/> keeps it (M.ORCH-T2032).
    /// The app, TermHost and CLI links are the daemon's own to read, so they're left out here.</summary>
    public static LinkProbe ForCli(string? workspaceRoot, OrchestratorDb db, HealthThresholds thresholds) => new()
    {
        ReadTicketDb = ct => TicketDbPing.TimeAsync(workspaceRoot, TimeSpan.FromSeconds(15), ct),
        Tick = () => DaemonTick.Load(db),
        Thresholds = thresholds,
    };

    /// <summary>One run: every link read and graded. Never throws for a source that fails; that link reports degraded with no
    /// reading, as docs/HEALTH.md section 2 asks.</summary>
    public async Task<HealthReport> RunAsync(CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        var results = new List<HealthResult>();

        DaemonReach reach;
        try { reach = await ReachDaemon(ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException) { reach = new(double.NaN, ex.Message); }
        var at = Clock();
        results.Add(reach.Ms switch
        {
            null => new HealthResult(LatencyCheck, DaemonLink, "component", null, "ms", HealthGrade.Unhealthy, at,
                Thresholds.LinkLatencyMs.Degraded, Thresholds.LinkLatencyMs.Unhealthy, reach.Error ?? "the daemon didn't answer"),
            { } ms => HealthResult.Graded(HealthCheck.LinkLatencyMs, Thresholds, ms, at, DaemonLink, reach.Error),
        });

        if (ReadTicketDb is { } readDb)
        {
            TicketDbRead read;
            try { read = await readDb(ct).ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException) { read = new(null, ex.Message); }
            var readAt = Clock();
            results.Add(read.Ms switch
            {
                null => new HealthResult(LatencyCheck, TicketDbLink, "datastore", null, "ms", HealthGrade.Unhealthy, readAt,
                    Thresholds.LinkLatencyMs.Degraded, Thresholds.LinkLatencyMs.Unhealthy, read.Error ?? "the ticket database didn't answer"),
                { } ms => HealthResult.Graded(HealthCheck.LinkLatencyMs, Thresholds, ms, readAt, TicketDbLink, read.Error) with { ComponentType = "datastore" },
            });
        }

        if (Tick is { } tick) results.Add(TickResult(tick));

        if (Presences is { } presences)
            foreach (var (app, link) in Apps)
                results.Add(Read("daemon->" + link, () =>
                {
                    if (presences.Heard(app) is not { } p) return (Absent, HealthGrade.Healthy, "not running");
                    return presences.IsLive(p) ? (Up, HealthGrade.Healthy, null) : (Gone, HealthGrade.Healthy, $"pid {p.Pid} is gone (closed)");
                }));

        foreach (var session in Safe(TermSessions))
            results.Add(Read("daemon->termhost:" + session.HostPid, () => HostAlive(session)
                ? (Up, HealthGrade.Healthy, null)
                : (Gone, HealthGrade.Degraded, $"session {session.Id} is still held; its host is gone")));

        foreach (var pid in Safe(CliPids).Select(c => c.CliPid).Distinct())
            results.Add(Read("daemon->cli:" + pid, () => CliAlive(pid)
                ? (Up, HealthGrade.Healthy, null)
                : (Gone, HealthGrade.Degraded, "its activity is still followed; the CLI is gone")));

        return new HealthReport(Name, Host, "The daemon's status stream and every app, TermHost and CLI it holds", Clock(),
            watch.ElapsedMilliseconds, IntervalSeconds, results);

        HealthResult TickResult(Func<DaemonTick?> read)
        {
            var now = Clock();
            var line = Thresholds.TickSeconds;
            HealthResult None(string why) =>
                new(DaemonTick.Check, DaemonTick.Component, "component", null, "s", HealthGrade.Degraded, now, line.Degraded, line.Unhealthy, why);
            try
            {
                if (read() is not { } t) return None("no tick kept: the daemon hasn't run here yet");
                if (!DaemonAlive(t.Pid)) return None($"the daemon that kept it (pid {t.Pid}) isn't running");
                var seconds = t.SecondsAt(now.UtcDateTime);
                var grade = line.Grade(seconds);
                string? output = t.Running && (now.UtcDateTime - t.StartedUtc).TotalSeconds >= seconds - 0.5
                    ? $"a tick has run {seconds:0} s so far"
                    : t.LastTookSeconds is { } last ? $"the last tick took {last:0} s" : null;
                return new HealthResult(DaemonTick.Check, DaemonTick.Component, "component", Math.Round(seconds, 1), "s", grade, now,
                    line.Degraded, line.Unhealthy, grade == HealthGrade.Healthy ? null : output);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return None("no reading: " + ex.Message); }
        }

        HealthResult Read(string link, Func<(string State, HealthGrade Grade, string? Output)> read)
        {
            var now = Clock();
            try
            {
                var (state, grade, output) = read();
                return new HealthResult(UpCheck, link, "component", null, "state", grade, now, Output: output, ObservedState: state);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return new HealthResult(UpCheck, link, "component", null, "state", HealthGrade.Degraded, now, Output: "no reading: " + ex.Message);
            }
        }

        static IReadOnlyList<T> Safe<T>(Func<IReadOnlyList<T>> list)
        {
            try { return list(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return []; }
        }
    }

    /// <summary>Connects to the status stream on <paramref name="pipeName"/> (the default pipe when null) and times its first line
    /// (the state it sends every subscriber first). Null ms when it isn't there or says nothing within <paramref name="timeout"/>.</summary>
    public static async Task<DaemonReach> ReachAsync(string? pipeName, TimeSpan timeout, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName ?? StatusStream.PipeName, PipeDirection.In,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try { await pipe.ConnectAsync(limit.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(null, "the daemon isn't listening"); }
            using var reader = new StreamReader(pipe, new UTF8Encoding(false));
            string? line;
            try { line = await reader.ReadLineAsync(limit.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new(null, $"connected, but nothing came in {timeout.TotalSeconds:0} s");
            }
            return line is null ? new(null, "the daemon closed the stream") : new(watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
        {
            return new(null, ex.Message);
        }
    }
}
