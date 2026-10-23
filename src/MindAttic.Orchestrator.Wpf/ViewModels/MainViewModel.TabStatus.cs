using System.Windows.Media;
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

    /// <summary>The tab's far-right figure: a person's weekly % left, as the weekly card shows it ("23% left", "—" never read); on the
    /// 'API key' tab, the tokens the CLIs billing it spent today.</summary>
    public string TabTrailing => tab.IsApiKey ? $"{Formatting.Tokens(ApiKeyUsageToday.Fresh)} today" : TabWeekLeftOf(WeekUsed);

    /// <summary>"23% left": 100 minus the week used, never below 0, the same figure as the weekly card (the M.ORCH-T609 reserve
    /// shows only in its "stops at 95%"); "—" when the week was never read.</summary>
    internal static string TabWeekLeftOf(double? usedPercent) =>
        usedPercent is { } used ? $"{Math.Max(0, 100 - used):0}% left" : "—";

    private (TabLight Light, string Word) TabStatus => tab.IsApiKey
        ? (TabLight.Warning, IsOnApiKey ? "API key billing every CLI" : "API key on standby (Claude Team bills)")
        : StatusOf(tab.SignedIn, string.Equals(Root.signingInEmail, tab.Email, StringComparison.OrdinalIgnoreCase) && tab.Email is not null,
            SignedInAsOther, WeekUsed, weeklyStopPercent, FiveHourUsed);

    /// <summary>A person's light and its word: red signed out, purple while signing in, signed in as someone else, at its weekly
    /// stop line or with its 5-hour limit hit, else green.</summary>
    internal static (TabLight Light, string Word) StatusOf(bool signedIn, bool signingIn, string? signedInAs, double? weekUsed,
        int stopPercent, double? fiveHourUsed)
    {
        if (signingIn) return (TabLight.Other, "Signing in");
        if (!signedIn) return (TabLight.Bad, "Signed out");
        if (signedInAs is not null) return (TabLight.Other, $"Login mismatch: signed in as {signedInAs}");
        if (weekUsed >= stopPercent) return (TabLight.Other, $"At its weekly stop line ({stopPercent}%)");
        if (fiveHourUsed >= 100) return (TabLight.Other, "5-hour limit hit");
        return (TabLight.Good, "Signed in and usable");
    }

    /// <summary>The 5-hour window's % used: its own pace's against a real limit, else the roster's last reading.</summary>
    private double? FiveHourUsed => Budget is { CurrentBlock: not null, BlockLimitIsPeak: false, BlockLimit: > 0 } b
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
                                     nameof(ApiKeyClis), nameof(ApiKeyCostText) })
            Raise(name);
    }

    /// <summary>Marks <paramref name="email"/>'s sign-in as in progress (null: none) and relights every tab.</summary>
    private void SetSigningInEmail(string? email)
    {
        Root.signingInEmail = email;
        foreach (var t in Root.loginTabs) t.RaiseTabStatus();
    }
}
