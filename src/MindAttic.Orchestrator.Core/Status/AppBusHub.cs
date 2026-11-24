using System.Text.Json;
using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// Publishing on the app bus (M.ORCH-T879) from any Orchestrator process: in the daemon straight to its hub (<see cref="Hub"/>),
/// anywhere else (the CLI's <c>next --claim</c> and <c>release</c>, the dashboard) through the daemon's inbox. Best effort: with no
/// daemon running nothing is sent, and the daemon's change feed (<see cref="TicketActivityFeed"/>) still carries a ticket's change
/// once it's back. Never throws.
/// </summary>
public static class AppBusClient
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TicketBus");

    /// <summary>The daemon's own hub, set while it runs: its messages go straight to the stream, not round its own pipe.</summary>
    internal static AppBusHub? Hub { get; set; }

    /// <summary>How a message leaves a process with no hub; tests swap it.</summary>
    internal static Func<StatusEnvelope, bool> Send { get; set; } = e => StatusInbox.TrySend(e);

    /// <summary>Publishes <paramref name="payload"/> as <paramref name="kind"/>; whether it reached the daemon.</summary>
    public static bool Publish<T>(string kind, T payload) => Publish(kind, payload, AppBus.Version);

    /// <summary>Publishes <paramref name="payload"/> as version <paramref name="version"/> of <paramref name="kind"/> (M.ORCH-T891:
    /// the <see cref="StateBus"/> kinds); whether it reached the daemon.</summary>
    public static bool Publish<T>(string kind, T payload, int version)
    {
        try
        {
            var envelope = StatusEnvelope.Of(kind, payload, version);
            if (Hub is { } hub)
            {
                hub.Take(envelope);
                return true;
            }
            return Send(envelope);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Debug(ex, "Couldn't publish a {Kind:l} message", kind);
            return false;
        }
    }

    /// <summary>Says ticket <paramref name="id"/> was <paramref name="ticketEvent"/> by the Orchestrator, as it acted.</summary>
    public static bool PublishTicket(string id, string ticketEvent, string? login = null, string? session = null) =>
        Publish(AppBus.Ticket, new TicketBusEvent
        {
            Id = id.Trim().ToUpperInvariant(), Event = ticketEvent, Login = login, Session = session,
            AtUtc = DateTime.UtcNow, Source = "orchestrator",
        });

    /// <summary>Publishes one probe run's <paramref name="report"/> as the <see cref="AppBus.Health"/> kind (M.ORCH-T2021), its payload
    /// the report's health+json; whether it reached the daemon. Best effort, as every publish.</summary>
    public static bool PublishHealth(HealthReport report)
    {
        try { return Publish(AppBus.Health, JsonSerializer.Deserialize<JsonElement>(report.ToJson()), AppBus.HealthVersion); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Debug(ex, "Couldn't publish a health report");
            return false;
        }
    }

    /// <summary>Says this process (<paramref name="app"/>) listens for <paramref name="handles"/>.</summary>
    public static bool PublishPresence(string app, IReadOnlyList<string> handles) =>
        Publish(AppBus.Presence, new AppPresence { App = app, Pid = Environment.ProcessId, Handles = handles, AtUtc = DateTime.UtcNow });
}

/// <summary>
/// The apps a subscriber has heard are listening (M.ORCH-T879, <see cref="AppBus.Presence"/>): whether a request sent now will be
/// acted on, or the app has to be started instead. A presence counts only while the process that said it is alive: its pid is
/// running and that process started no later than the presence was said (M.ORCH-T1010: judged by pid alone, a presence the hub
/// kept from an app long gone counted once its pid was reused, and open-ticket went to nobody instead of starting
/// Tickets.Desktop). Thread-safe.
/// </summary>
public sealed class AppPresences
{
    private readonly Dictionary<string, AppPresence> byApp = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When the process with a pid started (UTC), or null when none runs; tests swap it.</summary>
    internal Func<int, DateTime?> StartOf { get; init; } = StartTimeOf;

    /// <summary>How much later than its presence a process may seem to have started (clock reads differ a little).</summary>
    internal static readonly TimeSpan StartSlack = TimeSpan.FromSeconds(2);

