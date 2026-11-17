using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Vault.Launch;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's app-level settings in MindAttic Vault (LAMP, M.ORCH-T1308), edited only in MindAttic.Vault.Desktop at the
/// Orchestrator tab: <c>%LOCALAPPDATA%\MindAttic\Orchestrator\settings.json</c>, per machine (Local), as every one of them is
/// about this machine's daemon, its CLIs, its folders, its database and its gateway's address and port.
/// <para><see cref="JsonStore"/> overlays these on the database's settings row whenever the settings are read, and writes them
/// back here whenever the orchestrator itself changes one (the CLI's <c>orchestrator settings</c>, <c>/allocate</c>'s master
/// switch, the dashboard's sun/moon switch), so every consumer of <see cref="OrchestratorSettings"/> reads the Vault's value. The
/// row keeps being written as before, as the hooks read it through stdlib sqlite3.</para>
/// <para>What stays in the database: the legacy queue maps (reservations, blocks, approvals, priorities, assignees), the API
/// credit ledger, the per-repo and per-ticket maps the CLI edits (<c>--repo-cap</c>, <c>--repo-sessions</c>, <c>--model</c>,
/// <c>orgs repo</c>) and the gateway's teammate list (whose tokens are sealed secrets). None is a secret here: the only key the
/// orchestrator uses is Claude's, from the shared named keys (<see cref="Billing.OrchestratorKeys"/>).</para>
/// </summary>
[SettingsApp(AppId, "MindAttic.Orchestrator", Local = true, Icon = "dashboard",
    Description = "Paces, schedules and hosts the workspace's Claude CLIs: budgets, pacing, allocate, logins, alerts and the team gateway.")]
[UsesCredential("claude", Purpose = "The API billing mode and the API key person: the Claude key CLIs bill when they run on API credits")]
public sealed class OrchestratorVaultSettings
{
    /// <summary>The Vault app id: the folder under <c>%LOCALAPPDATA%\MindAttic\</c> and the <c>MindAttic:Settings:Orchestrator</c> section.</summary>
    public const string AppId = "Orchestrator";

    // ── Budget ─────────────────────────────────────────────────────────────

    /// <summary>Uncached tokens (input + cache writes + output).</summary>
    [Setting("Tokens per 5-hour block", Group = "Budget", Order = 1,
        Help = "Uncached tokens (input + cache writes + output). Empty uses Claude's own reported limit, else the limits learned from the times you hit the wall.")]
    [SettingRange(1, 1e15)]
    public long? BlockBudget { get; set; }

    /// <summary>Uncached tokens per week.</summary>
    [Setting("Tokens per week", Group = "Budget", Order = 2, Help = "Uncached tokens per week. Empty means no weekly line.")]
    [SettingRange(1, 1e15)]
    public long? WeeklyBudget { get; set; }

    /// <summary>When Claude's weekly limit restarts, e.g. "Monday 09:00".</summary>
    [Setting("Week resets (local time)", Group = "Budget", Order = 3,
        Help = "When Claude's weekly limit restarts, e.g. \"Monday 09:00\". Claude's own reported reset comes first; empty uses the reset your last weekly limit hit reported, or a rolling 7 days.")]
    public string? WeeklyReset { get; set; }

    /// <summary>Percent of a budget at which the dashboard warns.</summary>
    [Setting("Warn at (%)", Group = "Budget", Order = 4, Help = "Percent of a budget at which the dashboard warns. Without pacing, the scheduler also stops proposing here.")]
    [SettingRange(1, 100)]
    public int WarnPercent { get; set; } = 80;

    /// <summary>The share of each account's week kept back for your own questions: every CLI on an account stops at 100 minus this of its week (10 stops at 90%).</summary>
    [Setting("Weekly reserve (%)", Group = "Budget", Order = 5,
        Help = "The share of each account's week kept back for your own questions: every CLI on an account stops at 100 minus this of its week (10 stops at 90%).")]
    [SettingRange(0, OrchestratorSettings.MaxWeeklyReservePercent)]
    public int WeeklyReservePercent { get; set; } = OrchestratorSettings.DefaultWeeklyReservePercent;

    /// <summary>Only with an Admin API key in the Vault keyring (provider "anthropic-admin"): the daemon reads Anthropic's cost report this often, and the API key's spend is Anthropic's own figures up to then instead of the list-price estimate.</summary>
    [Setting("API key: read Anthropic's bill every (minutes)", Group = "Budget", Order = 6,
        Help = "Only with an Admin API key in the Vault keyring (provider \"anthropic-admin\"): the daemon reads Anthropic's cost report this often, and the API key's spend is Anthropic's own figures up to then instead of the list-price estimate. 0 never reads it.")]
    [SettingRange(0, 1440)]
    public double ApiCostReadMinutes { get; set; } = OrchestratorSettings.DefaultApiCostReadMinutes;

    // ── Pacing ─────────────────────────────────────────────────────────────

    /// <summary>Caps new sessions so spend lands on the pace target as the 5-hour window resets, instead of hitting the wall.</summary>
    [Setting("Pace to the reset", Group = "Pacing", Order = 1,
        Help = "Caps new sessions so spend lands on the pace target as the 5-hour window resets, instead of hitting the wall. Off: new work holds at the warn line.")]
    public bool Pacing { get; set; } = true;

    /// <summary>How much of each 5-hour limit pacing aims to use by its reset.</summary>
    [Setting("Pace target (%)", Group = "Pacing", Order = 2, Help = "How much of each 5-hour limit pacing aims to use by its reset.")]
    [SettingRange(1, 100)]
    public int PaceTargetPercent { get; set; } = 96;

    /// <summary>A login's 5-hour or weekly use under which no pace guard is considered: no pace holds, no subagent allowance cap, no cool-off.</summary>
    [Setting("Pace guards start at (% used)", Group = "Pacing", Order = 2,
        Help = "A login's 5-hour or weekly use under which no pace guard is considered: no pace holds, no subagent allowance cap, no cool-off. Work runs full speed until either window reaches it.")]
    [SettingRange(0, 100)]
    public double PaceGuardStartPercent { get; set; } = OrchestratorSettings.DefaultPaceGuardStartPercent;

    /// <summary>Slash commands whose turns skip every pace gate (no hold, no subagent wait, no stop); their spend still counts.</summary>
    [Setting("Exempt from pacing", Group = "Pacing", Order = 3,
        Help = "Slash commands whose turns skip every pace gate (no hold, no subagent wait, no stop); their spend still counts. Empty exempts none.")]
    public List<string> ExemptCommands { get; set; } = [.. OrchestratorSettings.DefaultExemptCommands];

    /// <summary>Past this age, the local tokens spent since Claude's last 5-hour report are added back in.</summary>
    [Setting("Claude's report counts as fresh for (minutes)", Group = "Pacing", Order = 4,
        Help = "Past this age, the local tokens spent since Claude's last 5-hour report are added back in.")]
    [SettingRange(0, 1440)]
    public int ReportFreshMinutes { get; set; } = 2;

    /// <summary>For a provider other than Claude: its own reported 5-hour use at which dispatching to it holds.</summary>
    [Setting("Other providers hold at (%)", Group = "Pacing", Order = 5,
        Help = "For a provider other than Claude: its own reported 5-hour use at which dispatching to it holds. 0 means never.")]
    [SettingRange(0, 100)]
    public int ProviderCapPercent { get; set; } = 90;

    /// <summary>Weekly-aware pacing is locked off; kept for when it returns: the week's percent from which it would take the week in.</summary>
    [Setting("Weekly-aware take-in line (%)", Group = "Pacing", Order = 6,
        Help = "Weekly-aware pacing is locked off; kept for when it returns: the week's percent from which it would take the week in.")]
    [SettingRange(0, 100)]
    public int WeeklyAwareLinePercent { get; set; } = OrchestratorSettings.DefaultWeeklyAwareLinePercent;

    /// <summary>Pace profiles are locked to linear; kept for when they return: under 1 bends the curve up early, 1 is a straight line.</summary>
    [Setting("Front-loaded curve exponent", Group = "Pacing", Order = 7,
        Help = "Pace profiles are locked to linear; kept for when they return: under 1 bends the curve up early, 1 is a straight line.")]
    [SettingRange(0.1, 1)]
    public double PaceCurveK { get; set; } = 0.55;

    /// <summary>Agent throttling is locked off; kept for when it returns: the longest one tool call would be held.</summary>
    [Setting("Longest agent hold (seconds)", Group = "Pacing", Order = 8,
        Help = "Agent throttling is locked off; kept for when it returns: the longest one tool call would be held.")]
    [SettingRange(0, 3600)]
    public int MaxAgentDelaySeconds { get; set; } = 300;

    /// <summary>Last resort: when a login's live 5-hour trend stays past its pace line with new subagents already slowed, abort just enough of its ticket subagents to bend it back under.</summary>
    [Setting("Cool off by aborting subagents", Group = "Pacing", Order = 9,
        Help = "Last resort: when a login's live 5-hour trend stays past its pace line with new subagents already slowed, abort just enough of its ticket subagents to bend it back under.")]
    public bool AgentAbort { get; set; } = true;

    /// <summary>How long the new-start cool-off gets to bend the trend back under before the abort steps in.</summary>
    [Setting("Abort after (minutes past the line)", Group = "Pacing", Order = 10,
        Help = "How long the new-start cool-off gets to bend the trend back under before the abort steps in.")]
    [SettingRange(0, 60)]
    public int AgentAbortAfterMinutes { get; set; } = 3;

    // ── Scheduler ──────────────────────────────────────────────────────────

    /// <summary>The master switch for /allocate: off pauses it in every CLI where it was typed (their marks stay).</summary>
    [Setting("Allocate", Group = "Scheduler", Order = 1,
        Help = "The master switch for /allocate: off pauses it in every CLI where it was typed (their marks stay). /allocate on and off change it too.")]
    public bool Allocate { get; set; } = true;

    /// <summary>Sessions the scheduler fills up to (working + waiting).</summary>
    [Setting("Max concurrent sessions", Group = "Scheduler", Order = 2, Help = "Sessions the scheduler fills up to (working + waiting).")]
    [SettingRange(1, 1000)]
    public int MaxConcurrentSessions { get; set; } = 4;

    /// <summary>Sessions one repo may run at once (orchestrator settings --repo-sessions overrides it per repo).</summary>
    [Setting("Sessions per repo", Group = "Scheduler", Order = 3, Help = "Sessions one repo may run at once (orchestrator settings --repo-sessions overrides it per repo).")]
    [SettingRange(0, 1000)]
    public int MaxSessionsPerRepo { get; set; } = 2;

    /// <summary>The most subagents running at once across every CLI; under it the pace decides, live.</summary>
    [Setting("Max subagents", Group = "Scheduler", Order = 4,
        Help = "The most subagents running at once across every CLI; under it the pace decides, live. 0 holds every new one.")]
    [SettingRange(0, 1000)]
    public int MaxSubagents { get; set; } = 16;

    /// <summary>Provider for dispatched tickets.</summary>
    [Setting("Provider for dispatched tickets", Group = "Scheduler", Order = 5)]
    [SettingChoices("Claude", "Codex", "Gemini", "Kimi")]
    public string DefaultProvider { get; set; } = "Claude";

    /// <summary>A CLI whose turn ends asking whether to go on while its repo still has open tickets is told to carry on.</summary>
    [Setting("Keep going", Group = "Scheduler", Order = 6,
        Help = "A CLI whose turn ends asking whether to go on while its repo still has open tickets is told to carry on. Off lets it stop and ask.")]
    public bool KeepGoing { get; set; } = true;

    /// <summary>Once the window a CLI stopped for has reset, it's told to carry on.</summary>
    [Setting("Resume after a reset", Group = "Scheduler", Order = 7,
        Help = "Once the window a CLI stopped for has reset, it's told to carry on. Off only alerts you that it's waiting.")]
    public bool ResumeAfterReset { get; set; } = true;

