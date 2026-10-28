using System.Windows.Media;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>What a tab's status light says (M.ORCH-T630): the theme's colour for it, and always a word as well.</summary>
public enum TabLight
{
    /// <summary>Signed in and usable: MaGood.</summary>
    Good,

    /// <summary>Signed out: MaBad.</summary>
    Bad,

    /// <summary>Any other state (at its weekly stop line, 5-hour limit hit, signing in, signed in as someone else): MaOther.</summary>
    Other,

    /// <summary>The 'API key' tab: MaWarn, so billing the key is never left on unnoticed.</summary>
    Warning,
}

/// <summary>
/// Status at a glance on the person tab strip (M.ORCH-T630): each person's tab has a status light before its name and its
/// weekly % left (after the M.ORCH-T609 reserve, so the room its CLIs still have) on its far right, so who has room shows
/// without opening the tab; and an 'API key' tab after them, with a warning light, holding the Vault API key's switch, what
/// the CLIs billing it spent today and which CLIs bill it. Every light's word is in the tab's tooltip and accessible name, so
/// colour is never the only cue.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The email whose sign-in is in progress, on the global login's tab (every tab's light reads it).</summary>
    private string? signingInEmail;

    /// <summary>Whether this is the 'API key' tab rather than a person's.</summary>
    public bool IsApiKeyTab => tab.IsApiKey;

    /// <summary>The tab's light: green, red, purple or (the 'API key' tab) orange.</summary>
    public TabLight TabLight => TabStatus.Light;

    /// <summary>The light's word: "Usable", "Signed out", "At its weekly stop line", "API key billing".</summary>
    public string TabStatusWord => TabStatus.Word;

    /// <summary>The light as the library's status, for the tab's MaTabItem.Status (M.ORCH-T667): MaGood, MaBad, MaWarn or MaOther.</summary>
    public MaStatus TabLightStatus => StatusOf(TabLight);

    internal static MaStatus StatusOf(TabLight light) => light switch
    {
        TabLight.Good => MaStatus.Good,
        TabLight.Bad => MaStatus.Bad,
        TabLight.Warning => MaStatus.Warning,
        _ => MaStatus.Other,
    };

    /// <summary>The light in the theme's colour.</summary>
    public Brush TabLightBrush => LightBrush(TabLight);

    internal static Brush LightBrush(TabLight light) => light switch
    {
        TabLight.Good => Palette.Working,
        TabLight.Bad => Palette.Over,
        TabLight.Warning => Palette.Waiting,
        _ => Palette.Other,
    };

    /// <summary>The text beside a person's name (M.ORCH-T793): their live 5-hour and weekly use, each against that person's own
    /// live cutoff ("5hr: 29% | wk: 15%", M.ORCH-T968), the cutoffs moved to the tooltip, and the same 5-hour figure once
    /// more as λ ("λ: 29%", M.ORCH-T1207, replacing the subagent-slot-load reading from M.ORCH-T1194). Unsigned: empty, just the name. On the API key's person (M.ORCH-T906), money: what's left of its credit, else what it spent today.</summary>
    public string TabTrailing => !tab.SignedIn ? ""
        : tab.IsApiKey ? ApiTrailingOf(Spend)
        : TabUsageOf(FiveHourUsed, WeekUsed, MeterReadUtc, NeedsSignInAgain, DateTime.UtcNow, Lambda);

    /// <summary>When this login's meter was last read: the pushed reading's, else its <c>claude-usage.json</c>'s; null on the global
    /// login's tab, or never read (M.ORCH-T1119).</summary>
    private DateTime? MeterReadUtc
    {
        get
        {
            if (ScopeEmail is not { } email) return null;
            DateTime? file = Root.meterReadUtc.TryGetValue(email, out var f) ? f : null;
            return Live?.ReadUtc is { } live && (file is null || live > file) ? live : file;
        }
    }

    /// <summary>Whether this login's meter can't be read until the person signs in again (M.ORCH-T1119).</summary>
    private bool NeedsSignInAgain => CutoffEmail is { } e && Root.signInAgain.Contains(e);

    /// <summary>
    /// The tab's usage text (M.ORCH-T1119): <see cref="TabUsagePairsOf"/> while the meter's reading is fresh; "sign in again" when
    /// only the person can get it read again; and once it's older than <c>org usage</c>'s <see cref="UsageReport.StaleAfter"/>,
    /// "stale · read 11h ago" instead of its figures, never shown as current (the tab sizes itself to its own content, M.ORCH-T1096,
    /// so the old figures go to the tooltip, <see cref="TabStaleLineOf"/>).
    /// </summary>
    internal static string TabUsageOf(double? fiveHourUsed, double? weekUsed, DateTime? readUtc, bool signInAgain, DateTime nowUtc, double? lambda) =>
        signInAgain ? "sign in again"
        : IsStale(readUtc, nowUtc) ? $"stale · read {Formatting.Ago(readUtc, nowUtc)}"
        : TabUsagePairsOf(fiveHourUsed, weekUsed, lambda);

    private static bool IsStale(DateTime? readUtc, DateTime nowUtc) => readUtc is { } r && nowUtc - r > UsageReport.StaleAfter;

    /// <summary>For the tooltip, once the reading is stale (M.ORCH-T1119): " Claude's meter was last read 11h ago (5hr: 0% | wk: 91%),
    /// so these figures may be out of date."; empty while it's fresh or never read.</summary>
    internal static string TabStaleLineOf(double? fiveHourUsed, double? weekUsed, DateTime? readUtc, DateTime nowUtc, double? lambda) =>
        IsStale(readUtc, nowUtc)
            ? $" Claude's meter was last read {Formatting.Ago(readUtc, nowUtc)} ({TabUsagePairsOf(fiveHourUsed, weekUsed, lambda)}), so these figures may be out of date."
            : "";

    internal string TabStaleLine => tab.SignedIn && !tab.IsApiKey ? TabStaleLineOf(FiveHourUsed, WeekUsed, MeterReadUtc, DateTime.UtcNow, Lambda) : "";

    /// <summary>The cutoffs the visible label leaves out (M.ORCH-T968), for the tooltip and accessible name: "cutoffs 5-hour 98%, weekly 90%".</summary>
    internal string TabCutoffsLine => tab.SignedIn && !tab.IsApiKey ? $" Cutoffs: 5-hour {FiveHourCutoff}%, weekly {WeeklyCutoff}%." : "";

    /// <summary>"$37.66 left", or "$1.20 today" with no starting credit set; "" before the first read.</summary>
    internal static string ApiTrailingOf(ApiSpend? spend) =>
        spend is null ? "" : spend.LeftUsd is { } left ? $"{ApiSpend.Usd(left)} left" : $"{ApiSpend.Usd(spend.TodayUsd)} today";

    /// <summary>"5hr: 29% | wk: 15%", with the 5-hour figure repeated as λ, "5hr: 29% | wk: 15% | λ: 29%"
    /// (M.ORCH-T968, M.ORCH-T1207): each window's % used, rounded for display only ("—" never read).</summary>
    internal static string TabUsagePairsOf(double? fiveHourUsed, double? weekUsed, double? lambda) =>
        $"5hr: {UsedOf(fiveHourUsed)}% | wk: {UsedOf(weekUsed)}%" + (lambda is { } l ? $" | λ: {UsedOf(l)}%" : "");

    private static string UsedOf(double? used) =>
        used is { } u ? Math.Max(0, u).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "—";

    /// <summary>λ (M.ORCH-T1207, replacing the subagent-slot-load reading from M.ORCH-T1194): the same 5-hour window % used
    /// as <see cref="FiveHourUsed"/>, shown a second time next to "5hr" and "wk" so the pace figure reads at a glance.</summary>
    internal double? Lambda => FiveHourUsed;

    private (TabLight Light, string Word) TabStatus => tab.IsApiKey
        ? ApiStatusOf(tab.SignedIn, Spend)
        : StatusOf(tab.SignedIn, string.Equals(Root.signingInEmail, tab.Email, StringComparison.OrdinalIgnoreCase) && tab.Email is not null,
            SignedInAsOther, WeekUsed, WeeklyCutoff, FiveHourUsed, FiveHourCutoff, NeedsSignInAgain ? ClaudeMeterProbe.SignInAgain(CutoffEmail) : null);

    /// <summary>The API key's person's light (M.ORCH-T906): red with no key in the Vault, purple once its credit is spent, else green.</summary>
    internal static (TabLight Light, string Word) ApiStatusOf(bool hasKey, ApiSpend? spend) =>
        !hasKey ? (TabLight.Bad, "No Claude key in the MindAttic Vault")
        : spend is { OutOfCredit: true } ? (TabLight.Other, "Its API credit is spent")
        : (TabLight.Good, "Ready: its CLIs bill API credits");

    /// <summary>A person's light and its word: red signed out, purple while signing in, signed in as someone else, at its weekly
    /// stop line (its own weekly cutoff, M.ORCH-T707) or at its 5-hour cutoff, else green. Purple too, saying what to run, while
    /// its meter can't be read until the person signs in again (<paramref name="signInAgain"/>, M.ORCH-T1119).</summary>
    internal static (TabLight Light, string Word) StatusOf(bool signedIn, bool signingIn, string? signedInAs, double? weekUsed,
        int stopPercent, double? fiveHourUsed, int fiveHourStopPercent = 100, string? signInAgain = null)
    {
        if (signingIn) return (TabLight.Other, "Signing in");
        if (!signedIn) return (TabLight.Bad, "Signed out");
        if (signedInAs is not null) return (TabLight.Other, $"Login mismatch: signed in as {signedInAs}");
        if (signInAgain is not null) return (TabLight.Other, $"Claude's meter can't be read: {signInAgain}");
        if (weekUsed >= stopPercent) return (TabLight.Other, $"At its weekly stop line ({stopPercent}%)");
        if (fiveHourUsed >= fiveHourStopPercent)
            return (TabLight.Other, fiveHourStopPercent >= 100 ? "5-hour limit hit" : $"At its 5-hour cutoff ({fiveHourStopPercent}%)");
        return (TabLight.Good, "Signed in and usable");
    }

    // ── The status icon (M.ORCH-T757) ───────────────────────────────────────

    /// <summary>Every member's own cutoffs and the weekly allowances in force, read once a refresh by the global login's tab.</summary>
    private IReadOnlyList<UsageCutoff> cutoffs = [];
    private IReadOnlyList<WeeklyAllowance> allowances = [];

    /// <summary>A busy tab blinks once per this many tokens (the TabBlinkTokens setting), read with the settings each refresh.</summary>
    private int tabBlinkTokens = MindAttic.Orchestrator.Settings.OrchestratorSettings.DefaultTabBlinkTokens;

    private TabBlinker? blinker;
    private TabBlinker Blinker => blinker ??= new TabBlinker(tab.Key.Length == 0 ? "~global" : tab.Key);

    /// <summary>The one timer every tab's blinks run on, on the global login's tab; running only while a blink is queued.</summary>
    private DispatcherTimer? blinkTimer;

    /// <summary>This login's email for its cutoffs: its roster row's, else the tab's own.</summary>
    private string? CutoffEmail => OwnRow?.Email ?? ScopeEmail;

    /// <summary>Where this login's week is cut off: its own weekly cutoff, or the wall (100) while it's allowed past it.</summary>
    private int WeeklyCutoff => WeeklyAllowances.For(Root.allowances, CutoffEmail, DateTime.UtcNow) is not null
        ? 100 : UsageCutoffs.WeeklyFor(Root.cutoffs, CutoffEmail);

    /// <summary>Where this login's 5-hour window is cut off: its own 5-hour cutoff.</summary>
    private int FiveHourCutoff => UsageCutoffs.FiveHourFor(Root.cutoffs, CutoffEmail);

    // ── The person page's stats (M.ORCH-T763) ───────────────────────────────

    /// <summary>The Cutoffs tile: "98% · 90%", the 5-hour then the weekly cutoff.</summary>
    public string CutoffsHeadline => $"{FiveHourCutoff}% · {WeeklyCutoff}%";

    /// <summary>Under it: which is which, and whether the login is allowed past its weekly one or keeps the defaults.</summary>
    public string CutoffsCaption => "5-hour · weekly cutoff" +
        (WeeklyAllowances.For(Root.allowances, CutoffEmail, DateTime.UtcNow) is not null ? " · allowed past the weekly one"
            : UsageCutoffs.Custom(Root.cutoffs, CutoffEmail) is null ? " · the defaults" : " · its own");

    /// <summary>The Last updated tile: how long ago Claude's meter for this login was read ("3m ago"), "—" never.</summary>
    public string LastUpdatedHeadline => Live is { } l && (OwnRow?.ReadUtc is not { } read || l.ReadUtc > read)
        ? Formatting.Ago(l.ReadUtc, DateTime.UtcNow) : LastUpdatedOf(OwnRow, DateTime.UtcNow);

    internal static string LastUpdatedOf(AccountRow? row, DateTime nowUtc) => row?.ReadUtc is { } r ? Formatting.Ago(r, nowUtc) : "—";

    /// <summary>Under it: its plan, and plainly when the reading is stale (M.ORCH-T684).</summary>
    public string LastUpdatedCaption => WeeklyAccountLine is { Length: > 0 } line ? line : "Claude's meter not read yet";

    /// <summary>The tab's icon: pause idle, a blinking green play arrow busy, gray disabled.</summary>
    public TabActivity TabActivity => tab.IsApiKey
        ? ActivityOf(usable: tab.SignedIn, pastCutoff: Spend is { OutOfCredit: true }, WorkingCount > 0 || HostCount > 0, Blinker.Grew || Blinker.Pending > 0)
        : ActivityOf(tab.SignedIn, PastCutoffOf(WeekUsed, WeeklyCutoff, FiveHourUsed, FiveHourCutoff), WorkingCount > 0 || HostCount > 0,
            Blinker.Grew || Blinker.Pending > 0);

    /// <summary>Disabled when the login can't take work (signed out, or the 'API key' tab with API billing off) or is past its
    /// cutoff; busy while one of its CLIs is alive (mid-turn, idle at a prompt, or just spent tokens) -- a live CLI counts
    /// whether it's hosted inside the Orchestrator's own Terminals panel or runs outside it (M.LNCH-T77); else idle.</summary>
    internal static TabActivity ActivityOf(bool usable, bool pastCutoff, bool working, bool spending) =>
        !usable || pastCutoff ? TabActivity.Disabled : working || spending ? TabActivity.Busy : TabActivity.Idle;

    /// <summary>Whether a login is at or past its weekly or its 5-hour cutoff (M.ORCH-T707/T742); never read is not past.</summary>
    internal static bool PastCutoffOf(double? weekUsed, int weeklyCutoff, double? fiveHourUsed, int fiveHourCutoff) =>
        weekUsed >= weeklyCutoff || fiveHourUsed >= fiveHourCutoff;

    // Every tab shows its activity icon (the API key's person too, since M.ORCH-T906), its word in the tooltip and accessible name.
    public bool IsTabIdle => IsPersonTab && TabActivity == TabActivity.Idle && !TabAllStopped;
    public bool IsTabBusy => IsPersonTab && TabActivity == TabActivity.Busy && !TabAllStopped;
    public bool IsTabDisabled => IsPersonTab && TabActivity == TabActivity.Disabled;

    /// <summary>Every open CLI of this person has stopped (M.ORCH-T890): the green play arrow turns into a steady pause icon (M.ORCH-T1116),
    /// so it shows at a glance that all of their CLIs are done. Never with no CLI open, and never while the login is disabled.</summary>
    public bool IsTabAllStopped => IsPersonTab && TabActivity != TabActivity.Disabled && TabAllStopped;

    /// <summary>The tab's gray "zzz" box (M.ORCH-T1173): idle, or every open CLI has stopped, and none of them is asking (the
    /// orange box below takes that instead).</summary>
    public bool ShowsIdleBadge => (IsTabIdle || IsTabAllStopped) && !TabNeedsYou;

    /// <summary>The tab's green play-arrow box (M.ORCH-T1173): busy, and none of this person's CLIs is asking.</summary>
    public bool ShowsBusyBadge => IsTabBusy && !TabNeedsYou;

    /// <summary>How many CLIs are open on this login when every one of them is idle; 0 when none is open or any is busy.</summary>
    private int StoppedCliCount
    {
        get
        {
            try { return AllStoppedOf(snapshot?.Agents, snapshot?.Repos.SelectMany(r => r.Hosts)); }
            catch (Exception) { return 0; } // never let a half-read snapshot break the tab strip
        }
    }

    private bool TabAllStopped => StoppedCliCount > 0;

    /// <summary>
    /// The number of open CLIs when all of them are idle, else 0, judged per CLI as <c>orchestrator busy</c> does (M.ORCH-T873):
    /// a CLI works while its turn runs, while any of its subagents runs, while it holds an /allocate claim, or while it holds on
    /// a prompt. A Launcher tab with no CLI row behind it (no prompt typed yet, its state unread) is unknown, never idle.
    /// </summary>
    internal static int AllStoppedOf(IEnumerable<MindAttic.Orchestrator.Activity.AgentBoardRow>? agents, IEnumerable<HostSession>? hosts)
    {
        var rows = agents?.ToList() ?? [];
        if (rows.Count == 0) return 0;
        if (rows.Any(a => a.State != MindAttic.Orchestrator.Activity.AgentState.Waiting || a.Subagents.Count > 0 || a.Claims.Count > 0 || a.Asking))
            return 0;
        var covered = rows.Where(a => a.Host is not null).Select(a => a.Host!.PipeName).ToHashSet(StringComparer.Ordinal);
        if (hosts?.Any(h => !covered.Contains(h.PipeName)) == true) return 0;
        return rows.Count;
    }

    /// <summary>The icon in words, so it's never only a picture: "Busy", "Idle", "All N CLIs stopped", or why it's disabled.</summary>
    public string TabActivityWord => TabActivity switch
    {
        TabActivity.Disabled => tab.IsApiKey ? tab.SignedIn ? "Disabled: its API credit is spent" : "Disabled: no key in the Vault"
            : tab.SignedIn ? "Disabled: past its cutoff" : "Disabled: signed out",
        _ when IsPersonTab && StoppedCliCount is > 0 and var n => n == 1 ? "Stopped: its one open CLI is idle" : $"Stopped: all {n} open CLIs are idle",
        TabActivity.Busy => "Busy",
        _ => "Idle",
    };

    /// <summary>The play arrow's opacity: dimmed for the moment of a blink.</summary>
    public double TabIconOpacity => Blinker.IsDim ? 0.25 : 1.0;

    /// <summary>The tokens this tab counts for its blinks: its login's uncached tokens today, as the dashboard already read them.</summary>
    private long TabTokens => snapshot?.UsageToday.Fresh ?? 0;

    /// <summary>Hands this tab's token count from the refresh just applied to its blinker; a busy tab's new blinks start the
    /// shared blink timer, and one that isn't busy drops any it had queued.</summary>
    private void CountTabTokens(DateTime nowUtc)
    {
        var queued = Blinker.Count(TabTokens, Root.tabBlinkTokens, nowUtc);
        if (TabActivity != TabActivity.Busy) Blinker.Clear();
        else if (queued > 0) Root.StartBlinking();
        Raise(nameof(TabIconOpacity));
    }

    private void StartBlinking()
    {
        if (blinkTimer is null)
        {
            blinkTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(40) };
            blinkTimer.Tick += (_, _) => TickBlinks(DateTime.UtcNow);
        }
        blinkTimer.Start();
    }

    /// <summary>Moves every tab's blink on; the timer stops once no tab has one queued or on screen.</summary>
    private void TickBlinks(DateTime nowUtc)
    {
        var active = false;
        foreach (var t in loginTabs)
        {
            if (t.blinker is not { } b) continue;
            if (b.Tick(nowUtc)) t.Raise(nameof(TabIconOpacity));
            active |= b.IsActive;
        }
        if (!active) blinkTimer?.Stop();
    }

    /// <summary>The 5-hour window's % used: the reading the status stream just pushed (M.ORCH-T854), else the snapshot's.</summary>
    private double? FiveHourUsed => LiveFiveHour ?? SnapshotFiveHour;

    /// <summary>The snapshot's 5-hour % used: its own pace's against a real limit, else the roster's last reading.</summary>
    private double? SnapshotFiveHour => Budget is { CurrentBlock: not null, BlockLimitIsPeak: false, BlockLimit: > 0 } b
        ? b.BlockPercent : OwnRow?.FiveHourUsedAt(DateTime.UtcNow);

    /// <summary>On a per-CLI login's tab, who its folder is signed in to when that's another account's email; else null.</summary>
    private string? SignedInAsOther => MismatchOf(ScopeEmail, snapshot?.Account, accountRows);

    internal static string? MismatchOf(string? email, string? signedInAccount, IReadOnlyList<AccountRow> rows)
    {
        if (email is null || signedInAccount is null || signedInAccount == ClaudeAccount.ApiKey) return null;
        var who = rows.FirstOrDefault(r => r.Account == signedInAccount)?.Email;
        return who is not null && !who.Equals(email, StringComparison.OrdinalIgnoreCase) ? who : null;
    }

    internal static TokenUsage ApiKeyUsageOf(BillingStatus? status, Snapshot? all)
    {
        if (status is null || all is null) return default;
        var keyed = status.Clis.Where(c => c.Bills == BillingModes.Api).Select(c => c.SessionId).ToHashSet(StringComparer.Ordinal);
        return all.Repos.SelectMany(r => r.Sessions).Where(s => keyed.Contains(s.SessionId))
            .Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday);
    }

    /// <summary>"2 CLIs bill the API key: MindAttic.Foo (pid 12), …" or "No CLI bills the API key."</summary>
    internal static string ApiKeyClisOf(BillingStatus? status)
    {
        var keyed = status?.Clis.Where(c => c.Bills == BillingModes.Api).ToList() ?? [];
        return keyed.Count == 0 ? "No CLI bills the API key."
            : $"{keyed.Count} CLI{(keyed.Count == 1 ? "" : "s")} bill{(keyed.Count == 1 ? "s" : "")} the API key: " +
              string.Join(", ", keyed.Select(c => $"{c.Where} (pid {c.Pid})")) + ".";
    }

    // ── The API key's person's page (M.ORCH-T906): money, not Claude's 5-hour and weekly meters ──────────────────────

    /// <summary>Its money as the last read had it; null before one (and on every other tab's page, which never shows it).</summary>
    public ApiSpend? Spend => full?.ApiSpend;

    /// <summary>The 5-hour and weekly cards, the cutoffs and the 5-hour chart: every page but the API key's, which has no window.</summary>
    public bool ShowsClaudeMeters => !tab.IsApiKey;

    /// <summary>"$37.66 left", or "—" with no starting credit set.</summary>
    public string ApiLeftHeadline => Spend?.LeftUsd is { } left ? ApiSpend.Usd(left) : "—";

    /// <summary>"of $50.00 since Oct 26", or how to set a starting credit.</summary>
    public string ApiLeftCaption => Spend is { CreditUsd: { } c } s
        ? $"of {ApiSpend.Usd(c)}{(s.SinceUtc is { } at ? $" since {at.ToLocalTime():MMM d}" : "")}"
        : "no starting credit: orchestrator billing credit <usd>";

    /// <summary>"$12.34", spent since the credit started (or everything on record).</summary>
    public string ApiSpentHeadline => Spend is { } s ? ApiSpend.Usd(s.SpentUsd) : "—";

    /// <summary>"$1.20 today · $0.45/h lately".</summary>
    public string ApiSpentCaption => Spend is { } s ? $"{ApiSpend.Usd(s.TodayUsd)} today · {ApiSpend.Usd(s.PerHourUsd)}/h lately" : "not read yet";

    /// <summary>When the credit runs out at the recent rate: "Thu 3:40 PM"; "—" when it won't (nothing spent lately, no credit).</summary>
    public string ApiRunsOutHeadline => Spend is { OutOfCredit: true } ? "Spent"
        : Spend?.RunsOutUtc(DateTime.UtcNow) is { } at ? at.ToLocalTime().ToString("ddd h:mm tt", System.Globalization.CultureInfo.CurrentCulture) : "—";

    /// <summary>What the runs-out card is based on.</summary>
    public string ApiRunsOutCaption => Spend is { OutOfCredit: true } ? "its credit is used up: set a new one to give it work"
        : Spend?.CreditUsd is null ? "set a starting credit to see when it runs out"
        : $"at the last {ApiSpend.RateWindow.TotalHours:0} hours' rate";

    /// <summary>The burndown chart's caption, in words, so the picture is never the only cue.</summary>
    public string ApiBurnText => ApiBurnTextOf(Spend, DateTime.UtcNow);

    internal static string ApiBurnTextOf(ApiSpend? spend, DateTime nowUtc) => spend switch
    {
        null => "",
        { CreditUsd: null } => $"No starting credit set, so there's nothing to burn down: {spend.Line}. Prices are Anthropic's list prices applied to the transcripts' tokens, an estimate.",
        { OutOfCredit: true } => $"The credit is spent: {spend.Line}.",
        _ => $"{spend.Line}." + (spend.RunsOutUtc(nowUtc) is { } at ? $" At that rate it runs out around {at.ToLocalTime():ddd MMM d h:mm tt}." : " Nothing spent lately, so it isn't running down.") +
             " Prices are Anthropic's list prices applied to the transcripts' tokens, an estimate.",
    };

    /// <summary>Raises everything the tab strip shows for this tab.</summary>
    private void RaiseTabStatus()
    {
        foreach (var name in new[] { nameof(TabLight), nameof(TabLightStatus), nameof(TabStatusWord), nameof(TabLightBrush), nameof(TabTrailing), nameof(TabTip), nameof(TabHoverTip),
                                     nameof(TabActivity), nameof(IsTabIdle), nameof(IsTabBusy), nameof(IsTabDisabled), nameof(IsTabAllStopped), nameof(ShowsIdleBadge), nameof(ShowsBusyBadge), nameof(TabActivityWord),
                                     nameof(TabAskingCount), nameof(TabNeedsYou), nameof(TabNeedsYouTip), nameof(Spend), nameof(ApiLeftHeadline), nameof(ApiLeftCaption),
                                     nameof(ApiSpentHeadline), nameof(ApiSpentCaption), nameof(ApiRunsOutHeadline), nameof(ApiRunsOutCaption),
                                     nameof(ApiBurnText), nameof(ShowsClaudeMeters) })
            Raise(name);
    }

    /// <summary>Marks <paramref name="email"/>'s sign-in as in progress (null: none) and relights every tab.</summary>
    private void SetSigningInEmail(string? email)
    {
        Root.signingInEmail = email;
        foreach (var t in Root.loginTabs) t.RaiseTabStatus();
    }
}