    /// <summary>Keeps <paramref name="presence"/> as its app's latest, replacing the one before; one with no app or pid is ignored.</summary>
    public void Note(AppPresence presence)
    {
        if (presence.App.Length == 0 || presence.Pid <= 0) return;
        lock (byApp) byApp[presence.App] = presence;
    }

    /// <summary>Forgets them all: the stream dropped, and each comes again with the full state on the next connection.</summary>
    public void Clear()
    {
        lock (byApp) byApp.Clear();
    }

    /// <summary>The live presence of <paramref name="app"/> that acts on <paramref name="kind"/>; null when there's none.</summary>
    public AppPresence? Listening(string app, string kind)
    {
        AppPresence? p;
        lock (byApp) p = byApp.GetValueOrDefault(app);
        return p is not null && p.Handles.Contains(kind, StringComparer.OrdinalIgnoreCase) && SaidByLiveProcess(p) ? p : null;
    }

    /// <summary>The latest presence heard from <paramref name="app"/>, live or not; null when none has been (M.ORCH-T2031: the links
    /// probe tells an app that left from one never started).</summary>
    public AppPresence? Heard(string app)
    {
        lock (byApp) return byApp.GetValueOrDefault(app);
    }

    /// <summary>Whether the process that said <paramref name="presence"/> still runs (M.ORCH-T2031).</summary>
    public bool IsLive(AppPresence presence) => SaidByLiveProcess(presence);

    /// <summary>Whether the process that said <paramref name="presence"/> still runs: its pid does, and not as a process started after
    /// it was said (a reused pid). One with no time of its own (an older writer) is judged by its pid alone.</summary>
    private bool SaidByLiveProcess(AppPresence presence)
    {
        if (StartOf(presence.Pid) is not { } started) return false;
        return presence.AtUtc == default || started.ToUniversalTime() <= presence.AtUtc.ToUniversalTime() + StartSlack;
    }

    private static DateTime? StartTimeOf(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return p.HasExited ? null : p.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return null; }
    }
}