    /// <summary>Local-time windows when allocate pauses, like 22:00-07:00.</summary>
    [Setting("Quiet hours", Group = "Scheduler", Order = 8, Help = "Local-time windows when allocate pauses, like 22:00-07:00. Manual starts still work.")]
    public List<string> QuietHours { get; set; } = [];

    /// <summary>Repos /allocate never starts agents in, even when named or their CLI is open.</summary>
    [Setting("Repos allocate skips", Group = "Scheduler", Order = 9, Help = "Repos /allocate never starts agents in, even when named or their CLI is open.")]
    public List<string> AllocateSkip { get; set; } = [];

    /// <summary>Minutes since a repo's own CLI last ended a turn that it still counts as worked by hand.</summary>
    [Setting("Worked by hand for (minutes)", Group = "Scheduler", Order = 10,
        Help = "Minutes since a repo's own CLI last ended a turn that it still counts as worked by hand. 0 counts only a CLI that's mid-turn.")]
    [SettingRange(0, 1440)]
    public int AllocateHandMinutes { get; set; } = 15;

    /// <summary>Hours with no activity where a ticket was dispatched before it goes back in the queue.</summary>
    [Setting("Stale after (hours)", Group = "Scheduler", Order = 11,
        Help = "Hours with no activity where a ticket was dispatched before it goes back in the queue. 0 keeps it until the ticket closes.")]
    [SettingRange(0, 100000)]
    public int StaleAfterHours { get; set; } = 12;

    /// <summary>Context fill at which a session is wrapped up and cycled.</summary>
    [Setting("Cycle context at (%)", Group = "Scheduler", Order = 12, Help = "Context fill at which a session is wrapped up and cycled.")]
    [SettingRange(10, 95)]
    public int ContextCyclePercent { get; set; } = 80;

    /// <summary>How many times a ticket whose CLI or tab exited mid-task is started again before it's left for you.</summary>
    [Setting("Orphan redispatches", Group = "Scheduler", Order = 13,
        Help = "How many times a ticket whose CLI or tab exited mid-task is started again before it's left for you. 0 only shows it as orphaned.")]
    [SettingRange(0, 20)]
    public int OrphanRedispatches { get; set; } = 3;

    /// <summary>How long a dispatch's CLI or tab must be gone, with nothing working its ticket, before the ticket counts as orphaned.</summary>
    [Setting("Orphan quiet minutes", Group = "Scheduler", Order = 14,
        Help = "How long a dispatch's CLI or tab must be gone, with nothing working its ticket, before the ticket counts as orphaned.")]
    [SettingRange(1, 1440)]
    public int OrphanQuietMinutes { get; set; } = 10;

    /// <summary>Two agents editing the same file in a shared checkout.</summary>
    [Setting("Same-file edit claims", Group = "Scheduler", Order = 15,
        Help = "Two agents editing the same file in a shared checkout. Warn (the default) tells the second who else has it; Block also refuses a subagent's edit of a file another live agent claimed (a CLI's own main thread is only warned, so your edits never deadlock); Off records nothing.")]
    [SettingChoices("Off", "Warn", "Block")]
    public string EditClaims { get; set; } = OrchestratorSettings.DefaultEditClaims;

    /// <summary>Minutes a file's edit claim stands after its agent's last edit of it, so a crashed agent's claim lapses.</summary>
    [Setting("Edit claim lasts (minutes)", Group = "Scheduler", Order = 16,
        Help = "Minutes a file's edit claim stands after its agent's last edit of it, so a crashed agent's claim lapses. A commit naming the file, a finished subagent or a closed CLI releases it sooner.")]
    [SettingRange(1, 1440)]
    public double EditClaimMinutes { get; set; } = OrchestratorSettings.DefaultEditClaimMinutes;

    // ── Logins ─────────────────────────────────────────────────────────────

    /// <summary>A new Claude tab with no login chosen goes on a signed-in per-CLI login no CLI runs on yet, most headroom first, else the global login.</summary>
    [Setting("One CLI per account (Auto login)", Group = "Logins", Order = 1,
        Help = "A new Claude tab with no login chosen goes on a signed-in per-CLI login no CLI runs on yet, most headroom first, else the global login.")]
    public bool AutoLogin { get; set; } = true;

    /// <summary>A CLI whose login crosses its pace target or weekly stop line moves to a signed-in per-CLI login with more headroom at its next idle point.</summary>
    [Setting("Rebalance when an account runs low", Group = "Logins", Order = 2,
        Help = "A CLI whose login crosses its pace target or weekly stop line moves to a signed-in per-CLI login with more headroom at its next idle point.")]
    public bool RebalanceLogins { get; set; } = true;

    // ── Reviews ────────────────────────────────────────────────────────────

    /// <summary>Lines changed past which a Sonnet ticket's diff gets one Opus review before it lands.</summary>
    [Setting("Review diffs over (lines)", Group = "Reviews", Order = 1,
        Help = "Lines changed past which a Sonnet ticket's diff gets one Opus review before it lands. 0 reviews every one; -1 none for size.")]
    [SettingRange(-1, 1000000)]
    public int ReviewDiffLines { get; set; } = 300;

    /// <summary>Wildcards (* and ?) for files whose change by a Sonnet ticket always gets that review: the gates, pacing and git landing.</summary>
    [Setting("Risky files", Group = "Reviews", Order = 2,
        Help = "Wildcards (* and ?) for files whose change by a Sonnet ticket always gets that review: the gates, pacing and git landing.")]
    public List<string> RiskyFiles { get; set; } = [.. OrchestratorSettings.DefaultRiskyFiles];

    // ── Alerts ─────────────────────────────────────────────────────────────

    /// <summary>Who's texted through Psst when a login reaches its own cutoff: a Psst contact (ryan) or a US number.</summary>
    [Setting("Text cutoff alerts to", Group = "Alerts", Order = 1,
        Help = "Who's texted through Psst when a login reaches its own cutoff: a Psst contact (ryan) or a US number. Empty follows Global's alert recipient; none texts nobody.")]
    public string PsstAlertTo { get; set; } = "";

    /// <summary>Text the alert recipient through Psst when something new waits on the owner in the inbox, once per item.</summary>
    [Setting("Text new inbox items", Group = "Alerts", Order = 1,
        Help = "Text the alert recipient through Psst when something new waits on you in the owner inbox (orchestrator inbox): a ticket to approve, one parked as waiting-on-owner, a stale or orphaned ticket nobody picked up, a login to sign in again. Once per item, never repeated. Off, the inbox still lists them.")]
    public bool InboxAlerts { get; set; } = true;

    /// <summary>A URL that takes a plain-text POST, like an ntfy.sh topic, for phone pushes.</summary>
    [Setting("Push alerts to", Group = "Alerts", Order = 2, Help = "A URL that takes a plain-text POST, like an ntfy.sh topic, for phone pushes. Empty sends nothing.")]
    [UrlSetting]
    public string? NotifyUrl { get; set; }

    /// <summary>Free GB on the system or worktree drive under which the daemon alerts.</summary>
    [Setting("Disk warning (free GB)", Group = "Alerts", Order = 3, Help = "Free GB on the system or worktree drive under which the daemon alerts. 0 turns the watch off.")]
    [SettingRange(0, 100000)]
    public double DiskWarnGb { get; set; } = 20;

    /// <summary>Free GB under which the disk alert is urgent.</summary>
    [Setting("Disk critical (free GB)", Group = "Alerts", Order = 4, Help = "Free GB under which the disk alert is urgent.")]
    [SettingRange(0, 100000)]
    public double DiskCriticalGb { get; set; } = 5;

    /// <summary>Free GB on the temp drive under which agent-made temp (ended sessions, stale scratch builds) is deleted, oldest first.</summary>
    [Setting("Disk self-cleanup (free GB)", Group = "Alerts", Order = 5, Help = "Free GB on the temp drive under which agent-made temp (ended sessions, stale scratch builds) is deleted, oldest first. 0 turns it off.")]
    [SettingRange(0, 100000)]
    public double DiskCleanupGb { get; set; } = 30;

    /// <summary>The hard disk floor: free GB on the system or worktree drive under which no new work starts, once a self-cleanup has run and the drive is still under it.</summary>
    [Setting("Disk floor: hold new work (free GB)", Group = "Alerts", Order = 5, Help = "Free GB on the system or worktree drive under which no new work starts (no tickets handed out, no new CLI, no main-line tests or branch builds), once a self-cleanup has run and the drive is still under it. Running work is never stopped. 0 turns the hold off.")]
    [SettingRange(0, 100000)]
    public double DiskHoldGb { get; set; } = 5;

    /// <summary>GB a held drive must be clear of the disk floor before new work starts again.</summary>
    [Setting("Disk floor: resume margin (GB)", Group = "Alerts", Order = 5, Help = "GB a held drive must be clear of the disk floor before new work starts again, so a drive hovering on the floor doesn't flap.")]
    [SettingRange(0, 100000)]
    public double DiskHoldResumeMarginGb { get; set; } = 2;

    /// <summary>Hours a %TEMP%\claude session folder is kept whatever the free space: older, with nothing in it written for as long, it goes once its session isn't running; a running session keeps its folder and loses only scratchpad entries that old and that quiet.</summary>
    [Setting("Keep session temp (hours)", Group = "Alerts", Order = 10,
        Help = "Hours a %TEMP%\\claude session folder is kept whatever the free space: older, with nothing in it written for as long, it goes once its session isn't running; a running session keeps its folder and loses only scratchpad entries that old and that quiet. 0 turns it off.")]
    [SettingRange(0, 8760)]
    public double TempRetentionHours { get; set; } = OrchestratorSettings.DefaultTempRetentionHours;

    /// <summary>Hours a claimed ticket's own scratch dir (its agent's ORCH_SCRATCH) is leased for in the resource ledger; claiming it again renews it.</summary>
    [Setting("Ticket scratch dir lease (hours)", Group = "Alerts", Order = 11,
        Help = "Hours a claimed ticket's own scratch dir (its agent's ORCH_SCRATCH) is leased for in the resource ledger; claiming it again renews it. Past it, a sweeper may reclaim it even if the claim never ended. 0 means no expiry: kept until released.")]
    [SettingRange(0, 8760)]
    public double ScratchLeaseHours { get; set; } = OrchestratorSettings.DefaultScratchLeaseHours;

    /// <summary>Seconds the background daemon waits before starting its loop again after it died; doubles with each consecutive failure up to the cap below.</summary>
    [Setting("Daemon restart wait (s)", Group = "Scheduler", Order = 90, Help = "Seconds the background daemon waits before starting its loop again after it died; doubles with each consecutive failure up to the cap below.")]
    [SettingRange(1, 3600)]
    public double DaemonRestartSeconds { get; set; } = 15;

    /// <summary>The longest the daemon waits between restarts of its loop.</summary>
    [Setting("Daemon restart wait cap (s)", Group = "Scheduler", Order = 91, Help = "The longest the daemon waits between restarts of its loop.")]
    [SettingRange(1, 86400)]
    public double DaemonRestartMaxSeconds { get; set; } = 300;

    /// <summary>Seconds between the daemon's scans for tool processes (find, git, node, dotnet...) that dead agents left behind.</summary>
    [Setting("Reap leaked tool processes every (s)", Group = "Scheduler", Order = 93,
        Help = "Seconds between the daemon's scans for tool processes (find, git, node, dotnet...) that dead agents left behind. Each scan is a WMI query.")]
    [SettingRange(1, 86400)]
    public double ReapEverySeconds { get; set; } = 60;

