using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Settings;

/// <summary>What you tell the orchestrator. Everything has a working default.</summary>
public sealed record OrchestratorSettings
{
    /// <summary>The folder holding every repo. Null uses MindAttic.Tickets's default; the first run
    /// asks for one when that doesn't exist on this machine.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>Uncached tokens (input + cache write + output) allowed per 5-hour usage block.
    /// Null measures blocks against your own peak block in the last 7 days instead.</summary>
    public long? BlockBudget { get; init; }

    /// <summary>Uncached tokens allowed per week. Null means no weekly line.</summary>
    public long? WeeklyBudget { get; init; }

    /// <summary>When the week resets, as "Monday 09:00" local time (see <see cref="Budget.WeeklyReset"/>).
    /// Null means the week is a rolling 7 days.</summary>
    public string? WeeklyReset { get; init; }

    /// <summary>Percent of a budget at which the dashboard warns. Without pacing, the scheduler also stops proposing here.</summary>
    public int WarnPercent { get; init; } = 80;

    /// <summary>On: with a limit to pace against (set or learned), the scheduler caps sessions so
    /// spend lands on <see cref="PaceTargetPercent"/> at the reset instead of holding at the warn line.</summary>
    public bool Pacing { get; init; } = true;

    /// <summary>How much of each limit pacing aims to use by the reset; the rest is margin for usage
    /// the orchestrator can't see (claude.ai, other machines).</summary>
    public int PaceTargetPercent { get; init; } = 98;

    /// <summary>The share of each account's week kept back (M.ORCH-T609): every CLI on an account stops at
    /// <see cref="WeeklyStopPercent"/> (100 minus this) of that account's week instead of burning it all. 0 to
    /// <see cref="MaxWeeklyReservePercent"/>; applies to each login on its own.</summary>
    public int WeeklyReservePercent { get; init; } = DefaultWeeklyReservePercent;

    public const int DefaultWeeklyReservePercent = 10, MaxWeeklyReservePercent = 50;

    /// <summary>The percent of an account's week at which its CLIs stop: 100 minus <see cref="WeeklyReservePercent"/>
    /// (kept to 0–<see cref="MaxWeeklyReservePercent"/>, so a hand-edited value can't stop it at nothing). Weekly-aware pacing
    /// lands the week on this same line (<see cref="WeeklyAwareTarget"/>, M.ORCH-T680), so on or off it's one line.</summary>
    [JsonIgnore]
    public int WeeklyStopPercent => Math.Min(WeeklyStopCapPercent ?? 100, WeeklyStopFor(WeeklyReservePercent));

    public static int WeeklyStopFor(int reservePercent) => 100 - Math.Clamp(reservePercent, 0, MaxWeeklyReservePercent);

    /// <summary>Weekly-aware pacing (M.ORCH-T666), per login: each 5-hour window still aims at <see cref="PaceTargetPercent"/>, and
    /// while a login's week is under <see cref="WeeklyAwareLine"/> nothing else changes. From there the week is taken in
    /// too, against the login's own weekly reset: a window is held only when the week, spent as it's being spent, would reach
    /// <see cref="WeeklyAwareTarget"/> before it resets, and then only as far as needed: the week's room spread over the hours
    /// left, so it lands on its target seconds to minutes before its reset (<see cref="Budget.WeeklyPacing"/>, M.ORCH-T669); scarce
    /// room goes to higher-priority tickets first; <c>next --claim</c> hands a ticket that doesn't fit what's left to another
    /// signed-in login with room, else to the fallback login; the week stops at <see cref="WeeklyAwareTarget"/> (and winds down at
    /// <see cref="WeeklyAwareWindDown"/> only while there's no plan for it). Each window's planned and actual end percent is logged
    /// (<see cref="Budget.PacePlan"/>). Hard-coded off (M.ORCH-T706): the owner's rule is no weekly-aware pacing, ever, so a saved
    /// value can't turn it back on.</summary>
    [JsonIgnore]
    public bool WeeklyAwarePacing => false;

    /// <summary>The week's percent from which weekly-aware pacing takes the week in (M.ORCH-T680); below the weekly target.</summary>
    public int WeeklyAwareLinePercent { get; init; } = DefaultWeeklyAwareLinePercent;

