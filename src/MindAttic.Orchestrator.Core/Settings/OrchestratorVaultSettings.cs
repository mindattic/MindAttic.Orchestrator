using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Vault.Launch;
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
[SettingsApp(AppId, "MindAttic Orchestrator", Local = true, Icon = "dashboard",
    Description = "Paces, schedules and hosts the workspace's Claude CLIs: budgets, pacing, allocate, logins, alerts and the team gateway.")]
[UsesCredential("claude", Purpose = "The API billing mode and the API key person: the Claude key CLIs bill when they run on API credits")]
public sealed class OrchestratorVaultSettings
{
    /// <summary>The Vault app id: the folder under <c>%LOCALAPPDATA%\MindAttic\</c> and the <c>MindAttic:Settings:Orchestrator</c> section.</summary>
    public const string AppId = "Orchestrator";

    // ── Budget ─────────────────────────────────────────────────────────────

    [Setting("Tokens per 5-hour block", Group = "Budget", Order = 1,
        Help = "Uncached tokens (input + cache writes + output). Empty uses Claude's own reported limit, else the limits learned from the times you hit the wall.")]
    [SettingRange(1, 1e15)]
    public long? BlockBudget { get; set; }

    [Setting("Tokens per week", Group = "Budget", Order = 2, Help = "Uncached tokens per week. Empty means no weekly line.")]
    [SettingRange(1, 1e15)]
    public long? WeeklyBudget { get; set; }

    [Setting("Week resets (local time)", Group = "Budget", Order = 3,
        Help = "When Claude's weekly limit restarts, e.g. \"Monday 09:00\". Claude's own reported reset comes first; empty uses the reset your last weekly limit hit reported, or a rolling 7 days.")]
    public string? WeeklyReset { get; set; }

    [Setting("Warn at (%)", Group = "Budget", Order = 4, Help = "Percent of a budget at which the dashboard warns. Without pacing, the scheduler also stops proposing here.")]
    [SettingRange(1, 100)]
    public int WarnPercent { get; set; } = 80;

    [Setting("Weekly reserve (%)", Group = "Budget", Order = 5,
        Help = "The share of each account's week kept back for your own questions: every CLI on an account stops at 100 minus this of its week (10 stops at 90%).")]
    [SettingRange(0, OrchestratorSettings.MaxWeeklyReservePercent)]
    public int WeeklyReservePercent { get; set; } = OrchestratorSettings.DefaultWeeklyReservePercent;

    // ── Pacing ─────────────────────────────────────────────────────────────

    [Setting("Pace to the reset", Group = "Pacing", Order = 1,
        Help = "Caps new sessions so spend lands on the pace target as the 5-hour window resets, instead of hitting the wall. Off: new work holds at the warn line.")]
    public bool Pacing { get; set; } = true;

    [Setting("Pace target (%)", Group = "Pacing", Order = 2, Help = "How much of each 5-hour limit pacing aims to use by its reset.")]
    [SettingRange(1, 100)]
    public int PaceTargetPercent { get; set; } = 98;

    [Setting("Exempt from pacing", Group = "Pacing", Order = 3,
        Help = "Slash commands whose turns skip every pace gate (no hold, no subagent wait, no stop); their spend still counts. Empty exempts none.")]
    public List<string> ExemptCommands { get; set; } = [.. OrchestratorSettings.DefaultExemptCommands];

    [Setting("Claude's report counts as fresh for (minutes)", Group = "Pacing", Order = 4,
        Help = "Past this age, the local tokens spent since Claude's last 5-hour report are added back in.")]
    [SettingRange(0, 1440)]
    public int ReportFreshMinutes { get; set; } = 2;

    [Setting("Other providers hold at (%)", Group = "Pacing", Order = 5,
        Help = "For a provider other than Claude: its own reported 5-hour use at which dispatching to it holds. 0 means never.")]
    [SettingRange(0, 100)]
    public int ProviderCapPercent { get; set; } = 90;

