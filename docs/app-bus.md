# The app bus (M.ORCH-T879)

Orchestrator.Desktop (the dashboard) and Tickets.Desktop are separate apps that share one local message bus. The
Orchestrator daemon is the hub. The bus is the status bus of M.ORCH-T854 and M.ORCH-T872: two named pipes, each carrying
one JSON envelope per line (NDJSON, UTF-8, no BOM). Both pipes accept only the current Windows user.

| Pipe | Direction | Use |
|---|---|---|
| `mindattic-orchestrator-status-<user>` | daemon to subscribers | Connect, then read lines until the pipe closes. Any number of apps may subscribe at once. |
| `mindattic-orchestrator-status-in-<user>` | writers to daemon (the inbox) | Connect, write one or more lines, then disconnect. |

`<user>` is `Environment.UserName` with every character that isn't a letter or digit replaced by `-`.

Every line is an envelope:

```json
{"kind":"open-ticket","v":1,"at":"2026-10-25T12:00:00Z","pid":1234,"payload":{"id":"M.ORCH-T879","from":"orchestrator-desktop","to":"tickets-desktop"}}
```

A subscriber skips any kind it doesn't read, and any `v` higher than the version it reads. A field added to a payload
keeps its version; a breaking change bumps it. Payload property names are read case-insensitively.

The contract is `src/MindAttic.Orchestrator.Core/Status/AppBus.cs`. It depends only on System.Text.Json, so Tickets
can carry a copy of it.

## Behaviour on connect and reconnect

- When an app connects, the daemon first sends the full status state (`state`), then the messages it keeps: the last
  snapshot's figures, every app's latest `app-present`, and the latest `members`, `orgs`, `settings`, `pause` and
  `reminders` of its own store. After that it sends each message as it happens. A heartbeat
  arrives every 20 seconds. If the stream is silent for 65 seconds, treat it as dead and reconnect.
- When the daemon is down, the pipe doesn't exist. Keep working without it and retry the connection with a backoff
  (1 second, doubling up to 10 seconds). Send `app-present` again after every connect, because the daemon forgets
  presences when it restarts.
- When the stream drops, forget every presence you were holding. The next connection sends them all again.
- Writing to the inbox is best effort. If the connect fails (no daemon, or every listener busy), give up immediately
  (use about a 100 ms timeout) and use the fallback described for that kind.

## Message kinds

### `ticket` (v1): a ticket changed

The daemon publishes these from the ticket database's change feed (`TicketsDb.Activity`, polled every 2 seconds). A
change made by the tickets CLI, Tickets.Desktop or the Orchestrator therefore reaches every app, and neither writer has
to publish it. The Orchestrator also publishes `claimed` and `released` at the moment it claims or releases a ticket
(`orchestrator next --claim`, allocate, Start/Release, reaping). The daemon then drops the feed's copy of that change.

| Field | Meaning |
|---|---|
| `id` | Ticket id, e.g. `M.ORCH-T879` |
| `event` | `filed`, `approved`, `unapproved`, `rejected`, `claimed`, `released`, `closed`, `wont-do`, `reopened`, `reworded`, `assigned`, `prioritized`, `linked`, or `changed` (any other field) |
| `repo`, `summary` | The ticket's repo and summary. Feed events only. |
| `field`, `old`, `new` | The database field that changed and its values. Feed events only. |
| `login`, `session` | Who works the ticket. Set for `claimed`. |
| `seq` | The feed's event number. Null when the Orchestrator published the event itself. |
| `atUtc` | When the change happened. |
| `source` | `tickets-db` or `orchestrator` |

Subscribers should refresh whatever shows that ticket. The dashboard re-reads its Queue tabs (`ReloadTicketQueues`).
While its stream is up it no longer polls them every minute.

### `app-present` (v1): an app is listening

`{"app":"tickets-desktop","pid":4321,"handles":["open-ticket"],"atUtc":"..."}`

Each app sends this to the inbox on every connect. The daemon keeps the latest one per app and passes them to every
subscriber that connects. A presence counts only while its `pid` is alive. Before sending a request, check that the
target app is present and that its `handles` list includes the request kind. If it isn't, use the request's fallback.

- The dashboard sends `{"app":"orchestrator-desktop","handles":["show-worker"]}`.
- Tickets.Desktop should send `{"app":"tickets-desktop","handles":["open-ticket"]}`.

### `open-ticket` (v1): the Orchestrator asks Tickets.Desktop to show a ticket

`{"id":"M.ORCH-T879","from":"orchestrator-desktop","to":"tickets-desktop"}`

The dashboard sends this when a ticket id or title is clicked, including in the Queue's Active, Pending, Closed and
Won't do tabs. It sends it only while Tickets.Desktop is present and handles `open-ticket`. Before sending, it calls
`AllowSetForegroundWindow(pid)` for Tickets.Desktop's pid, so Tickets.Desktop may bring its own window forward.
Fallback: when Tickets.Desktop isn't present, or the send fails, the dashboard starts
`MindAttic.Tickets.Desktop.exe --root <workspace> --ticket <ID>`. That starts it if needed, and its single-instance
handoff passes the id to a copy that's already running.