    /// <summary>How many points under the weekly target weekly-aware pacing winds down while there's no plan for the week
    /// (M.ORCH-T680), 0 to <see cref="MaxWeeklyWindDownMargin"/>: past it only what can finish starts.</summary>
    public int WeeklyWindDownMargin { get; init; } = DefaultWeeklyWindDownMargin;

    public const int DefaultWeeklyAwareLinePercent = 75, DefaultWeeklyWindDownMargin = 3, MaxWeeklyWindDownMargin = 20;

    /// <summary>Weekly-aware pacing's numbers, the one place they're read from (M.ORCH-T669), each from the settings
    /// (M.ORCH-T680): where each week lands and stops, 100 minus <see cref="WeeklyReservePercent"/> (50 to 100; 95 by default),
    /// the same line as <see cref="WeeklyStopPercent"/>.</summary>
    [JsonIgnore]
    public int WeeklyAwareTarget => WeeklyStopPercent;

    /// <summary>The line the week is taken in from, <see cref="WeeklyAwareLinePercent"/> as it applies: a hand-edited value at or
    /// past the target is held one point under it.</summary>
    [JsonIgnore]
    public int WeeklyAwareLine => Math.Clamp(WeeklyAwareLinePercent, 0, WeeklyAwareTarget - 1);

    /// <summary>Where the week winds down while there's no plan for it: the target less <see cref="WeeklyWindDownMargin"/>.</summary>
    [JsonIgnore]
    public int WeeklyAwareWindDown => WeeklyAwareTarget - Math.Clamp(WeeklyWindDownMargin, 0, MaxWeeklyWindDownMargin);

    /// <summary>What's wrong with the weekly-pacing numbers as typed (target 50–100, line below the target, margin 0–20), or
    /// null; the CLI and the Settings modal both check with it.</summary>
    public static string? WeeklyPacingError(int target, int line, int margin) =>
        target is < 100 - MaxWeeklyReservePercent or > 100 ? $"Weekly target: {100 - MaxWeeklyReservePercent} to 100."
        : line < 0 || line >= target ? $"Weekly take-in line: 0 to {target - 1} (below the weekly target, {target}%)."
        : margin is < 0 or > MaxWeeklyWindDownMargin ? $"Weekly wind-down margin: 0 to {MaxWeeklyWindDownMargin}."
        : null;

    /// <summary>The curve the 5-hour window is paced along (M.ORCH-T234): <see cref="LinearProfile"/> (the default) spreads what's
    /// left of the target evenly to the reset; <see cref="FrontLoadedProfile"/> aims at <see cref="PaceTargetPercent"/> ×
    /// elapsed^<see cref="PaceCurveK"/>, spending faster early and easing off toward the reset. Read on every pace check, so a switch mid-window applies at once.</summary>
    [JsonIgnore]
    public string PaceProfile => LinearProfile;

    /// <summary>The front-loaded curve's exponent: under 1 bends it up early (0.55 by default); 1 is a straight line.</summary>
    public double PaceCurveK { get; init; } = 0.55;

    public const string LinearProfile = "linear", FrontLoadedProfile = "front-loaded";

    public static IReadOnlyList<string> PaceProfiles { get; } = [LinearProfile, FrontLoadedProfile];

