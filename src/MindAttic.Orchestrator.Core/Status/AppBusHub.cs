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

    /// <summary>Says this process (<paramref name="app"/>) listens for <paramref name="handles"/>.</summary>
    public static bool PublishPresence(string app, IReadOnlyList<string> handles) =>
        Publish(AppBus.Presence, new AppPresence { App = app, Pid = Environment.ProcessId, Handles = handles, AtUtc = DateTime.UtcNow });
}

/// <summary>
/// The apps a subscriber has heard are listening (M.ORCH-T879, <see cref="AppBus.Presence"/>): whether a request sent now will be
/// acted on, or the app has to be started instead. A presence counts only while its process is alive. Thread-safe.
/// </summary>
public sealed class AppPresences
{
    private readonly Dictionary<string, AppPresence> byApp = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a process is alive; tests swap it.</summary>
    internal Func<int, bool> Alive { get; init; } = IsAlive;

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
        return p is not null && p.Handles.Contains(kind, StringComparer.OrdinalIgnoreCase) && Alive(p.Pid) ? p : null;
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
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

    /// <summary>The daemon's own store (<see cref="StateBus.StoreOf(Settings.OrchestratorDb)"/>, M.ORCH-T891): only its roster, orgs,
    /// settings, pause and reminders are kept for subscribers as they connect (another store's, a scratch one, still goes out but
    /// can't stand in for them). Null keeps every store's.</summary>
    public string? OwnStore { get; init; }

    /// <summary>Called after the daemon's own store's roster, orgs, settings, pause or reminders changed: its next plan reads them.</summary>
    public Action? StateChanged { get; init; }

    /// <summary>The key a presence is kept under on the stream: one per app.</summary>
    public static string PresenceKey(string app) => AppBus.Presence + ":" + app.Trim().ToLowerInvariant();

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
                    stream.Publish(envelope, PresenceKey(presence.App));
                    return;
                case StateBus.Members or StateBus.Orgs or StateBus.Settings or StateBus.Pause or StateBus.Reminders:
                    // The whole of its kind each time: the latest is kept, so a dashboard connecting gets it after the state.
                    var own = OwnStore is null || StateBus.SameStore(StateBus.StoreOf(envelope), OwnStore);
                    stream.Publish(envelope, own ? envelope.Kind : null);
                    if (own) StateChanged?.Invoke();
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
