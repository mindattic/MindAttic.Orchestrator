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
    /// the orchestrator can't see (claude.ai, other machines). M.ORCH-T1462 lowered this to 96% (2026-11-05) as a
    /// burst-overshoot safety margin, since a single tool call's size can't be predicted before it runs; M.ORCH-T1750
    /// (2026-11-17) put it back to 98% at the user's explicit, repeated request ("ending almost perfectly on 98%...
    /// nothing else is acceptable"), now that the restored per-call hold (<see cref="ThrottleAgents"/>) slows a CLI
    /// down throughout the window again, not just at this target.</summary>
    public int PaceTargetPercent { get; init; } = 98;

    /// <summary>A login's 5-hour or weekly use (percent) under which the pace guards are dormant (M.ORCH-T1592): no pace holds,
    /// no subagent allowance cap and no cool-off, so work runs full speed until either window reaches it. The hard stops
    /// (the pace target, the weekly stop, a wall Claude reports) and the memory guard are untouched.</summary>
    public double PaceGuardStartPercent { get; init; } = DefaultPaceGuardStartPercent;

    /// <summary>The default <see cref="PaceGuardStartPercent"/>.</summary>
    public const double DefaultPaceGuardStartPercent = 90;

    /// <summary>The weekly use a login with no 5-hour window (a solo Ultra account) is paced to reach by its weekly reset
    /// (M.ORCH-T1885): a straight line from 0% at the week's start to this at its reset; above it, new subagents start slower
    /// and tool calls are held. Never past the login's own weekly cutoff. A login with a 5-hour window is left to its cutoffs.</summary>
    public double WeeklyPaceTargetPercent { get; init; } = DefaultWeeklyPaceTargetPercent;

    /// <summary>The default <see cref="WeeklyPaceTargetPercent"/>.</summary>
    public const double DefaultWeeklyPaceTargetPercent = 95;

    /// <summary>The share of each account's week kept back (M.ORCH-T609): every CLI on an account stops at
    /// <see cref="WeeklyStopPercent"/> (100 minus this) of that account's week instead of burning it all. 0 to
    /// <see cref="MaxWeeklyReservePercent"/>; applies to each login on its own.</summary>
    public int WeeklyReservePercent { get; init; } = DefaultWeeklyReservePercent;

    /// <summary>The constants default weekly reserve percent, max weekly reserve percent.</summary>
    public const int DefaultWeeklyReservePercent = 10, MaxWeeklyReservePercent = 50;

    /// <summary>The percent of an account's week at which its CLIs stop: 100 minus <see cref="WeeklyReservePercent"/>
    /// (kept to 0–<see cref="MaxWeeklyReservePercent"/>, so a hand-edited value can't stop it at nothing). Weekly-aware pacing
    /// lands the week on this same line (<see cref="WeeklyAwareTarget"/>, M.ORCH-T680), so on or off it's one line.</summary>
    [JsonIgnore]
    public int WeeklyStopPercent => WeeklyStopFor(WeeklyReservePercent);

    /// <summary>Weekly stop for.</summary>
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

    /// <summary>The default weekly aware line percent constant.</summary>
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

    /// <summary>The constants linear profile, front loaded profile.</summary>
    public const string LinearProfile = "linear", FrontLoadedProfile = "front-loaded";

    /// <summary>The pace profiles.</summary>
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

    /// <summary>On (the default, restored M.ORCH-T1750): while the forecast says the burn won't last until the reset,
    /// running Claude Code agents wait before each tool call (the pace-gate hook, up to <see cref="MaxAgentDelaySeconds"/>),
    /// so they slow down instead of running the window well past its pace curve before the hard stop at
    /// <see cref="PaceTargetPercent"/> even notices. M.ORCH-T706 hard-coded this off (2026-10-23, "run to the cutoff,
    /// nothing throttled or held before it"); found live 2026-11-17: with no per-call hold, a lone CLI with no subagents to
    /// cap (<see cref="Budget.Pacer.SubagentAllowance"/> only governs subagents) ran a window from ~20% to 91.6% while its
    /// pace line sat at 34%, since nothing but the final stop could have caught it. Weekly-aware pacing, the front-loaded
    /// profile and API-key failover stay hard-coded off, as the owner asked when only this one was restored.</summary>
    public bool ThrottleAgents { get; init; } = true;

    /// <summary>The slash commands exempt from orchestration pacing (M.ORCH-T198): a turn whose prompt starts with one skips
    /// every pacing gate (no pace delay, no subagent hold) until it ends, and a Skill call naming one is let through anywhere;
    /// the hard 5-hour and weekly stops still refuse its tool calls (bughunt 2026-10-27). The pace gate reads this live from
    /// settings.json; its spend is still counted.</summary>
    // A saved list still naming the retired /throttle or /unthrottle reads as /pacing, which replaced them (M.ORCH-T483).
    public List<string> ExemptCommands { get => exemptCommands; init => exemptCommands = RenameRetired(value); }
    private readonly List<string> exemptCommands = [.. DefaultExemptCommands];

    /// <summary>The default exempt commands.</summary>
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

    /// <summary>The default edit claims constant.</summary>
    public const string DefaultEditClaims = "Warn";

    /// <summary>Minutes a file's edit claim stands after its agent's last edit of it (M.ORCH-T1193), so a crashed agent's claim lapses;
    /// a commit naming the file, the subagent's stop or its CLI's exit releases it sooner.</summary>
    public double EditClaimMinutes { get; init; } = DefaultEditClaimMinutes;

    /// <summary>The default edit claim minutes constant.</summary>
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

    /// <summary>The default provider.</summary>
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

    /// <summary>The most Claude CLIs <c>orchestrator start</c> opens on one login of its own (M.ORCH-T1736); 0 is no limit. A
    /// login's quota caps its throughput, not its CLI count, and each CLI holds about 1.2 GB, so at the limit a start with a
    /// prompt goes to that login's CLI in the same repo or at the workspace root instead. A tab opened by hand in the dashboard
    /// is never limited.</summary>
    public int MaxClisPerLogin { get; init; }

    /// <summary>The repo max sessions.</summary>
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

    /// <summary>The constants default tab blink tokens, min tab blink tokens, max tab blink tokens.</summary>
    public const int DefaultTabBlinkTokens = 1000, MinTabBlinkTokens = 1, MaxTabBlinkTokens = 10_000_000;

    /// <summary>The size of one task, one unit of work, in uncached tokens (M.ORCH-T1905): a dispatched agent aims to finish
    /// its ticket within it. <see cref="MinUnitTokenBudget"/> to <see cref="MaxUnitTokenBudget"/>; read it clamped through
    /// <see cref="Scheduling.WorkUnits.Budget"/>.</summary>
    public int UnitTokenBudget { get; init; } = DefaultUnitTokenBudget;

    /// <summary>The constants default, min and max unit token budget.</summary>
    public const int DefaultUnitTokenBudget = 80_000, MinUnitTokenBudget = 20_000, MaxUnitTokenBudget = 400_000;

    /// <summary>How long the dashboard's first load waits on the ticket database before it lifts its loader anyway (M.ORCH-T1936):
    /// a pegged database held it on the full-screen loader for 20 minutes (M.ORCH-T1922). Past it the dashboard shows and says
    /// tickets are loading until the read lands. <see cref="MinFirstLoadWaitSeconds"/> to <see cref="MaxFirstLoadWaitSeconds"/>;
    /// read it clamped through <see cref="FirstLoadWait"/>.</summary>
    public int FirstLoadWaitSeconds { get; init; } = DefaultFirstLoadWaitSeconds;

    /// <summary>The constants default, min and max first load wait seconds.</summary>
    public const int DefaultFirstLoadWaitSeconds = 20, MinFirstLoadWaitSeconds = 1, MaxFirstLoadWaitSeconds = 600;

    /// <summary>The first load's wait (M.ORCH-T1936), clamped to its bounds; the default when no settings were read.</summary>
    public static TimeSpan FirstLoadWait(OrchestratorSettings? s) =>
        TimeSpan.FromSeconds(Math.Clamp(s?.FirstLoadWaitSeconds ?? DefaultFirstLoadWaitSeconds, MinFirstLoadWaitSeconds, MaxFirstLoadWaitSeconds));

    /// <summary>The dashboard's tab trailing text (M.ORCH-T1255): symbols, "Σ: 15% | λ: 29%" (the default, M.ORCH-T1251), or
    /// words, "wk: 15% | 5h: 29%" -- same figures and the same omit-the-5-hour-part-while-it's-unknown rule either way.</summary>
    public bool SymbolTabLabels { get; init; } = true;

    /// <summary>The dashboard's login sidebar (M.ORCH-T1759): on (the default), a collapsible tree -- each login a node with
    /// a chevron that expands to its own Usage entry and open CLI/Launcher panes, right there in the sidebar, and the body
    /// (terminal, stats, 5-hour chart, tickets) takes the whole right side of the window. Off: the old flat person-tab strip
    /// plus a fixed, always-shown CLI tab bar under the selected person.</summary>
    public bool TreeMenuLayout { get; init; } = true;

    /// <summary>The gap below each login's whole tree node (its header and, expanded, its open panes) in the sidebar
    /// (M.ORCH-T1824, the owner's mockup): enough that one person's section visibly ends before the next one's colour
    /// stripe begins, instead of the old 2px row gap every sibling row already had.</summary>
    public double LoginTreeGutterPx { get; init; } = DefaultLoginTreeGutterPx;

    /// <summary>The default login tree gutter constant.</summary>
    public const double DefaultLoginTreeGutterPx = 10;

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

    /// <summary>Whether a new item in the owner inbox (M.ORCH-T1689, <see cref="Status.OwnerInbox"/>: a ticket to approve, one
    /// parked on the owner, a stale or orphaned one nobody picked up, a login to sign in again) is texted to
    /// <see cref="PsstAlertTo"/> through Psst, once per item. Off, new items are still noted as seen, so turning it on later
    /// texts no backlog.</summary>
    public bool InboxAlerts { get; init; } = true;

    /// <summary>Whether a reminder that nags (M.ORCH-T1864, <see cref="Reminders.NagBook"/>) is also texted to
    /// <see cref="PsstAlertTo"/> through Psst each time it comes due on its backoff (M.ORCH-T1934). Off, it's still toasted on
    /// the dashboard and pushed to <see cref="NotifyUrl"/>.</summary>
    public bool NagAlerts { get; init; } = true;

    /// <summary>The default psst alert to constant.</summary>
    public const string DefaultPsstAlertTo = "ryan";

    /// <summary>The API key person's starting credit in US dollars (M.ORCH-T906): what its burndown starts from, entered by you with
    /// <c>orchestrator billing credit &lt;usd&gt;</c> (Anthropic exposes no balance to read; its cost report, M.ORCH-T953, gives
    /// only what's spent). Null: none set, so its page
    /// shows only what's been spent.</summary>
    public double? ApiCreditUsd { get; init; }

    /// <summary>When <see cref="ApiCreditUsd"/> was entered: spend is counted against it from then on.</summary>
    public DateTime? ApiCreditSinceUtc { get; init; }

    /// <summary>Minutes between two reads of Anthropic's cost report (M.ORCH-T953), made by the daemon only while an Admin API key
    /// is in the Vault (<see cref="Billing.OrchestratorKeys.AdminKey"/>); 0 never reads it. What's spent against
    /// <see cref="ApiCreditUsd"/> is then Anthropic's own figures up to the last read and the list-price estimate since.</summary>
    public double ApiCostReadMinutes { get; init; } = DefaultApiCostReadMinutes;

    /// <summary>The default api cost read minutes constant.</summary>
    public const double DefaultApiCostReadMinutes = 15;

    /// <summary>The dashboard's theme (M.ORCH-T678): "Light" or "Dark", as its sun/moon switch saved it. Null (never switched),
    /// or anything else, is dark, the default; the dashboard reads it at start and writes it at each switch.</summary>
    public string? Theme { get; init; }

    /// <summary>The dashboard's font (M.ORCH-T1274): "Segoe UI", the font it always had and the default, or "Outfit", bundled in
    /// the dashboard (Fonts/). Every window, menu and tooltip follows it, live; the terminal panes keep their monospace font.</summary>
    public string Font { get; init; } = DefaultFont;

    /// <summary>The default font constant.</summary>
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

    /// <summary>KB <c>disk-cleanup.log</c> may grow to before the hourly prune rotates it to <c>disk-cleanup.1.log</c> (M.ORCH-T1681).
    /// 0 never rotates it.</summary>
    public int DiskCleanupLogMaxKb { get; init; } = 1024;

    /// <summary>Rotated copies of <c>disk-cleanup.log</c> kept (<c>.1</c> newest); older ones are deleted (M.ORCH-T1681).</summary>
    public int DiskCleanupLogKeep { get; init; } = 3;

    /// <summary>Hours between the orchestrator database's maintenance runs: its age-prune, then a SQLite VACUUM when enough of the
    /// file is free pages (M.ORCH-T1681). 0 turns it off.</summary>
    public double DbMaintenanceHours { get; init; } = 24;

    /// <summary>Days the database keeps its event rows (<c>term_audit</c>, the pace gate's <c>gate_events</c>) before maintenance
    /// deletes older ones (M.ORCH-T1681). 0 keeps them for good.</summary>
    public int DbEventKeepDays { get; init; } = 90;

    /// <summary>Days the database keeps its token history (<c>usage_history</c> by day, <c>ticket_costs</c> by last use) before
    /// maintenance deletes older rows (M.ORCH-T1681). 0, the default, keeps them for good.</summary>
    public int DbHistoryKeepDays { get; init; }

    /// <summary>Percent of the SQLite file that must be free pages before maintenance VACUUMs it (M.ORCH-T1681): high enough that a
    /// file with little to reclaim isn't rewritten, so its committed copy isn't churned for nothing. 0 VACUUMs every run.</summary>
    public double DbVacuumMinFreePercent { get; init; } = 20;

    /// <summary>Days Claude Code keeps session transcripts: written into each login's <c>cleanupPeriodDays</c> (M.ORCH-T843, a
    /// setting since M.ORCH-T1681). 0 leaves each login's own value alone.</summary>
    public int TranscriptKeepDays { get; init; } = Setup.AgentGate.TranscriptKeepDays;

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

    /// <summary>The hard disk floor (M.ORCH-T1679): free GB on the system drive (or the <see cref="WorktreeRoot"/>'s drive) under
    /// which no new work starts, once a self-cleanup has run and the drive is still under it (<see cref="Activity.DiskGuard"/>).
    /// Running work is never stopped for it. 0 turns the hold off.</summary>
    public double DiskHoldGb { get; init; } = 5;

    /// <summary>GB a held drive must be clear of <see cref="DiskHoldGb"/> before new work starts again, so a drive hovering on
    /// the floor doesn't flap: at the defaults new work resumes at 7 GB free, not 5.</summary>
    public double DiskHoldResumeMarginGb { get; init; } = 2;

    /// <summary>Hours a %TEMP%\claude session folder is kept, whatever the free space (M.ORCH-T1466): one created longer ago than
    /// this, with nothing in it created or written for as long, goes once its session isn't running (an ended one goes anyway, see
    /// <see cref="Sessions.SessionTempPruner"/>); a running session keeps its folder and only its scratchpad entries that old and
    /// that quiet go. 0 turns it off.</summary>
    public double TempRetentionHours { get; init; } = DefaultTempRetentionHours;

    /// <summary>The default temp retention hours constant.</summary>
    public const double DefaultTempRetentionHours = 48;

    /// <summary>Hours a claimed ticket's own scratch dir (<see cref="Activity.TicketScratch"/>, its agent's <c>ORCH_SCRATCH</c>) is
    /// leased for in the resource ledger (M.ORCH-T1676); claiming it again renews the lease. Past it, a sweeper may reclaim it
    /// even if its ticket's claim never ended. 0 means no expiry: it's kept until released.</summary>
    public double ScratchLeaseHours { get; init; } = DefaultScratchLeaseHours;

    /// <summary>The default scratch lease hours constant.</summary>
    public const double DefaultScratchLeaseHours = 48;

    /// <summary>Whether the ledger sweep (M.ORCH-T1677, <see cref="Activity.LedgerSweeper"/>) deletes nothing and only writes
    /// what it would delete, with bytes, to the disk-cleanup log. On by default: the sweep is new, so it's watched first.
    /// <c>orchestrator disk-cleanup --ledger-run</c> runs it for real whatever this says.</summary>
    public bool LedgerSweepDryRun { get; init; } = true;

    /// <summary>Minutes nothing may have been created or written in a ledger entry's folder before the sweep deletes it, even
    /// once its owner is gone or its ticket delivered: an agent closing its ticket may still be copying something out. 0: no wait.</summary>
    public double LedgerSweepQuietMinutes { get; init; } = 30;

    /// <summary>Hours a crash leftover in %TEMP% that no ledger row covers (<c>orchestrator-review-*.txt</c>,
    /// <c>mindattic-token-check-*</c>) must be old and quiet before the sweep deletes it. 0 turns that sweep off.</summary>
    public double LedgerSweepLeftoverHours { get; init; } = 24;

    /// <summary>Hours the <c>bin</c>/<c>obj</c> folders of a daemon worktree (main-tests, main-bisect, local-tests, merge-main,
    /// branch-*) must have gone unwritten, with no build or test running there, before the sweep deletes them. 0 turns it off.</summary>
    public double LedgerSweepBuildOutputHours { get; init; } = 72;

    /// <summary>Days a released ledger row whose folder is gone is kept as history before the sweep drops it. 0 keeps them.</summary>
    public double LedgerSweepHistoryDays { get; init; } = 30;

    /// <summary>Seconds the background daemon waits before starting its loop again after it died (M.ORCH-T1615); doubles with each
    /// consecutive failure up to <see cref="DaemonRestartMaxSeconds"/> (M.ORCH-T1619).</summary>
    public double DaemonRestartSeconds { get; init; } = 15;

    /// <summary>The longest the daemon waits between restarts of its loop (seconds).</summary>
    public double DaemonRestartMaxSeconds { get; init; } = 300;

    /// <summary>How many extra times a database write retries after SQL Server's write lock stayed busy (M.ORCH-T1620; was the
    /// const <see cref="OrchestratorDb.BusyRetries"/>, 3 then 6 under M.ORCH-T1617). A live store pushes it into that static.</summary>
    public int DbBusyRetries { get; init; } = 6;

    /// <summary>How many heavy processes (a game player, a Unity/Unreal/s&amp;box build or editor, a long soak) may run on the
    /// machine at once (M.ORCH-T1680): the pace gate makes a shell command matching the heavy pattern list take a machine-wide
    /// lease first and refuses it while that many are held (<see cref="Setup.HeavyGate"/>, <see cref="Scheduling.HeavyLease"/>).
    /// 0 turns the lease off.</summary>
    public int HeavyProcessLimit { get; init; } = DefaultHeavyProcessLimit;

    /// <summary>What <see cref="HeavyProcessLimit"/> is until it's set: the agent standard's one heavy process per machine.</summary>
    public const int DefaultHeavyProcessLimit = 1;

    /// <summary>More heavy commands for the heavy-process lease (M.ORCH-T1680), added to <see cref="Setup.HeavyGate.DefaultPatternsJson"/>
    /// and a repo's own <see cref="Setup.HeavyGate.RepoPatternsFile"/>, never replacing them: JSON in the same shape
    /// (<c>{"patterns":[{"name":..,"regex":..,"at":..}]}</c>, or the bare list). Empty adds nothing.</summary>
    public string HeavyProcessPatterns { get; init; } = "";

    /// <summary>Seconds a heavy lease just taken counts as held before a process running its command must be found (M.ORCH-T1680):
    /// the time between the hook letting the call through and the command's process existing.</summary>
    public int HeavyProcessGraceSeconds { get; init; } = DefaultHeavyProcessGraceSeconds;

    /// <summary>What <see cref="HeavyProcessGraceSeconds"/> is until it's set.</summary>
    public const int DefaultHeavyProcessGraceSeconds = 60;

    /// <summary>Seconds a heavy lease past its grace still counts as held while the machine's processes can't be listed (M.ORCH-T1680).</summary>
    public int HeavyProcessUnknownMaxSeconds { get; init; } = DefaultHeavyProcessUnknownMaxSeconds;

    /// <summary>What <see cref="HeavyProcessUnknownMaxSeconds"/> is until it's set.</summary>
    public const int DefaultHeavyProcessUnknownMaxSeconds = 900;

    /// <summary>Seconds a heavy lease's process may have started before the lease's own time and still count as its process
    /// (clocks read apart, M.ORCH-T1680).</summary>
    public int HeavyProcessSlackSeconds { get; init; } = DefaultHeavyProcessSlackSeconds;

    /// <summary>What <see cref="HeavyProcessSlackSeconds"/> is until it's set.</summary>
    public const int DefaultHeavyProcessSlackSeconds = 5;

    /// <summary>The memory guard (M.ORCH-T1494): when RAM runs low, new work stops (subagents, CLIs, main-line tests) and running
    /// work finishes, the way the 5-hour and weekly stops scale back; past <see cref="MemoryCriticalGb"/> the newest subagent is
    /// shed, past <see cref="MemoryEmergencyGb"/> tool calls are refused. /pacing off doesn't turn it off; only this does.</summary>
    public bool MemoryGuard { get; init; } = true;

    /// <summary>Physical RAM in use (percent) under which the guard is dormant (M.ORCH-T1591): no holds and no subagent cap by
    /// free RAM, so everything runs full speed until the machine is near the edge. Windows 11 keeps RAM as full as it can on
    /// purpose, so at this line the guard first collects garbage and trims working sets (<see cref="Activity.MemoryReclaim"/>)
    /// rather than holding work; 98 (was 90).</summary>
    public double MemoryGuardStartPercent { get; init; } = 98;

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

    // ── Health check thresholds (M.ORCH-T2035): the lines the health probes grade each reading against ──

    /// <summary>Free physical RAM (GB) under which the machine probe grades RAM degraded.</summary>
    public double HealthRamDegradedGb { get; init; } = 4;

    /// <summary>Free physical RAM (GB) under which the machine probe grades RAM unhealthy.</summary>
    public double HealthRamUnhealthyGb { get; init; } = 2;

    /// <summary>Commit charge (percent of the commit limit) at or past which the machine probe grades commit degraded.</summary>
    public double HealthCommitDegradedPercent { get; init; } = 80;

    /// <summary>Commit charge percent at or past which the machine probe grades commit unhealthy.</summary>
    public double HealthCommitUnhealthyPercent { get; init; } = 90;

    /// <summary>Free space (GB) on any one drive under which the machine probe grades that disk degraded.</summary>
    public double HealthDiskFreeDegradedGb { get; init; } = 20;

    /// <summary>Free space (GB) on any one drive under which the machine probe grades that disk unhealthy.</summary>
    public double HealthDiskFreeUnhealthyGb { get; init; } = 5;

    /// <summary>An Azure SQL database's DTU (or vCore CPU) percent at or past which the database probe grades it degraded.</summary>
    public double HealthDtuDegradedPercent { get; init; } = 80;

    /// <summary>DTU (or vCore CPU) percent at or past which the database probe grades it unhealthy (the 2026-11-22 stall sat at 100).</summary>
    public double HealthDtuUnhealthyPercent { get; init; } = 95;

    /// <summary>An Azure SQL database's storage percent (of its max size) at or past which the database probe grades it degraded.</summary>
    public double HealthStorageDegradedPercent { get; init; } = 80;

    /// <summary>Storage percent at or past which the database probe grades it unhealthy.</summary>
    public double HealthStorageUnhealthyPercent { get; init; } = 95;

    /// <summary>Round-trip milliseconds at or past which the links probe grades a link (dashboard, daemon, TermHost, ticket DB,
    /// Azure) degraded.</summary>
    public double HealthLinkLatencyDegradedMs { get; init; } = 1000;

    /// <summary>Round-trip milliseconds at or past which the links probe grades a link unhealthy.</summary>
    public double HealthLinkLatencyUnhealthyMs { get; init; } = 5000;

    // ── Health line self-correction (M.ORCH-T2057): each line above is the tightest it goes; the bounds below the loosest ──

    /// <summary>Whether the daemon learns the health lines from the probes' history (<see cref="Health.HealthLearning"/>): a line
    /// the machine's normal reading sits past moves just past it, never looser than its bound; false grades on the lines as set.</summary>
    public bool HealthLearn { get; init; } = true;

    /// <summary>The fewest readings of a check the daemon learns its lines from (60 is half an hour at the 30 s sample).</summary>
    public int HealthLearnMinSamples { get; init; } = 60;

    /// <summary>How far past the machine's normal reading (percent of it) a learned degraded line is put.</summary>
    public double HealthLearnMarginPercent { get; init; } = 5;

    /// <summary>Which reading of the history counts as the machine's normal one: this percentile of the readings, worst first
    /// (the mirror percentile, 100 minus it, for a lower-is-worse line).</summary>
    public double HealthLearnPercentile { get; init; } = 90;

    /// <summary>How many line moves the daemon keeps to read back with their reasons.</summary>
    public int HealthLearnChangesKept { get; init; } = 200;

    /// <summary>Seconds a daemon tick may run before daemon:tick reads degraded (the daemon logs a tick this long as slow).</summary>
    public double HealthTickDegradedSeconds { get; init; } = 60;

    /// <summary>Seconds a daemon tick may run before daemon:tick reads unhealthy (the pace gates' stale line).</summary>
    public double HealthTickUnhealthySeconds { get; init; } = 180;

    /// <summary>The lowest free-RAM (GB) a learned RAM line may go.</summary>
    public double HealthRamLearnFloorGb { get; init; } = 1;

    /// <summary>The highest commit percent a learned commit line may go.</summary>
    public double HealthCommitLearnCeilingPercent { get; init; } = 97;

    /// <summary>The lowest free GB a learned disk line may go.</summary>
    public double HealthDiskFreeLearnFloorGb { get; init; } = 2;

    /// <summary>The highest DTU percent a learned database-load line may go.</summary>
    public double HealthDtuLearnCeilingPercent { get; init; } = 99;

    /// <summary>The highest storage percent a learned database-storage line may go.</summary>
    public double HealthStorageLearnCeilingPercent { get; init; } = 98;

    /// <summary>The highest round-trip milliseconds a learned link line may go.</summary>
    public double HealthLinkLatencyLearnCeilingMs { get; init; } = 15000;

    // ── Probe runs (M.ORCH-T2037): the daemon launches each probe CLI on its own interval; 0 turns that probe off ──

    /// <summary>Seconds between the daemon's runs of <c>orchestrator probe machine</c> (0: never).</summary>
    public double HealthProbeMachineSeconds { get; init; } = 30;

    /// <summary>Seconds between the daemon's runs of <c>orchestrator probe links</c> (0: never).</summary>
    public double HealthProbeLinksSeconds { get; init; } = 30;

    /// <summary>Seconds between the daemon's runs of <c>orchestrator probe azure</c> (0: never; each run lists every database through az).</summary>
    public double HealthProbeAzureSeconds { get; init; } = 300;

    // ── Alerts (M.ORCH-T2039): change-only, after N in a row; degraded never alerts ──

    /// <summary>Whether a check turning unhealthy, and its recovery, is texted to <see cref="PsstAlertTo"/> through Psst. Off,
    /// it's still logged, toasted and pushed to <see cref="NotifyUrl"/>.</summary>
    public bool HealthAlerts { get; init; } = true;

    /// <summary>Unhealthy results in a row before a check alerts unhealthy (Kubernetes' <c>failureThreshold</c>; at least 1).</summary>
    public int HealthFailureThreshold { get; init; } = 2;

    /// <summary>Non-unhealthy results in a row before an alerted check alerts its recovery (<c>successThreshold</c>; at least 1).</summary>
    public int HealthSuccessThreshold { get; init; } = 1;

    /// <summary>Minutes between prunes of old log entries and finished terminal hosts' logs (was <see cref="LogPruner.Every"/>).</summary>
    public double LogPruneMinutes { get; init; } = 60;

    /// <summary>Local backups kept under the backup base: after each Backup &amp; Restore backup, all but the newest this many
    /// dated folders are deleted (<see cref="Backup.BackupService.Prune"/>, M.ORCH-T2064). 0 keeps every one. Default 30, the
    /// Launcher's own <c>backup --keep 30</c> example.</summary>
    public int BackupKeepLocal { get; init; } = 30;

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

    /// <summary>The default of <see cref="ElevatedTermWaitSeconds"/>: long enough for a human to answer the UAC prompt.</summary>
    public const double DefaultElevatedTermWaitSeconds = 120;

    /// <summary>Seconds an elevated (Administrator) CLI tab waits for its TermHost to register, the UAC prompt included
    /// (M.ORCH-T1656; <c>TermLauncher.StartElevated</c>).</summary>
    public double ElevatedTermWaitSeconds { get; init; } = DefaultElevatedTermWaitSeconds;

    /// <summary>Seconds between the dashboard's rereads of each login's meter state (when it was last read, who must sign in again).</summary>
    public double MeterStatesSeconds { get; init; } = 30;

    /// <summary>Seconds between the dashboard's re-raises of every login's busy/subagent-sum figures (M.ORCH-T1838): a tick of
    /// its own, independent of the poll/push refresh, so those figures stay current between them.</summary>
    public double BusyCountRefreshSeconds { get; init; } = 5;

    /// <summary>Milliseconds the dashboard's UI thread may keep input waiting before the stall is logged with the work it was in
    /// (M.ORCH-T1733, <c>UiStallProbe</c>); 0 turns the probe off.</summary>
    public double UiStallLogMs { get; init; } = 100;

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

    /// <summary>A setting's minutes as a <see cref="TimeSpan"/>; <paramref name="fallback"/> when it isn't a positive finite number.</summary>
    public static TimeSpan MinutesSpan(double minutes, TimeSpan fallback) =>
        double.IsFinite(minutes) && minutes > 0 ? TimeSpan.FromMinutes(Math.Min(minutes, 1e6)) : fallback;

    /// <summary>Off by default (M.ORCH-T1585): open a MindAttic.Launcher (a process of its own, ~80 MB) for every signed-in login,
    /// and again when its last tab closes. The tab right-click's Open Project lists the same projects in-app; Run Launcher still
    /// opens one by hand.</summary>
    public bool AutoOpenLaunchers { get; init; }

    /// <summary>Off by default (M.ORCH-T2103, was on since M.ORCH-T1951): on, every login signed in as itself keeps exactly one
    /// General Prompt CLI (a Claude CLI at the workspace root) open on it, reopened when it closes or exits. Off: none is opened
    /// or reopened; the login's '+ Add' node opens one by hand.</summary>
    public bool AutoOpenGeneralPrompts { get; init; }

    /// <summary>On by default (M.ORCH-T1442): closing a CLI tab by hand (its X) whose CLI is still running asks first, in the
    /// dashboard's own modal. A tab whose CLI already ended closes unasked either way.</summary>
    public bool ConfirmCliClose { get; init; } = true;

    /// <summary>Whether the dashboard window exposes its controls to UI Automation clients (screen readers, UI test tools).
    /// Off by default (M.ORCH-T1738): with any such client listening, WPF re-walks the window's whole control tree after every
    /// layout pass, which made showing a CLI tab freeze typing for 4 to 40 seconds. Read when the dashboard starts.</summary>
    public bool UiAutomation { get; init; }

    /// <summary>Minutes an ended terminal tab stays, for its last lines, before it's closed unasked (M.ORCH-T1599; was a fixed
    /// 24 hours, M.ORCH-T1177): each holds its native renderer (~50 MB) until then.</summary>
    public double EndedTabMinutes { get; init; } = DefaultEndedTabMinutes;

    /// <summary>The default ended tab minutes constant.</summary>
    public const double DefaultEndedTabMinutes = 60;

    /// <summary>Minutes a CLI tab may stay hidden before its native terminal renderer (~50 MB) is released (M.ORCH-T1737): its
    /// connection to the host stays, paused, and a new renderer is made and given the host's screen when the tab is shown
    /// again. 0 never releases one.</summary>
    public double HiddenRendererMinutes { get; init; } = DefaultHiddenRendererMinutes;

    /// <summary>The default hidden renderer minutes constant.</summary>
    public const double DefaultHiddenRendererMinutes = 10;

    /// <summary>Minutes a CLI's turn must have been idle before the dashboard parks it (M.ORCH-T2074, part of M.ORCH-T1739): closes
    /// its claude process (about 1.2 GB) and keeps its tab as a stub that resumes the conversation when shown. Only a CLI the park
    /// check passes (<see cref="Sessions.ParkCheck"/>: /allocate off, no pending ScheduleWakeup, CronCreate or /loop, no pace hold,
    /// nothing running, not on a /goal) and whose tab isn't the one on screen is parked. 0 never parks one.</summary>
    public int ParkIdleCliMinutes { get; init; } = DefaultParkIdleCliMinutes;

    /// <summary>The default park idle CLI minutes constant.</summary>
    public const int DefaultParkIdleCliMinutes = 60;

    /// <summary>The longest <see cref="ParkIdleCliMinutes"/> (a week).</summary>
    public const int MaxParkIdleCliMinutes = 10080;

    /// <summary>Minutes between the dashboard's passes that name a workspace CLI tab ("General", "MindAttic") for what its CLI
    /// is doing (M.ORCH-T1817, <c>AutoTabName</c>): the first pass comes this long after the tab is first seen, and a tab not
    /// renamed by hand is reconsidered at every one. 0 never renames a tab.</summary>
    public double AutoTabNameMinutes { get; init; } = DefaultAutoTabNameMinutes;

    /// <summary>The default auto tab name minutes constant.</summary>
    public const double DefaultAutoTabNameMinutes = 5;

    /// <summary>The most characters an auto-given tab name has (M.ORCH-T1817).</summary>
    public double AutoTabNameMaxChars { get; init; } = DefaultAutoTabNameMaxChars;

    /// <summary>The default auto tab name length constant.</summary>
    public const double DefaultAutoTabNameMaxChars = 32;

    /// <summary>Milliseconds before the CLI row's hover tooltip (its usage chart, M.ORCH-T1814) opens (<c>ToolTipService.InitialShowDelay</c>
    /// on the owning tab/row, M.ORCH-T1823): 0 opens it the instant the pointer lands, with no hover-delay wait.</summary>
    public double CliTooltipDelayMs { get; init; } = DefaultCliTooltipDelayMs;

    /// <summary>The default CLI tooltip delay constant: 20% below its old hardcoded 0ms (M.ORCH-T1823) - still 0, now live-dialable.</summary>
    public const double DefaultCliTooltipDelayMs = 0;

    /// <summary>The folder, under the workspace root and under each repo, whose markdown files are the saved plans the
    /// dashboard's Plans button lists (M.ORCH-T1826, <see cref="Status.PlanCatalog"/>): half-formed ideas and features not
    /// ready for an implementation ticket yet.</summary>
    public string PlansFolder { get; init; } = DefaultPlansFolder;

    /// <summary>The default plans folder constant.</summary>
    public const string DefaultPlansFolder = @"docs\plans";

    /// <summary>The most characters of a plan's summary (and of its status) the Plans list shows (M.ORCH-T1826); a longer one
    /// is cut at a word and ended with an ellipsis.</summary>
    public int PlanSummaryMaxChars { get; init; } = DefaultPlanSummaryMaxChars;

    /// <summary>The default plan summary length constant.</summary>
    public const int DefaultPlanSummaryMaxChars = 200;
    /// <summary>The shortest plan summary length.</summary>
    public const int MinPlanSummaryMaxChars = 20;
    /// <summary>The longest plan summary length.</summary>
    public const int MaxPlanSummaryMaxChars = 2000;

    /// <summary>KB read from the start of each plan file to find its title, status and summary (M.ORCH-T1826): the rest of a
    /// very large file is never read.</summary>
    public int PlanHeadKb { get; init; } = DefaultPlanHeadKb;

    /// <summary>The default plan head size constant.</summary>
    public const int DefaultPlanHeadKb = 8;
    /// <summary>The smallest plan head size.</summary>
    public const int MinPlanHeadKb = 1;
    /// <summary>The largest plan head size.</summary>
    public const int MaxPlanHeadKb = 1024;

    /// <summary>Minutes a TermHost running in an orch-test-* temp folder may live before the daemon ends it (M.ORCH-T1623): a test that
    /// opened a real hosted tab and never closed it (it failed first) leaves one running all night. One whose folder is gone is ended
    /// at once; a host in any other folder is never touched.</summary>
    public double TestTermHostMaxMinutes { get; init; } = DefaultTestTermHostMaxMinutes;

    /// <summary>The default test term host max minutes constant.</summary>
    public const double DefaultTestTermHostMaxMinutes = 60;

    /// <summary>Seconds after an Enter typed in a CLI tab that a status read still saying idle ends its presumed busy (M.ORCH-T1413):
    /// the context gate marks a submitted prompt busy within a hook's first moments, so a read this much later that says idle is real.</summary>
    public double PresumeBusyConfirmSeconds { get; init; } = DefaultPresumeBusyConfirmSeconds;

    /// <summary>The default presume busy confirm seconds constant.</summary>
    public const double DefaultPresumeBusyConfirmSeconds = 8;

    /// <summary>The grace period (M.ORCH-T1413): the longest, in seconds, an Enter typed in a CLI tab keeps it and its person's tab
    /// presumed busy with no status read at all.</summary>
    public double PresumeBusyMaxSeconds { get; init; } = DefaultPresumeBusyMaxSeconds;

    /// <summary>The default presume busy max seconds constant.</summary>
    public const double DefaultPresumeBusyMaxSeconds = 60;

    /// <summary>Minutes an idle CLI may go unseen before its Team Wall activity line ages to stale and is drawn dimmed (M.ORCH-T965);
    /// 0 never ages one. A CLI waiting on you never ages.</summary>
    public double ActivityStaleMinutes { get; init; } = DefaultActivityStaleMinutes;

    /// <summary>The default activity stale minutes constant.</summary>
    public const double DefaultActivityStaleMinutes = 20;

    /// <summary>The least seconds between two activity bus messages for one CLI session (M.ORCH-T962): a busy CLI's newer pictures
    /// inside the gap are held and the latest goes out when it passes. 0 sends each at once.</summary>
    public double ActivityCoalesceSeconds { get; init; } = DefaultActivityCoalesceSeconds;

    /// <summary>The default activity coalesce seconds constant.</summary>
    public const double DefaultActivityCoalesceSeconds = 0.5;

    /// <summary>Seconds between the daemon's slow rescans of the activity records folder (M.ORCH-T962): a change its file watcher
    /// missed is read then, and silent sessions are checked for an end.</summary>
    public double ActivityRescanSeconds { get; init; } = DefaultActivityRescanSeconds;

    /// <summary>The default activity rescan seconds constant.</summary>
    public const double DefaultActivityRescanSeconds = 30;

    /// <summary>Minutes a CLI session may send no activity, with its process gone, before the bus says it ended and stops keeping it
    /// (M.ORCH-T962).</summary>
    public double ActivityEndedMinutes { get; init; } = DefaultActivityEndedMinutes;

    /// <summary>The default activity ended minutes constant.</summary>
    public const double DefaultActivityEndedMinutes = 10;

    /// <summary>How many more times a main-line suite's build is tried when it fails copying a file another process held (MSB3027/MSB3021),
    /// ending that worktree's own leftover test processes first (M.ORCH-T1632; was MainlineTests.FileLockRetries).</summary>
    public double MainTestsLockRetries { get; init; } = DefaultMainTestsLockRetries;

    /// <summary>The default main tests lock retries constant.</summary>
    public const double DefaultMainTestsLockRetries = 2;

    /// <summary>Seconds waited before each such retry (M.ORCH-T1632; was a fixed 30).</summary>
    public double MainTestsLockRetrySeconds { get; init; } = DefaultMainTestsLockRetrySeconds;

    /// <summary>The default main tests lock retry seconds constant.</summary>
    public const double DefaultMainTestsLockRetrySeconds = 30;

    /// <summary>Minutes one suite may run before it's ended and counted as not completed: the main-line suites and
    /// <c>orchestrator branches test</c> (M.ORCH-T1662; was a fixed 90).</summary>
    public double MainTestsTimeoutMinutes { get; init; } = DefaultMainTestsTimeoutMinutes;

    /// <summary>The default main tests timeout minutes constant.</summary>
    public const double DefaultMainTestsTimeoutMinutes = 90;

    /// <summary>The most suite runs a red main line's bisect makes to find the commit that broke it, after the two that check the
    /// failure reproduces at the red head and not at the last green commit (M.ORCH-T1687). 0 turns the bisect off.</summary>
    public double MainBisectMaxSteps { get; init; } = DefaultMainBisectMaxSteps;

    /// <summary>The default main bisect max steps constant.</summary>
    public const double DefaultMainBisectMaxSteps = 8;

    /// <summary>Minutes a red main line's bisect may take in all before it stops and blames no one (M.ORCH-T1687).</summary>
    public double MainBisectTimeoutMinutes { get; init; } = DefaultMainBisectTimeoutMinutes;

    /// <summary>The default main bisect timeout minutes constant.</summary>
    public const double DefaultMainBisectTimeoutMinutes = 240;

    /// <summary>Minutes <c>orchestrator branches test</c> waits for the memory guard to clear before each branch's run; past it the
    /// branch is skipped, nothing reported (M.ORCH-T1662).</summary>
    public double BranchTestsMemoryWaitMinutes { get; init; } = DefaultBranchTestsMemoryWaitMinutes;

    /// <summary>The default branch tests memory wait minutes constant.</summary>
    public const double DefaultBranchTestsMemoryWaitMinutes = 30;

    /// <summary>Minutes <c>orchestrator branches test</c> waits for the disk floor (<see cref="DiskHoldGb"/>) to lift before each
    /// branch's run, as it does on RAM; past it the branch is skipped, nothing reported (M.ORCH-T1692).</summary>
    public double BranchTestsDiskWaitMinutes { get; init; } = DefaultBranchTestsDiskWaitMinutes;

    /// <summary>The default branch tests disk wait minutes constant.</summary>
    public const double DefaultBranchTestsDiskWaitMinutes = 30;

    /// <summary>Off by default, since it commits setup-host.ps1 and texts the admin (M.ORCH-T1488): once a day at
    /// <see cref="SqlFreshnessCheckAt"/>, the daemon asks winget for the newest SQL Server Developer and Express (LocalDB) packages,
    /// writes a newer version or bootstrapper URL into setup-host.ps1, commits and pushes it, and texts
    /// <see cref="PsstAlertTo"/> that the host stack's dependencies changed (see <see cref="Setup.SqlServerFreshness"/>).</summary>
    public bool SqlFreshnessCheck { get; init; }

    /// <summary>The local time of day (HH:mm) the daily SQL Server check runs; a day the daemon was off at that time runs it at
    /// its next tick that day.</summary>
    public string SqlFreshnessCheckAt { get; init; } = DefaultSqlFreshnessCheckAt;

    /// <summary>The default sql freshness check at constant.</summary>
    public const string DefaultSqlFreshnessCheckAt = "04:00";

    /// <summary>Off by default (M.ORCH-T1501): on the remote host, build each <c>origin/work/*</c> branch in a worktree of its own
    /// and queue the ones that build for the user's local test run (<see cref="Scheduling.BranchBuilds"/>). Off, nothing changes:
    /// work lands on main as it always has.</summary>
    public bool RemoteBranchBuilds { get; init; }

    /// <summary>Work-branch builds at once across every repo (M.ORCH-T1501): the host's extra RAM decides it.</summary>
    public double BranchBuildsMaxRunning { get; init; } = DefaultBranchBuildsMaxRunning;

    /// <summary>The default branch builds max running constant.</summary>
    public const double DefaultBranchBuildsMaxRunning = 2;

    /// <summary>Minutes one work-branch build may run before it's stopped and recorded failed (M.ORCH-T1501).</summary>
    public double BranchBuildsTimeoutMinutes { get; init; } = DefaultBranchBuildsTimeoutMinutes;

    /// <summary>The default branch builds timeout minutes constant.</summary>
    public const double DefaultBranchBuildsTimeoutMinutes = 30;

    /// <summary>Seconds between the daemon's looks at the work branches for heads that moved (M.ORCH-T1501).</summary>
    public double BranchBuildsCheckSeconds { get; init; } = DefaultBranchBuildsCheckSeconds;

    /// <summary>The default branch builds check seconds constant.</summary>
    public const double DefaultBranchBuildsCheckSeconds = 60;

    /// <summary>Minutes between the client's Scheduler job's fetches of its host's ready-to-test queue (M.ORCH-T1634,
    /// <see cref="Scheduling.BranchQueue"/>); only on a client with <see cref="RemoteBranchBuilds"/> on.</summary>
    public double BranchQueueFetchMinutes { get; init; } = DefaultBranchQueueFetchMinutes;

    /// <summary>The default branch queue fetch minutes constant.</summary>
    public const double DefaultBranchQueueFetchMinutes = 10;

    /// <summary>On the host (hub phase 1, M.ORCH-T1640): how many minutes after its gateway last saw an invited member (a token
    /// minted, sessions listed, a tickets command) they still count as connected, for the dashboard's connected-members readout
    /// and <c>orchestrator machine members</c>.</summary>
    public double MemberConnectedMinutes { get; init; } = DefaultMemberConnectedMinutes;

    /// <summary>The default member connected minutes constant.</summary>
    public const double DefaultMemberConnectedMinutes = 3;

    /// <summary>Off by default (hub phase 2, M.ORCH-T1641): on the host, org members start, stop and type into their own CLIs (the
    /// session's owner is the member whose login it runs on), and another member's interact grant needs the owner's consent. Off,
    /// or on any machine but the host, the gateway decides exactly as before (<see cref="Setup.HubPolicy"/>).</summary>
    public bool HubMemberSessions { get; init; }

    /// <summary>Off by default (hub phase 2): with <see cref="HubMemberSessions"/>, the org accepts that members' CLIs run as the
    /// host's one Windows user (which can read every login's credentials), so members may type into them without per-member
    /// Windows users.</summary>
    public bool HubTrustedOrg { get; init; }

    /// <summary>Off by default (hub phase 2): with <see cref="HubMemberSessions"/>, members' CLIs run under their own Windows accounts
    /// (<c>orchestrator machine isolate</c>; the accounts themselves are made by an admin, docs/REMOTE_HOST.md).</summary>
    public bool HubMemberWindowsUsers { get; init; }

    /// <summary>Hub phase 2: how many seconds a consent request waits for the session owner's answer before the viewer is left
    /// watching.</summary>
    public double HubConsentSeconds { get; init; } = DefaultHubConsentSeconds;

    /// <summary>The default hub consent seconds constant.</summary>
    public const double DefaultHubConsentSeconds = Term.TermHubPolicy.DefaultConsentSeconds;

    /// <summary>Hub phase 2b (M.ORCH-T1667): how many seconds the gateway waits for a TermHost it started under a member's own
    /// Windows account to register before answering the member's start.</summary>
    public double HubMemberStartWaitSeconds { get; init; } = DefaultHubMemberStartWaitSeconds;

    /// <summary>The default hub member start wait seconds constant.</summary>
    public const double DefaultHubMemberStartWaitSeconds = Term.TermHubPolicy.DefaultStartWaitSeconds;

    /// <summary>Hub phase 2: on a member's client, how often (seconds) the dashboard asks the host for consent requests on the
    /// member's own CLIs, to show each as a toast.</summary>
    public double HubConsentPollSeconds { get; init; } = DefaultHubConsentPollSeconds;

    /// <summary>The default hub consent poll seconds constant.</summary>
    public const double DefaultHubConsentPollSeconds = 5;

    /// <summary>The max server memory (MB) the daemon keeps localhost's SQL Server at (M.ORCH-T1621): 0 (the default) leaves SQL
    /// Server's own setting alone; any other value under <see cref="SqlServerMaxMemoryFloorMb"/> is raised to it, never applied
    /// lower, since 1024 MB crashed the instance (error 17300) and only an admin restart in minimal mode recovered it.</summary>
    public double SqlServerMaxMemoryMb { get; init; } = DefaultSqlServerMaxMemoryMb;

    /// <summary>The default sql server max memory mb constant.</summary>
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

    /// <summary>The default risky files.</summary>
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

    /// <summary>The agent working it (M.ORCH-T1743, <see cref="Term.AgentIds"/>): the stable id of the tab's CLI the dispatcher
    /// opened, which outlives its pid, a <c>/clear</c> and a restart, as <see cref="Cli"/> (a pid) doesn't. Null when unknown: a
    /// claim or background agent's assignment, or one made before agents had ids.</summary>
    public string? AgentId { get; init; }

    /// <summary>The role it's worked as (M.ORCH-T1746, <see cref="Scheduling.Roles"/>): the ticket's <c>role:</c> label, else
    /// <see cref="Scheduling.Roles.Default"/>. Null for an assignment made before roles were recorded, or not by a claim.</summary>
    public string? Role { get; init; }

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
