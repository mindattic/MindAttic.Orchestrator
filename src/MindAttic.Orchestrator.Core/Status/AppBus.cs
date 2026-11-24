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

    /// <summary>What one CLI session is doing now (<see cref="ActivityPayload"/>, M.ORCH-T962; docs/spikes/activity-feed.md section 5):
    /// the current picture of one session, not a log, so the latest for a <see cref="ActivityPayload.Session"/> replaces the last. The
    /// daemon publishes it from the hooks' per-session activity records, at most one per session per coalescing interval, keeps the
    /// latest per session and hands them all to each subscriber as it connects (the activity snapshot). Version
    /// <see cref="ActivityVersion"/>.</summary>
    public const string Activity = "activity";

    /// <summary>A Claude CLI's badge state changed (M.ORCH-T1996): sent by the context gate as its hook exits, when a turn starts or
    /// ends, a subagent starts or stops, or a badge-relevant field of its <c>.state.json</c> (asking, deciding, goal, closed,
    /// limit_stop, needs_typed, exempt) changed. Relayed as is to every subscriber, not kept. Payload (v1):
    /// <c>session</c> (the CLI's session id), <c>event</c> (the hook event), <c>state</c> (a hint: "working", "idle", "asking" or
    /// ""), and when known <c>agent</c> (the subagent's id), <c>subagents</c> (the CLI's running subagents, on a subagent event) and
    /// <c>cliPid</c>. A notice to re-read that session, not the truth itself.</summary>
    public const string AgentState = "agent-state";

    /// <summary>One health probe run's report (M.ORCH-T2021, docs/HEALTH.md section 4): the <c>application/health+json</c> shape of
    /// <c>HealthReport</c> (status, probe, host, time, durationMs, intervalSeconds and its results grouped under <c>checks</c> by
    /// check id). Probes write it to the inbox; the daemon keeps the latest report per probe for subscribers as they connect and,
    /// per check instance (probe + check id + componentId), the latest result and a bounded history. Version
    /// <see cref="HealthVersion"/>.</summary>
    public const string Health = "health";

    /// <summary>"Show preview set X" (<see cref="ShowPreviewRequest"/>, M.ORCH-T2133): MindAttic.MediaViewer opens the set and comes
    /// forward. Any process may write it to the inbox, even one with no desktop of its own (a scheduled one-shot, a headless CLI);
    /// the daemon relays it, and when no <see cref="MediaViewer"/> is present that handles it, starts the viewer on the set itself
    /// in the owner's session (or, without the viewer installed, opens the set's first media file).</summary>
    public const string ShowPreview = "show-preview";

    /// <summary>The version of the <see cref="Health"/> payload.</summary>
    public const int HealthVersion = 1;

    /// <summary>The version of the <see cref="Activity"/> payload.</summary>
    public const int ActivityVersion = 1;

    /// <summary>The version of each of these kinds' payloads.</summary>
    public const int Version = 1;

    /// <summary>The Orchestrator's dashboard (MindAttic.Orchestrator.Desktop).</summary>
    public const string OrchestratorDesktop = "orchestrator-desktop";

    /// <summary>MindAttic.Tickets.Desktop.</summary>
    public const string TicketsDesktop = "tickets-desktop";

    /// <summary>MindAttic.MediaViewer, which shows preview sets (<see cref="ShowPreview"/>).</summary>
    public const string MediaViewer = "viewer";

    /// <summary>Every kind of this bus.</summary>
    public static bool IsAppKind(string? kind) => kind is Ticket or OpenTicket or ShowWorker or Presence or Activity or AgentState or Health or ShowPreview;
}

/// <summary>A session's state in an <see cref="AppBus.Activity"/> message: <see cref="ActivityPayload.State"/>'s values.</summary>
public static class ActivityStates
{
    /// <summary>A tool call or turn is in progress.</summary>
    public const string Working = "working";
    /// <summary>Its turn ended; it waits for you.</summary>
    public const string Idle = "idle";
    /// <summary>Blocked on a permission prompt or a question (the reason is never sent).</summary>
    public const string Asking = "asking";
    /// <summary>The pace gate is holding it.</summary>
    public const string Held = "held";
    /// <summary>The CLI exited (or went silent with its process gone): sent once, then the session is dropped.</summary>
    public const string Ended = "ended";
}

/// <summary>
/// The <see cref="AppBus.Activity"/> payload (v1): one session's current picture. Never a raw tool input, a prompt or a result:
/// <see cref="Target"/> is a short masked target (a file's base name, a host, a pattern, a shell command's description or its
/// program name), at most <see cref="MaxTarget"/> characters.
/// </summary>
public sealed record ActivityPayload
{
    /// <summary>The longest <see cref="Target"/> sent.</summary>
    public const int MaxTarget = 80;

