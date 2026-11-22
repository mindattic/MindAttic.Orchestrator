# Health checks: probe contract and reuse choices (M.ORCH-T2019)

Part of the health dashboard epic (M.ORCH-T1935) and its contract story (M.ORCH-T2013). This is the design the probes
(M.ORCH-T2014 machine, M.ORCH-T2015 Azure SQL, M.ORCH-T2016 links), the dashboard screen (M.ORCH-T2017) and the
schedule/alerts work (M.ORCH-T2018) build on. The next tasks implement it: `HealthReport` types (M.ORCH-T2020) and the
`health` kind on the app bus (M.ORCH-T2021).

What already exists and is kept: `Core/Health/HealthThresholds.cs` (M.ORCH-T2035) holds each check's degraded and
unhealthy lines as settings (`orchestrator settings --health-*`, Vault.Desktop's Health group) and grades a reading as
`HealthGrade.Healthy`, `Degraded` or `Unhealthy`. Every probe grades through it; nothing below adds a second set of
thresholds.

## 1. What was looked at

| Source | What it is | Taken | Left |
|---|---|---|---|
| IETF draft *Health Check Response Format for HTTP APIs* (draft-inadarei-api-health-check, last revision -06, expired, never an RFC) | A JSON body, media type `application/health+json`: top-level `status` (`pass`/`warn`/`fail`), `version`, `releaseId`, `serviceId`, `description`, `notes`, `output`, `links`, and `checks`, an object keyed `"<componentName>:<measurementName>"` whose value is an **array** of results, each with `componentId`, `componentType` (`component`/`datastore`/`system`), `observedValue`, `observedUnit`, `status`, `time`, `output`, `links`. HTTP 200 for pass/warn, 503 for fail. | The JSON shape and field names, as the wire format of a report (section 3). The `name:measurement` key with one array element per instance (each drive, each database, each link). `pass`/`warn`/`fail` as the wire status strings. | HTTP itself: probes talk over the app bus, not HTTP. `version`/`releaseId`/`affectedEndpoints`. |
| ASP.NET Core health checks (`Microsoft.Extensions.Diagnostics.HealthChecks`): `IHealthCheck.CheckHealthAsync`, `HealthCheckResult` (status, description, exception, data), `HealthReport` (entries, total duration, status = worst entry), `IHealthCheckPublisher` (delay 5 s, period 30 s, timeout 30 s by default), tags for liveness/readiness | The .NET convention. Status enum `Unhealthy`/`Degraded`/`Healthy`. | The three grades and their meaning (already `HealthGrade`). Overall status = worst check. Per-check duration and exception text. The publisher model: each probe runs on its own period with a timeout and pushes results, nobody polls it. | The package and DI host. Core's bus contract depends only on System.Text.Json so Tickets can copy it (docs/app-bus.md); `HealthReport` stays our own record with the same names, and a probe that wants a Xabaril check can wrap it (below). |
| AspNetCore.Diagnostics.HealthChecks (Xabaril): ready checks (`AspNetCore.HealthChecks.SqlServer`, Azure Storage, Redis, ...), HealthChecks UI with stored history and webhooks | The biggest catalogue of ready checks and a ready dashboard. | Its SqlServer check's approach (open a connection, run `SELECT 1`, time it) for the link check of each Azure SQL database. The UI's ideas: per-check history timeline, webhook on failure *and* on recovery. | The UI itself: it is a web app needing an ASP.NET host and its own storage; the dashboard is WPF (and phone access is RDP, M.ORCH-T1542). The packages, for now: one `SELECT 1` is a few lines and the Azure probe's load figures come from Azure, not from a connection. |
| Kubernetes probes (liveness, readiness, startup) | Restart vs. stop-routing semantics, `failureThreshold`, `periodSeconds`. | `failureThreshold`: a check must read unhealthy N times in a row before it alerts (section 5). | Restart semantics: nothing here restarts on a failed check. |
| OpenTelemetry .NET / `System.Diagnostics.Metrics` | Metrics API (`Meter`, `ObservableGauge`), OTLP export to a collector, semantic conventions (`system.memory.usage`, `system.paging.usage`, `system.filesystem.usage`, `db.client.*`). | Measurement names follow the conventions (memory, paging, filesystem); units stay plain (`GB`, `%`, `ms`) rather than UCUM. Probes may also expose the same readings as `Meter` gauges later, so `dotnet-counters monitor` works on them for free. | A collector, OTLP, traces. No backend to send them to and nothing on this machine would read them; revisit if Grafana ever lands. |
| Prometheus exposition format and Alertmanager | Text `name{labels} value` per line; gauges; `up`; alert rules with a `for:` duration; notify on resolve. | `for:` as "unhealthy for N runs" (same as Kubernetes `failureThreshold`). Notify on resolve. One sample per (check, instance) like a label set. | The text format and a scrape endpoint: no Prometheus server. A `/metrics` writer could be added on top of `HealthReport` in an afternoon if one ever is. |
| Uptime Kuma | Self-hosted monitor: per-monitor interval, retries before DOWN, heartbeat bar of the last N results, notification on state *change* only, PENDING while retrying, MAINTENANCE. | The heartbeat bar as the dashboard's history view (a strip of coloured cells per check). Notify on change only. A "stale"/pending state when a probe stops reporting (section 4). | Running it: Node.js web app, polls HTTP/TCP/ping; it can't read commit charge or Azure DTU. |
| Netdata | Per-second agent with health alarms: `WARNING`/`CRITICAL`/`CLEAR`, hysteresis (`warn: $this > (($status >= $WARNING) ? 75 : 80)`), `delay: up/down`. | Hysteresis as the cure for flapping, as an option (section 5). | Running it: heavy, Linux-first, its own web UI. |
| Glances, Grafana | Dashboards. | Nothing beyond what's above. | Both need a host and a data source we don't run. |
| Nagios plugin convention | Exit code 0 OK, 1 WARNING, 2 CRITICAL, 3 UNKNOWN, one summary line on stdout. | Probe CLIs exit with it (section 6), so any scheduler, script or Psst `--when` command can use a probe without the bus. | Perfdata syntax. |

**Decision.** Reuse the *conventions* (health+json shape, ASP.NET grades and publisher model, Kubernetes/Prometheus
"for N runs", Uptime Kuma history and change-only alerts, Nagios exit codes) and build the small parts ourselves on the
app bus. Run no third-party monitoring server: each needs a web host and a store, can't read most of what we check
(commit charge, Azure DTU, our own named pipes), and the dashboard already exists in WPF. Revisit only if a check
needs something a package does far better than a few lines (then wrap it as a probe, not host it).

## 2. Grades

One grade type, `HealthGrade` (Core/Health/HealthThresholds.cs). The older `Activity.HealthLevel` maps onto it so
MachineHealth's existing samples can be reported without rewriting their alerts.

| `HealthGrade` | Wire `status` (health+json) | ASP.NET `HealthStatus` | `HealthLevel` (MachineHealth) | Nagios exit | Dashboard |
|---|---|---|---|---|---|
| `Healthy` | `pass` | `Healthy` | `Ok` | 0 | green |
| `Degraded` | `warn` | `Degraded` | `Warn` | 1 | amber |
| `Unhealthy` | `fail` | `Unhealthy` | `Danger` | 2 | red |
| (no fresh report) | not sent; the hub derives it | n/a | n/a | 3 (probe couldn't run) | grey, "stale" |

Readers also accept `ok`/`up` as `pass` and `error`/`down` as `fail`, as the draft allows.

A probe that can't take a reading (WMI error, `az` not signed in, pipe refused) does not report the check healthy (the
NaN case `HealthLine.Grade` leaves to the probe): it reports `warn` with `observedValue: null` and the reason in
`output`. Not being able to see a thing is worth a look, not a page; a link that *is* down is a reading (`fail`), not
a missing one.

## 3. The report (wire format)

One report is what one probe run produced. It is health+json-shaped so it reads in any tool that knows the draft, with
extension fields the draft permits (`probe`, `host`, `durationMs`, `intervalSeconds`, `degradedAt`, `unhealthyAt`).

```json
{
  "status": "warn",
  "serviceId": "machine",
  "description": "RAM, commit/pagefile and every disk",
  "probe": "machine",
  "host": "RYAN-PC",
  "time": "2026-11-22T20:40:00Z",
  "durationMs": 182,
  "intervalSeconds": 30,
  "checks": {
    "memory:free": [
      { "componentType": "system", "observedValue": 5.8, "observedUnit": "GB", "status": "pass",
        "degradedAt": 4, "unhealthyAt": 2, "time": "2026-11-22T20:40:00Z" }
    ],
    "memory:commit": [
      { "componentType": "system", "observedValue": 86.0, "observedUnit": "%", "status": "warn",
        "degradedAt": 80, "unhealthyAt": 90, "time": "2026-11-22T20:40:00Z", "output": "lghub_agent 3.1 GB, GTFO 2.4 GB" }
    ],
    "disk:free": [
      { "componentId": "C:", "componentType": "system", "observedValue": 41.2, "observedUnit": "GB", "status": "pass",
        "degradedAt": 20, "unhealthyAt": 5, "time": "2026-11-22T20:40:00Z" },
      { "componentId": "R:", "componentType": "system", "observedValue": null, "observedUnit": "GB", "status": "warn",
        "time": "2026-11-22T20:40:00Z", "output": "drive not ready" }
    ]
  }
}
```

Rules:

- **Check id** = the `checks` key, `"<component>:<measurement>"`, lower-case, hyphenated. One element per instance;
  `componentId` names the instance (drive letter, `server/database`, `dashboard->daemon`) and is omitted when there is
  only ever one. A single check is addressed as `<key>` + `componentId`, e.g. `disk:free` / `C:`.
- **status** at the top is the worst of every element (ASP.NET `HealthReport.Status`).
- **observedValue** is a number or null; **observedUnit** is one of `GB`, `%`, `ms`, `count`, or a word for an
  enumerated value (`state` with `observedValue` a string like `Paused`, as the draft allows).
- **degradedAt / unhealthyAt** are the `HealthLine` the value was graded against, copied in at grading time, so the
  dashboard and history show the line that applied *then*, even after a setting changes. Omitted for checks graded by
  rule rather than a line (a database `Paused` is `pass`; `Resuming` for longer than a run is `warn`; `Unavailable` is
  `fail`).
- **time** is when the value was observed (UTC, ISO 8601), per element; the top-level `time` is when the run ended.
- **output** is one human line, never a stack trace (that goes to the probe's log).
- Unknown fields are ignored; field names are read case-insensitively (as everywhere on the bus).

Initial check ids (the probe tickets may add more, and must list them here when they do):

| Probe | Check id | Instance (`componentId`) | Unit | Graded by |
|---|---|---|---|---|
| machine (T2014) | `memory:free` | | GB | `HealthCheck.RamFreeGb` |
| | `memory:commit` | | % | `HealthCheck.CommitPercent` |
| | `memory:handles` | | count | MachineHealth's existing handle lines (mapped from `HealthLevel`) |
| | `disk:free` | drive letter | GB | `HealthCheck.DiskFreeGb` |
| azure-sql (T2015) | `sqldb:state` | `server/database` | state | rule (above) |
| | `sqldb:dtu` | `server/database` | % | `HealthCheck.DtuPercent` |
| | `sqldb:storage` | `server/database` | % | `HealthCheck.StoragePercent` |
| links (T2016) | `link:latency` | `from->to` (`probe->daemon`: connect to the first line on the status stream; `probe->tickets-db` (`daemon->tickets-db` in the daemon): one timed open of the ticket database, `TicketDbPing`, M.ORCH-T2032; `dashboard->daemon`, `daemon->termhost:<pid>`, ...) | ms | `HealthCheck.LinkLatencyMs`; unreachable is `fail` |
| | `daemon:tick` | `daemon` | s | `DaemonTick.Line` (M.ORCH-T2032, Core/Health/DaemonTick.cs): the tick under way so far, or the last one when that was longer, from the volatile `daemon-tick` state row the daemon writes at each tick's start and end; `warn` at the daemon's slow-tick line (60 s), `fail` at the pace gates' stale line (180 s), the M.ORCH-T1922 stall; no row kept or its daemon gone is `warn` with no reading |
| | `link:up` | `daemon->dashboard`, `daemon->tickets-desktop`, `daemon->termhost:<pid>`, `daemon->cli:<pid>` | state | rule (M.ORCH-T2031, Core/Health/LinkProbe.cs): `up` (its process runs) `pass`; `gone` (still held by the daemon, process not running) `warn`; `absent` (an app never heard) `pass` |

## 4. Transport: the `health` kind on the app bus

Probes publish over the existing status bus (docs/app-bus.md), not a new channel: they write to the inbox pipe, the
daemon (the hub) fans out to every subscriber. Envelope kind `health`, version 1, payload = the report above.

```json
{"kind":"health","v":1,"at":"2026-11-22T20:40:00Z","pid":5120,"payload":{ ...report... }}
```

- `AppBusClient.PublishHealth(report)` (M.ORCH-T2021) writes it, best effort with the inbox's ~100 ms connect timeout.
  A probe that can't reach the daemon still prints its report and exits with its code (section 6); nothing is lost but
  that run's history point.
- The hub keeps, per check instance (`probe` + check id + `componentId`), the **latest** element and a **bounded
  history** (a ring of the last 360 points: three hours at the 30 s default `HealthSampleSeconds`). On connect a
  subscriber gets the latest report of every probe after `state`, like the other kept kinds; history is requested, not
  replayed on every connect (a later `health-history` request kind, M.ORCH-T2017's call).
- **Stale.** The hub marks a check stale when its last report is older than three of its probe's periods (the probe
  sends its period in the report as `intervalSeconds`). Stale shows grey, and a stale check does not alert as
  unhealthy; a probe that has stopped is itself reported by the links probe.
- Probes read each other through the same stream (the shape the epic asks for): e.g. the links probe can mark
  `daemon->tickets-db` degraded while `sqldb:dtu` for the tickets database is unhealthy, by subscribing like the
  dashboard does. No probe calls another directly.

## 5. Alerting, flapping and thresholds

- **Change-only.** Alerts fire on a transition, never on every run (Uptime Kuma, Alertmanager).
- **For N runs.** A check alerts unhealthy only after `failureThreshold` consecutive unhealthy reports (default 2), and
  recovers after `successThreshold` consecutive non-unhealthy ones (default 1): Kubernetes' names, Prometheus' `for:`.
  Both are settings (M.ORCH-T2018): `HealthFailureThreshold` and `HealthSuccessThreshold` (Settings modal, Health;
  `orchestrator settings --health-failure-threshold/--health-success-threshold`), and `HealthAlerts` turns the Psst off.
  Implemented by `Health/HealthAlerts.cs` (M.ORCH-T2039): the daemon grades each report as the hub keeps it, one alert
  per transition, the alerted set kept in the database so a restart neither repeats an alert nor loses a recovery.
- **Hysteresis**, optional per check: once degraded, a reading stays degraded until it is back past the line by a margin
  (Netdata's `$status >= $WARNING ? 75 : 80`). Off by default; turn it on for a check that is seen to flap.
- **Where.** An unhealthy transition sends a Psst (Reminders/PsstAlerts.cs, PsstClient.cs) with the check, value, line
  and the element's `output`; a recovery sends one line. Degraded never alerts; it shows on the dashboard only.
- **Thresholds** stay in `HealthThresholds`/`OrchestratorSettings`; the report carries the line used. Per the standing
  rule that numbers are configurable and self-correcting, a later task may let a line drift within user-set bounds from
  its alert history (e.g. a degraded line that fires daily with no unhealthy ever following is raised); that changes the
  setting, never the contract.

## 6. Probe CLIs

Each probe is its own process (the epic's "separate CLIs that each own one concern"), started by the daemon's schedule
(M.ORCH-T2018) or by hand:

```
orchestrator probe machine   [--once] [--json] [--interval <s>]
orchestrator probe azure-sql [--once] [--json] [--interval <s>]
orchestrator probe links     [--once] [--json] [--interval <s>]
```

- `--once` runs one check, publishes it, prints a one-line summary (`WARN memory:commit 86% (>= 80)`), and exits with
  the Nagios code of the top-level status (0/1/2, 3 if the probe itself failed). Without it the probe loops on its
  interval.
- `--json` prints the report (health+json shape) instead of the summary line, so a probe is usable from a script,
  Psst's `--when` or Task Scheduler with no daemon at all.
- Each run has a timeout (default its interval, as ASP.NET's publisher does); a check that times out is reported `warn`
  with `output: "timed out after N s"`, the others in the run still report.
- The daemon's schedule (M.ORCH-T2037, Core/Daemon.Health.cs): the daemon holding the `health-probes.lock` lease starts
  `probe machine`, `probe links` and `probe azure` with `--once --interval <its interval>` as hidden processes, each on
  its own interval (`--health-probe-machine-seconds` 30, `--health-probe-links-seconds` 30,
  `--health-probe-azure-seconds` 300; 0 turns one off), never two runs of one probe at once, and kills a run still going
  60 s past its timeout. It logs a probe that couldn't run (exit 3 with its stderr, another code, no start, killed)
  once per new failure and again when it runs again; a warn or critical exit is the probe's verdict, left to its report.
- Probes only read and report. They never reclaim memory, kill processes or resume databases; MemoryGuard, DiskGuard,
  MemoryReclaim, the reaper and `/kickstart` keep doing that on their own rules, and may later *read* health reports.

## 7. Types (for M.ORCH-T2020)

`Core/Health/HealthReport.cs`, System.Text.Json only (copyable, like AppBus.cs):

- `HealthResult` (one element): `CheckId`, `ComponentId?`, `ComponentType`, `ObservedValue` (double? or string for
  `state`), `ObservedUnit`, `Grade` (`HealthGrade`), `DegradedAt?`, `UnhealthyAt?`, `Time`, `Output?`.
- `HealthReport`: `Probe`, `Host`, `Description?`, `Time`, `DurationMs`, `IntervalSeconds`, `Results`; `Status` is
  computed as the worst `Grade`. `ToJson()`/`FromJson()` write and read the section 3 shape (results grouped under
  `checks` by `CheckId`), with `HealthGrade` converted to and from `pass`/`warn`/`fail` (aliases accepted on read).
- `HealthResult.Graded(HealthCheck check, HealthThresholds t, double value, ...)` builds a result from a reading and
  stamps the line it used; `HealthGrades.From(HealthLevel)` maps MachineHealth's levels.