    /// <summary>Handles past which a tool process under a live agent is killed as runaway.</summary>
    [Setting("Reap a tool process past (handles)", Group = "Scheduler", Order = 94,
        Help = "Handles past which a tool process under a live agent is killed as runaway.")]
    [SettingRange(1000, 10000000)]
    public int ReapMaxHandles { get; set; } = (int)Sessions.ProcessReaper.MaxHandleCount;

    /// <summary>Minutes past which a find, rg or grep under a live agent is killed as runaway.</summary>
    [Setting("Reap a search running past (minutes)", Group = "Scheduler", Order = 95,
        Help = "Minutes past which a find, rg or grep under a live agent is killed as runaway. Builds and tests are never killed for age.")]
    [SettingRange(1, 10080)]
    public double ReapSearchMaxMinutes { get; set; } = 30;

    /// <summary>Minutes between cleanups of unused side-by-side TermHost, Gateway and TermViewer versions.</summary>
    [Setting("Remove old TermHost versions every (minutes)", Group = "Scheduler", Order = 96,
        Help = "Minutes between cleanups of unused side-by-side TermHost, Gateway and TermViewer versions.")]
    [SettingRange(1, 10080)]
    public double TermHostCleanupMinutes { get; set; } = 30;

    /// <summary>Minutes until a cleanup of old side-by-side versions that failed is tried again.</summary>
    [Setting("Retry a failed version cleanup after (minutes)", Group = "Scheduler", Order = 97,
        Help = "Minutes until a cleanup of old side-by-side versions that failed is tried again.")]
    [SettingRange(1, 10080)]
    public double TermHostCleanupRetryMinutes { get; set; } = 5;

    /// <summary>Hours between prunes of the CLI cycler's old marks.</summary>
    [Setting("Prune old CLI marks every (hours)", Group = "Scheduler", Order = 98, Help = "Hours between prunes of the CLI cycler's old marks.")]
    [SettingRange(1, 8760)]
    public double CliMarksPruneHours { get; set; } = 24;

    /// <summary>Minutes between looks for ended sessions' %TEMP%\claude folders to delete.</summary>
    [Setting("Delete ended sessions' temp every (minutes)", Group = "Scheduler", Order = 99,
        Help = "Minutes between looks for ended sessions' %TEMP%\\claude folders to delete.")]
    [SettingRange(1, 10080)]
    public double SessionTempPruneMinutes { get; set; } = 10;

    /// <summary>Seconds between copies of the settings, assignments and state into data/orchestrator.db, where the hooks read them.</summary>
    [Setting("Mirror SQL Server into orchestrator.db every (s)", Group = "Scheduler", Order = 100,
        Help = "Seconds between copies of the settings, assignments and state into data/orchestrator.db, where the hooks read them.")]
    [SettingRange(5, 86400)]
    public double MirrorSeconds { get; set; } = 60;

    /// <summary>Minutes between tries of tickets db sync while the ticket database has changed.</summary>
    [Setting("Sync the ticket database every (minutes)", Group = "Scheduler", Order = 101,
        Help = "Minutes between tries of tickets db sync while the ticket database has changed.")]
    [SettingRange(1, 10080)]
    public double TicketsDbSyncMinutes { get; set; } = 10;

    /// <summary>Minutes between commits of data/orchestrator.db and its dump while it has changed.</summary>
    [Setting("Commit orchestrator.db every (minutes)", Group = "Scheduler", Order = 102,
        Help = "Minutes between commits of data/orchestrator.db and its dump while it has changed.")]
    [SettingRange(1, 10080)]
    public double OrchestratorDbSyncMinutes { get; set; } = 10;

    /// <summary>The least minutes between two tabs opened for the same org when none of its members runs /allocate.</summary>
    [Setting("Org reach: one tab per org every (minutes)", Group = "Scheduler", Order = 103,
        Help = "The least minutes between two tabs opened for the same org when none of its members runs /allocate.")]
    [SettingRange(1, 10080)]
    public double OrgReachSpacingMinutes { get; set; } = 5;

    /// <summary>Seconds tickets db sync, and the git calls that commit orchestrator.db, may run before they're killed, so a hung push can't stall the daemon.</summary>
    [Setting("Stop a ticket db sync after (s)", Group = "Scheduler", Order = 104,
        Help = "Seconds tickets db sync, and the git calls that commit orchestrator.db, may run before they're killed, so a hung push can't stall the daemon.")]
    [SettingRange(5, 3600)]
    public double SyncTimeoutSeconds { get; set; } = 120;

    /// <summary>The soonest a signal (a turn ending, a usage report rewritten) brings the daemon's next refresh forward after the last one.</summary>
    [Setting("Early refresh at most every (s)", Group = "Scheduler", Order = 105,
        Help = "The soonest a signal (a turn ending, a usage report rewritten) brings the daemon's next refresh forward after the last one.")]
    [SettingRange(1, 3600)]
    public double RefreshMinGapSeconds { get; set; } = 10;

    /// <summary>Off (the default): work lands on main as always.</summary>
    [Setting("Build work branches (remote host)", Group = "Scheduler", Order = 110,
        Help = "Off (the default): work lands on main as always. On, for the remote host: each origin/work/* branch is built in a worktree of its own, apart from the others, and one that builds is queued for your PC's local test run (orchestrator branches). Your PC never sees a branch.")]
    public bool RemoteBranchBuilds { get; set; }

    /// <summary>How many work branches the host builds at the same time, across every repo.</summary>
    [Setting("Work-branch builds at once", Group = "Scheduler", Order = 111,
        Help = "How many work branches the host builds at the same time, across every repo. Each is a dotnet build of its own.")]
    [SettingRange(1, 32)]
    public double BranchBuildsMaxRunning { get; set; } = OrchestratorSettings.DefaultBranchBuildsMaxRunning;

    /// <summary>Minutes one work-branch build may run before it's stopped and recorded as failed; the branch's next push builds it again.</summary>
    [Setting("Work-branch build timeout (minutes)", Group = "Scheduler", Order = 112,
        Help = "Minutes one work-branch build may run before it's stopped and recorded as failed; the branch's next push builds it again.")]
    [SettingRange(1, 1440)]
    public double BranchBuildsTimeoutMinutes { get; set; } = OrchestratorSettings.DefaultBranchBuildsTimeoutMinutes;

    /// <summary>Seconds between the daemon's looks at each repo's work branches for a head that moved since its last build.</summary>
    [Setting("Look for moved work branches every (s)", Group = "Scheduler", Order = 113,
        Help = "Seconds between the daemon's looks at each repo's work branches for a head that moved since its last build.")]
    [SettingRange(5, 86400)]
    public double BranchBuildsCheckSeconds { get; set; } = OrchestratorSettings.DefaultBranchBuildsCheckSeconds;

    /// <summary>Minutes between the client's Scheduler job's fetches of its host's ready-to-test work branches.</summary>
    [Setting("Fetch the host's ready-to-test branches every (min)", Group = "Scheduler", Order = 114,
        Help = "On the owner's PC (a client) with \"Build work branches (remote host)\" on: minutes between the Scheduler job's fetches of the host's queue of work branches built clean and waiting for a local test run. Fetching runs no test.")]
    [SettingRange(1, 1440)]
    public double BranchQueueFetchMinutes { get; set; } = OrchestratorSettings.DefaultBranchQueueFetchMinutes;

    /// <summary>On the host: minutes after the gateway last saw an invited member that they still count as connected.</summary>
    [Setting("Member counts as connected for (min)", Group = "Gateway", Order = 2,
        Help = "On the host (hub phase 1): how many minutes after the gateway last saw an invited org member (a sign-in, their Team Wall's look at the sessions, a tickets command) they still count as connected, for the dashboard's connected-members readout and orchestrator machine members.")]
    [SettingRange(0.5, 1440)]
    public double MemberConnectedMinutes { get; set; } = OrchestratorSettings.DefaultMemberConnectedMinutes;

    /// <summary>Off (the default): on the host, members only watch, as in hub phase 1.</summary>
    [Setting("Members work their own CLIs (host)", Group = "Gateway", Order = 3,
        Help = "Off (the default): the gateway works exactly as before; members connected to the host only watch. On, on the host (hub phase 2): a CLI's owner is the member whose Claude login it runs on; members start and stop CLIs on their own logins from their PC, and type into their own CLIs once per-member Windows users or 'trusted org' allow it; another member's interact grant on a member's CLI needs that member's consent. Interactive streams are sent uncompressed. Ignored on every machine but the host.")]
    public bool HubMemberSessions { get; set; }

    /// <summary>Off (the default): members type into CLIs on the host only when those run under their own Windows accounts.</summary>
    [Setting("Trusted org: members' CLIs may run as the host's user", Group = "Gateway", Order = 4,
        Help = "Off (the default). On, with 'Members work their own CLIs': you accept that members' CLIs on the host run as the host's one Windows user. That user can read every Claude login's credentials and every DPAPI secret on the host, so any member who can type into a CLI can reach all of them. Only turn it on for an org whose members you trust with each other's logins.")]
    public bool HubTrustedOrg { get; set; }

    /// <summary>Off (the default): every CLI on the host runs as its one Windows user.</summary>
    [Setting("Per-member Windows users (host)", Group = "Gateway", Order = 5,
        Help = "Off (the default). On, with 'Members work their own CLIs': each member's CLIs on the host run under their own Windows account (own profile, own DPAPI scope), set with orchestrator machine isolate <member> <account>. An admin makes the accounts once by hand (docs/REMOTE_HOST.md); the Orchestrator never creates, changes or deletes a Windows account.")]
    public bool HubMemberWindowsUsers { get; set; }

    /// <summary>Seconds a consent request waits for the session owner before the viewer is left watching.</summary>
    [Setting("Consent wait (s)", Group = "Gateway", Order = 6,
        Help = "With 'Members work their own CLIs': when another member's interact grant is first used on a member's CLI, that CLI's owner is asked (a toast on their dashboard, or orchestrator machine consents). With no answer in this many seconds the viewer is left watching, read-only.")]
    [SettingRange(5, 600)]
    public double HubConsentSeconds { get; set; } = OrchestratorSettings.DefaultHubConsentSeconds;

    /// <summary>Seconds the gateway waits for a member's CLI, started under their own Windows account, to come up.</summary>
    [Setting("Member CLI start wait (s)", Group = "Gateway", Order = 6,
        Help = "With 'Members work their own CLIs' and 'Per-member Windows users': how long the gateway waits for a CLI it started under a member's own Windows account to come up (its terminal host to register) before answering their start. A host that comes up later still runs and shows on their Team Wall.")]
    [SettingRange(2, 120)]
    public double HubMemberStartWaitSeconds { get; set; } = OrchestratorSettings.DefaultHubMemberStartWaitSeconds;

    /// <summary>Seconds between a member client's looks at the host for consent requests on their own CLIs.</summary>
    [Setting("Consent check every (s)", Group = "Gateway", Order = 7,
        Help = "On a member's PC connected to the host: how often the dashboard asks the host whether anyone is asking to type into one of your CLIs, to show it as a toast. Keep it well under the consent wait.")]
    [SettingRange(2, 300)]
    public double HubConsentPollSeconds { get; set; } = OrchestratorSettings.DefaultHubConsentPollSeconds;

    /// <summary>How many extra times a write retries taking SQL Server's write lock after it stayed busy past its timeout (each wait longer than the last) before the command fails.</summary>
    [Setting("Database busy retries", Group = "Scheduler", Order = 92, Help = "How many extra times a write retries taking SQL Server's write lock after it stayed busy past its timeout (each wait longer than the last) before the command fails. Raise it when many CLIs and subagents write at once.")]
    [SettingRange(0, 100)]
    public int DbBusyRetries { get; set; } = 6;

