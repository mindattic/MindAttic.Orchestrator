using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Health;

/// <summary>Whether the daemon answered on the status stream, and how fast: <see cref="Ms"/> from connecting to its first line,
/// null when it didn't answer, with <see cref="Error"/> saying why.</summary>
public sealed record DaemonReach(double? Ms, string? Error = null);

/// <summary>
/// The links probe (M.ORCH-T2031, docs/HEALTH.md section 3, part of M.ORCH-T2016): one run reports every link the daemon sits on.
/// <list type="bullet">
/// <item><c>link:latency</c> <c>probe-&gt;daemon</c>: the round trip to the daemon's first line on the status stream, graded by
/// <see cref="HealthCheck.LinkLatencyMs"/>; a daemon that doesn't answer is unhealthy.</item>
/// <item><c>link:up</c> for each other end the daemon holds, a state graded by rule: <c>daemon-&gt;dashboard</c> and
/// <c>daemon-&gt;tickets-desktop</c> from their presences, <c>daemon-&gt;termhost:&lt;pid&gt;</c> for each hosted session kept and
/// <c>daemon-&gt;cli:&lt;pid&gt;</c> for each CLI whose activity it follows. <c>up</c> (its process runs) is healthy; <c>gone</c>
/// (still held, its process isn't running) is degraded, a stale entry worth a look; <c>absent</c> (an app never heard) is
/// healthy, as a closed app is no broken link.</item>
/// </list>
/// Every source is a function, so a test runs it on fakes; <see cref="ForHub"/> wires it to the daemon's own hub. Only reads.
/// </summary>
public sealed class LinkProbe
{
    /// <summary>The probe's name (its <c>probe</c>/<c>serviceId</c>).</summary>
    public const string Name = "links";

    /// <summary>The latency check id.</summary>
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
        Presences = hub.Presences,
        TermSessions = hub.TermSessionsKept,
        CliPids = hub.Activity.CliPids,
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

        if (Presences is { } presences)
            foreach (var (app, link) in Apps)
                results.Add(Read("daemon->" + link, () =>
                {
                    if (presences.Heard(app) is not { } p) return (Absent, HealthGrade.Healthy, "not running");
                    return presences.IsLive(p) ? (Up, HealthGrade.Healthy, null) : (Gone, HealthGrade.Degraded, $"pid {p.Pid} is gone");
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
