using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// The app bus (M.ORCH-T879): the message kinds Orchestrator.Desktop and Tickets.Desktop share over the daemon's status bus, beside
/// the status kinds of M.ORCH-T854/T872. Every message is one envelope line (<c>{"kind":..,"v":1,"at":..,"pid":..,"payload":{..}}</c>):
/// a writer writes it to the daemon's inbox pipe (<see cref="StatusStream.InboxPipeName"/>), the daemon relays it to every subscriber of
/// the status stream (<see cref="StatusStream.PipeName"/>), and each subscriber acts on the kinds it reads and skips the rest. The
/// daemon is the hub: it also publishes every ticket change itself, read from the ticket database's change feed, so a ticket filed,
/// approved or closed by the tickets CLI or Tickets.Desktop reaches every app with no change to either.
/// <para>
/// This file is the contract: it depends on nothing but System.Text.Json, so another repo can carry a copy of it (M.TIX-T158), and
/// <c>docs/app-bus.md</c> describes the same lines for a reader in any language. A field added keeps a kind's version; a breaking
/// change bumps it.
/// </para>
/// </summary>
public static class AppBus
{
    /// <summary>A ticket changed (<see cref="TicketBusEvent"/>). Published by the daemon from the ticket database's change feed, and by
    /// the Orchestrator the moment it claims or releases a ticket; a subscriber refreshes what shows that ticket.</summary>
    public const string Ticket = "ticket";

    /// <summary>"Open ticket X" (<see cref="OpenTicketRequest"/>): Tickets.Desktop shows the ticket and comes forward. Sent by the
    /// dashboard when a ticket id is clicked and Tickets.Desktop is present; when it isn't, the dashboard starts it with
    /// <c>--ticket X</c> instead.</summary>
    public const string OpenTicket = "open-ticket";

    /// <summary>"Show who is working X" (<see cref="ShowWorkerRequest"/>): the dashboard selects the person working the ticket and the
    /// CLI tab working it, and comes forward. Sent by Tickets.Desktop.</summary>
    public const string ShowWorker = "show-worker";

    /// <summary>An app is listening (<see cref="AppPresence"/>), with the request kinds it acts on. Each app sends it on every
    /// (re)connect; the daemon keeps the latest per app and hands them to each subscriber as it connects, so a sender knows whether
    /// a request will be heard (and, if not, starts the app instead). A presence whose process has exited is stale.</summary>
    public const string Presence = "app-present";

    /// <summary>The version of each of these kinds' payloads.</summary>
    public const int Version = 1;

    /// <summary>The Orchestrator's dashboard (MindAttic.Orchestrator.Desktop).</summary>
    public const string OrchestratorDesktop = "orchestrator-desktop";

    /// <summary>MindAttic.Tickets.Desktop.</summary>
    public const string TicketsDesktop = "tickets-desktop";

    /// <summary>Every kind of this bus.</summary>
    public static bool IsAppKind(string? kind) => kind is Ticket or OpenTicket or ShowWorker or Presence;
}

/// <summary>What happened to a ticket: <see cref="TicketBusEvent.Event"/>'s values.</summary>
public static class TicketEvents
{
    public const string Filed = "filed";
    public const string Approved = "approved";
    /// <summary>Its approval was taken back (proposed again).</summary>
    public const string Unapproved = "unapproved";
    public const string Rejected = "rejected";
    /// <summary>Someone started working it: <see cref="TicketBusEvent.Login"/> and/or <see cref="TicketBusEvent.Session"/> say who.</summary>
    public const string Claimed = "claimed";
    /// <summary>Nobody works it any more (landed, released, given back).</summary>
    public const string Released = "released";
    public const string Closed = "closed";
    public const string WontDo = "wont-do";
    public const string Reopened = "reopened";
    /// <summary>Its summary changed: <see cref="TicketBusEvent.New"/> is the new one.</summary>
    public const string Reworded = "reworded";
    /// <summary>Assigned to someone (<see cref="TicketBusEvent.New"/>, a login email) or unassigned (null).</summary>
    public const string Assigned = "assigned";
    public const string Prioritized = "prioritized";
    /// <summary>Its links changed.</summary>
    public const string Linked = "linked";
    /// <summary>Anything else (type, description, reference, id, repo): <see cref="TicketBusEvent.Field"/> names it.</summary>
    public const string Changed = "changed";
}

/// <summary>
/// The <see cref="AppBus.Ticket"/> payload: one change to one ticket. <see cref="Seq"/> is the ticket database's event number when it
/// came from its change feed (a subscriber may use it to skip one it saw), null when the Orchestrator published it as it acted.
/// </summary>
public sealed record TicketBusEvent
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("event")] public string Event { get; init; } = "";
    [JsonPropertyName("repo")] public string? Repo { get; init; }
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    /// <summary>The database field that changed ("status", "approval", "working", ...); null when published by the Orchestrator.</summary>
    [JsonPropertyName("field")] public string? Field { get; init; }
    [JsonPropertyName("old")] public string? Old { get; init; }
    [JsonPropertyName("new")] public string? New { get; init; }
    /// <summary>Who works it, for <see cref="TicketEvents.Claimed"/>: the login's email.</summary>
    [JsonPropertyName("login")] public string? Login { get; init; }
    /// <summary>Who works it, for <see cref="TicketEvents.Claimed"/>: the Claude session id.</summary>
    [JsonPropertyName("session")] public string? Session { get; init; }
    [JsonPropertyName("seq")] public long? Seq { get; init; }
    /// <summary>When it happened (UTC).</summary>
    [JsonPropertyName("atUtc")] public DateTime? AtUtc { get; init; }
    /// <summary>"tickets-db" (the daemon read it from the change feed) or "orchestrator" (published as it acted).</summary>
    [JsonPropertyName("source")] public string Source { get; init; } = "";
}

/// <summary>The <see cref="AppBus.OpenTicket"/> payload.</summary>
public sealed record OpenTicketRequest
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>The asking app (<see cref="AppBus.OrchestratorDesktop"/>).</summary>
    [JsonPropertyName("from")] public string? From { get; init; }
    /// <summary>The app meant to act on it (<see cref="AppBus.TicketsDesktop"/>); null for any that can.</summary>
    [JsonPropertyName("to")] public string? To { get; init; }
}

/// <summary>The <see cref="AppBus.ShowWorker"/> payload. <see cref="Login"/> and <see cref="Session"/> are who the asker knows works
/// it (the ticket database's <c>ticket_work</c>); either may be null, and the dashboard looks them up itself when both are.</summary>
public sealed record ShowWorkerRequest
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("login")] public string? Login { get; init; }
    [JsonPropertyName("session")] public string? Session { get; init; }
    [JsonPropertyName("from")] public string? From { get; init; }
    [JsonPropertyName("to")] public string? To { get; init; }
}

/// <summary>The <see cref="AppBus.Presence"/> payload.</summary>
public sealed record AppPresence
{
    [JsonPropertyName("app")] public string App { get; init; } = "";
    [JsonPropertyName("pid")] public int Pid { get; init; }
    /// <summary>The request kinds it acts on (<see cref="AppBus.OpenTicket"/>, <see cref="AppBus.ShowWorker"/>).</summary>
    [JsonPropertyName("handles")] public IReadOnlyList<string> Handles { get; init; } = [];
    [JsonPropertyName("atUtc")] public DateTime AtUtc { get; init; }
}