    /// <summary>When RAM runs low, no new subagents, CLIs or test runs start and running work finishes; lower still, the newest subagent is shed, then tool calls are refused until memory recovers.</summary>
    [Setting("Memory guard", Group = "Memory", Order = 1,
        Help = "When RAM runs low, no new subagents, CLIs or test runs start and running work finishes; lower still, the newest subagent is shed, then tool calls are refused until memory recovers. /pacing off doesn't turn it off.")]
    public bool MemoryGuard { get; set; } = true;

    /// <summary>How many heavy processes (a game player, an engine build or editor, a long soak) may run on the machine at once; a heavy shell command is refused while that many hold the lease.</summary>
    [Setting("Heavy processes at once", Group = "Memory", Order = 1,
        Help = "How many heavy processes (a game player, a Unity/Unreal/s&box build or editor, a long soak) may run on the machine at once. A shell command matching the heavy pattern list takes a machine-wide lease first and is refused, with who holds it, while that many are held; nothing is killed. 0 turns the lease off.")]
    [SettingRange(0, 64)]
    public int HeavyProcessLimit { get; set; } = OrchestratorSettings.DefaultHeavyProcessLimit;

    /// <summary>Physical RAM in use under which the guard does nothing: no holds and no subagent cap by free RAM.</summary>
    [Setting("Guard starts at (RAM used %)", Group = "Memory", Order = 1,
        Help = "Physical RAM in use under which the guard does nothing: no holds and no subagent cap by free RAM. Windows 11 keeps RAM full on purpose, so at this line the guard first collects garbage and trims working sets instead of holding work.")]
    [SettingRange(0, 100)]
    public double MemoryGuardStartPercent { get; set; } = 98;

    /// <summary>Free RAM under which nothing new starts; running work finishes.</summary>
    [Setting("Hold new work under (free GB)", Group = "Memory", Order = 2, Help = "Free RAM under which nothing new starts; running work finishes.")]
    [SettingRange(0.5, 1024)]
    public double MemoryTightGb { get; set; } = 4;

    /// <summary>Free RAM under which the newest /allocate subagent is stopped each minute and its ticket requeued.</summary>
    [Setting("Shed a subagent under (free GB)", Group = "Memory", Order = 3, Help = "Free RAM under which the newest /allocate subagent is stopped each minute and its ticket requeued.")]
    [SettingRange(0.25, 1024)]
    public double MemoryCriticalGb { get; set; } = 2;

    /// <summary>Free RAM under which every agent's tool calls are refused so turns end, until memory recovers.</summary>
    [Setting("Refuse tool calls under (free GB)", Group = "Memory", Order = 4, Help = "Free RAM under which every agent's tool calls are refused so turns end, until memory recovers.")]
    [SettingRange(0.1, 1024)]
    public double MemoryEmergencyGb { get; set; } = 1;

    /// <summary>What one more subagent is assumed to take: it starts only while free RAM less this stays above the hold line.</summary>
    [Setting("RAM per subagent (GB)", Group = "Memory", Order = 5, Help = "What one more subagent is assumed to take: it starts only while free RAM less this stays above the hold line.")]
    [SettingRange(0.1, 64)]
    public double MemoryPerSubagentGb { get; set; } = 1;

    /// <summary>Commit charge (percent of RAM plus a fixed pagefile) at or past which new work holds even with free RAM over the GB line above.</summary>
    [Setting("Hold new work past (commit %)", Group = "Memory", Order = 6, Help = "Commit charge (percent of RAM plus a fixed pagefile) at or past which new work holds even with free RAM over the GB line above.")]
    [SettingRange(1, 100)]
    public double MemoryTightCommitPercent { get; set; } = 80;

    /// <summary>Commit charge percent at or past which the newest /allocate subagent is stopped each minute.</summary>
    [Setting("Shed a subagent past (commit %)", Group = "Memory", Order = 7, Help = "Commit charge percent at or past which the newest /allocate subagent is stopped each minute.")]
    [SettingRange(1, 100)]
    public double MemoryCriticalCommitPercent { get; set; } = 88;

    /// <summary>Commit charge percent at or past which every agent's tool calls are refused until memory recovers.</summary>
    [Setting("Refuse tool calls past (commit %)", Group = "Memory", Order = 8, Help = "Commit charge percent at or past which every agent's tool calls are refused until memory recovers.")]
    [SettingRange(1, 100)]
    public double MemoryEmergencyCommitPercent { get; set; } = 94;

    /// <summary>How far clear of a level's free-GB line a reading must be before the guard steps back down from it.</summary>
    [Setting("Resume margin (free GB)", Group = "Memory", Order = 9, Help = "How far clear of a level's free-GB line a reading must be before the guard steps back down from it.")]
    [SettingRange(0, 1024)]
    public double MemoryResumeMarginGb { get; set; } = 2;

    /// <summary>How far clear of a level's commit-percent line a reading must be before the guard steps back down from it.</summary>
    [Setting("Resume margin (commit points)", Group = "Memory", Order = 10, Help = "How far clear of a level's commit-percent line a reading must be before the guard steps back down from it.")]
    [SettingRange(0, 100)]
    public double MemoryResumeMarginCommit { get; set; } = 5;

    /// <summary>Seconds between the guard's reads of free RAM and commit charge; readings in between reuse the last.</summary>
    [Setting("Read RAM every (s)", Group = "Memory", Order = 11, Help = "Seconds between the guard's reads of free RAM and commit charge; readings in between reuse the last.")]
    [SettingRange(1, 3600)]
    public double MemorySampleSeconds { get; set; } = 5;

    /// <summary>Seconds a reading must stay clear of a level's line (by the resume margins) before the guard steps down a level.</summary>
    [Setting("Step down after clear for (s)", Group = "Memory", Order = 12,
        Help = "Seconds a reading must stay clear of a level's line (by the resume margins) before the guard steps down a level.")]
    [SettingRange(0, 3600)]
    public double MemorySettleSeconds { get; set; } = 60;

    /// <summary>The max server memory the daemon keeps localhost's SQL Server at, applied with sp_configure when it changes.</summary>
    [Setting("SQL Server max memory (MB)", Group = "Memory", Order = 13,
        Help = "The max server memory the daemon keeps localhost's SQL Server at, applied with sp_configure when it changes. 0 (the default) leaves SQL Server's own setting alone; any other value under 4096 is raised to 4096, since 1024 crashed it (error 17300).")]
    [SettingRange(0, 1048576)]
    public double SqlServerMaxMemoryMb { get; set; } = OrchestratorSettings.DefaultSqlServerMaxMemoryMb;

    /// <summary>Seconds between the daemon's samples of handles, commit charge and free RAM for the machine-health alert.</summary>
    [Setting("Sample machine health every (s)", Group = "Alerts", Order = 63,
        Help = "Seconds between the daemon's samples of handles, commit charge and free RAM for the machine-health alert.")]
    [SettingRange(1, 86400)]
    public double HealthSampleSeconds { get; set; } = 30;

    /// <summary>Hours between the daemon's checks for a system-managed pagefile, which can fill the disk.</summary>
    [Setting("Check the pagefile every (hours)", Group = "Alerts", Order = 64, Help = "Hours between the daemon's checks for a system-managed pagefile, which can fill the disk.")]
    [SettingRange(1, 8760)]
    public double PagefileCheckHours { get; set; } = 24;

    /// <summary>Minutes between prunes of old log entries and finished terminal hosts' log files.</summary>
    [Setting("Prune old logs every (minutes)", Group = "Alerts", Order = 65, Help = "Minutes between prunes of old log entries and finished terminal hosts' log files.")]
    [SettingRange(1, 10080)]
    public double LogPruneMinutes { get; set; } = 60;

    /// <summary>The least minutes between two disk self-cleanups while the temp drive stays under its free-GB line.</summary>
    [Setting("Disk self-cleanup at most every (minutes)", Group = "Alerts", Order = 66,
        Help = "The least minutes between two disk self-cleanups while the temp drive stays under its free-GB line.")]
    [SettingRange(1, 10080)]
    public double DiskCleanupEveryMinutes { get; set; } = 5;

    /// <summary>Minutes between GitHub intakes while GitHub intake is on.</summary>
    [Setting("GitHub intake every (minutes)", Group = "Alerts", Order = 67, Help = "Minutes between GitHub intakes while GitHub intake is on.")]
    [SettingRange(5, 10080)]
    public double GitHubIntakeMinutes { get; set; } = 60;

    /// <summary>Hours between the audits the daemon runs by itself.</summary>
    [Setting("Audit every (hours)", Group = "Alerts", Order = 5, Help = "Hours between the audits the daemon runs by itself. 0 means only on demand.")]
    [SettingRange(0, 100000)]
    public int AuditEveryHours { get; set; } = 24;

    /// <summary>Open tickets older than this with nobody on them are listed for grooming.</summary>
    [Setting("Groom tickets older than (days)", Group = "Alerts", Order = 6, Help = "Open tickets older than this with nobody on them are listed for grooming. 0 means never.")]
    [SettingRange(0, 100000)]
    public int GroomAfterDays { get; set; } = 30;

    /// <summary>Days the Orchestrator's MindAttic.Log database keeps its entries before the daemon deletes older ones.</summary>
    [Setting("Keep log entries (days)", Group = "Alerts", Order = 61, Help = "Days the Orchestrator's MindAttic.Log database keeps its entries before the daemon deletes older ones. 0 keeps them for good.")]
    [SettingRange(0, 100000)]
    public int LogKeepDays { get; set; } = 30;

    /// <summary>Days a finished terminal host's own log file is kept before the daemon deletes it.</summary>
    [Setting("Keep terminal host logs (days)", Group = "Alerts", Order = 62, Help = "Days a finished terminal host's own log file is kept before the daemon deletes it. 0 keeps them for good.")]
    [SettingRange(0, 100000)]
    public int TermLogKeepDays { get; set; } = 7;

    /// <summary>Hourly, file GitHub issues labelled "orchestrator" and CI runs that failed on main as tickets.</summary>
    [Setting("GitHub intake", Group = "Alerts", Order = 7,
        Help = "Hourly, file GitHub issues labelled \"orchestrator\" and CI runs that failed on main as tickets.")]
    public bool GitHubIntake { get; set; }

    /// <summary>Once a day, ask winget for a newer SQL Server Developer or Express (LocalDB) bootstrapper; a newer one is written into setup-host.ps1, committed and pushed, and the alert recipient is texted.</summary>
    [Setting("Daily SQL Server check", Group = "Alerts", Order = 8,
        Help = "Once a day, ask winget for a newer SQL Server Developer or Express (LocalDB) bootstrapper; a newer one is written into setup-host.ps1, committed and pushed, and the alert recipient is texted. A newer major is only texted.")]
    public bool SqlFreshnessCheck { get; set; }

    /// <summary>Local time of day (HH:mm) the daily SQL Server check runs; a day the daemon was off then runs it at its next tick.</summary>
    [Setting("Daily SQL Server check at", Group = "Alerts", Order = 9,
        Help = "Local time of day (HH:mm) the daily SQL Server check runs; a day the daemon was off then runs it at its next tick.")]
    public string SqlFreshnessCheckAt { get; set; } = OrchestratorSettings.DefaultSqlFreshnessCheckAt;

    // ── Dashboard ──────────────────────────────────────────────────────────

    /// <summary>The dashboard's theme; its sun/moon switch changes this too.</summary>
    [Setting("Theme", Group = "Dashboard", Order = 1, Help = "The dashboard's theme; its sun/moon switch changes this too. Empty is dark.")]
    [SettingChoices("Dark", "Light")]
    public string? Theme { get; set; }

    /// <summary>The dashboard's font in every window, menu and tooltip, changed live: Segoe UI (the default) or Outfit.</summary>
    [Setting("Font", Group = "Dashboard", Order = 24,
        Help = "The dashboard's font in every window, menu and tooltip, changed live: Segoe UI (the default) or Outfit. The terminal panes keep their monospace font.")]
    [SettingChoices("Segoe UI", "Outfit")]
    public string Font { get; set; } = OrchestratorSettings.DefaultFont;