    [Setting("Weekly-aware take-in line (%)", Group = "Pacing", Order = 6,
        Help = "Weekly-aware pacing is locked off; kept for when it returns: the week's percent from which it would take the week in.")]
    [SettingRange(0, 100)]
    public int WeeklyAwareLinePercent { get; set; } = OrchestratorSettings.DefaultWeeklyAwareLinePercent;

    [Setting("Front-loaded curve exponent", Group = "Pacing", Order = 7,
        Help = "Pace profiles are locked to linear; kept for when they return: under 1 bends the curve up early, 1 is a straight line.")]
    [SettingRange(0.1, 1)]
    public double PaceCurveK { get; set; } = 0.55;

    [Setting("Longest agent hold (seconds)", Group = "Pacing", Order = 8,
        Help = "Agent throttling is locked off; kept for when it returns: the longest one tool call would be held.")]
    [SettingRange(0, 3600)]
    public int MaxAgentDelaySeconds { get; set; } = 300;

    [Setting("Cool off by aborting subagents", Group = "Pacing", Order = 9,
        Help = "Last resort: when a login's live 5-hour trend stays past its pace line with new subagents already slowed, abort just enough of its ticket subagents to bend it back under.")]
    public bool AgentAbort { get; set; } = true;

    [Setting("Abort after (minutes past the line)", Group = "Pacing", Order = 10,
        Help = "How long the new-start cool-off gets to bend the trend back under before the abort steps in.")]
    [SettingRange(0, 60)]
    public int AgentAbortAfterMinutes { get; set; } = 3;

    // ── Scheduler ──────────────────────────────────────────────────────────

    [Setting("Allocate", Group = "Scheduler", Order = 1,
        Help = "The master switch for /allocate: off pauses it in every CLI where it was typed (their marks stay). /allocate on and off change it too.")]
    public bool Allocate { get; set; } = true;

    [Setting("Max concurrent sessions", Group = "Scheduler", Order = 2, Help = "Sessions the scheduler fills up to (working + waiting).")]
    [SettingRange(1, 1000)]
    public int MaxConcurrentSessions { get; set; } = 4;

    [Setting("Sessions per repo", Group = "Scheduler", Order = 3, Help = "Sessions one repo may run at once (orchestrator settings --repo-sessions overrides it per repo).")]
    [SettingRange(0, 1000)]
    public int MaxSessionsPerRepo { get; set; } = 2;

    [Setting("Max subagents", Group = "Scheduler", Order = 4,
        Help = "The most subagents running at once across every CLI; under it the pace decides, live. 0 holds every new one.")]
    [SettingRange(0, 1000)]
    public int MaxSubagents { get; set; } = 16;

    [Setting("Provider for dispatched tickets", Group = "Scheduler", Order = 5)]
    [SettingChoices("Claude", "Codex", "Gemini", "Kimi")]
    public string DefaultProvider { get; set; } = "Claude";

    [Setting("Keep going", Group = "Scheduler", Order = 6,
        Help = "A CLI whose turn ends asking whether to go on while its repo still has open tickets is told to carry on. Off lets it stop and ask.")]
    public bool KeepGoing { get; set; } = true;

    [Setting("Resume after a reset", Group = "Scheduler", Order = 7,
        Help = "Once the window a CLI stopped for has reset, it's told to carry on. Off only alerts you that it's waiting.")]
    public bool ResumeAfterReset { get; set; } = true;

    [Setting("Quiet hours", Group = "Scheduler", Order = 8, Help = "Local-time windows when allocate pauses, like 22:00-07:00. Manual starts still work.")]
    public List<string> QuietHours { get; set; } = [];

    [Setting("Repos allocate skips", Group = "Scheduler", Order = 9, Help = "Repos /allocate never starts agents in, even when named or their CLI is open.")]
    public List<string> AllocateSkip { get; set; } = [];

    [Setting("Worked by hand for (minutes)", Group = "Scheduler", Order = 10,
        Help = "Minutes since a repo's own CLI last ended a turn that it still counts as worked by hand. 0 counts only a CLI that's mid-turn.")]
    [SettingRange(0, 1440)]
    public int AllocateHandMinutes { get; set; } = 15;