    /// <summary>A typed profile ("Front-Loaded", "frontloaded", "front") as saved, or null when it's neither.</summary>
    public static string? ParsePaceProfile(string? text) =>
        text?.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "") switch
        {
            "linear" => LinearProfile,
            "frontloaded" or "front" => FrontLoadedProfile,
            _ => null,
        };

    /// <summary>The front-loaded curve is on (anything else, a hand-edited typo included, paces linearly).</summary>
    [JsonIgnore]
    public bool FrontLoaded => ParsePaceProfile(PaceProfile) == FrontLoadedProfile;

    /// <summary>The exponent the pace and the chart use: the front-loaded <see cref="PaceCurveK"/> (kept to 0.1–1, so the curve
    /// never dips below the straight line), 1 for linear.</summary>
    [JsonIgnore]
    public double PaceExponent => FrontLoaded && double.IsFinite(PaceCurveK) ? Math.Clamp(PaceCurveK, 0.1, 1) : 1;

    /// <summary>On: while the forecast says the burn won't last until the reset, running Claude Code agents
    /// wait before each tool call (the pace-gate hook), so they slow down instead of hitting the wall.</summary>
    [JsonIgnore]
    public bool ThrottleAgents => false;

    /// <summary>The slash commands exempt from orchestration pacing (M.ORCH-T198): a turn whose prompt starts with one skips
    /// every Orchestrator gate (no pace delay, no subagent hold, no stop refusal) until it ends, and a Skill call naming one
    /// is let through anywhere. The pace gate reads this live from settings.json; its spend is still counted.</summary>
    // A saved list still naming the retired /throttle or /unthrottle reads as /pacing, which replaced them (M.ORCH-T483).
    public List<string> ExemptCommands { get => exemptCommands; init => exemptCommands = RenameRetired(value); }
    private readonly List<string> exemptCommands = [.. DefaultExemptCommands];

    public static IReadOnlyList<string> DefaultExemptCommands { get; } = ["/pacing", "/deploy", "/run"];

    private static List<string> RenameRetired(List<string>? commands) =>
        commands is null ? [] : [.. commands.Select(c => c?.Trim().TrimStart('/').ToLowerInvariant() is "throttle" or "unthrottle" ? "/pacing" : c!).Distinct()];

    /// <summary>Normalizes typed command names ("deploy, /Run") to "/deploy", "/run"; blanks and repeats dropped.</summary>
    public static List<string> ParseCommands(string text) =>
        [.. text.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => "/" + c.TrimStart('/').ToLowerInvariant()).Where(c => c.Length > 1).Distinct()];

    /// <summary>The longest one tool call is held while throttling.</summary>
    public int MaxAgentDelaySeconds { get; init; } = 300;

    /// <summary>The most subagents that may run at once across every CLI, however much room the pace has; under it,
    /// the allowance is worked out live from the pace (<see cref="Budget.Pace.SubagentAllowance"/>).</summary>
    public int MaxSubagents { get; init; } = 16;

    /// <summary>Context fill at which a session is wrapped up and cycled; high enough to use most of the
    /// window, low enough that the wrap-up turn fits before Claude Code compacts on its own.</summary>
    public int ContextCyclePercent { get; init; } = 80;

    /// <summary>Sessions the scheduler will fill up to (working + waiting both occupy a slot).</summary>
    public int MaxConcurrentSessions { get; init; } = 4;

    /// <summary>The master switch for <c>/allocate</c>: on, every CLI where <c>/allocate</c> was typed is fed its
    /// repos' next story whenever it's idle and the pace allows; off pauses all of them (their marks stay).</summary>
    public bool Autopilot { get; init; } = true;

    /// <summary>Hours without a transcript write or open tab where a ticket was dispatched before it
    /// goes back in the queue as stale. 0 keeps assignments until the ticket closes or is released.</summary>
    public int StaleAfterHours { get; init; } = 12;

    public string DefaultProvider { get; init; } = "Claude";

    /// <summary>Worktrees for concurrent tickets live here, outside the workspace root so they never
    /// show up as repos of their own.</summary>
    public string WorktreeRoot { get; init; } = @"D:\Projects\MindAttic.worktrees";

    /// <summary>Percent of the 5-hour limit one repo may spend in a block before it gets no new sessions
    /// (repo name → percent), so one greedy repo can't eat the block. Missing means no cap.</summary>
    public Dictionary<string, int> RepoBudgetPercent { get; init; } = [];

    /// <summary>For a provider other than Claude: its own reported 5-hour used percent at which dispatching to it holds. 0 means never.</summary>
    public int ProviderCapPercent { get; init; } = 90;

    /// <summary>Uncached tokens set aside for a ticket (ticket → tokens): it's proposed first and may start
    /// while pacing coasts, and everything else paces against what's left after it.</summary>
    public Dictionary<string, long> Reservations { get; init; } = [];

    /// <summary>Local-time windows when autopilot pauses, like "22:00-07:00". Manual starts still work.</summary>
    public List<string> QuietHours { get; init; } = [];

    /// <summary>Ticket → the tickets it waits for. A summary saying "blocked by X-T1" counts too.</summary>
    public Dictionary<string, List<string>> BlockedBy { get; init; } = [];

    /// <summary>Sessions one repo may run at once (the repo itself plus worktrees), unless <see cref="RepoMaxSessions"/> says otherwise.</summary>
    public int MaxSessionsPerRepo { get; init; } = 2;

    public Dictionary<string, int> RepoMaxSessions { get; init; } = [];

    /// <summary><paramref name="repo"/>'s entry in a per-repo map, its name matched ignoring case as everywhere else repos are
    /// named (typed as "prose", saved as such, it still applies to Prose); <paramref name="otherwise"/> when it has none.</summary>
    public static int ForRepo(IReadOnlyDictionary<string, int> map, string repo, int otherwise) =>
        map.TryGetValue(repo, out var exact) ? exact
            : map.FirstOrDefault(kv => kv.Key.Equals(repo, StringComparison.OrdinalIgnoreCase)) is { Key: not null } kv ? kv.Value
            : otherwise;

    /// <summary>Keep development going without you: a CLI whose turn ends asking whether to go on while its repo still has
    /// open tickets is told to carry on (the context gate's Stop hook). False lets it stop and ask.</summary>
    public bool KeepGoing { get; init; } = true;

    /// <summary>Once the window a CLI stopped for has reset, type a resume prompt into it (a Launcher tab) so it carries on
    /// (<see cref="Sessions.LimitWaker"/>). False only alerts you that it's waiting; so does a CLI in a plain terminal.</summary>
    public bool ResumeAfterReset { get; init; } = true;

    /// <summary>One CLI per account (M.ORCH-T605): a Claude tab started without a login chosen for it (dashboard Start on
    /// Auto, <c>orchestrator start</c> without <c>--account</c>, Queue and autopilot starts, <c>orchestrator accounts pick</c>)
    /// goes on a signed-in per-CLI login no live CLI runs on, the one with the most headroom first
    /// (<see cref="Sessions.LoginPicker"/>), else the global login. False starts every such tab on the global login.</summary>
    public bool AutoLogin { get; init; } = true;

    /// <summary>Rebalance when an account runs low (M.ORCH-T608): a CLI whose login crosses its 5-hour pace target or its weekly
    /// stop line is moved by the daemon to a signed-in per-CLI login with no CLI and more headroom, restarting with
    /// <c>claude --resume</c> at its next idle point, and you're toasted (<see cref="Sessions.AccountRebalancer"/>).
    /// False leaves every CLI on its login.</summary>
    public bool RebalanceLogins { get; init; } = true;

    /// <summary>Emergency failover to the API key (M.ORCH-T664): when a CLI's login runs out (its 5-hour or weekly limit, or its
    /// pace says stop) and no signed-in login has room to rebalance it onto, the daemon moves that one CLI onto the Vault key,
    /// resuming its session (at once when a limit error ended its turn), with the billing mode left as it is, and moves it back
    /// once its login resets. A CLI on someone's own per-CLI login goes straight to the key at its 5-hour pace target or weekly
    /// target, never to another person's login, while the fallback is the API key (M.ORCH-T681). Hard-coded off (M.ORCH-T706): API billing is for
    /// real emergencies and only the owner switches it on, so the daemon never moves a CLI onto the key by itself.</summary>
    [JsonIgnore]
    public bool FailoverToApiKey => false;

    // ---- A member's own use outside the orchestrator (M.ORCH-T682) ----
    // When a login's meter rises by more than its CLIs here spent (the desktop app, claude.ai, a phone), that login's 5-hour pace
    // ceiling drops from the pace target to OutsideUseCeilingPercent (lower still when their measured rate needs it) and its
    // weekly stop keeps extra headroom, until OutsideUseQuietMinutes pass with no new outside activity. Each value is the
    // starting point; each login learns its own within the Min/Max bounds (Sessions.OutsideUseWatch), never past them.

    /// <summary>The 5-hour ceiling a login's CLIs are held to while its member uses Claude elsewhere.</summary>
    public int OutsideUseCeilingPercent { get; init; } = 90;
    public int OutsideUseCeilingMinPercent { get; init; } = 70;
    public int OutsideUseCeilingMaxPercent { get; init; } = 95;

    /// <summary>How long with no new outside activity before the full target comes back.</summary>
    public int OutsideUseQuietMinutes { get; init; } = 20;
    public int OutsideUseQuietMinMinutes { get; init; } = 10;
    public int OutsideUseQuietMaxMinutes { get; init; } = 60;

    /// <summary>The noise margin: meter points a stretch's rise must exceed what the CLIs explain by (plus
    /// <see cref="OutsideUseMarginSharePercent"/> of what they explain) to count as someone's.</summary>
    public int OutsideUseMarginPoints { get; init; } = 2;
    public int OutsideUseMarginMinPoints { get; init; } = 1;
    public int OutsideUseMarginMaxPoints { get; init; } = 5;

    /// <summary>The share of what the CLIs explain allowed as the error of the learned tokens per percent.</summary>
    public int OutsideUseMarginSharePercent { get; init; } = 25;

    /// <summary>Extra headroom, in points, kept above the member's projected use (5-hour and weekly) while it lasts.</summary>
    public int OutsideUseHeadroomPoints { get; init; } = 2;
    public int OutsideUseHeadroomMinPoints { get; init; } = 1;
    public int OutsideUseHeadroomMaxPoints { get; init; } = 10;

    /// <summary>Psst on outside use (M.ORCH-T702): the logins whose outside use is texted to <see cref="OutsideUsePsstTo"/>,
    /// once at the start of each stretch (<see cref="Reminders.OutsideUsePsst"/>). Each is a login's email, the part before the
    /// @, or its name, any case; empty texts nobody.</summary>
    public List<string> OutsideUsePsstLogins { get; init; } = [.. DefaultOutsideUsePsstLogins];

    /// <summary>Jordan's login, as the user first asked for it.</summary>
    public static IReadOnlyList<string> DefaultOutsideUsePsstLogins { get; } = ["pdftransferjr@mindattic.com"];

    /// <summary>Who the outside-use text goes to: a Psst contact ("ryan") or a US number; blank texts nobody (blank, not null:
    /// a null isn't written, so it would read back as the default).</summary>
    public string? OutsideUsePsstTo { get; init; } = DefaultOutsideUsePsstTo;

    public const string DefaultOutsideUsePsstTo = "ryan";

    /// <summary>A typed list of watched logins ("Jordan, amy@x.com") as saved; "none" or blank is none.</summary>
    public static List<string> ParseLogins(string? text) =>
        text is null || text.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? []
            : [.. text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>A lower weekly stop for one login while its member uses Claude elsewhere (M.ORCH-T682): set per login by the
    /// pacer, never saved.</summary>
    [JsonIgnore]
    public int? WeeklyStopCapPercent { get; init; }

    /// <summary>Where CLIs the Orchestrator opens run (M.ORCH-T618): <see cref="WindowsTerminalHost"/> (the default) opens each
    /// tab in Windows Terminal, one window per person; <see cref="OrchestratorHost"/> opens it as a terminal pane inside the
    /// dashboard (its Terminals view), falling back to Windows Terminal while no dashboard is open to hold it. Read at each
    /// start, so a switch affects only tabs opened afterwards.</summary>
    public string TerminalHost { get; init; } = WindowsTerminalHost;

    public const string WindowsTerminalHost = "windows-terminal", OrchestratorHost = "orchestrator";

    public static IReadOnlyList<string> TerminalHosts { get; } = [WindowsTerminalHost, OrchestratorHost];

    /// <summary>A typed host ("Windows Terminal", "wt", "Orchestrator", "dashboard") as saved, or null when it's neither.</summary>
    public static string? ParseTerminalHost(string? text) =>
        text?.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "") switch
        {
            "windowsterminal" or "wt" or "terminal" or "windows" => WindowsTerminalHost,
            "orchestrator" or "dashboard" or "embedded" => OrchestratorHost,
            _ => null,
        };

    /// <summary>New CLIs open inside the Orchestrator (anything else, a hand-edited typo included, opens Windows Terminal).</summary>
    [JsonIgnore]
    public bool EmbeddedTerminals => ParseTerminalHost(TerminalHost) == OrchestratorHost;

    /// <summary>The dashboard's theme (M.ORCH-T678): "Light" or "Dark", as its sun/moon switch saved it. Null (never switched),
    /// or anything else, is dark, the default; the dashboard reads it at start and writes it at each switch.</summary>
    public string? Theme { get; init; }

    /// <summary>Minutes since a repo's own CLI last ended a turn that it still counts as worked by hand, so autopilot
    /// leaves the repo alone through the pauses between your prompts. 0 counts only a CLI that's mid-turn.</summary>
    public int AutopilotHandMinutes { get; init; } = 15;

    /// <summary>Repos <c>/allocate</c> never starts agents in, even when named or their CLI is open.</summary>
    public List<string> AutopilotSkip { get; init; } = [];

    /// <summary>Approvals as settings.json kept them before the ticket database's queue held them (M.ORCH-T384): read only by
    /// <see cref="Tickets.QueueImport"/>, which moves them into <c>ticket_queue</c> once and drops them here. Approval is
    /// read from and written to the database alone, where <c>tickets ticket approve</c> and the Tickets Desktop write it.</summary>
    public List<string>? Approved { get; set; }

    /// <summary>Open tickets older than this many days with nobody on them are listed for grooming. 0 means never.</summary>
    public int GroomAfterDays { get; init; } = 30;

    /// <summary>Where alerts go off the desktop: a URL that takes a plain-text POST, like an ntfy.sh topic
    /// (https://ntfy.sh/your-topic) for phone pushes. Null sends nothing.</summary>
    public string? NotifyUrl { get; init; }

    /// <summary>Hours between audits the daemon runs by itself (it alerts on anything new). 0 means only on demand.</summary>
    public int AuditEveryHours { get; init; } = 24;

    /// <summary>Free GB on the system drive (or the <see cref="WorktreeRoot"/>'s drive) under which the daemon alerts with the
    /// biggest folders in %TEMP% and the biggest files agents left under %TEMP%\claude (M.ORCH-T471). 0 turns the watch off.</summary>
    public double DiskWarnGb { get; init; } = 20;

    /// <summary>Free GB under which the disk alert is urgent (see <see cref="Activity.DiskWatch"/>).</summary>
    public double DiskCriticalGb { get; init; } = 5;

    /// <summary>Off by default, since it writes tickets into every repo's log: hourly, file GitHub issues
    /// labelled "orchestrator" and CI runs that failed on main as tickets (see <see cref="Tickets.GitHubIntake"/>).</summary>
    public bool GitHubIntake { get; init; }

    /// <summary>Manual priorities as settings.json kept them before the ticket database (M.ORCH-T384): moved into
    /// <c>ticket_queue</c> once by <see cref="Tickets.QueueImport"/> and dropped here, like <see cref="Approved"/>.</summary>
    public Dictionary<string, int>? Priorities { get; set; }

    /// <summary>Approved tickets the ticket database doesn't hold: those of a repo still on its own <c>docs/USER_STORIES.md</c>
    /// (or every ticket, before the workspace is migrated). <c>ticket_queue</c> only takes tickets in the database, so without
    /// this such a repo's features, chores and docs could never be approved (M.ORCH-T441). Written by
    /// <see cref="Tickets.TicketQueue"/>, read by the scheduler next to the database's queue, and moved into that queue by
    /// <see cref="Tickets.TicketQueue.MoveIntoDatabase"/> once the database holds the ticket.</summary>
    public List<string> StoryApproved { get; init; } = [];

    /// <summary>Manual priorities of tickets the ticket database doesn't hold, like <see cref="StoryApproved"/>.</summary>
    public Dictionary<string, long> StoryPriorities { get; init; } = [];

    /// <summary>The model a ticket's subagent runs on, by ticket id or repo (a name or a wildcard like <c>GTFO.*</c>) → "sonnet"
    /// or "opus" (M.ORCH-T563). A ticket's entry wins over its repo's; with neither, <see cref="Scheduling.ModelChoice"/> picks.</summary>
    public Dictionary<string, string> ModelOverrides { get; init; } = [];

    /// <summary>Tickets assigned to a user (M.ORCH-T668), ticket id → that user's login (its email, as
    /// <see cref="Sessions.AccountDirs.Normalize"/> keeps it). An assigned ticket is a pin: <c>next --claim</c>, autopilot and
    /// Start dispatch it only on that login, and while its user is out of room it waits for them instead of being redelegated,
    /// until "Redelegate now" drops the pin (<see cref="Scheduling.TicketPins"/>).</summary>
    public Dictionary<string, string> Assignees { get; init; } = [];

    /// <summary>Lines changed (added + removed) past which a Sonnet ticket's diff gets one Opus review before it lands. 0 reviews
    /// every Sonnet diff; a negative number none for size (risky files still are).</summary>
    public int ReviewDiffLines { get; init; } = 300;

    /// <summary>Files whose change by a Sonnet ticket always gets one Opus review before it lands: the gates, pacing and git
    /// landing. Wildcards (<c>*</c>, <c>?</c>) match against the path as git prints it, folders included.</summary>
    public List<string> RiskyFiles { get; init; } = [.. DefaultRiskyFiles];

    public static IReadOnlyList<string> DefaultRiskyFiles { get; } =
    [
        // The gates: the hooks every CLI runs, and what writes them.
        "*AgentGate*.cs", "*pace-gate*", "*context-gate*", "*commit-msg*", "*/hooks/*",
        // Pacing.
        "*/Budget/Pacer.cs", "*/Budget/BudgetCalculator.cs", "*/Budget/PaceLedger*.cs", "*/Scheduling/SubagentShare.cs", "*RateLimitStatus*",
        // Git landing.
        "*/Scheduling/Dispatcher.cs", "*/Scheduling/Autopilot.cs", "*land*.ps1", "*deploy.ps1",
    ];
}