/// <summary>
/// The daemon's end of the app bus (M.ORCH-T879): takes each app message (from its inbox or from its own process) and sends it to
/// every subscriber of the status stream. A ticket change is also noted against the change feed (so the feed's copy of it isn't
/// sent twice) and brings the next snapshot forward (<see cref="TicketChanged"/>); a presence is kept per app and handed to each
/// subscriber as it connects. Never throws.
/// </summary>
public sealed class AppBusHub(StatusStreamServer stream, TicketActivityFeed feed)
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TicketBus");

    /// <summary>Called after a ticket change went out: the daemon refreshes its plan so the queue follows within seconds.</summary>
    public Action? TicketChanged { get; init; }

    /// <summary>Called with each health report kept (M.ORCH-T2039), off the hub's lock: the daemon grades it for change-only
    /// alerts (<see cref="Health.HealthAlerts"/>). One older than the probe's newest, or unreadable, isn't passed on.</summary>
    public Action<HealthReport>? HealthTaken { get; init; }

    /// <summary>The daemon's own store (<see cref="StateBus.StoreOf(Settings.OrchestratorDb)"/>, M.ORCH-T891): only its roster, orgs,
    /// settings, pause, reminders, cutoffs and allowances are kept for subscribers as they connect (another store's, a scratch
    /// one, still goes out but can't stand in for them). Null keeps every store's.</summary>
    public string? OwnStore { get; init; }

    /// <summary>Called after the daemon's own store's roster, orgs, settings, pause, reminders, cutoffs or allowances changed: its
    /// next plan reads them at once instead of at its next tick (<c>org limit</c>/<c>org allow</c>/<c>org disallow</c>,
    /// bughunt 2026-11-02).</summary>
    public Action? StateChanged { get; init; }

    /// <summary>Called with a <see cref="AppBus.ShowPreview"/> request no present <see cref="AppBus.MediaViewer"/> handles (M.ORCH-T2133),
    /// off the hub's lock, after the request went out: the daemon starts the viewer on the set itself.</summary>
    public Action<ShowPreviewRequest>? PreviewUnheard { get; init; }

    /// <summary>The presences this hub has taken (M.ORCH-T2031), for the links probe run in the daemon.</summary>
    public AppPresences Presences { get; init; } = new();

    /// <summary>The key a presence is kept under on the stream: one per app.</summary>
    public static string PresenceKey(string app) => AppBus.Presence + ":" + app.Trim().ToLowerInvariant();

    /// <summary>The key a session's latest activity is kept under on the stream (M.ORCH-T962): one per session, all of them handed
    /// to each subscriber as it connects (the activity snapshot).</summary>
    public static string ActivityKey(string session) => AppBus.Activity + ":" + session.Trim();

    /// <summary>The per-session coalescing of the activity kind (M.ORCH-T962): at most one message per session per its
    /// <see cref="ActivityCoalescer.Every"/>, the latest kept per session.</summary>
    public ActivityCoalescer Activity { get; } = new();

    /// <summary>
    /// The masking every activity text field (target, verb, summary) passes before it's published (M.ORCH-T962): the shared activity
    /// masker (<see cref="Term.SecretShapes.MaskActivity"/>, M.ORCH-T963), a second layer over the hooks' records, which are masked
    /// as they're written (M.ORCH-T964), and the only one over a line a hook wrote to the inbox. Tests swap it.
    /// </summary>
    public Func<string, string> MaskActivityText { get; init; } = Term.SecretShapes.MaskActivity;

    /// <summary>
    /// Takes <paramref name="activity"/>, the newest picture of its session (from its activity record or the inbox), masked and cut
    /// (<see cref="CleanActivity"/>), and publishes it now or holds it for <see cref="FlushActivity"/> (<see cref="ActivityCoalescer.Offer"/>).
    /// <paramref name="cliPid"/> is its CLI's process, when known, by which a silent session is judged gone. Never throws.
    /// </summary>
    public void OfferActivity(ActivityPayload activity, DateTime now, int? cliPid = null)
    {
        try
        {
            var clean = CleanActivity(activity);
            // Decided and published under one lock (M.ORCH-T1976): else a stale "running" could land after the "ended" and be kept.
            lock (Activity.Gate)
                if (Activity.Offer(clean, now, cliPid) is { } send) SendActivity(send);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't publish a session's activity");
        }
    }

    /// <summary>Publishes each held activity message whose session's gap has passed by <paramref name="now"/>. Never throws.</summary>
    public void FlushActivity(DateTime now)
    {
        try
        {
            lock (Activity.Gate)
                foreach (var due in Activity.Due(now)) SendActivity(due);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't publish the held activity");
        }
    }

    /// <summary>Ends, once, each session silent for <paramref name="silentFor"/> whose CLI is gone (<see cref="ActivityCoalescer.Expire"/>),
    /// and stops keeping it for connecting subscribers. Never throws.</summary>
    public void ExpireActivity(DateTime now, TimeSpan silentFor, Func<int, bool> alive)
    {
        try
        {
            lock (Activity.Gate)
                foreach (var ended in Activity.Expire(now, silentFor, alive)) SendActivity(ended);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't end the silent sessions' activity");
        }
    }

    /// <summary>
    /// <paramref name="activity"/> fit to publish: its text fields masked (<see cref="MaskActivityText"/>) and cut to their lengths
    /// (<see cref="ActivityPayload.MaxTarget"/>, <see cref="ActivityPayload.MaxSummary"/>), an MCP tool collapsed to <c>mcp</c>, and
    /// blank fields left out. Only <see cref="ActivityPayload"/>'s own fields ever go out, whatever its writer put beside them.
    /// </summary>
    public ActivityPayload CleanActivity(ActivityPayload activity)
    {
        var tool = Blank(activity.Tool);
        if (tool is not null && (tool.StartsWith("mcp__", StringComparison.OrdinalIgnoreCase) || tool.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase))) tool = "mcp";
        return activity with
        {
            Session = activity.Session.Trim(),
            Provider = Blank(activity.Provider)?.ToLowerInvariant(),
            Repo = Blank(activity.Repo),
            Login = Blank(activity.Login),
            State = Blank(activity.State)?.ToLowerInvariant() ?? "",
            Tool = Cut(tool, 40),
            Target = Cut(Masked(activity.Target), ActivityPayload.MaxTarget),
            Verb = Cut(Masked(activity.Verb), 40),
            Ticket = Cut(Blank(activity.Ticket), 40),
            Summary = Cut(Masked(activity.Summary), ActivityPayload.MaxSummary),
            Subagents = activity.Subagents is > 0 ? activity.Subagents : null,
        };

        string? Masked(string? text) => Blank(text) is { } t ? Blank(MaskActivityText(t)) : null;
        static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        static string? Cut(string? text, int max) => text is null || text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
    }

    /// <summary>One activity message out: kept per session for connecting subscribers, or, for its end, sent once and no longer kept.</summary>
    private void SendActivity(ActivityPayload activity)
    {
        var envelope = StatusEnvelope.Of(AppBus.Activity, activity, AppBus.ActivityVersion);
        var key = ActivityKey(activity.Session);
        if (activity.State == ActivityStates.Ended)
        {
            stream.Forget(key);
            stream.Publish(envelope);
            return;
        }
        stream.Publish(envelope, key);
    }

    /// <summary>The key a hosted session's announcement is kept under on the stream (M.ORCH-T1998): one per session, so a
    /// dashboard (re)connecting gets every live session's <c>started</c> replayed instead of waiting for its resync timer.</summary>
    public static string TermSessionKey(string id) => Term.TermBus.Kind + ":" + id.Trim();

    // The sessions kept under TermSessionKey, by id, with their records: ExpireTermSessions drops one whose host is gone without
    // its ended ever arriving (a killed host), so no dashboard is handed a tab for it.
    private readonly Dictionary<string, Term.TermSessionRecord> termKept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A <c>term-session</c> message (M.ORCH-T1998): its <c>started</c> kept per session for connecting subscribers;
    /// its <c>ended</c> or <c>undock</c> sent once and the session no longer kept; anything else (a change, a launcher request)
    /// only sent.</summary>
    private void TakeTermSession(StatusEnvelope envelope)
    {
        if (envelope.V > Term.TermBus.Version || envelope.PayloadAs<Term.TermSessionEvent>() is not { Id.Length: > 0 } message)
        {
            stream.Publish(envelope);
            return;
        }
        var key = TermSessionKey(message.Id);
        lock (termKept)
        {
            switch (message.Event)
            {
                case Term.TermSessionEvents.Started:
                    termKept[message.Id] = message.ToRecord();
                    stream.Publish(envelope, key);
                    return;
                case Term.TermSessionEvents.Ended or Term.TermSessionEvents.Undock:
                    termKept.Remove(message.Id);
                    stream.Forget(key);
                    break;
            }
        }
        stream.Publish(envelope);
    }

    /// <summary>The hosted sessions kept for connecting subscribers (M.ORCH-T2031: the links probe checks each one's host).</summary>
    public IReadOnlyList<Term.TermSessionRecord> TermSessionsKept()
    {
        lock (termKept) return [.. termKept.Values];
    }

    /// <summary>Stops keeping each hosted session whose host no longer runs (M.ORCH-T1998), its ended lost. Never throws.</summary>
    public void ExpireTermSessions()
    {
        try
        {
            List<Term.TermSessionRecord> kept;
            lock (termKept) kept = [.. termKept.Values];
            foreach (var gone in kept.Where(s => !Term.TermSessions.IsHostRunning(s)))
                lock (termKept)
                    if (termKept.TryGetValue(gone.Id, out var now) && now.HostPid == gone.HostPid && now.HostStartUtc == gone.HostStartUtc)
                    {
                        termKept.Remove(gone.Id);
                        stream.Forget(TermSessionKey(gone.Id));
                    }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't drop the gone hosted sessions");
        }
    }

    /// <summary>The key a probe's latest health report is kept under on the stream (M.ORCH-T2021): one per probe, all of them handed
    /// to each subscriber as it connects.</summary>
    public static string HealthKey(string probe) => AppBus.Health + ":" + probe.Trim().ToLowerInvariant();

    /// <summary>How many results the hub keeps per check instance (docs/HEALTH.md section 4): three hours at the 30 s default sample.</summary>
    public const int HealthHistoryLength = 360;

    // Per check instance (probe + check id + componentId): its results oldest first, at most HealthHistoryLength, the last the latest.
    private readonly Dictionary<(string Probe, string CheckId, string ComponentId), Queue<HealthResult>> healthKept = [];
    // Each probe's newest report time, so one arriving out of order is relayed but never kept over a newer one.
    private readonly Dictionary<string, DateTimeOffset> healthAt = new(StringComparer.OrdinalIgnoreCase);

    private static (string, string, string) HealthInstance(string probe, string checkId, string? componentId) =>
        (probe.Trim().ToLowerInvariant(), checkId.Trim(), componentId?.Trim() ?? "");

    /// <summary>The latest result kept for one check instance; null when none has come.</summary>
    public HealthResult? HealthLatest(string probe, string checkId, string? componentId = null)
    {
        lock (healthKept)
            return healthKept.TryGetValue(HealthInstance(probe, checkId, componentId), out var ring) && ring.Count > 0 ? ring.Last() : null;
    }

    /// <summary>The results kept for one check instance, oldest first, at most <see cref="HealthHistoryLength"/>.</summary>
    public IReadOnlyList<HealthResult> HealthHistory(string probe, string checkId, string? componentId = null)
    {
        lock (healthKept)
            return healthKept.TryGetValue(HealthInstance(probe, checkId, componentId), out var ring) ? [.. ring] : [];
    }

    /// <summary>Every numeric reading kept for <paramref name="checkId"/> across all its probes and instances, oldest first per
    /// instance: what the health lines are learned from (M.ORCH-T2057). Results with no reading are left out.</summary>
    public IReadOnlyList<double> HealthReadings(string checkId)
    {
        var id = checkId.Trim();
        lock (healthKept)
            return [.. healthKept.Where(kv => string.Equals(kv.Key.CheckId, id, StringComparison.Ordinal))
                .SelectMany(kv => kv.Value)
                .Where(r => r.ObservedValue is { } v && double.IsFinite(v))
                .Select(r => r.ObservedValue!.Value)];
    }

    /// <summary>A <c>health</c> message (M.ORCH-T2021): relayed to every subscriber, its report kept per probe for connecting ones,
    /// and each of its results added to its check instance's history. One older than the probe's newest is only relayed; one this
    /// daemon can't read (a later version, not a report) is relayed unread.</summary>
    private void TakeHealth(StatusEnvelope envelope)
    {
        if (envelope.V > AppBus.HealthVersion || envelope.Payload.ValueKind != JsonValueKind.Object
            || HealthReport.FromJson(envelope.Payload.GetRawText()) is not { Probe.Length: > 0 } report)
        {
            stream.Publish(envelope);
            return;
        }
        var probe = report.Probe.Trim();
        lock (healthKept)
        {
            if (healthAt.TryGetValue(probe, out var held) && report.Time < held)
            {
                stream.Publish(envelope);
                return;
            }
            healthAt[probe] = report.Time;
            foreach (var result in report.Results)
            {
                var instance = HealthInstance(probe, result.CheckId, result.ComponentId);
                if (!healthKept.TryGetValue(instance, out var ring)) healthKept[instance] = ring = new Queue<HealthResult>();
                ring.Enqueue(result);
                while (ring.Count > HealthHistoryLength) ring.Dequeue();
            }
            stream.Publish(envelope, HealthKey(probe));
        }
        try { HealthTaken?.Invoke(report); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warning(ex, "Couldn't grade a health report for alerts"); }
    }

    /// <summary>Sends <paramref name="envelope"/> on, by its kind.</summary>
    public void Take(StatusEnvelope envelope)
    {
        try
        {
            switch (envelope.Kind)
            {
                case AppBus.Ticket:
                    if (envelope.PayloadAs<TicketBusEvent>() is not { Id.Length: > 0 } change) return;
                    if (change.Source != "tickets-db") feed.Published(change);
                    stream.Publish(envelope);
                    TicketChanged?.Invoke();
                    return;
                case AppBus.Presence:
                    if (envelope.PayloadAs<AppPresence>() is not { App.Length: > 0 } presence) return;
                    Presences.Note(presence);
                    stream.Publish(envelope, PresenceKey(presence.App));
                    return;
                case AppBus.Activity:
                    // A hook's own line on the inbox (M.ORCH-T962): through the same coalescing as the activity records. A version
                    // past this daemon's is relayed unread, as any kind it doesn't know.
                    if (envelope.V > AppBus.ActivityVersion)
                    {
                        stream.Publish(envelope);
                        return;
                    }
                    if (envelope.PayloadAs<ActivityPayload>() is not { Session.Length: > 0 } activity) return;
                    OfferActivity(activity, DateTime.UtcNow);
                    return;
                case Term.TermBus.Kind:
                    TakeTermSession(envelope);
                    return;
                case AppBus.Health:
                    TakeHealth(envelope);
                    return;
                case AppBus.ShowPreview:
                    // Relayed to every subscriber like any request; when no viewer that handles it is present, the daemon, which runs
                    // in the owner's session, shows the set itself (M.ORCH-T2133). A version past this daemon's is relayed unread.
                    stream.Publish(envelope);
                    if (envelope.V > AppBus.Version || envelope.PayloadAs<ShowPreviewRequest>() is not { Set.Length: > 0 } preview) return;
                    if (preview.To is { Length: > 0 } to && !string.Equals(to, AppBus.MediaViewer, StringComparison.OrdinalIgnoreCase)) return;
                    if (Presences.Listening(AppBus.MediaViewer, AppBus.ShowPreview) is null) PreviewUnheard?.Invoke(preview);
                    return;
                case StateBus.Members or StateBus.Orgs or StateBus.Settings or StateBus.Reminders or StateBus.Cutoffs or StateBus.Allowances or StateBus.Locks:
                    // The whole of its kind each time: the latest is kept, so a dashboard connecting gets it after the state.
                    var own = OwnStore is null || StateBus.SameStore(StateBus.StoreOf(envelope), OwnStore);
                    // Each send is its own connection and the inbox's listeners relay at once, so two quick changes can arrive in
                    // either order (M.ORCH-T1002): one read before the payload already held is older news, and is dropped, never
                    // kept or applied over the newer one. The check and the publish happen under one lock (M.ORCH-T1013): with the
                    // publish after it, an older payload checked first could still be published after a newer one and stay kept.
                    if (!own)
                    {
                        stream.Publish(envelope);
                        return;
                    }
                    lock (stateAt)
                    {
                        if (!NewerState(envelope))
                        {
                            log.Debug("Dropped a {Kind:l} message older than the one held", envelope.Kind);
                            return;
                        }
                        stream.Publish(envelope, envelope.Kind);
                    }
                    StateChanged?.Invoke();
                    return;
                default:
                    stream.Publish(envelope);
                    return;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't relay a {Kind:l} app message", envelope.Kind);
        }
    }

    private readonly Dictionary<string, DateTime> stateAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="envelope"/>'s payload was read no earlier than the one of its kind held, noting it when so.
    /// One with no time of its own (an older writer) is taken. The caller holds the lock on <see cref="stateAt"/>.</summary>
    private bool NewerState(StatusEnvelope envelope)
    {
        if (StateBus.AtOf(envelope) is not { } at) return true;
        if (stateAt.TryGetValue(envelope.Kind, out var held) && at < held) return false;
        stateAt[envelope.Kind] = at;
        return true;
    }

    /// <summary>A change the feed read from the ticket database, sent to every subscriber.</summary>
    public void PublishFromFeed(TicketBusEvent change) => Take(StatusEnvelope.Of(AppBus.Ticket, change, AppBus.Version));

    /// <summary>
    /// Reads the ticket database's change feed every <paramref name="every"/> until <paramref name="ct"/> ends, publishing each
    /// change. <paramref name="workspaceRoot"/> is asked each time, as the daemon's workspace can change while it runs.
    /// </summary>
    public async Task FollowFeedAsync(Func<string?> workspaceRoot, TimeSpan every, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { feed.Poll(workspaceRoot(), PublishFromFeed, DateTime.UtcNow); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warning(ex, "The ticket change feed failed; trying again"); }
            try { await Task.Delay(every, ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
        }
    }
}