    /// <summary>On: "Σ: 15% | λ: 29%".</summary>
    [Setting("Symbols in tab labels (Σ/λ)", Group = "Dashboard", Order = 2,
        Help = "On: \"Σ: 15% | λ: 29%\". Off: \"wk: 15% | 5h: 29%\". Same figures either way.")]
    public bool SymbolTabLabels { get; set; } = true;

    /// <summary>A busy login's green play arrow blinks once for every this many tokens it spends, so the busiest blink fastest.</summary>
    [Setting("Blink once per (tokens)", Group = "Dashboard", Order = 3,
        Help = "A busy login's green play arrow blinks once for every this many tokens it spends, so the busiest blink fastest.")]
    [SettingRange(OrchestratorSettings.MinTabBlinkTokens, OrchestratorSettings.MaxTabBlinkTokens)]
    public int TabBlinkTokens { get; set; } = OrchestratorSettings.DefaultTabBlinkTokens;

    /// <summary>Off (the default): the Launcher's own menu tab shows as a tile on the Team Wall like any other CLI.</summary>
    [Setting("Exclude Launcher from Team Wall", Group = "Dashboard", Order = 4,
        Help = "Off (the default): the Launcher's own menu tab shows as a tile on the Team Wall like any other CLI.")]
    public bool ExcludeLauncherFromTeamWall { get; set; }

    /// <summary>Off (the default): no MindAttic.Launcher process (~80 MB each) is started for every signed-in login or when its last tab closes; a login tab's right-click Open Project lists the same projects in-app, and Run Launcher still opens one by hand.</summary>
    [Setting("Open a Launcher per login", Group = "Dashboard", Order = 5,
        Help = "Off (the default): no MindAttic.Launcher process (~80 MB each) is started for every signed-in login or when its last tab closes; a login tab's right-click Open Project lists the same projects in-app, and Run Launcher still opens one by hand.")]
    public bool AutoOpenLaunchers { get; set; }

    /// <summary>How long a CLI tab whose CLI has ended stays for its last lines before it's closed unasked.</summary>
    [Setting("Ended tab lifetime (minutes)", Group = "Dashboard", Order = 6,
        Help = "How long a CLI tab whose CLI has ended stays for its last lines before it's closed unasked. Each holds its terminal renderer (~50 MB) until then.")]
    [SettingRange(0, 10080)]
    public double EndedTabMinutes { get; set; } = OrchestratorSettings.DefaultEndedTabMinutes;

    /// <summary>How long a TermHost whose folder is an orch-test-* temp folder (left by a test that never closed its tab) may run before the daemon ends it; one whose folder is gone is ended at once.</summary>
    [Setting("Leaked test TermHost lifetime (minutes)", Group = "Dashboard", Order = 90,
        Help = "How long a TermHost whose folder is an orch-test-* temp folder (left by a test that never closed its tab) may run before the daemon ends it; one whose folder is gone is ended at once. A host in any other folder is never touched.")]
    [SettingRange(1, 10080)]
    public double TestTermHostMaxMinutes { get; set; } = OrchestratorSettings.DefaultTestTermHostMaxMinutes;

    /// <summary>After an Enter in a CLI tab lights it and its person's tab busy at once, a status read taken this many seconds or more later that still says idle ends it (nothing started).</summary>
    [Setting("Presumed busy confirm after (seconds)", Group = "Dashboard", Order = 21,
        Help = "After an Enter in a CLI tab lights it and its person's tab busy at once, a status read taken this many seconds or more later that still says idle ends it (nothing started).")]
    [SettingRange(1, 600)]
    public double PresumeBusyConfirmSeconds { get; set; } = OrchestratorSettings.DefaultPresumeBusyConfirmSeconds;

    /// <summary>The longest an Enter in a CLI tab keeps it and its person's tab presumed busy when no status read comes at all.</summary>
    [Setting("Presumed busy grace period (seconds)", Group = "Dashboard", Order = 22,
        Help = "The longest an Enter in a CLI tab keeps it and its person's tab presumed busy when no status read comes at all.")]
    [SettingRange(1, 3600)]
    public double PresumeBusyMaxSeconds { get; set; } = OrchestratorSettings.DefaultPresumeBusyMaxSeconds;

    /// <summary>An idle CLI unseen this long has its Team Wall activity line aged to stale and dimmed.</summary>
    [Setting("Activity stale after (minutes)", Group = "Dashboard", Order = 25,
        Help = "An idle CLI unseen this long has its Team Wall activity line aged to stale and dimmed. 0 never ages one; a CLI waiting on you never ages.")]
    [SettingRange(0, 1440)]
    public double ActivityStaleMinutes { get; set; } = OrchestratorSettings.DefaultActivityStaleMinutes;

    /// <summary>The least time between two activity bus messages for one CLI session.</summary>
    [Setting("Activity feed gap per CLI (seconds)", Group = "Dashboard", Order = 26,
        Help = "The least time between two activity bus messages for one CLI session. A busy CLI's newer pictures inside the gap are held, and the latest goes out when it passes. 0 sends each at once.")]
    [SettingRange(0, 60)]
    public double ActivityCoalesceSeconds { get; set; } = OrchestratorSettings.DefaultActivityCoalesceSeconds;

    /// <summary>How often the daemon reads the activity records folder again, for a change its file watcher missed, and checks silent sessions for an end.</summary>
    [Setting("Activity feed rescan (seconds)", Group = "Dashboard", Order = 27,
        Help = "How often the daemon reads the activity records folder again, for a change its file watcher missed, and checks silent sessions for an end.")]
    [SettingRange(1, 3600)]
    public double ActivityRescanSeconds { get; set; } = OrchestratorSettings.DefaultActivityRescanSeconds;

    /// <summary>A CLI session that sends no activity this long, with its process gone, is said to have ended on the bus and no longer kept for apps that connect.</summary>
    [Setting("Activity ended after (minutes)", Group = "Dashboard", Order = 28,
        Help = "A CLI session that sends no activity this long, with its process gone, is said to have ended on the bus and no longer kept for apps that connect.")]
    [SettingRange(0, 1440)]
    public double ActivityEndedMinutes { get; set; } = OrchestratorSettings.DefaultActivityEndedMinutes;

    /// <summary>How many more times a main-line suite's build is tried when it fails copying a file another process held (MSB3027).</summary>
    [Setting("Main-line test build lock retries", Group = "Dashboard", Order = 92,
        Help = "How many more times a main-line suite's build is tried when it fails copying a file another process held (MSB3027). Before each try, leftover test processes running from that worktree are ended; nothing outside it is touched.")]
    [SettingRange(0, 10)]
    public double MainTestsLockRetries { get; set; } = OrchestratorSettings.DefaultMainTestsLockRetries;

    /// <summary>Seconds waited before each retry of a main-line suite's build that failed on a locked file.</summary>
    [Setting("Main-line test build lock retry wait (seconds)", Group = "Dashboard", Order = 93,
        Help = "Seconds waited before each retry of a main-line suite's build that failed on a locked file.")]
    [SettingRange(0, 600)]
    public double MainTestsLockRetrySeconds { get; set; } = OrchestratorSettings.DefaultMainTestsLockRetrySeconds;

    /// <summary>Minutes one suite may run before it's ended and counted as not completed (main-line suites and branches test).</summary>
    [Setting("Test suite timeout (minutes)", Group = "Dashboard", Order = 93,
        Help = "Minutes one suite may run before it's ended and counted as not completed. Applies to the main-line suites and to orchestrator branches test (its --timeout overrides it for one run).")]
    [SettingRange(1, 1440)]
    public double MainTestsTimeoutMinutes { get; set; } = OrchestratorSettings.DefaultMainTestsTimeoutMinutes;

    /// <summary>Minutes orchestrator branches test waits for the memory guard to clear before each branch's run.</summary>
    [Setting("Branch tests memory wait (minutes)", Group = "Dashboard", Order = 93,
        Help = "Minutes orchestrator branches test waits, before each branch's run, for RAM to recover while the memory guard holds new work. Past it the branch is skipped and nothing is reported.")]
    [SettingRange(0, 1440)]
    public double BranchTestsMemoryWaitMinutes { get; set; } = OrchestratorSettings.DefaultBranchTestsMemoryWaitMinutes;

    /// <summary>How long an ended Launcher menu tab stays; 0 (the default) closes it at once.</summary>
    [Setting("Ended Launcher tab lifetime (minutes)", Group = "Dashboard", Order = 7,
        Help = "How long an ended Launcher menu tab stays; 0 (the default) closes it at once.")]
    [SettingRange(0, 10080)]
    public double EndedLauncherTabMinutes { get; set; }

    /// <summary>Seconds between the dashboard's refreshes while it's shown and polling: no daemon status stream up, or it holds the status lease itself.</summary>
    [Setting("Refresh every (s)", Group = "Dashboard", Order = 8,
        Help = "Seconds between the dashboard's refreshes while it's shown and polling: no daemon status stream up, or it holds the status lease itself.")]
    [SettingRange(1, 3600)]
    public double DashboardPollSeconds { get; set; } = 5;

    /// <summary>Seconds between refreshes while the dashboard is minimized with no daemon status stream: nobody is looking.</summary>
    [Setting("Refresh while minimized every (s)", Group = "Dashboard", Order = 9,
        Help = "Seconds between refreshes while the dashboard is minimized with no daemon status stream: nobody is looking.")]
    [SettingRange(1, 86400)]
    public double DashboardHiddenPollSeconds { get; set; } = 60;

    /// <summary>Seconds between refreshes while minimized but holding the status lease (no daemon running): rl-status.json and allocate depend on it.</summary>
    [Setting("Refresh while minimized, no daemon, every (s)", Group = "Dashboard", Order = 10,
        Help = "Seconds between refreshes while minimized but holding the status lease (no daemon running): rl-status.json and allocate depend on it.")]
    [SettingRange(1, 3600)]
    public double DashboardHiddenHolderPollSeconds { get; set; } = 15;

    /// <summary>Seconds between rereads of the hosted-session registry, so a CLI whose announcement was lost still gets its tab.</summary>
    [Setting("Recheck hosted CLIs every (s)", Group = "Dashboard", Order = 11,
        Help = "Seconds between rereads of the hosted-session registry, so a CLI whose announcement was lost still gets its tab.")]
    [SettingRange(5, 86400)]
    public double TermResyncSeconds { get; set; } = 45;

    /// <summary>The least seconds between two Launchers opened for one person, so one that exits at once can't loop.</summary>
    [Setting("Launcher respawn spacing (s)", Group = "Dashboard", Order = 12,
        Help = "The least seconds between two Launchers opened for one person, so one that exits at once can't loop.")]
    [SettingRange(1, 3600)]
    public double LauncherRespawnSeconds { get; set; } = 30;

    /// <summary>Seconds an elevated (Administrator) CLI tab waits for its TermHost to register, UAC prompt included (M.ORCH-T1656).</summary>
    [Setting("Elevated tab start wait (s)", Group = "Dashboard", Order = 12,
        Help = "Seconds an elevated (Administrator) CLI tab waits for its TermHost to register after the UAC prompt opens; past it the tab reports that the host never started.")]
    [SettingRange(5, 3600)]
    public double ElevatedTermWaitSeconds { get; set; } = OrchestratorSettings.DefaultElevatedTermWaitSeconds;

    /// <summary>Seconds between the dashboard's rereads of each login's meter state: when it was last read and who must sign in again.</summary>
    [Setting("Reread login meters every (s)", Group = "Dashboard", Order = 13,
        Help = "Seconds between the dashboard's rereads of each login's meter state: when it was last read and who must sign in again.")]
    [SettingRange(1, 3600)]
    public double MeterStatesSeconds { get; set; } = 30;