/// <summary>A ticket the orchestrator has handed to a session.</summary>
public sealed record Assignment(string TicketId, string Repo, string Directory, string Provider, DateTime StartedUtc, bool Worktree)
{
    /// <summary>The background agent <c>/allocate</c> started on it (its process id); null for a tab.</summary>
    public int? Pilot { get; init; }

    /// <summary>When <see cref="Pilot"/>'s process started, so a later process given the same id isn't taken for it.</summary>
    public DateTime? PilotStartedUtc { get; init; }

    /// <summary>Where that background agent's output goes.</summary>
    public string? Log { get; init; }

    /// <summary>The Claude CLI that claimed it (<c>orchestrator next --claim</c>), by process id as the pace gate resolves it
    /// (<c>MINDATTIC_CLI_PID</c>, else the claude.exe it runs under): its subagent is that CLI's to start (M.ORCH-T532). Null
    /// when unknown: a tab's or background agent's assignment, or a claim made before claims recorded it.</summary>
    public int? Cli { get; init; }

    /// <summary>The claiming CLI's session at the claim (its newest, as the context gate recorded it); null when unknown.</summary>
    public string? CliSession { get; init; }

    /// <summary>The model the claim's subagent was told to run on ("sonnet" or "opus", M.ORCH-T563); null for anything else.</summary>
    public string? Model { get; init; }

    /// <summary>When its diff had its one Opus review (<c>orchestrator review</c>); null before it has.</summary>
    public DateTime? ReviewedUtc { get; init; }

    /// <summary>The login it was dispatched on, by its account email (M.ORCH-T668): the claiming CLI's for a claim, the tab's
    /// for a Start. For a ticket assigned to a user it is always that user's login; null when not known (the global login
    /// with no email read yet, or an assignment made before this was recorded).</summary>
    public string? Account { get; init; }

    /// <summary>A claim for a CLI's subagent: a worktree, no background agent on it.</summary>
    [JsonIgnore]
    public bool IsClaim => Worktree && Pilot is null && Log is null;
}