    [Setting("Stale after (hours)", Group = "Scheduler", Order = 11,
        Help = "Hours with no activity where a ticket was dispatched before it goes back in the queue. 0 keeps it until the ticket closes.")]
    [SettingRange(0, 100000)]
    public int StaleAfterHours { get; set; } = 12;

    [Setting("Cycle context at (%)", Group = "Scheduler", Order = 12, Help = "Context fill at which a session is wrapped up and cycled.")]
    [SettingRange(10, 95)]
    public int ContextCyclePercent { get; set; } = 80;

    [Setting("Orphan redispatches", Group = "Scheduler", Order = 13,
        Help = "How many times a ticket whose CLI or tab exited mid-task is started again before it's left for you. 0 only shows it as orphaned.")]
    [SettingRange(0, 20)]
    public int OrphanRedispatches { get; set; } = 3;

    [Setting("Orphan quiet minutes", Group = "Scheduler", Order = 14,
        Help = "How long a dispatch's CLI or tab must be gone, with nothing working its ticket, before the ticket counts as orphaned.")]
    [SettingRange(1, 1440)]
    public int OrphanQuietMinutes { get; set; } = 10;

    // ── Logins ─────────────────────────────────────────────────────────────

    [Setting("One CLI per account (Auto login)", Group = "Logins", Order = 1,
        Help = "A new Claude tab with no login chosen goes on a signed-in per-CLI login no CLI runs on yet, most headroom first, else the global login.")]
    public bool AutoLogin { get; set; } = true;

    [Setting("Rebalance when an account runs low", Group = "Logins", Order = 2,
        Help = "A CLI whose login crosses its pace target or weekly stop line moves to a signed-in per-CLI login with more headroom at its next idle point.")]
    public bool RebalanceLogins { get; set; } = true;

    // ── Reviews ────────────────────────────────────────────────────────────

    [Setting("Review diffs over (lines)", Group = "Reviews", Order = 1,
        Help = "Lines changed past which a Sonnet ticket's diff gets one Opus review before it lands. 0 reviews every one; -1 none for size.")]
    [SettingRange(-1, 1000000)]
    public int ReviewDiffLines { get; set; } = 300;

    [Setting("Risky files", Group = "Reviews", Order = 2,
        Help = "Wildcards (* and ?) for files whose change by a Sonnet ticket always gets that review: the gates, pacing and git landing.")]
    public List<string> RiskyFiles { get; set; } = [.. OrchestratorSettings.DefaultRiskyFiles];

    // ── Alerts ─────────────────────────────────────────────────────────────

    [Setting("Text cutoff alerts to", Group = "Alerts", Order = 1,
        Help = "Who's texted through Psst when a login reaches its own cutoff: a Psst contact (ryan) or a US number. Empty texts nobody.")]
    public string PsstAlertTo { get; set; } = OrchestratorSettings.DefaultPsstAlertTo;

    [Setting("Push alerts to", Group = "Alerts", Order = 2, Help = "A URL that takes a plain-text POST, like an ntfy.sh topic, for phone pushes. Empty sends nothing.")]
    [UrlSetting]
    public string? NotifyUrl { get; set; }

    [Setting("Disk warning (free GB)", Group = "Alerts", Order = 3, Help = "Free GB on the system or worktree drive under which the daemon alerts. 0 turns the watch off.")]
    [SettingRange(0, 100000)]
    public double DiskWarnGb { get; set; } = 20;

    [Setting("Disk critical (free GB)", Group = "Alerts", Order = 4, Help = "Free GB under which the disk alert is urgent.")]
    [SettingRange(0, 100000)]
    public double DiskCriticalGb { get; set; } = 5;

    [Setting("Audit every (hours)", Group = "Alerts", Order = 5, Help = "Hours between the audits the daemon runs by itself. 0 means only on demand.")]
    [SettingRange(0, 100000)]
    public int AuditEveryHours { get; set; } = 24;

