using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Settings;

/// <summary>What you tell the orchestrator. Everything has a working default.</summary>
public sealed record OrchestratorSettings
{
    /// <summary>The folder holding every repo, resolved (M.ORCH-T1328): the orchestrator's own override in Vault, else Vault
    /// Global's workspace root. Null uses MindAttic.Tickets's default; the first run asks for one when that doesn't exist on this
    /// machine. The hooks read it from the database row, so the row always holds the resolved folder.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>The orchestrator's own workspace root in Vault, as set there (M.ORCH-T1328): blank follows Vault Global. Null
    /// (a row from before, or a value set straight on <see cref="WorkspaceRoot"/>) means <see cref="WorkspaceRoot"/> is the
    /// override itself. Set by <see cref="OrchestratorVaultSettings.ApplyTo"/>; see <see cref="OrchestratorVaultSettings.WithWorkspaceRoot"/>.</summary>
    public string? WorkspaceRootOverride { get; init; }

    /// <summary>Uncached tokens (input + cache write + output) allowed per 5-hour usage block.
    /// Null measures blocks against your own peak block in the last 7 days instead.</summary>
    public long? BlockBudget { get; init; }

    /// <summary>How fresh Claude's own 5-hour report must be to pace straight off its percent exactly, with nothing added
    /// (M.ORCH-T788): past this many minutes old (nothing redraws <c>claude-usage.json</c> while no interactive session
    /// is open), the local tokens spent since that report are added back in so the hard cutoff isn't blind to real spend
    /// during a quiet statusline (M.ORCH-T783).</summary>
    public int ReportFreshMinutes { get; init; } = 2;

    /// <summary>Uncached tokens allowed per week. Null means no weekly line.</summary>
    public long? WeeklyBudget { get; init; }

    /// <summary>When the week resets, as "Monday 09:00" local time (see <see cref="Budget.WeeklyReset"/>).
    /// Null means the week is a rolling 7 days.</summary>
    public string? WeeklyReset { get; init; }

    /// <summary>Percent of a budget at which the dashboard warns. Without pacing, the scheduler also stops proposing here.</summary>
    public int WarnPercent { get; init; } = 80;

    /// <summary>Where the week actually warns for a given login (M.ORCH-T707 follow-up, bughunt 2026-11-01): <see cref="WarnPercent"/>
    /// by default, but <see cref="Budget.UsageCutoffs.Apply"/> moves it with that member's own weekly cutoff, the same number of
    /// points short of it that the org-wide default keeps short of <see cref="WeeklyStopPercent"/>'s own default. Without this, a
    /// member who raised their cutoff well past <see cref="WarnPercent"/> (<c>org limit --weekly</c>) still warned, and was held
    /// while unpaced, at the org-wide line as if they hadn't.</summary>
    public int WeeklyWarnPercent { get; init; } = 80;

    /// <summary>The same, for the 5-hour window and <see cref="PaceTargetPercent"/>.</summary>
    public int FiveHourWarnPercent { get; init; } = 80;

    /// <summary>On: with a limit to pace against (set or learned), the scheduler caps sessions so
    /// spend lands on <see cref="PaceTargetPercent"/> at the reset instead of holding at the warn line.</summary>
    public bool Pacing { get; init; } = true;

    /// <summary>How much of each limit pacing aims to use by the reset; the rest is margin for usage
    /// the orchestrator can't see (claude.ai, other machines).</summary>
    public int PaceTargetPercent { get; init; } = 96;

    /// <summary>A login's 5-hour or weekly use (percent) under which the pace guards are dormant (M.ORCH-T1592): no pace holds,
    /// no subagent allowance cap and no cool-off, so work runs full speed until either window reaches it. The hard stops
    /// (the pace target, the weekly stop, a wall Claude reports) and the memory guard are untouched.</summary>
    public double PaceGuardStartPercent { get; init; } = DefaultPaceGuardStartPercent;

    /// <summary>The default <see cref="PaceGuardStartPercent"/>.</summary>
    public const double DefaultPaceGuardStartPercent = 90;

    /// <summary>The share of each account's week kept back (M.ORCH-T609): every CLI on an account stops at
    /// <see cref="WeeklyStopPercent"/> (100 minus this) of that account's week instead of burning it all. 0 to
    /// <see cref="MaxWeeklyReservePercent"/>; applies to each login on its own.</summary>
    public int WeeklyReservePercent { get; init; } = DefaultWeeklyReservePercent;

    public const int DefaultWeeklyReservePercent = 10, MaxWeeklyReservePercent = 50;

    /// <summary>The percent of an account's week at which its CLIs stop: 100 minus <see cref="WeeklyReservePercent"/>
    /// (kept to 0–<see cref="MaxWeeklyReservePercent"/>, so a hand-edited value can't stop it at nothing). Weekly-aware pacing
    /// lands the week on this same line (<see cref="WeeklyAwareTarget"/>, M.ORCH-T680), so on or off it's one line.</summary>
    [JsonIgnore]
    public int WeeklyStopPercent => WeeklyStopFor(WeeklyReservePercent);

    public static int WeeklyStopFor(int reservePercent) => 100 - Math.Clamp(reservePercent, 0, MaxWeeklyReservePercent);