    /// <summary>The longest <see cref="Summary"/> sent.</summary>
    public const int MaxSummary = 120;

    /// <summary>The CLI's session id: the key, a newer message replaces the older.</summary>
    [JsonPropertyName("session")] public string Session { get; init; } = "";
    /// <summary><c>claude</c>, <c>codex</c>, <c>gemini</c> or <c>kimi</c>.</summary>
    [JsonPropertyName("provider")] public string? Provider { get; init; }
    /// <summary>The repo the session works in.</summary>
    [JsonPropertyName("repo")] public string? Repo { get; init; }
    /// <summary>The login's email; left out when the session is private or unknown.</summary>
    [JsonPropertyName("login")] public string? Login { get; init; }
    /// <summary>One of <see cref="ActivityStates"/>.</summary>
    [JsonPropertyName("state")] public string State { get; init; } = "";
    /// <summary>The tool name (<c>Edit</c>, <c>Bash</c>, ...); MCP tools collapse to <c>mcp</c>. Absent when idle.</summary>
    [JsonPropertyName("tool")] public string? Tool { get; init; }
    /// <summary>A short masked target of the tool call (file base name, host, pattern or command name); absent when idle.</summary>
    [JsonPropertyName("target")] public string? Target { get; init; }
    /// <summary>A human phrase (<c>Editing</c>).</summary>
    [JsonPropertyName("verb")] public string? Verb { get; init; }
    /// <summary>The workflow ticket id the session is working on, when known.</summary>
    [JsonPropertyName("ticket")] public string? Ticket { get; init; }
    /// <summary>How many subagents the session has running.</summary>
    [JsonPropertyName("subagents")] public int? Subagents { get; init; }
    /// <summary>The masked last-message summary, on idle only and only when enabled; off in v1.</summary>
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    /// <summary>A per-session counter, so a consumer can drop a reordered message.</summary>
    [JsonPropertyName("seq")] public long? Seq { get; init; }
    /// <summary>When the hook saw it (UTC).</summary>
    [JsonPropertyName("atUtc")] public DateTime? AtUtc { get; init; }
}

/// <summary>What happened to a ticket: <see cref="TicketBusEvent.Event"/>'s values.</summary>
public static class TicketEvents
{
    /// <summary>The filed constant.</summary>
    public const string Filed = "filed";
    /// <summary>The approved constant.</summary>
    public const string Approved = "approved";
    /// <summary>Its approval was taken back (proposed again).</summary>
    public const string Unapproved = "unapproved";
    /// <summary>The rejected constant.</summary>
    public const string Rejected = "rejected";
    /// <summary>Someone started working it: <see cref="TicketBusEvent.Login"/> and/or <see cref="TicketBusEvent.Session"/> say who.</summary>
    public const string Claimed = "claimed";
    /// <summary>Nobody works it any more (landed, released, given back).</summary>
    public const string Released = "released";
    /// <summary>The closed constant.</summary>
    public const string Closed = "closed";
    /// <summary>The wont do constant.</summary>
    public const string WontDo = "wont-do";
    /// <summary>The reopened constant.</summary>
    public const string Reopened = "reopened";
    /// <summary>Its summary changed: <see cref="TicketBusEvent.New"/> is the new one.</summary>
    public const string Reworded = "reworded";
    /// <summary>Assigned to someone (<see cref="TicketBusEvent.New"/>, a login email) or unassigned (null).</summary>
    public const string Assigned = "assigned";
    /// <summary>The prioritized constant.</summary>
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
    /// <summary>The ticket id.</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>One of <see cref="TicketEvents"/>: what happened to it.</summary>
    [JsonPropertyName("event")] public string Event { get; init; } = "";
    /// <summary>The repo the ticket belongs to.</summary>
    [JsonPropertyName("repo")] public string? Repo { get; init; }
    /// <summary>The ticket summary at the time.</summary>
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    /// <summary>The database field that changed ("status", "approval", "working", ...); null when published by the Orchestrator.</summary>
    [JsonPropertyName("field")] public string? Field { get; init; }
    /// <summary>The field's previous value.</summary>
    [JsonPropertyName("old")] public string? Old { get; init; }
    /// <summary>The field's new value.</summary>
    [JsonPropertyName("new")] public string? New { get; init; }
    /// <summary>Who works it, for <see cref="TicketEvents.Claimed"/>: the login's email.</summary>
    [JsonPropertyName("login")] public string? Login { get; init; }
    /// <summary>Who works it, for <see cref="TicketEvents.Claimed"/>: the Claude session id.</summary>
    [JsonPropertyName("session")] public string? Session { get; init; }
    /// <summary>The ticket database change-feed sequence number, null when the Orchestrator published it as it acted.</summary>
    [JsonPropertyName("seq")] public long? Seq { get; init; }
    /// <summary>When it happened (UTC).</summary>
    [JsonPropertyName("atUtc")] public DateTime? AtUtc { get; init; }
    /// <summary>"tickets-db" (the daemon read it from the change feed) or "orchestrator" (published as it acted).</summary>
    [JsonPropertyName("source")] public string Source { get; init; } = "";
}