    /// <summary>The shortest gap in milliseconds from one tab blink to the next, so each still reads as a blink; it also caps how many blinks one refresh queues.</summary>
    [Setting("Blink spacing at least (ms)", Group = "Dashboard", Order = 14,
        Help = "The shortest gap in milliseconds from one tab blink to the next, so each still reads as a blink; it also caps how many blinks one refresh queues.")]
    [SettingRange(50, 5000)]
    public double TabBlinkMinSpacingMs { get; set; } = 320;

    /// <summary>Readings of the 5-hour chart closer together than this many seconds are thinned to one point.</summary>
    [Setting("5-hour chart point spacing (s)", Group = "Dashboard", Order = 15,
        Help = "Readings of the 5-hour chart closer together than this many seconds are thinned to one point.")]
    [SettingRange(1, 3600)]
    public double UtilizationMinSpacingSeconds { get; set; } = 60;

    /// <summary>Minutes a read of the Open Project menu's roster (and GitHub's wip topic) is kept before opening the menu reads it again.</summary>
    [Setting("Reread the project roster after (minutes)", Group = "Dashboard", Order = 16,
        Help = "Minutes a read of the Open Project menu's roster (and GitHub's wip topic) is kept before opening the menu reads it again.")]
    [SettingRange(1, 10080)]
    public double ProjectsRefreshMinutes { get; set; } = 10;

    /// <summary>Seconds a read of each project's uncommitted file count is kept before opening the Open Project menu reads them again.</summary>
    [Setting("Recount project changes after (s)", Group = "Dashboard", Order = 17,
        Help = "Seconds a read of each project's uncommitted file count is kept before opening the Open Project menu reads them again.")]
    [SettingRange(1, 86400)]
    public double ProjectChangesRefreshSeconds { get; set; } = 30;

    /// <summary>Seconds between rereads of the recently closed tickets while ticket changes aren't pushed by the status stream.</summary>
    [Setting("Reread closed tickets every (s)", Group = "Dashboard", Order = 18,
        Help = "Seconds between rereads of the recently closed tickets while ticket changes aren't pushed by the status stream.")]
    [SettingRange(5, 86400)]
    public double ClosedTicketsReadSeconds { get; set; } = 60;

    /// <summary>Minutes between the same rereads while the status stream is up and ticket changes are pushed: a safety net only.</summary>
    [Setting("Reread closed tickets, stream up, every (minutes)", Group = "Dashboard", Order = 19,
        Help = "Minutes between the same rereads while the status stream is up and ticket changes are pushed: a safety net only.")]
    [SettingRange(1, 10080)]
    public double ClosedTicketsReadLiveMinutes { get; set; } = 15;

    /// <summary>On (the default): a CLI tab's X asks before it ends a CLI that's still running.</summary>
    [Setting("Confirm closing a running CLI", Group = "Dashboard", Order = 20,
        Help = "On (the default): a CLI tab's X asks before it ends a CLI that's still running. A tab whose CLI already ended closes unasked either way.")]
    public bool ConfirmCliClose { get; set; } = true;

    /// <summary>Minutes between the daemon's sweeps for TermHosts left running in orch-test-* temp folders.</summary>
    [Setting("Sweep for leaked test TermHosts every (minutes)", Group = "Dashboard", Order = 91,
        Help = "Minutes between the daemon's sweeps for TermHosts left running in orch-test-* temp folders. Each sweep is a WMI query.")]
    [SettingRange(1, 10080)]
    public double TestTermHostSweepMinutes { get; set; } = 5;

    // ── Folders ────────────────────────────────────────────────────────────

    /// <summary>The folder holding every repo.</summary>
    [Setting("Workspace root", Group = "Folders", Order = 1,
        Help = "The folder holding every repo. Empty follows Global's workspace root, else MindAttic.Tickets's default.")]
    [PathPicker(PathKind.Folder)]
    public string? WorkspaceRoot { get; set; }

    /// <summary>Where the tools' own clean worktrees of origin/main live, outside the workspace root.</summary>
    [Setting("Tools' worktree root", Group = "Folders", Order = 2,
        Help = "Where the tools' own clean worktrees of origin/main live, outside the workspace root.")]
    [PathPicker(PathKind.Folder)]
    public string WorktreeRoot { get; set; } = @"D:\Projects\MindAttic.worktrees";

    /// <summary>Entries email=date joined by ; (a date is yyyy-MM-dd, that day's end, or yyyy-MM-ddTHH:mm, local).</summary>
    [Setting("Member until dates", Group = "Dashboard", Order = 23,
        Help = "Entries email=date joined by ; (a date is yyyy-MM-dd, that day's end, or yyyy-MM-ddTHH:mm, local). After its date the dashboard drops that member's tab and stops auto-starting or respawning its CLI tabs; nothing of the member (login, credentials, data) is deleted. Clear the entry to bring the tab back.")]
    public string MemberUntil { get; set; } = Budget.MemberUntil.Default;

    // ── Gateway ────────────────────────────────────────────────────────────

    /// <summary>The team gateway the daemon keeps running when enabled.</summary>
    [Setting("Team gateway", Group = "Gateway", Order = 1,
        Help = "The team gateway the daemon keeps running when enabled. Teammates' gateways (orchestrator gateway remote add) stay with the orchestrator.")]
    public OrchestratorGatewayVaultSettings Gateway { get; set; } = new();

    // ── Mapping ────────────────────────────────────────────────────────────

    /// <summary>The Vault values in the database row's terms (today's storage).</summary>
    public static OrchestratorVaultSettings From(OrchestratorSettings s) => new()
    {
        BlockBudget = s.BlockBudget,
        WeeklyBudget = s.WeeklyBudget,
        WeeklyReset = s.WeeklyReset,
        WarnPercent = s.WarnPercent,
        WeeklyReservePercent = s.WeeklyReservePercent,
        Pacing = s.Pacing,
        PaceTargetPercent = s.PaceTargetPercent,
        PaceGuardStartPercent = s.PaceGuardStartPercent,
        ExemptCommands = [.. s.ExemptCommands],
        ReportFreshMinutes = s.ReportFreshMinutes,
        ProviderCapPercent = s.ProviderCapPercent,
        WeeklyAwareLinePercent = s.WeeklyAwareLinePercent,
        PaceCurveK = s.PaceCurveK,
        MaxAgentDelaySeconds = s.MaxAgentDelaySeconds,
        AgentAbort = s.AgentAbort,
        AgentAbortAfterMinutes = s.AgentAbortAfterMinutes,
        Allocate = s.Allocate,
        MaxConcurrentSessions = s.MaxConcurrentSessions,
        MaxSessionsPerRepo = s.MaxSessionsPerRepo,
        MaxSubagents = s.MaxSubagents,
        DefaultProvider = s.DefaultProvider,
        KeepGoing = s.KeepGoing,
        ResumeAfterReset = s.ResumeAfterReset,
        QuietHours = [.. s.QuietHours],
        AllocateSkip = [.. s.AllocateSkip],
        AllocateHandMinutes = s.AllocateHandMinutes,
        StaleAfterHours = s.StaleAfterHours,
        ContextCyclePercent = s.ContextCyclePercent,
        OrphanRedispatches = s.OrphanRedispatches,
        OrphanQuietMinutes = s.OrphanQuietMinutes,
        InboxAlerts = s.InboxAlerts,
        EditClaims = s.EditClaims,
        EditClaimMinutes = s.EditClaimMinutes,
        AutoLogin = s.AutoLogin,
        RebalanceLogins = s.RebalanceLogins,
        ReviewDiffLines = s.ReviewDiffLines,
        RiskyFiles = [.. s.RiskyFiles],
        // The orchestrator's own overrides of Vault Global's values (M.ORCH-T1328), never the resolved ones.
        PsstAlertTo = AlertOverrideOf(s),
        NotifyUrl = s.NotifyUrl,
        DiskWarnGb = s.DiskWarnGb,
        DiskCriticalGb = s.DiskCriticalGb,
        DiskCleanupGb = s.DiskCleanupGb,
        DiskHoldGb = s.DiskHoldGb,
        DiskHoldResumeMarginGb = s.DiskHoldResumeMarginGb,
        TempRetentionHours = s.TempRetentionHours,
        ScratchLeaseHours = s.ScratchLeaseHours,
        DaemonRestartSeconds = s.DaemonRestartSeconds,
        DaemonRestartMaxSeconds = s.DaemonRestartMaxSeconds,
        DbBusyRetries = s.DbBusyRetries,
        ReapEverySeconds = s.ReapEverySeconds,
        ReapMaxHandles = s.ReapMaxHandles,
        ReapSearchMaxMinutes = s.ReapSearchMaxMinutes,
        TermHostCleanupMinutes = s.TermHostCleanupMinutes,
        TermHostCleanupRetryMinutes = s.TermHostCleanupRetryMinutes,
        CliMarksPruneHours = s.CliMarksPruneHours,
        SessionTempPruneMinutes = s.SessionTempPruneMinutes,
        MirrorSeconds = s.MirrorSeconds,
        TicketsDbSyncMinutes = s.TicketsDbSyncMinutes,
        OrchestratorDbSyncMinutes = s.OrchestratorDbSyncMinutes,
        MemorySampleSeconds = s.MemorySampleSeconds,
        MemorySettleSeconds = s.MemorySettleSeconds,
        HealthSampleSeconds = s.HealthSampleSeconds,
        PagefileCheckHours = s.PagefileCheckHours,
        LogPruneMinutes = s.LogPruneMinutes,
        DiskCleanupEveryMinutes = s.DiskCleanupEveryMinutes,
        GitHubIntakeMinutes = s.GitHubIntakeMinutes,
        DashboardPollSeconds = s.DashboardPollSeconds,
        DashboardHiddenPollSeconds = s.DashboardHiddenPollSeconds,
        DashboardHiddenHolderPollSeconds = s.DashboardHiddenHolderPollSeconds,
        TermResyncSeconds = s.TermResyncSeconds,
        LauncherRespawnSeconds = s.LauncherRespawnSeconds,
        ElevatedTermWaitSeconds = s.ElevatedTermWaitSeconds,
        MeterStatesSeconds = s.MeterStatesSeconds,
        TestTermHostSweepMinutes = s.TestTermHostSweepMinutes,
        OrgReachSpacingMinutes = s.OrgReachSpacingMinutes,
        ApiCostReadMinutes = s.ApiCostReadMinutes,
        UtilizationMinSpacingSeconds = s.UtilizationMinSpacingSeconds,
        SyncTimeoutSeconds = s.SyncTimeoutSeconds,
        RefreshMinGapSeconds = s.RefreshMinGapSeconds,
        RemoteBranchBuilds = s.RemoteBranchBuilds,
        BranchBuildsMaxRunning = s.BranchBuildsMaxRunning,
        BranchBuildsTimeoutMinutes = s.BranchBuildsTimeoutMinutes,
        BranchBuildsCheckSeconds = s.BranchBuildsCheckSeconds,
        BranchQueueFetchMinutes = s.BranchQueueFetchMinutes,
        MemberConnectedMinutes = s.MemberConnectedMinutes,
        HubMemberSessions = s.HubMemberSessions,
        HubTrustedOrg = s.HubTrustedOrg,
        HubMemberWindowsUsers = s.HubMemberWindowsUsers,
        HubConsentSeconds = s.HubConsentSeconds,
        HubMemberStartWaitSeconds = s.HubMemberStartWaitSeconds,
        HubConsentPollSeconds = s.HubConsentPollSeconds,
        TabBlinkMinSpacingMs = s.TabBlinkMinSpacingMs,
        ProjectsRefreshMinutes = s.ProjectsRefreshMinutes,
        ProjectChangesRefreshSeconds = s.ProjectChangesRefreshSeconds,
        ClosedTicketsReadSeconds = s.ClosedTicketsReadSeconds,
        ClosedTicketsReadLiveMinutes = s.ClosedTicketsReadLiveMinutes,
        MemoryGuard = s.MemoryGuard,
        HeavyProcessLimit = s.HeavyProcessLimit,
        MemoryGuardStartPercent = s.MemoryGuardStartPercent,
        MemoryTightGb = s.MemoryTightGb,
        MemoryCriticalGb = s.MemoryCriticalGb,
        MemoryEmergencyGb = s.MemoryEmergencyGb,
        MemoryPerSubagentGb = s.MemoryPerSubagentGb,
        MemoryTightCommitPercent = s.MemoryTightCommitPercent,
        MemoryCriticalCommitPercent = s.MemoryCriticalCommitPercent,
        MemoryEmergencyCommitPercent = s.MemoryEmergencyCommitPercent,
        MemoryResumeMarginGb = s.MemoryResumeMarginGb,
        MemoryResumeMarginCommit = s.MemoryResumeMarginCommit,
        SqlServerMaxMemoryMb = s.SqlServerMaxMemoryMb,
        AuditEveryHours = s.AuditEveryHours,
        GroomAfterDays = s.GroomAfterDays,
        LogKeepDays = s.LogKeepDays,
        TermLogKeepDays = s.TermLogKeepDays,
        GitHubIntake = s.GitHubIntake,
        SqlFreshnessCheck = s.SqlFreshnessCheck,
        SqlFreshnessCheckAt = s.SqlFreshnessCheckAt,
        Theme = s.Theme,
        Font = s.Font,
        SymbolTabLabels = s.SymbolTabLabels,
        TabBlinkTokens = s.TabBlinkTokens,
        ExcludeLauncherFromTeamWall = s.ExcludeLauncherFromTeamWall,
        AutoOpenLaunchers = s.AutoOpenLaunchers,
        ConfirmCliClose = s.ConfirmCliClose,
        EndedTabMinutes = s.EndedTabMinutes,
        TestTermHostMaxMinutes = s.TestTermHostMaxMinutes,
        PresumeBusyConfirmSeconds = s.PresumeBusyConfirmSeconds,
        PresumeBusyMaxSeconds = s.PresumeBusyMaxSeconds,
        ActivityStaleMinutes = s.ActivityStaleMinutes,
        ActivityCoalesceSeconds = s.ActivityCoalesceSeconds,
        ActivityRescanSeconds = s.ActivityRescanSeconds,
        ActivityEndedMinutes = s.ActivityEndedMinutes,
        MainTestsLockRetries = s.MainTestsLockRetries,
        MainTestsLockRetrySeconds = s.MainTestsLockRetrySeconds,
        MainTestsTimeoutMinutes = s.MainTestsTimeoutMinutes,
        BranchTestsMemoryWaitMinutes = s.BranchTestsMemoryWaitMinutes,
        EndedLauncherTabMinutes = s.EndedLauncherTabMinutes,
        WorkspaceRoot = WorkspaceOverrideOf(s),
        WorktreeRoot = s.WorktreeRoot,
        MemberUntil = s.MemberUntil,
        Gateway = OrchestratorGatewayVaultSettings.From(s.Gateway ?? new GatewaySettings()),
    };