    /// <summary>Weekly-aware pacing (M.ORCH-T666), per login: each 5-hour window still aims at <see cref="PaceTargetPercent"/>, and
    /// while a login's week is under <see cref="WeeklyAwareLine"/> nothing else changes. From there the week is taken in
    /// too, against the login's own weekly reset: a window is held only when the week, spent as it's being spent, would reach
    /// <see cref="WeeklyAwareTarget"/> before it resets, and then only as far as needed: the week's room spread over the hours
    /// left, so it lands on its target seconds to minutes before its reset (<see cref="Budget.WeeklyPacing"/>, M.ORCH-T669); scarce
    /// room goes to higher-priority tickets first; <c>next --claim</c> hands a ticket that doesn't fit what's left to another
    /// signed-in login with room, else to the fallback login; the week stops at <see cref="WeeklyAwareTarget"/>. Each window's planned and actual end percent is logged
    /// (<see cref="Budget.PacePlan"/>). Hard-coded off (M.ORCH-T706): the owner's rule is no weekly-aware pacing, ever, so a saved
    /// value can't turn it back on.</summary>
    [JsonIgnore]
    public bool WeeklyAwarePacing => false;

    /// <summary>The week's percent from which weekly-aware pacing takes the week in (M.ORCH-T680); below the weekly target.</summary>
    public int WeeklyAwareLinePercent { get; init; } = DefaultWeeklyAwareLinePercent;

    public const int DefaultWeeklyAwareLinePercent = 75;

    /// <summary>Weekly-aware pacing's numbers, the one place they're read from (M.ORCH-T669), each from the settings
    /// (M.ORCH-T680): where each week lands and stops, 100 minus <see cref="WeeklyReservePercent"/> (50 to 100; 95 by default),
    /// the same line as <see cref="WeeklyStopPercent"/>.</summary>
    [JsonIgnore]
    public int WeeklyAwareTarget => WeeklyStopPercent;

    /// <summary>The line the week is taken in from, <see cref="WeeklyAwareLinePercent"/> as it applies: a hand-edited value at or
    /// past the target is held one point under it.</summary>
    [JsonIgnore]
    public int WeeklyAwareLine => Math.Clamp(WeeklyAwareLinePercent, 0, WeeklyAwareTarget - 1);

    /// <summary>What's wrong with the weekly target as typed (50-100), or null.</summary>
    public static string? WeeklyTargetError(int target) =>
        target is < 100 - MaxWeeklyReservePercent or > 100 ? $"Weekly target: {100 - MaxWeeklyReservePercent} to 100." : null;

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
    /// every pacing gate (no pace delay, no subagent hold) until it ends, and a Skill call naming one is let through anywhere;
    /// the hard 5-hour and weekly stops still refuse its tool calls (bughunt 2026-10-27). The pace gate reads this live from
    /// settings.json; its spend is still counted.</summary>
    // A saved list still naming the retired /throttle or /unthrottle reads as /pacing, which replaced them (M.ORCH-T483).
    public List<string> ExemptCommands { get => exemptCommands; init => exemptCommands = RenameRetired(value); }
    private readonly List<string> exemptCommands = [.. DefaultExemptCommands];

    public static IReadOnlyList<string> DefaultExemptCommands { get; } = ["/pacing", "/deploy", "/run", "/allocate", "/commit", "/quicksave", "/quickload"];

    private static List<string> RenameRetired(List<string>? commands) =>
        commands is null ? [] : [.. commands.Where(c => c is not null).Select(c => c!.Trim().TrimStart('/').ToLowerInvariant() is "throttle" or "unthrottle" ? "/pacing" : c!).Distinct()];