    [Setting("Groom tickets older than (days)", Group = "Alerts", Order = 6, Help = "Open tickets older than this with nobody on them are listed for grooming. 0 means never.")]
    [SettingRange(0, 100000)]
    public int GroomAfterDays { get; set; } = 30;

    [Setting("GitHub intake", Group = "Alerts", Order = 7,
        Help = "Hourly, file GitHub issues labelled \"orchestrator\" and CI runs that failed on main as tickets.")]
    public bool GitHubIntake { get; set; }

    // ── Dashboard ──────────────────────────────────────────────────────────

    [Setting("Theme", Group = "Dashboard", Order = 1, Help = "The dashboard's theme; its sun/moon switch changes this too. Empty is dark.")]
    [SettingChoices("Dark", "Light")]
    public string? Theme { get; set; }

    [Setting("Symbols in tab labels (Σ/λ)", Group = "Dashboard", Order = 2,
        Help = "On: \"Σ: 15% | λ: 29%\". Off: \"wk: 15% | 5h: 29%\". Same figures either way.")]
    public bool SymbolTabLabels { get; set; } = true;

    [Setting("Blink once per (tokens)", Group = "Dashboard", Order = 3,
        Help = "A busy login's green play arrow blinks once for every this many tokens it spends, so the busiest blink fastest.")]
    [SettingRange(OrchestratorSettings.MinTabBlinkTokens, OrchestratorSettings.MaxTabBlinkTokens)]
    public int TabBlinkTokens { get; set; } = OrchestratorSettings.DefaultTabBlinkTokens;

    [Setting("Exclude Launcher from Team Wall", Group = "Dashboard", Order = 4,
        Help = "Off (the default): the Launcher's own menu tab shows as a tile on the Team Wall like any other CLI.")]
    public bool ExcludeLauncherFromTeamWall { get; set; }

    // ── Folders ────────────────────────────────────────────────────────────

    [Setting("Workspace root", Group = "Folders", Order = 1,
        Help = "The folder holding every repo. Empty uses MindAttic.Tickets's default.")]
    [PathPicker(PathKind.Folder)]
    public string? WorkspaceRoot { get; set; }

    [Setting("Tools' worktree root", Group = "Folders", Order = 2,
        Help = "Where the tools' own clean worktrees of origin/main live, outside the workspace root.")]
    [PathPicker(PathKind.Folder)]
    public string WorktreeRoot { get; set; } = @"D:\Projects\MindAttic.worktrees";