    /// <summary><paramref name="s"/> with these values in place of its own; everything the Vault doesn't own (the legacy maps, the
    /// per-repo maps, the API credit, the gateway's teammates) is kept as it is. The alert recipient and the workspace root are
    /// resolved against <paramref name="global"/> (M.ORCH-T1328): blank follows Vault Global, set overrides it; a null
    /// <paramref name="global"/> is a Global with nothing set.</summary>
    public OrchestratorSettings ApplyTo(OrchestratorSettings s, MindAtticGlobalSettings? global = null) =>
        WithWorkspaceRoot(WithPsstAlertTo(ApplyOwn(s), PsstAlertTo, global), WorkspaceRoot, global);

    private OrchestratorSettings ApplyOwn(OrchestratorSettings s) => s with
    {
        BlockBudget = BlockBudget,
        WeeklyBudget = WeeklyBudget,
        WeeklyReset = string.IsNullOrWhiteSpace(WeeklyReset) ? null : WeeklyReset,
        WarnPercent = WarnPercent,
        WeeklyReservePercent = WeeklyReservePercent,
        Pacing = Pacing,
        PaceTargetPercent = PaceTargetPercent,
        PaceGuardStartPercent = PaceGuardStartPercent,
        ExemptCommands = [.. ExemptCommands ?? []],
        ReportFreshMinutes = ReportFreshMinutes,
        ProviderCapPercent = ProviderCapPercent,
        WeeklyAwareLinePercent = WeeklyAwareLinePercent,
        PaceCurveK = PaceCurveK,
        MaxAgentDelaySeconds = MaxAgentDelaySeconds,
        AgentAbort = AgentAbort,
        AgentAbortAfterMinutes = AgentAbortAfterMinutes,
        Allocate = Allocate,
        MaxConcurrentSessions = MaxConcurrentSessions,
        MaxSessionsPerRepo = MaxSessionsPerRepo,
        MaxSubagents = MaxSubagents,
        DefaultProvider = string.IsNullOrWhiteSpace(DefaultProvider) ? s.DefaultProvider : DefaultProvider,
        KeepGoing = KeepGoing,
        ResumeAfterReset = ResumeAfterReset,
        QuietHours = [.. QuietHours ?? []],
        AllocateSkip = [.. AllocateSkip ?? []],
        AllocateHandMinutes = AllocateHandMinutes,
        StaleAfterHours = StaleAfterHours,
        ContextCyclePercent = ContextCyclePercent,
        OrphanRedispatches = OrphanRedispatches,
        OrphanQuietMinutes = OrphanQuietMinutes,
        InboxAlerts = InboxAlerts,
        EditClaims = string.IsNullOrWhiteSpace(EditClaims) ? s.EditClaims : EditClaims,
        EditClaimMinutes = EditClaimMinutes,
        AutoLogin = AutoLogin,
        RebalanceLogins = RebalanceLogins,
        ReviewDiffLines = ReviewDiffLines,
        RiskyFiles = [.. RiskyFiles ?? []],
        NotifyUrl = string.IsNullOrWhiteSpace(NotifyUrl) ? null : NotifyUrl,
        DiskWarnGb = DiskWarnGb,
        DiskCriticalGb = DiskCriticalGb,
        DiskCleanupGb = DiskCleanupGb,
        DiskHoldGb = DiskHoldGb,
        DiskHoldResumeMarginGb = DiskHoldResumeMarginGb,
        TempRetentionHours = TempRetentionHours,
        ScratchLeaseHours = ScratchLeaseHours,
        DaemonRestartSeconds = DaemonRestartSeconds,
        DaemonRestartMaxSeconds = DaemonRestartMaxSeconds,
        DbBusyRetries = DbBusyRetries,
        ReapEverySeconds = ReapEverySeconds,
        ReapMaxHandles = ReapMaxHandles,
        ReapSearchMaxMinutes = ReapSearchMaxMinutes,
        TermHostCleanupMinutes = TermHostCleanupMinutes,
        TermHostCleanupRetryMinutes = TermHostCleanupRetryMinutes,
        CliMarksPruneHours = CliMarksPruneHours,
        SessionTempPruneMinutes = SessionTempPruneMinutes,
        MirrorSeconds = MirrorSeconds,
        TicketsDbSyncMinutes = TicketsDbSyncMinutes,
        OrchestratorDbSyncMinutes = OrchestratorDbSyncMinutes,
        MemorySampleSeconds = MemorySampleSeconds,
        MemorySettleSeconds = MemorySettleSeconds,
        HealthSampleSeconds = HealthSampleSeconds,
        PagefileCheckHours = PagefileCheckHours,
        LogPruneMinutes = LogPruneMinutes,
        DiskCleanupEveryMinutes = DiskCleanupEveryMinutes,
        GitHubIntakeMinutes = GitHubIntakeMinutes,
        DashboardPollSeconds = DashboardPollSeconds,
        DashboardHiddenPollSeconds = DashboardHiddenPollSeconds,
        DashboardHiddenHolderPollSeconds = DashboardHiddenHolderPollSeconds,
        TermResyncSeconds = TermResyncSeconds,
        LauncherRespawnSeconds = LauncherRespawnSeconds,
        ElevatedTermWaitSeconds = ElevatedTermWaitSeconds,
        MeterStatesSeconds = MeterStatesSeconds,
        TestTermHostSweepMinutes = TestTermHostSweepMinutes,
        OrgReachSpacingMinutes = OrgReachSpacingMinutes,
        ApiCostReadMinutes = ApiCostReadMinutes,
        UtilizationMinSpacingSeconds = UtilizationMinSpacingSeconds,
        SyncTimeoutSeconds = SyncTimeoutSeconds,
        RefreshMinGapSeconds = RefreshMinGapSeconds,
        RemoteBranchBuilds = RemoteBranchBuilds,
        BranchBuildsMaxRunning = BranchBuildsMaxRunning,
        BranchBuildsTimeoutMinutes = BranchBuildsTimeoutMinutes,
        BranchBuildsCheckSeconds = BranchBuildsCheckSeconds,
        BranchQueueFetchMinutes = BranchQueueFetchMinutes,
        MemberConnectedMinutes = MemberConnectedMinutes,
        HubMemberSessions = HubMemberSessions,
        HubTrustedOrg = HubTrustedOrg,
        HubMemberWindowsUsers = HubMemberWindowsUsers,
        HubConsentSeconds = HubConsentSeconds,
        HubMemberStartWaitSeconds = HubMemberStartWaitSeconds,
        HubConsentPollSeconds = HubConsentPollSeconds,
        TabBlinkMinSpacingMs = TabBlinkMinSpacingMs,
        ProjectsRefreshMinutes = ProjectsRefreshMinutes,
        ProjectChangesRefreshSeconds = ProjectChangesRefreshSeconds,
        ClosedTicketsReadSeconds = ClosedTicketsReadSeconds,
        ClosedTicketsReadLiveMinutes = ClosedTicketsReadLiveMinutes,
        MemoryGuard = MemoryGuard,
        HeavyProcessLimit = HeavyProcessLimit,
        MemoryGuardStartPercent = MemoryGuardStartPercent,
        MemoryTightGb = MemoryTightGb,
        MemoryCriticalGb = MemoryCriticalGb,
        MemoryEmergencyGb = MemoryEmergencyGb,
        MemoryPerSubagentGb = MemoryPerSubagentGb,
        MemoryTightCommitPercent = MemoryTightCommitPercent,
        MemoryCriticalCommitPercent = MemoryCriticalCommitPercent,
        MemoryEmergencyCommitPercent = MemoryEmergencyCommitPercent,
        MemoryResumeMarginGb = MemoryResumeMarginGb,
        MemoryResumeMarginCommit = MemoryResumeMarginCommit,
        SqlServerMaxMemoryMb = SqlServerMaxMemoryMb,
        AuditEveryHours = AuditEveryHours,
        GroomAfterDays = GroomAfterDays,
        LogKeepDays = LogKeepDays,
        TermLogKeepDays = TermLogKeepDays,
        GitHubIntake = GitHubIntake,
        SqlFreshnessCheck = SqlFreshnessCheck,
        SqlFreshnessCheckAt = string.IsNullOrWhiteSpace(SqlFreshnessCheckAt) ? OrchestratorSettings.DefaultSqlFreshnessCheckAt : SqlFreshnessCheckAt.Trim(),
        Theme = string.IsNullOrWhiteSpace(Theme) ? null : Theme,
        Font = string.IsNullOrWhiteSpace(Font) ? OrchestratorSettings.DefaultFont : Font,
        SymbolTabLabels = SymbolTabLabels,
        TabBlinkTokens = TabBlinkTokens,
        ExcludeLauncherFromTeamWall = ExcludeLauncherFromTeamWall,
        AutoOpenLaunchers = AutoOpenLaunchers,
        ConfirmCliClose = ConfirmCliClose,
        EndedTabMinutes = EndedTabMinutes,
        TestTermHostMaxMinutes = TestTermHostMaxMinutes,
        PresumeBusyConfirmSeconds = PresumeBusyConfirmSeconds,
        PresumeBusyMaxSeconds = PresumeBusyMaxSeconds,
        ActivityStaleMinutes = ActivityStaleMinutes,
        ActivityCoalesceSeconds = ActivityCoalesceSeconds,
        ActivityRescanSeconds = ActivityRescanSeconds,
        ActivityEndedMinutes = ActivityEndedMinutes,
        MainTestsLockRetries = MainTestsLockRetries,
        MainTestsLockRetrySeconds = MainTestsLockRetrySeconds,
        MainTestsTimeoutMinutes = MainTestsTimeoutMinutes,
        BranchTestsMemoryWaitMinutes = BranchTestsMemoryWaitMinutes,
        EndedLauncherTabMinutes = EndedLauncherTabMinutes,
        WorktreeRoot = string.IsNullOrWhiteSpace(WorktreeRoot) ? s.WorktreeRoot : WorktreeRoot,
        MemberUntil = MemberUntil ?? "",
        Gateway = (Gateway ?? new()).ApplyTo(s.Gateway ?? new GatewaySettings()),
    };

