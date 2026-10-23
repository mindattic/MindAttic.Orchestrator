# Data stores: SQLite or SQL Server

The owner's rule (2026-10-22): SQLite is for simple needs; corporate-grade data gets its own
database on the full SQL Server instance on this machine (`MSSQLSERVER`, `localhost`). Video
games keep SQLite.

## The reference pattern

MindAttic.Orchestrator (M.ORCH-T658) and MindAttic.Tickets (M.TIX-T131) were moved this way. A
migration ticket for another repo asks for the same shape:

- a dedicated database named after the app on the full instance, as the store of record, chosen
  by a connection string (a config file or an environment variable, `off` meaning SQLite);
- SQLite kept fully working as a selectable store, and as a local copy wherever a hook or another
  process reads the file directly;
- a `db migrate` command that copies every table across and checks each row count, a `db` status
  command, and a `db back` command that copies it all back to SQLite;
- the tests run against both stores, SQL Server when an instance is reachable.

## The audit (M.ORCH-T657, 2026-10-23)

Sizes are the live files on this machine on 2026-10-23.

| Repo | What it stores | How big | Who reads it | Decision |
|---|---|---|---|---|
| Automata | collections, tasks, datasets and their rows, run history and run events, the schedule, parked runs, settings, in `%LocalAppData%\MindAttic\Automata\automata.db` (EF Core) | 0.34 MB: 19 tasks, 49 dataset rows, 603 run events | the desktop app, `automata-runner` on the same machine, and `tools/automata-db.mjs` (read-only, for the test harness) | **Stays on SQLite.** One user's desktop workspace that needs nothing installed, backed up as one `*.automata.json`. Its operational logging goes through MindAttic.Log (M.LOGS-T17). |
| JobHunt | the user's profile (work history, skills, projects), saved searches, jobs, applications and their events, settings, in `%LocalAppData%\MindAttic\JobHunt\jobhunt.db` (EF Core); API keys are in Vault, not the database | 0.23 MB: 2 profiles, 81 skills, 0 jobs or applications yet | the desktop app only | **Stays on SQLite.** A personal desktop app made to be shared ("nothing to install"), holding one person's job search. A required SQL Server would break that. |
| MindAttic.Log | every app's operational log, one SQLite file per app under `%APPDATA%\MindAttic\Logging\<app>\MindAttic.Log.db` | from 0.03 MB (most apps) to 157 MB (Prose.Hub) and 1.76 GB (ProcessLogger) | each app writes its own; MindAttic.Log.Reader reads them all | **Already ticketed: M.LOGS-T17** (pluggable log stores with a SQL Server store chosen per app, the Reader finding logs in every store, SQLite kept as the default and a migrator). No second ticket. |
| ProcessLogger | every process start and stop (ETW and WMI, reconciled) with memory peaks, and every foreground change, in `%LocalAppData%\MindAttic\ProcessLogger\processlogger.db` (EF Core, WAL) | 65 MB: 216,378 process and 1,019 focus rows over the days it ran from 2026-09-25 to 2026-10-20; it grows without a limit while the logger runs | the elevated logger writes; the viewer on the same machine reads | **Stays on SQLite.** A local diagnostic for one machine whose promise is "one SQLite file under your own profile, nothing sent anywhere"; one writer and one reader on WAL is what SQLite does well. Its operational log is a bug, not a store question: **PL-T28** (EF Core's per-command logging and per-process lines filled 1.76 GB in 19 hours). |
| TaxRateCollector | US sales and excise tax rates with their hashed .gov source documents, the jurisdiction hierarchy, Census and ZIP data | 1.6 GB | the Blazor app and the Worker | **Already SQL Server, no SQLite path** (TRC-LAW-8; SQLite in-memory is only a test double). But its development database is on LocalDB (`(localdb)\MSSQLLocalDB`), not the full instance: **TRC-T22** moves it to its own `TaxRateCollector` database there, keeping LocalDB selectable by connection string. |

Found on the way: the full instance holds only `MindAttic.Orchestrator` and `MindAttic.Tickets`
today. Prose (17 GB), IdiotProof, StreetSamurai and the other web apps' databases live in LocalDB,
so "beside Prose" in the README means the same machine, not the same instance.
