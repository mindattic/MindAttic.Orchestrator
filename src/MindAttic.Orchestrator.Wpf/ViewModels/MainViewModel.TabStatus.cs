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
    /// live cutoff ("5hr: 10/98% | wk: 55/90%"), the cutoffs read from /org limit (or the wall while allowed past the weekly one),
    /// never a fixed number. On the 'API key' tab, the tokens the CLIs billing it spent today.</summary>
    public string TabTrailing => tab.IsApiKey ? $"{Formatting.Tokens(ApiKeyUsageToday.Fresh)} today"
        : TabUsagePairsOf(FiveHourUsed, FiveHourCutoff, WeekUsed, WeeklyCutoff);

    /// <summary>"5hr: 10/98% | wk: 55/90%": each window's % used (rounded, "—" never read) over its cutoff.</summary>
    internal static string TabUsagePairsOf(double? fiveHourUsed, int fiveHourCutoff, double? weekUsed, int weeklyCutoff) =>
        $"5hr: {UsedOf(fiveHourUsed)}/{fiveHourCutoff}% | wk: {UsedOf(weekUsed)}/{weeklyCutoff}%";

    private static string UsedOf(double? used) =>
        used is { } u ? Math.Max(0, u).ToString("0", System.Globalization.CultureInfo.InvariantCulture) : "—";

    private (TabLight Light, string Word) TabStatus => tab.IsApiKey
        ? (TabLight.Warning, IsOnApiKey ? "API key billing every CLI" : "API key on standby (Claude Team bills)")
        : StatusOf(tab.SignedIn, string.Equals(Root.signingInEmail, tab.Email, StringComparison.OrdinalIgnoreCase) && tab.Email is not null,
            SignedInAsOther, WeekUsed, WeeklyCutoff, FiveHourUsed, FiveHourCutoff);

    /// <summary>A person's light and its word: red signed out, purple while signing in, signed in as someone else, at its weekly
    /// stop line (its own weekly cutoff, M.ORCH-T707) or at its 5-hour cutoff, else green.</summary>
    internal static (TabLight Light, string Word) StatusOf(bool signedIn, bool signingIn, string? signedInAs, double? weekUsed,
        int stopPercent, double? fiveHourUsed, int fiveHourStopPercent = 100)
    {
        if (signingIn) return (TabLight.Other, "Signing in");
        if (!signedIn) return (TabLight.Bad, "Signed out");
        if (signedInAs is not null) return (TabLight.Other, $"Login mismatch: signed in as {signedInAs}");
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
        ? ActivityOf(usable: IsOnApiKey, pastCutoff: false, working: false, Blinker.Grew || Blinker.Pending > 0)
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

    // A person's tab shows its activity icon; the 'API key' tab shows a key instead (the owner's ask, M.ORCH-T763), each with
    // its word in the tooltip and accessible name.
    public bool IsTabIdle => IsPersonTab && TabActivity == TabActivity.Idle && !TabAllStopped;
    public bool IsTabBusy => IsPersonTab && TabActivity == TabActivity.Busy && !TabAllStopped;
    public bool IsTabDisabled => IsPersonTab && TabActivity == TabActivity.Disabled;

    /// <summary>Every open CLI of this person has stopped (M.ORCH-T890): the green play arrow turns into a flashing pause icon,
    /// so it shows at a glance that all of their CLIs are done. Never with no CLI open, and never while the login is disabled.</summary>
    public bool IsTabAllStopped => IsPersonTab && TabActivity != TabActivity.Disabled && TabAllStopped;

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
        TabActivity.Disabled => tab.IsApiKey ? "Disabled: API billing off" : tab.SignedIn ? "Disabled: past its cutoff" : "Disabled: signed out",
        _ when IsPersonTab && StoppedCliCount is > 0 and var n => n == 1 ? "Stopped: its one open CLI is idle" : $"Stopped: all {n} open CLIs are idle",
        TabActivity.Busy => "Busy",
        _ => "Idle",
    };

    /// <summary>The play arrow's opacity: dimmed for the moment of a blink.</summary>
    public double TabIconOpacity => Blinker.IsDim ? 0.25 : 1.0;

    /// <summary>The tokens this tab counts for its blinks: its login's uncached tokens today, as the dashboard already read them
    /// (on the 'API key' tab, the CLIs billing the key's).</summary>
    private long TabTokens => tab.IsApiKey ? ApiKeyUsageToday.Fresh : snapshot?.UsageToday.Fresh ?? 0;

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

    /// <summary>What the CLIs billing the API key spent today (their sessions' tokens), across the workspace.</summary>
    private TokenUsage ApiKeyUsageToday => ApiKeyUsageOf(billing, full);

    internal static TokenUsage ApiKeyUsageOf(BillingStatus? status, Snapshot? all)
    {
        if (status is null || all is null) return default;
        var keyed = status.Clis.Where(c => c.Bills == BillingModes.Api).Select(c => c.SessionId).ToHashSet(StringComparer.Ordinal);
        return all.Repos.SelectMany(r => r.Sessions).Where(s => keyed.Contains(s.SessionId))
            .Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday);
    }

    /// <summary>"2 CLIs bill the API key: MindAttic.Foo (pid 12), …" or "No CLI bills the API key.", for the 'API key' tab.</summary>
    public string ApiKeyClis => ApiKeyClisOf(billing);

    internal static string ApiKeyClisOf(BillingStatus? status)
    {
        var keyed = status?.Clis.Where(c => c.Bills == BillingModes.Api).ToList() ?? [];
        return keyed.Count == 0 ? "No CLI bills the API key."
            : $"{keyed.Count} CLI{(keyed.Count == 1 ? "" : "s")} bill{(keyed.Count == 1 ? "s" : "")} the API key: " +
              string.Join(", ", keyed.Select(c => $"{c.Where} (pid {c.Pid})")) + ".";
    }

    /// <summary>"1.2M tokens today (850K uncached) by the CLIs billing it", for the 'API key' tab.</summary>
    public string ApiKeyCostText
    {
        get
        {
            var u = ApiKeyUsageToday;
            return $"{Formatting.Tokens(u.Total)} tokens today ({Formatting.Tokens(u.Fresh)} uncached) by the CLIs billing it.";
        }
    }

    /// <summary>Raises everything the tab strip shows for this tab.</summary>
    private void RaiseTabStatus()
    {
        foreach (var name in new[] { nameof(TabLight), nameof(TabLightStatus), nameof(TabStatusWord), nameof(TabLightBrush), nameof(TabTrailing), nameof(TabTip),
                                     nameof(TabActivity), nameof(IsTabIdle), nameof(IsTabBusy), nameof(IsTabDisabled), nameof(IsTabAllStopped), nameof(TabActivityWord),
                                     nameof(ApiKeyClis), nameof(ApiKeyCostText), nameof(TabAskingCount), nameof(TabNeedsYou) })
            Raise(name);
    }

    /// <summary>Marks <paramref name="email"/>'s sign-in as in progress (null: none) and relights every tab.</summary>
    private void SetSigningInEmail(string? email)
    {
        Root.signingInEmail = email;
        foreach (var t in Root.loginTabs) t.RaiseTabStatus();
    }
}