/// <summary>The <see cref="AppBus.OpenTicket"/> payload.</summary>
public sealed record OpenTicketRequest
{
    /// <summary>The ticket to open.</summary>
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
    /// <summary>The ticket whose worker to show.</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>The login email that works it, if the asker knows.</summary>
    [JsonPropertyName("login")] public string? Login { get; init; }
    /// <summary>The CLI session id that works it, if the asker knows.</summary>
    [JsonPropertyName("session")] public string? Session { get; init; }
    /// <summary>The asking app (<see cref="AppBus.TicketsDesktop"/>).</summary>
    [JsonPropertyName("from")] public string? From { get; init; }
    /// <summary>The app meant to act on it (<see cref="AppBus.OrchestratorDesktop"/>); null for any that can.</summary>
    [JsonPropertyName("to")] public string? To { get; init; }
}

/// <summary>The <see cref="AppBus.ShowPreview"/> payload (v1, M.ORCH-T2133).</summary>
public sealed record ShowPreviewRequest
{
    /// <summary>The preview set's folder, a full path.</summary>
    [JsonPropertyName("set")] public string Set { get; init; } = "";
    /// <summary>The file in the set to show first, by its name in the folder; null for the viewer's choice.</summary>
    [JsonPropertyName("select")] public string? Select { get; init; }
    /// <summary>The ticket the set belongs to, when known.</summary>
    [JsonPropertyName("ticket")] public string? Ticket { get; init; }
    /// <summary>The set's title, when known.</summary>
    [JsonPropertyName("title")] public string? Title { get; init; }
    /// <summary>The asking app or agent (<c>capture-agent</c>, ...).</summary>
    [JsonPropertyName("from")] public string? From { get; init; }
    /// <summary>The app meant to act on it (<see cref="AppBus.MediaViewer"/>); null for any that can.</summary>
    [JsonPropertyName("to")] public string? To { get; init; }
}

/// <summary>The <see cref="AppBus.AgentState"/> payload (v1, M.ORCH-T1996): which CLI session's badge state changed, and a hint of
/// to what. The dashboard re-reads that one session on it (M.ORCH-T1997); the hint is only what it falls back on.</summary>
public sealed record AgentStateNotice
{
    /// <summary>The CLI's session id.</summary>
    [JsonPropertyName("session")] public string Session { get; init; } = "";
    /// <summary>The hook event that sent it (UserPromptSubmit, Stop, SubagentStart, ...).</summary>
    [JsonPropertyName("event")] public string? Event { get; init; }
    /// <summary>A hint: "working", "idle", "asking" or "".</summary>
    [JsonPropertyName("state")] public string? State { get; init; }
    /// <summary>The subagent's id, on a subagent's own event.</summary>
    [JsonPropertyName("agent")] public string? Agent { get; init; }
    /// <summary>The CLI's running subagents, on a subagent event; null otherwise.</summary>
    [JsonPropertyName("subagents")] public int? Subagents { get; init; }
    /// <summary>The CLI's claude.exe process id, when known.</summary>
    [JsonPropertyName("cliPid")] public int? CliPid { get; init; }
}

/// <summary>The <see cref="AppBus.Presence"/> payload.</summary>
public sealed record AppPresence
{
    /// <summary>The app announcing itself (one of the <see cref="AppBus"/> app names).</summary>
    [JsonPropertyName("app")] public string App { get; init; } = "";
    /// <summary>Its process id.</summary>
    [JsonPropertyName("pid")] public int Pid { get; init; }
    /// <summary>The request kinds it acts on (<see cref="AppBus.OpenTicket"/>, <see cref="AppBus.ShowWorker"/>).</summary>
    [JsonPropertyName("handles")] public IReadOnlyList<string> Handles { get; init; } = [];
    /// <summary>When it announced itself (UTC).</summary>
    [JsonPropertyName("atUtc")] public DateTime AtUtc { get; init; }
}