    // ── Gateway ────────────────────────────────────────────────────────────

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
        AutoLogin = s.AutoLogin,
        RebalanceLogins = s.RebalanceLogins,
        ReviewDiffLines = s.ReviewDiffLines,
        RiskyFiles = [.. s.RiskyFiles],
        // A null in the row (never written) reads as the default recipient; blank is "nobody".
        PsstAlertTo = s.PsstAlertTo ?? OrchestratorSettings.DefaultPsstAlertTo,
        NotifyUrl = s.NotifyUrl,
        DiskWarnGb = s.DiskWarnGb,
        DiskCriticalGb = s.DiskCriticalGb,
        AuditEveryHours = s.AuditEveryHours,
        GroomAfterDays = s.GroomAfterDays,
        GitHubIntake = s.GitHubIntake,
        Theme = s.Theme,
        SymbolTabLabels = s.SymbolTabLabels,
        TabBlinkTokens = s.TabBlinkTokens,
        ExcludeLauncherFromTeamWall = s.ExcludeLauncherFromTeamWall,
        WorkspaceRoot = s.WorkspaceRoot,
        WorktreeRoot = s.WorktreeRoot,
        Gateway = OrchestratorGatewayVaultSettings.From(s.Gateway ?? new GatewaySettings()),
    };

    /// <summary><paramref name="s"/> with these values in place of its own; everything the Vault doesn't own (the legacy maps, the
    /// per-repo maps, the API credit, the gateway's teammates) is kept as it is.</summary>
    public OrchestratorSettings ApplyTo(OrchestratorSettings s) => s with
    {
        BlockBudget = BlockBudget,
        WeeklyBudget = WeeklyBudget,
        WeeklyReset = string.IsNullOrWhiteSpace(WeeklyReset) ? null : WeeklyReset,
        WarnPercent = WarnPercent,
        WeeklyReservePercent = WeeklyReservePercent,
        Pacing = Pacing,
        PaceTargetPercent = PaceTargetPercent,
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
        AutoLogin = AutoLogin,
        RebalanceLogins = RebalanceLogins,
        ReviewDiffLines = ReviewDiffLines,
        RiskyFiles = [.. RiskyFiles ?? []],
        // Cleared in Vault (null) means nobody, as a blank always did.
        PsstAlertTo = PsstAlertTo ?? "",
        NotifyUrl = string.IsNullOrWhiteSpace(NotifyUrl) ? null : NotifyUrl,
        DiskWarnGb = DiskWarnGb,
        DiskCriticalGb = DiskCriticalGb,
        AuditEveryHours = AuditEveryHours,
        GroomAfterDays = GroomAfterDays,
        GitHubIntake = GitHubIntake,
        Theme = string.IsNullOrWhiteSpace(Theme) ? null : Theme,
        SymbolTabLabels = SymbolTabLabels,
        TabBlinkTokens = TabBlinkTokens,
        ExcludeLauncherFromTeamWall = ExcludeLauncherFromTeamWall,
        WorkspaceRoot = string.IsNullOrWhiteSpace(WorkspaceRoot) ? null : WorkspaceRoot,
        WorktreeRoot = string.IsNullOrWhiteSpace(WorktreeRoot) ? s.WorktreeRoot : WorktreeRoot,
        Gateway = (Gateway ?? new()).ApplyTo(s.Gateway ?? new GatewaySettings()),
    };

    /// <summary>JSON exactly as the Vault store writes it (indented, camelCase): what's compared and merged.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, JsonOptions)!;

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
    [Setting("Enabled", Group = "Gateway", Order = 1, Help = "Off by default. On, the daemon starts the gateway and keeps it running.")]
    public bool Enabled { get; set; }

    [Setting("Listen on", Group = "Gateway", Order = 2,
        Help = "127.0.0.1 exposes nothing; a Tailscale or VPN address (or 0.0.0.0) lets teammates in.")]
    public string Bind { get; set; } = "127.0.0.1";

    [Setting("Port", Group = "Gateway", Order = 3)]
    [SettingRange(1, 65535)]
    public int Port { get; set; } = Term.TermGateway.DefaultPort;

    [Setting("Certificate file (PFX)", Group = "Gateway", Order = 4,
        Help = "A PFX certificate to serve; its password comes from MINDATTIC_GATEWAY_CERT_PASSWORD, never from here.")]
    [PathPicker(PathKind.File, Filter = "PFX certificates|*.pfx;*.p12")]
    public string? CertPath { get; set; }

    [Setting("Certificate thumbprint", Group = "Gateway", Order = 5,
        Help = "Or a certificate in CurrentUser\\My or LocalMachine\\My. With neither, a self-signed dev certificate that clients pin.")]
    public string? CertThumbprint { get; set; }

    [Setting("Dev certificate names", Group = "Gateway", Order = 6, Help = "Extra DNS names or IPs the dev certificate covers (a Tailscale MagicDNS name or 100.x address).")]
    public List<string> DevCertNames { get; set; } = [];

    [Setting("Owner", Group = "Gateway", Order = 7, Help = "The machine owner's identity, its tokens' subject. Empty is the Windows user name.")]
    public string? Owner { get; set; }

    [Setting("Sign-in lifetime (minutes)", Group = "Gateway", Order = 8, Help = "How long a gateway sign-in lasts, 1 to 60 minutes.")]
    [SettingRange(1, 60)]
    public int TokenMinutes { get; set; } = 15;

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