    // ── Global default, app override (M.VALT-T77, M.ORCH-T1328) ───────────

    /// <summary>The alert-recipient override that texts nobody (blank follows Vault Global instead).</summary>
    public const string NoAlerts = "none";

    /// <summary><paramref name="s"/> with its alert recipient overridden by <paramref name="vaultValue"/> as Vault.Desktop would set
    /// it: blank follows <paramref name="global"/>'s alert recipient (else <see cref="OrchestratorSettings.DefaultPsstAlertTo"/>),
    /// <see cref="NoAlerts"/> (or "off") texts nobody, anything else is the recipient. <see cref="OrchestratorSettings.PsstAlertTo"/>
    /// gets the resolved recipient (what the daemon and the hooks' row use) and the override is kept beside it.</summary>
    public static OrchestratorSettings WithPsstAlertTo(OrchestratorSettings s, string? vaultValue, MindAtticGlobalSettings? global)
    {
        var value = vaultValue ?? "";
        var resolved = IsNoAlerts(value) ? ""
            : MindAtticGlobalSettings.Resolve(value, global, g => g.AlertRecipient) ?? OrchestratorSettings.DefaultPsstAlertTo;
        // Kept only when the resolved value alone wouldn't say it (e.g. blank while Global names someone else), so a value set
        // straight on PsstAlertTo, and every row from before, still reads as its own override.
        return s with { PsstAlertTo = resolved, PsstAlertToOverride = AlertOverrideFromResolved(resolved) == value ? null : value };
    }

    /// <summary><paramref name="s"/> with its workspace root overridden by <paramref name="vaultValue"/>: blank follows
    /// <paramref name="global"/>'s workspace root (else null, MindAttic.Tickets's default). <see cref="OrchestratorSettings.WorkspaceRoot"/>
    /// gets the resolved folder and the override is kept beside it.</summary>
    public static OrchestratorSettings WithWorkspaceRoot(OrchestratorSettings s, string? vaultValue, MindAtticGlobalSettings? global)
    {
        var value = string.IsNullOrWhiteSpace(vaultValue) ? null : vaultValue;
        var resolved = MindAtticGlobalSettings.Resolve(value, global, g => g.WorkspaceRoot);
        return s with { WorkspaceRoot = resolved, WorkspaceRootOverride = WorkspaceOverrideFromResolved(resolved) == value ? null : value ?? "" };
    }

    /// <summary>The orchestrator's own alert-recipient override in <paramref name="s"/>: blank follows Vault Global.</summary>
    public static string AlertOverrideOf(OrchestratorSettings s) => s.PsstAlertToOverride ?? AlertOverrideFromResolved(s.PsstAlertTo);

    /// <summary>The orchestrator's own workspace-root override in <paramref name="s"/>: null follows Vault Global.</summary>
    public static string? WorkspaceOverrideOf(OrchestratorSettings s) =>
        s.WorkspaceRootOverride is { } o ? (string.IsNullOrWhiteSpace(o) ? null : o) : WorkspaceOverrideFromResolved(s.WorkspaceRoot);

    /// <summary>Where a resolved value came from, for the CLI: the orchestrator's own override, Vault Global, or the default.</summary>
    public static string SourceOf(string? appOverride, string? globalValue) =>
        !string.IsNullOrWhiteSpace(appOverride) ? "the Orchestrator's own override in Vault"
        : !string.IsNullOrWhiteSpace(globalValue) ? "Vault Global" : "the default";

    private static bool IsNoAlerts(string value) =>
        value.Trim().Equals(NoAlerts, StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("off", StringComparison.OrdinalIgnoreCase);

    // A resolved value read as an override (a row from before, a value set straight on the setting): nobody is "none", the
    // default recipient is "follow Global" (whose own fallback is that default), anything else overrides.
    private static string AlertOverrideFromResolved(string? to) =>
        to is null ? ""
        : string.IsNullOrWhiteSpace(to) ? NoAlerts
        : to.Trim().Equals(OrchestratorSettings.DefaultPsstAlertTo, StringComparison.OrdinalIgnoreCase) ? ""
        : to;

    private static string? WorkspaceOverrideFromResolved(string? root) => string.IsNullOrWhiteSpace(root) ? null : root;

    /// <summary>JSON exactly as the Vault store writes it (indented, camelCase): what's compared and merged.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>To json.</summary>
    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, JsonOptions)!;

    /// <summary>From json.</summary>
    public static OrchestratorVaultSettings FromJson(JsonNode node) => node.Deserialize<OrchestratorVaultSettings>(JsonOptions) ?? new();

    /// <summary>True when both hold the same values.</summary>
    public bool SameAs(OrchestratorVaultSettings other) => JsonNode.DeepEquals(ToJson(), other.ToJson());

    /// <summary>
    /// A three-way merge, field by field: <paramref name="vault"/>'s value, except where the database row changed a field since
    /// the last time the two were synced (<paramref name="row"/> differs from <paramref name="synced"/>) and the Vault didn't
    /// (<paramref name="vault"/> still equals it). That is how a write the orchestrator made without going through the Vault (a
    /// copy from before LAMP still running while the new one starts, a hand edit of the row) still lands, while an edit made in
    /// Vault.Desktop always wins over the stale row. Nested objects merge per field; lists and values are one field each.
    /// </summary>
    public static JsonObject Merge(JsonObject? synced, JsonObject row, JsonObject vault)
    {
        if (synced is null) return (JsonObject)vault.DeepClone();
        var merged = new JsonObject();
        foreach (var (name, vaultValue) in vault)
        {
            var rowValue = row[name];
            var syncedValue = synced[name];
            if (vaultValue is JsonObject v && rowValue is JsonObject r)
            {
                merged[name] = Merge(syncedValue as JsonObject ?? (JsonObject)v.DeepClone(), r, v);
                continue;
            }
            var rowChanged = !JsonNode.DeepEquals(rowValue, syncedValue) && row.ContainsKey(name);
            var vaultChanged = !JsonNode.DeepEquals(vaultValue, syncedValue);
            merged[name] = (rowChanged && !vaultChanged ? rowValue : vaultValue)?.DeepClone();
        }
        return merged;
    }

    /// <summary>What the dashboard's cog calls: MindAttic.Vault.Desktop at the Orchestrator tab, optionally scrolled to one setting
    /// (its full name, e.g. <c>MindAttic:Settings:Orchestrator:maxConcurrentSessions</c>). False when Vault.Desktop isn't installed
    /// and isn't running.</summary>
    public static bool OpenInVault(string? setting = null) => VaultSettings.Open(AppId, setting);
}

/// <summary>The team gateway's settings (M.ORCH-T933) in the Vault: everything but its teammates' list, which holds sealed tokens.</summary>
public sealed class OrchestratorGatewayVaultSettings
{
    /// <summary>Off by default.</summary>
    [Setting("Enabled", Group = "Gateway", Order = 1, Help = "Off by default. On, the daemon starts the gateway and keeps it running.")]
    public bool Enabled { get; set; }

    /// <summary>127.0.0.1 exposes nothing; a Tailscale or VPN address (or 0.0.0.0) lets teammates in.</summary>
    [Setting("Listen on", Group = "Gateway", Order = 2,
        Help = "127.0.0.1 exposes nothing; a Tailscale or VPN address (or 0.0.0.0) lets teammates in.")]
    public string Bind { get; set; } = "127.0.0.1";

    /// <summary>Port.</summary>
    [Setting("Port", Group = "Gateway", Order = 3)]
    [SettingRange(1, 65535)]
    public int Port { get; set; } = Term.TermGateway.DefaultPort;

    /// <summary>A PFX certificate to serve; its password comes from MINDATTIC_GATEWAY_CERT_PASSWORD, never from here.</summary>
    [Setting("Certificate file (PFX)", Group = "Gateway", Order = 4,
        Help = "A PFX certificate to serve; its password comes from MINDATTIC_GATEWAY_CERT_PASSWORD, never from here.")]
    [PathPicker(PathKind.File, Filter = "PFX certificates|*.pfx;*.p12")]
    public string? CertPath { get; set; }

    /// <summary>Or a certificate in CurrentUser\My or LocalMachine\My.</summary>
    [Setting("Certificate thumbprint", Group = "Gateway", Order = 5,
        Help = "Or a certificate in CurrentUser\\My or LocalMachine\\My. With neither, a self-signed dev certificate that clients pin.")]
    public string? CertThumbprint { get; set; }

    /// <summary>Extra DNS names or IPs the dev certificate covers (a Tailscale MagicDNS name or 100.x address).</summary>
    [Setting("Dev certificate names", Group = "Gateway", Order = 6, Help = "Extra DNS names or IPs the dev certificate covers (a Tailscale MagicDNS name or 100.x address).")]
    public List<string> DevCertNames { get; set; } = [];

    /// <summary>The machine owner's identity, its tokens' subject.</summary>
    [Setting("Owner", Group = "Gateway", Order = 7, Help = "The machine owner's identity, its tokens' subject. Empty is the Windows user name.")]
    public string? Owner { get; set; }

    /// <summary>How long a gateway sign-in lasts, 1 to 60 minutes.</summary>
    [Setting("Sign-in lifetime (minutes)", Group = "Gateway", Order = 8, Help = "How long a gateway sign-in lasts, 1 to 60 minutes.")]
    [SettingRange(1, 60)]
    public int TokenMinutes { get; set; } = 15;

    /// <summary>From.</summary>
    public static OrchestratorGatewayVaultSettings From(GatewaySettings g) => new()
    {
        Enabled = g.Enabled,
        Bind = g.Bind,
        Port = g.Port,
        CertPath = g.CertPath,
        CertThumbprint = g.CertThumbprint,
        DevCertNames = [.. g.DevCertNames],
        Owner = g.Owner,
        TokenMinutes = g.TokenMinutes,
    };

    /// <summary>Applies the to.</summary>
    public GatewaySettings ApplyTo(GatewaySettings g) => g with
    {
        Enabled = Enabled,
        Bind = string.IsNullOrWhiteSpace(Bind) ? g.Bind : Bind,
        Port = Port,
        CertPath = string.IsNullOrWhiteSpace(CertPath) ? null : CertPath,
        CertThumbprint = string.IsNullOrWhiteSpace(CertThumbprint) ? null : CertThumbprint,
        DevCertNames = [.. DevCertNames ?? []],
        Owner = string.IsNullOrWhiteSpace(Owner) ? null : Owner,
        TokenMinutes = TokenMinutes,
    };
}