### `show-worker` (v1): Tickets.Desktop asks the Orchestrator to show who is working a ticket

`{"id":"M.ORCH-T879","login":"someone@example.com","session":"<claude session id>","from":"tickets-desktop","to":"orchestrator-desktop"}`

`login` and `session` are optional. Send what the ticket's `ticket_work` row says (`TicketsDb.Work(id)`). When both
are missing, the dashboard uses its own claim on the ticket. The dashboard first looks for the CLI tab running that
session and shows it. Failing that, it selects the person whose login works the ticket. Either way it comes forward.
If no one is working the ticket, its status line says so. Send this only while `orchestrator-desktop` is present and
handles `show-worker`. Call `AllowSetForegroundWindow(<its pid>)` before sending. Fallback: start
`MindAttic.Orchestrator.Desktop.exe`, which only brings the dashboard forward.

## The Orchestrator's own state (M.ORCH-T891)

Five kinds carry what the dashboard used to learn only by rereading the store: `members`, `orgs`, `settings`, `pause`
and `reminders`, each at v1. The contract is `src/MindAttic.Orchestrator.Core/Status/StateBus.cs`.

Each message is the whole of its kind, read back from the store right after the change, so the latest one is the
truth and one missed loses nothing. Numbers and times are carried at full precision. Whoever makes the change
publishes it as the write commits (`StoreChanges` in the store layer says each write):

- the dashboard (Settings, the theme switch, the pause button), through the inbox, off its UI thread;
- the `orchestrator` CLI (`members add|set|remove`, `org members add|set|remove`, `orgs add|rename|remove|member|unmember|repo`,
  `settings`, `budget`, `remind`, `remind --cancel`), through the inbox before the command exits;
- the daemon (a reminder sent or pruned, a member's name and role synced from claude.ai), straight to its hub.

A process publishes only once it turns publishing on (`StateBus.Enable`): the CLI, the dashboard and the daemon do, a
test never does. The daemon also publishes all five as they stand when it starts, so a dashboard connecting gets them
whatever changed while no daemon ran. It keeps the latest of each kind for its own store only, and a change to its own
store brings its next plan forward.

Every payload has these fields:

| Field | Meaning |
|---|---|
| `store` | The full path of the orchestrator database it was read from. Apply only messages whose `store` is your own (compared ignoring case): a scratch or another workspace's store says nothing about yours. |
| `atUtc` | When it was read back. |

The rest of each payload:

| Kind | Fields |
|---|---|
| `members` | `members`: every member of the organization, by email (`Email`, `Name`, `Role`, `Tier`, `Status`, `JoinedUtc`, `UpdatedUtc`). |
| `orgs` | `orgs`: every org, the default one first (`Id`, `Name`, `Position`); `members`: every login placed in one (`OrgId`, `Person`, `Login`, `Provider`). |
| `settings` | `settings`: the whole of the orchestrator's settings, as `OrchestratorSettings` writes them. |
| `pause` | `paused`, `sinceUtc`, `by`: the global pause. |
| `reminders` | `pending`: every reminder not sent yet, soonest first (`Id`, `Text`, `DueUtc`, `To`, `CreatedUtc`, `Failures`, `LastError`). |

Nested records keep their own property names; every payload is read case-insensitively. The dashboard lays each over
what it shows at once: the roster and the orgs re-split the person tabs, the pause and the reminders go straight to
every tab, and settings reload an open Settings panel that has no edits. With these on the stream, the dashboard no
longer polls while its stream is up and its daemon builds the snapshots. It polls only while disconnected (every 5s
shown, 60s hidden), or while it holds the status lease itself (every 5s shown, 15s hidden).

## What M.TIX-T158 implements in Tickets.Desktop

1. Subscribe to the status pipe, reconnecting as described under "Behaviour on connect and reconnect", and read only
   `ticket`, `app-present` and `open-ticket`. Never let a bus failure reach the UI: contain and log it.
2. After each connect, write `app-present` with `{"app":"tickets-desktop","pid":<own pid>,"handles":["open-ticket"]}`
   to the inbox.
3. On `ticket`: refresh the list and detail views that show that ticket's id. Coalesce bursts of events.
4. On `open-ticket` where `to` is `tickets-desktop` or empty: select the ticket, as `--ticket` does, and bring the
   window forward.
5. Add a "Show who is working it" action to a ticket that is being worked. It sends `show-worker` to the inbox, with
   `login` and `session` taken from `ticket_work`, and starts the dashboard when it isn't present.
6. Tickets.Desktop and the tickets CLI don't need to publish ticket changes. The daemon's change feed carries every
   write to the database.