    /// <summary>Normalizes typed command names ("deploy, /Run") to "/deploy", "/run"; blanks and repeats dropped.</summary>
    public static List<string> ParseCommands(string text) =>
        [.. text.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => "/" + c.TrimStart('/').ToLowerInvariant()).Where(c => c.Length > 1).Distinct()];

    /// <summary>The longest one tool call is held while throttling.</summary>
    public int MaxAgentDelaySeconds { get; init; } = 300;

    /// <summary>The most subagents that may run at once across every CLI, however much room the pace has; under it,
    /// the allowance is worked out live from the pace (<see cref="Budget.Pace.SubagentAllowance"/>).</summary>
    public int MaxSubagents { get; init; } = 16;

    /// <summary>The last-resort cool-off (M.ORCH-T786, <see cref="Scheduling.AgentAbort"/>): when a login's live 5-hour trend stays
    /// on course past its pace line with new subagents already slowed (<see cref="Budget.Pacer.CoolOff"/>) for
    /// <see cref="AgentAbortAfterMinutes"/>, the orchestrator aborts just enough of that login's ticket subagents to bend it back
    /// under, their claims released and their tickets put first in the queue. Off leaves the cool-off to slow new starts only.</summary>
    public bool AgentAbort { get; init; } = true;

    /// <summary>How long the new-start cool-off gets to bend a login's trend back under before <see cref="AgentAbort"/> steps in (0-60).</summary>
    public int AgentAbortAfterMinutes { get; init; } = 3;

    /// <summary>Advisory per-path edit claims (M.ORCH-T1193, <see cref="Setup.AgentGate.EditClaimsPython"/>): two agents editing the
    /// same file in a shared checkout. "Warn" (the default) tells the second who else has the file; "Block" also refuses a subagent's
    /// edit of a file another live agent claimed (a CLI's own main thread is only ever warned, so the user's edits can't deadlock);
    /// "Off" records nothing.</summary>
    public string EditClaims { get; init; } = DefaultEditClaims;

    public const string DefaultEditClaims = "Warn";

    /// <summary>Minutes a file's edit claim stands after its agent's last edit of it (M.ORCH-T1193), so a crashed agent's claim lapses;
    /// a commit naming the file, the subagent's stop or its CLI's exit releases it sooner.</summary>
    public double EditClaimMinutes { get; init; } = DefaultEditClaimMinutes;

    public const double DefaultEditClaimMinutes = 30;

    /// <summary>Context fill at which a session is wrapped up and cycled; high enough to use most of the
    /// window, low enough that the wrap-up turn fits before Claude Code compacts on its own.</summary>
    public int ContextCyclePercent { get; init; } = 80;

    /// <summary>Sessions the scheduler will fill up to (working + waiting both occupy a slot).</summary>
    public int MaxConcurrentSessions { get; init; } = 4;

    /// <summary>The master switch for <c>/allocate</c>: on, every CLI where <c>/allocate</c> was typed is fed its
    /// repos' next story whenever it's idle and the pace allows; off pauses all of them (their marks stay).</summary>
    public bool Allocate { get; init; } = true;

    /// <summary>Hours without a transcript write or open tab where a ticket was dispatched before it
    /// goes back in the queue as stale. 0 keeps assignments until the ticket closes or is released.</summary>
    public int StaleAfterHours { get; init; } = 12;

    public string DefaultProvider { get; init; } = "Claude";

    /// <summary>Where the tools' own clean worktrees of origin/main live (MainlineTests' main-tests, deploy, /test's verify-main),
    /// outside the workspace root so they never show up as repos of their own. Tickets got a worktree each here before
    /// M.ORCH-T1122; now every ticket is worked in its repo's own checkout on main, and the sweep only tidies those old ones.</summary>
    public string WorktreeRoot { get; init; } = @"D:\Projects\MindAttic.worktrees";

    /// <summary>Per-member "until" dates (M.ORCH-T1401): <c>email=date</c> entries joined by ";" (a date is yyyy-MM-dd, that day's end,
    /// or yyyy-MM-ddTHH:mm, local). Once a member's date has passed the dashboard drops that member's tab from the roster, so none of
    /// its CLI tabs are auto-started or respawned; the member, login folder, credentials and data stay, and clearing the entry brings
    /// the tab back. See <see cref="Budget.MemberUntil"/>.</summary>
    public string MemberUntil { get; init; } = Budget.MemberUntil.Default;

    /// <summary>Percent of the 5-hour limit one repo may spend in a block before it gets no new sessions
    /// (repo name → percent), so one greedy repo can't eat the block. Missing means no cap.</summary>
    public Dictionary<string, int> RepoBudgetPercent { get; init; } = [];

    /// <summary>For a provider other than Claude: its own reported 5-hour used percent at which dispatching to it holds. 0 means never.</summary>
    public int ProviderCapPercent { get; init; } = 90;

    /// <summary>Legacy (M.ORCH-T762): reservations the orchestrator kept before MindAttic.Tickets owned them (ticket → tokens). Never
    /// written or read for scheduling any more; <see cref="Tickets.TicketQueue.MoveWorkIntoDatabase"/> moves them into the ticket
    /// database (<c>tickets ticket reserve</c>), which the scheduler reads.</summary>
    public Dictionary<string, long> Reservations { get; init; } = [];

    /// <summary>Local-time windows when allocate pauses, like "22:00-07:00". Manual starts still work.</summary>
    public List<string> QuietHours { get; init; } = [];

    /// <summary>Legacy (M.ORCH-T762): blocks set by hand before MindAttic.Tickets owned them (ticket → the tickets it waits for).
    /// Never written or read for scheduling any more; <see cref="Tickets.TicketQueue.MoveWorkIntoDatabase"/> moves them into the
    /// ticket database as 'requires' links (<c>tickets ticket block</c>).</summary>
    public Dictionary<string, List<string>> BlockedBy { get; init; } = [];

    /// <summary>Sessions one repo may run at once (all in its own checkout, M.ORCH-T1122), unless <see cref="RepoMaxSessions"/> says otherwise.</summary>
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

    /// <summary>How many times the daemon redispatches a ticket whose session went dark (M.ORCH-T748: its CLI or tab exited
    /// mid-task, the ticket still open) before it stops and says the ticket keeps dying (<see cref="Scheduling.OrphanWatch"/>).
    /// 0 only shows it as orphaned.</summary>
    public int OrphanRedispatches { get; init; } = 3;

    /// <summary>Minutes a dispatch's owner must be gone with nothing written for its ticket before it counts as orphaned.</summary>
    public int OrphanQuietMinutes { get; init; } = 10;

    /// <summary>The dashboard's person tabs (M.ORCH-T757): a busy login's green play arrow blinks once for every this many tokens
    /// it spends (uncached), so busy tabs blink at their own rate. <see cref="MinTabBlinkTokens"/> to <see cref="MaxTabBlinkTokens"/>.</summary>
    public int TabBlinkTokens { get; init; } = DefaultTabBlinkTokens;

    public const int DefaultTabBlinkTokens = 1000, MinTabBlinkTokens = 1, MaxTabBlinkTokens = 10_000_000;

    /// <summary>The dashboard's tab trailing text (M.ORCH-T1255): symbols, "Σ: 15% | λ: 29%" (the default, M.ORCH-T1251), or
    /// words, "wk: 15% | 5h: 29%" -- same figures and the same omit-the-5-hour-part-while-it's-unknown rule either way.</summary>
    public bool SymbolTabLabels { get; init; } = true;

    /// <summary>Whether the Team Wall (M.ORCH-T936) leaves out the Launcher's own menu pane (M.ORCH-T1256): off by default
    /// (included, as it always was before this setting existed), since the Launcher is itself a live CLI someone may want to
    /// watch like any other.</summary>
    public bool ExcludeLauncherFromTeamWall { get; init; }

    /// <summary>One CLI per account (M.ORCH-T605): a Claude tab started without a login chosen for it (dashboard Start on
    /// Auto, <c>orchestrator start</c> without <c>--account</c>, Queue and allocate starts, <c>orchestrator accounts pick</c>)
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

    /// <summary>Who an urgent owner alert (a login reaching its own cutoff, M.ORCH-T742) is texted to through Psst: a Psst
    /// contact ("ryan") or a US number; blank texts nobody (blank, not null: a null isn't written, so it would read back as
    /// the default).
    /// <para>Resolved (M.ORCH-T1328): the orchestrator's own override in Vault, else Vault Global's alert recipient, else
    /// <see cref="DefaultPsstAlertTo"/>; the override <c>none</c> texts nobody. The database row holds the resolved value.</para></summary>
    public string? PsstAlertTo { get; init; } = DefaultPsstAlertTo;

    /// <summary>The orchestrator's own alert recipient in Vault, as set there (M.ORCH-T1328): blank follows Vault Global,
    /// <c>none</c> texts nobody. Null (a row from before, or a value set straight on <see cref="PsstAlertTo"/>) means
    /// <see cref="PsstAlertTo"/> stands for the override itself; see <see cref="OrchestratorVaultSettings.WithPsstAlertTo"/>.</summary>
    public string? PsstAlertToOverride { get; init; }

    public const string DefaultPsstAlertTo = "ryan";

    /// <summary>The API key person's starting credit in US dollars (M.ORCH-T906): what its burndown starts from, entered by you with
    /// <c>orchestrator billing credit &lt;usd&gt;</c> (the real balance isn't read from Anthropic yet). Null: none set, so its page
    /// shows only what's been spent.</summary>
    public double? ApiCreditUsd { get; init; }

    /// <summary>When <see cref="ApiCreditUsd"/> was entered: spend is counted against it from then on.</summary>
    public DateTime? ApiCreditSinceUtc { get; init; }

    /// <summary>The dashboard's theme (M.ORCH-T678): "Light" or "Dark", as its sun/moon switch saved it. Null (never switched),
    /// or anything else, is dark, the default; the dashboard reads it at start and writes it at each switch.</summary>
    public string? Theme { get; init; }

    /// <summary>The dashboard's font (M.ORCH-T1274): "Segoe UI", the font it always had and the default, or "Outfit", bundled in
    /// the dashboard (Fonts/). Every window, menu and tooltip follows it, live; the terminal panes keep their monospace font.</summary>
    public string Font { get; init; } = DefaultFont;

    public const string DefaultFont = "Segoe UI";

    /// <summary>Minutes since a repo's own CLI last ended a turn that it still counts as worked by hand, so allocate
    /// leaves the repo alone through the pauses between your prompts. 0 counts only a CLI that's mid-turn.</summary>
    public int AllocateHandMinutes { get; init; } = 15;

    /// <summary>Repos <c>/allocate</c> never starts agents in, even when named or their CLI is open.</summary>
    public List<string> AllocateSkip { get; init; } = [];

    /// <summary>The org each repo's tickets belong to (M.ORCH-T753), repo name (any case) → org id: they're dispatched only to that
    /// org's members' logins and paced on them. A repo not named here is the default org's (<c>orchestrator orgs repo</c> sets it).</summary>
    public Dictionary<string, string> RepoOrgs { get; init; } = [];

    /// <summary>Approvals as settings.json kept them before the ticket database's queue held them (M.ORCH-T384): read only by
    /// <see cref="Tickets.QueueImport"/>, which moves them into <c>ticket_queue</c> once and drops them here. Approval is
    /// read from and written to the database alone, where <c>tickets ticket approve</c> and the Tickets Desktop write it.</summary>
    public List<string>? Approved { get; set; }

    /// <summary>Open tickets older than this many days with nobody on them are listed for grooming. 0 means never.</summary>
    public int GroomAfterDays { get; init; } = 30;

    /// <summary>Days the shared MindAttic.Log database keeps this app's entries (M.ORCH-T1611: the library's own default is 365,
    /// ~3,500 rows a day). 0 keeps them for good.</summary>
    public int LogKeepDays { get; init; } = 30;

    /// <summary>Days a finished terminal host's own log file (terms\&lt;id&gt;.log) is kept (M.ORCH-T1611). 0 keeps them for good.</summary>
    public int TermLogKeepDays { get; init; } = 7;

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

    /// <summary>Free GB on %TEMP%'s drive under which ended sessions' %TEMP%\claude folders are deleted right away, rather than at
    /// the daemon's next look (M.ORCH-T1449), by session liveness, never age (M.ORCH-T1467); a running session's scratch is only
    /// named. 0 turns it off.</summary>
    public double DiskCleanupGb { get; init; } = 30;

    /// <summary>Hours a %TEMP%\claude session folder is kept, whatever the free space (M.ORCH-T1466): one created longer ago than
    /// this, with nothing in it created or written for as long, goes once its session isn't running (an ended one goes anyway, see
    /// <see cref="Sessions.SessionTempPruner"/>); a running session keeps its folder and only its scratchpad entries that old and
    /// that quiet go. 0 turns it off.</summary>
    public double TempRetentionHours { get; init; } = DefaultTempRetentionHours;

    public const double DefaultTempRetentionHours = 48;

    /// <summary>Seconds the background daemon waits before starting its loop again after it died (M.ORCH-T1615); doubles with each
    /// consecutive failure up to <see cref="DaemonRestartMaxSeconds"/> (M.ORCH-T1619).</summary>
    public double DaemonRestartSeconds { get; init; } = 15;

    /// <summary>The longest the daemon waits between restarts of its loop (seconds).</summary>
    public double DaemonRestartMaxSeconds { get; init; } = 300;

    /// <summary>How many extra times a database write retries after SQL Server's write lock stayed busy (M.ORCH-T1620; was the
    /// const <see cref="OrchestratorDb.BusyRetries"/>, 3 then 6 under M.ORCH-T1617). A live store pushes it into that static.</summary>
    public int DbBusyRetries { get; init; } = 6;

    /// <summary>The memory guard (M.ORCH-T1494): when RAM runs low, new work stops (subagents, CLIs, main-line tests) and running
    /// work finishes, the way the 5-hour and weekly stops scale back; past <see cref="MemoryCriticalGb"/> the newest subagent is
    /// shed, past <see cref="MemoryEmergencyGb"/> tool calls are refused. /pacing off doesn't turn it off; only this does.</summary>
    public bool MemoryGuard { get; init; } = true;

    /// <summary>Physical RAM in use (percent) under which the guard is dormant (M.ORCH-T1591): no holds and no subagent cap by
    /// free RAM, so everything runs full speed until the machine is near the edge.</summary>
    public double MemoryGuardStartPercent { get; init; } = 90;

    /// <summary>Free RAM (GB) under which no new work starts (<see cref="Activity.MemoryLevel.Tight"/>).</summary>
    public double MemoryTightGb { get; init; } = 4;

    /// <summary>Free RAM (GB) under which the newest /allocate subagent is shed (<see cref="Activity.MemoryLevel.Critical"/>).</summary>
    public double MemoryCriticalGb { get; init; } = 2;

    /// <summary>Free RAM (GB) under which every tool call is refused until memory recovers (<see cref="Activity.MemoryLevel.Emergency"/>).</summary>
    public double MemoryEmergencyGb { get; init; } = 1;

    /// <summary>RAM (GB) one more subagent is assumed to take: a new one starts only while free RAM less this stays above
    /// <see cref="MemoryTightGb"/>.</summary>
    public double MemoryPerSubagentGb { get; init; } = 1;

    /// <summary>Commit charge (percent of a fixed commit limit: RAM + a fixed pagefile) at or past which
    /// <see cref="Activity.MemoryLevel.Tight"/> holds even with free RAM over <see cref="MemoryTightGb"/>.</summary>
    public double MemoryTightCommitPercent { get; init; } = 80;

    /// <summary>Commit charge percent for <see cref="Activity.MemoryLevel.Critical"/>.</summary>
    public double MemoryCriticalCommitPercent { get; init; } = 88;

    /// <summary>Commit charge percent for <see cref="Activity.MemoryLevel.Emergency"/>.</summary>
    public double MemoryEmergencyCommitPercent { get; init; } = 94;

    /// <summary>How far clear of a level's free-GB line a reading must be before the guard steps back down from it.</summary>
    public double MemoryResumeMarginGb { get; init; } = 2;

    /// <summary>How far clear of a level's commit-percent line (in points) a reading must be before the guard steps back
    /// down from it.</summary>
    public double MemoryResumeMarginCommit { get; init; } = 5;

    // ── Live-dialed intervals, caps and thresholds (M.ORCH-T1595): each was a constant; its value then is its default ──

    /// <summary>How often the memory guard reads the machine (seconds); readings in between reuse the last
    /// (was <see cref="Activity.MemoryGuard.SampleEvery"/>).</summary>
    public double MemorySampleSeconds { get; init; } = 5;

    /// <summary>How long a reading must stay clear of a level's line before the guard steps down from it (seconds; was
    /// <see cref="Activity.MemoryGuard.SettleFor"/>).</summary>
    public double MemorySettleSeconds { get; init; } = 60;

    /// <summary>How often the daemon scans for tool processes dead agents left behind (seconds; was
    /// <see cref="Sessions.ProcessReaper.Every"/>): each scan is a WMI query WmiPrvSE pays for (M.ORCH-T117).</summary>
    public double ReapEverySeconds { get; init; } = 60;

    /// <summary>Handles past which a tool process under a live agent is reaped as runaway (was
    /// <see cref="Sessions.ProcessReaper.MaxHandleCount"/>).</summary>
    public int ReapMaxHandles { get; init; } = (int)Sessions.ProcessReaper.MaxHandleCount;

    /// <summary>Minutes past which a search (find, rg, grep) under a live agent is reaped as runaway (was
    /// <see cref="Sessions.ProcessReaper.MaxAge"/>).</summary>
    public double ReapSearchMaxMinutes { get; init; } = 30;

    /// <summary>Minutes between the daemon's cleanups of unused side-by-side TermHost, Gateway and TermViewer versions.</summary>
    public double TermHostCleanupMinutes { get; init; } = 30;

    /// <summary>Minutes until a failed cleanup of old side-by-side versions is tried again.</summary>
    public double TermHostCleanupRetryMinutes { get; init; } = 5;

    /// <summary>Hours between prunes of the CLI cycler's old marks (the logon daemon runs for weeks, M.ORCH-T330).</summary>
    public double CliMarksPruneHours { get; init; } = 24;

    /// <summary>Minutes between looks for ended sessions' %TEMP%\claude folders to delete (was
    /// <see cref="Sessions.SessionTempPruner.Every"/>).</summary>
    public double SessionTempPruneMinutes { get; init; } = 10;

    /// <summary>The least minutes between two disk self-cleanups while %TEMP%'s drive is under <see cref="DiskCleanupGb"/>.</summary>
    public double DiskCleanupEveryMinutes { get; init; } = 5;

    /// <summary>Seconds between the daemon's machine-health samples (handles, commit charge, free RAM).</summary>
    public double HealthSampleSeconds { get; init; } = 30;

    /// <summary>Hours between the daemon's checks for a system-managed pagefile (M.ORCH-T1493).</summary>
    public double PagefileCheckHours { get; init; } = 24;

    /// <summary>Minutes between prunes of old log entries and finished terminal hosts' logs (was <see cref="LogPruner.Every"/>).</summary>
    public double LogPruneMinutes { get; init; } = 60;

    /// <summary>Seconds between the daemon's copies of SQL Server's settings, assignments and state into data/orchestrator.db.</summary>
    public double MirrorSeconds { get; init; } = 60;

    /// <summary>Minutes between tries of <c>tickets db sync</c> when the ticket database has changed.</summary>
    public double TicketsDbSyncMinutes { get; init; } = 10;

    /// <summary>Minutes between commits of data/orchestrator.db (and its dump) when it has changed (M.ORCH-T490).</summary>
    public double OrchestratorDbSyncMinutes { get; init; } = 10;

    /// <summary>Minutes between GitHub intakes while <see cref="GitHubIntake"/> is on.</summary>
    public double GitHubIntakeMinutes { get; init; } = 60;

    /// <summary>Seconds between the dashboard's refreshes while it's shown and polling (no daemon status stream up, or it holds
    /// the status lease itself).</summary>
    public double DashboardPollSeconds { get; init; } = 5;

    /// <summary>Seconds between the dashboard's refreshes while minimized with no daemon status stream: nobody is looking.</summary>
    public double DashboardHiddenPollSeconds { get; init; } = 60;

    /// <summary>Seconds between the dashboard's refreshes while minimized but holding the status lease (no daemon running): the
    /// daemon's own pace, since rl-status.json and allocate depend on it.</summary>
    public double DashboardHiddenHolderPollSeconds { get; init; } = 15;

    /// <summary>Seconds between the dashboard's rereads of the hosted-session registry, for a host whose bus announcement was
    /// lost (M.ORCH-T1001).</summary>
    public double TermResyncSeconds { get; init; } = 45;

    /// <summary>The least seconds between two Launchers opened for one person (M.ORCH-T1150), so one that exits at once can't loop.</summary>
    public double LauncherRespawnSeconds { get; init; } = 30;

    /// <summary>Seconds between the dashboard's rereads of each login's meter state (when it was last read, who must sign in again).</summary>
    public double MeterStatesSeconds { get; init; } = 30;

    // ── The rest of the sweep (M.ORCH-T1624): each was a constant; its value then is its default ──

    /// <summary>Minutes between the daemon's sweeps for TermHosts a test run leaked (M.ORCH-T1623; was
    /// TestTermHostReaper.Every).</summary>
    public double TestTermHostSweepMinutes { get; init; } = 5;

    /// <summary>The least minutes between two tabs org-reach opens for the same org (was <see cref="Scheduling.OrgReach"/>'s Spacing).</summary>
    public double OrgReachSpacingMinutes { get; init; } = 5;

    /// <summary>Readings of the 5-hour chart closer together than this many seconds are thinned to one point (was
    /// UtilizationSeries.MinSpacing).</summary>
    public double UtilizationMinSpacingSeconds { get; init; } = 60;

    /// <summary>Seconds <c>tickets db sync</c> and the orchestrator.db commit's git calls may run before they're killed, so a
    /// hung push can't stall the daemon's loop (was Daemon.SyncTimeout).</summary>
    public double SyncTimeoutSeconds { get; init; } = 120;

    /// <summary>The soonest (seconds) a signal (a turn ending, a usage report rewritten) brings the daemon's next refresh forward
    /// after the last one (was Daemon.MinGap).</summary>
    public double RefreshMinGapSeconds { get; init; } = 10;

    /// <summary>The shortest gap (milliseconds) from one tab blink to the next, so each still reads as a blink (was
    /// TabBlinker.MinSpacing).</summary>
    public double TabBlinkMinSpacingMs { get; init; } = 320;

    /// <summary>Minutes a read of the Open Project menu's roster is kept before opening the menu reads it again.</summary>
    public double ProjectsRefreshMinutes { get; init; } = 10;

    /// <summary>Seconds a read of the projects' uncommitted file counts is kept before opening the Open Project menu reads them again.</summary>
    public double ProjectChangesRefreshSeconds { get; init; } = 30;

    /// <summary>Seconds between the dashboard's rereads of the recently closed tickets while ticket changes aren't pushed.</summary>
    public double ClosedTicketsReadSeconds { get; init; } = 60;

    /// <summary>Minutes between the same rereads while the status stream is up and ticket changes are pushed (a safety net only).</summary>
    public double ClosedTicketsReadLiveMinutes { get; init; } = 15;

    /// <summary>A setting's seconds as a <see cref="TimeSpan"/>, kept finite and at least <paramref name="minSeconds"/>, so a hand
    /// edit (negative, NaN, huge) can't throw or spin a timer. No allocation: read it straight off the settings each time.</summary>
    public static TimeSpan SecondsSpan(double seconds, double minSeconds = 0) =>
        TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Clamp(seconds, minSeconds, 1e9) : minSeconds);

    /// <summary>Off by default (M.ORCH-T1585): open a MindAttic.Launcher (a process of its own, ~80 MB) for every signed-in login,
    /// and again when its last tab closes. The tab right-click's Open Project lists the same projects in-app; Run Launcher still
    /// opens one by hand.</summary>
    public bool AutoOpenLaunchers { get; init; }

    /// <summary>Minutes an ended terminal tab stays, for its last lines, before it's closed unasked (M.ORCH-T1599; was a fixed
    /// 24 hours, M.ORCH-T1177): each holds its native renderer (~50 MB) until then.</summary>
    public double EndedTabMinutes { get; init; } = DefaultEndedTabMinutes;

    public const double DefaultEndedTabMinutes = 60;

    /// <summary>On by default (M.ORCH-T1442): closing a CLI tab by hand (its X) whose CLI is still running asks first, in the
    /// dashboard's own modal. A tab whose CLI already ended closes unasked either way.</summary>
    public bool ConfirmCliClose { get; init; } = true;

    /// <summary>Minutes a TermHost running in an orch-test-* temp folder may live before the daemon ends it (M.ORCH-T1623): a test that
    /// opened a real hosted tab and never closed it (it failed first) leaves one running all night. One whose folder is gone is ended
    /// at once; a host in any other folder is never touched.</summary>
    public double TestTermHostMaxMinutes { get; init; } = DefaultTestTermHostMaxMinutes;

    public const double DefaultTestTermHostMaxMinutes = 60;

    /// <summary>Seconds after an Enter typed in a CLI tab that a status read still saying idle ends its presumed busy (M.ORCH-T1413):
    /// the context gate marks a submitted prompt busy within a hook's first moments, so a read this much later that says idle is real.</summary>
    public double PresumeBusyConfirmSeconds { get; init; } = DefaultPresumeBusyConfirmSeconds;

    public const double DefaultPresumeBusyConfirmSeconds = 8;

    /// <summary>The grace period (M.ORCH-T1413): the longest, in seconds, an Enter typed in a CLI tab keeps it and its person's tab
    /// presumed busy with no status read at all.</summary>
    public double PresumeBusyMaxSeconds { get; init; } = DefaultPresumeBusyMaxSeconds;

    public const double DefaultPresumeBusyMaxSeconds = 60;

    /// <summary>Minutes an idle CLI may go unseen before its Team Wall activity line ages to stale and is drawn dimmed (M.ORCH-T965);
    /// 0 never ages one. A CLI waiting on you never ages.</summary>
    public double ActivityStaleMinutes { get; init; } = DefaultActivityStaleMinutes;

    public const double DefaultActivityStaleMinutes = 20;

    /// <summary>How many more times a main-line suite's build is tried when it fails copying a file another process held (MSB3027/MSB3021),
    /// ending that worktree's own leftover test processes first (M.ORCH-T1632; was MainlineTests.FileLockRetries).</summary>
    public double MainTestsLockRetries { get; init; } = DefaultMainTestsLockRetries;

    public const double DefaultMainTestsLockRetries = 2;

    /// <summary>Seconds waited before each such retry (M.ORCH-T1632; was a fixed 30).</summary>
    public double MainTestsLockRetrySeconds { get; init; } = DefaultMainTestsLockRetrySeconds;

    public const double DefaultMainTestsLockRetrySeconds = 30;

    /// <summary>Off by default, since it commits setup-host.ps1 and texts the admin (M.ORCH-T1488): once a day at
    /// <see cref="SqlFreshnessCheckAt"/>, the daemon asks winget for the newest SQL Server Developer and Express (LocalDB) packages,
    /// writes a newer version or bootstrapper URL into setup-host.ps1, commits and pushes it, and texts
    /// <see cref="PsstAlertTo"/> that the host stack's dependencies changed (see <see cref="Setup.SqlServerFreshness"/>).</summary>
    public bool SqlFreshnessCheck { get; init; }

    /// <summary>The local time of day (HH:mm) the daily SQL Server check runs; a day the daemon was off at that time runs it at
    /// its next tick that day.</summary>
    public string SqlFreshnessCheckAt { get; init; } = DefaultSqlFreshnessCheckAt;

    public const string DefaultSqlFreshnessCheckAt = "04:00";

    /// <summary>Off by default (M.ORCH-T1501): on the remote host, build each <c>origin/work/*</c> branch in a worktree of its own
    /// and queue the ones that build for the user's local test run (<see cref="Scheduling.BranchBuilds"/>). Off, nothing changes:
    /// work lands on main as it always has.</summary>
    public bool RemoteBranchBuilds { get; init; }

    /// <summary>Work-branch builds at once across every repo (M.ORCH-T1501): the host's extra RAM decides it.</summary>
    public double BranchBuildsMaxRunning { get; init; } = DefaultBranchBuildsMaxRunning;

    public const double DefaultBranchBuildsMaxRunning = 2;

    /// <summary>Minutes one work-branch build may run before it's stopped and recorded failed (M.ORCH-T1501).</summary>
    public double BranchBuildsTimeoutMinutes { get; init; } = DefaultBranchBuildsTimeoutMinutes;

    public const double DefaultBranchBuildsTimeoutMinutes = 30;

    /// <summary>Seconds between the daemon's looks at the work branches for heads that moved (M.ORCH-T1501).</summary>
    public double BranchBuildsCheckSeconds { get; init; } = DefaultBranchBuildsCheckSeconds;

    public const double DefaultBranchBuildsCheckSeconds = 60;

    /// <summary>The max server memory (MB) the daemon keeps localhost's SQL Server at (M.ORCH-T1621): 0 (the default) leaves SQL
    /// Server's own setting alone; any other value under <see cref="SqlServerMaxMemoryFloorMb"/> is raised to it, never applied
    /// lower, since 1024 MB crashed the instance (error 17300) and only an admin restart in minimal mode recovered it.</summary>
    public double SqlServerMaxMemoryMb { get; init; } = DefaultSqlServerMaxMemoryMb;

    public const double DefaultSqlServerMaxMemoryMb = 0;

    /// <summary>The least <see cref="SqlServerMaxMemoryMb"/> ever applied: a lower value set is raised to this.</summary>
    public const int SqlServerMaxMemoryFloorMb = 4096;

    /// <summary>Minutes an ended Launcher menu tab stays (M.ORCH-T1599): 0 closes it at once, since it has nothing to read.</summary>
    public double EndedLauncherTabMinutes { get; init; }

    /// <summary>Off by default, since it writes tickets into every repo's log: hourly, file GitHub issues
    /// labelled "orchestrator" and CI runs that failed on main as tickets (see <see cref="Tickets.GitHubIntake"/>).</summary>
    public bool GitHubIntake { get; init; }

    /// <summary>The team gateway (M.ORCH-T933): off by default; when on, the daemon keeps it running as a per-user process
    /// (see <see cref="Sessions.GatewaySupervisor"/>). <c>orchestrator gateway</c> shows and changes it.</summary>
    public GatewaySettings Gateway { get; init; } = new();

    /// <summary>Manual priorities as settings.json kept them before the ticket database (M.ORCH-T384): moved into
    /// <c>ticket_queue</c> once by <see cref="Tickets.QueueImport"/> and dropped here, like <see cref="Approved"/>.</summary>
    public Dictionary<string, int>? Priorities { get; set; }

    /// <summary>Approvals the orchestrator kept for tickets the ticket database didn't hold (M.ORCH-T441). Approval lives in the
    /// database's queue alone now, through the MindAttic.Tickets engine (M.ORCH-T761): nothing writes or reads this any more;
    /// <see cref="Tickets.TicketQueue.MoveIntoDatabase"/> moves what's left into that queue once the database holds the
    /// ticket, and drops each entry only once the database is verified to carry it.</summary>
    public List<string> StoryApproved { get; init; } = [];

    /// <summary>Manual priorities kept for tickets the ticket database didn't hold, moved into it like <see cref="StoryApproved"/>.</summary>
    public Dictionary<string, long> StoryPriorities { get; init; } = [];

    /// <summary>The model a ticket's subagent runs on, by ticket id or repo (a name or a wildcard like <c>GTFO.*</c>) → "sonnet"
    /// or "opus" (M.ORCH-T563). A ticket's entry wins over its repo's; with neither, <see cref="Scheduling.ModelChoice"/> picks.</summary>
    public Dictionary<string, string> ModelOverrides { get; init; } = [];

    /// <summary>Legacy (M.ORCH-T762): assignments the orchestrator kept before MindAttic.Tickets owned them (M.ORCH-T668), ticket id →
    /// that user's login. Never written or read for scheduling any more; <see cref="Tickets.TicketQueue.MoveWorkIntoDatabase"/> moves
    /// them into the ticket database (<c>tickets ticket assign</c>), which <see cref="Scheduling.TicketPins"/> reads through the
    /// snapshot.</summary>
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
        "*/Scheduling/Dispatcher.cs", "*/Scheduling/Allocate.cs", "*land*.ps1", "*run.ps1",
    ];
}

/// <summary>A ticket the orchestrator has handed to a session. <see cref="Worktree"/> (the stored name is kept) means it was
/// dispatched for an agent: a CLI's claim or a tab the orchestrator opened. Since M.ORCH-T1122 every one works in the repo's own
/// checkout on main (<see cref="Directory"/> is the repo); only one made before that has a worktree of its own
/// (<see cref="InOwnWorktree"/>).</summary>
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

    /// <summary>A claim for a CLI's subagent (or a tab the orchestrator opened): dispatched, no background agent on it.</summary>
    [JsonIgnore]
    public bool IsClaim => Worktree && Pilot is null && Log is null;

    /// <summary>Whether it works in a worktree of its own rather than the repo's checkout at <paramref name="repoPath"/>: only an
    /// assignment made before M.ORCH-T1122, which is still landed and tidied as it was; nothing makes a new one.</summary>
    public bool InOwnWorktree(string repoPath) => Worktree && !SamePath(Directory, repoPath);

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }
}
