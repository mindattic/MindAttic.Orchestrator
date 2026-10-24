using System.Globalization;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The settings panel: text fields that are parsed on Save, so a half-typed number is never applied.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly JsonStore store;
    private readonly Action saved;
    private string blockBudget = "", weeklyBudget = "", weeklyReset = "", warnPercent = "", maxSessions = "", provider = "Claude", error = "", paceTarget = "",
        contextPercent = "", orphanRedispatches = "", orphanQuiet = "", exemptCommands = "", weeklyTarget = "", tabBlinkTokens = "";
    private bool autopilot, pacing, autoLogin, rebalanceLogins = true;

    public SettingsViewModel(JsonStore store, Action saved)
    {
        this.store = store;
        this.saved = saved;
        SaveCommand = new RelayCommand(Save);
        Load();
    }

    public string BlockBudget { get => blockBudget; set => Set(ref blockBudget, value); }
    public string WeeklyBudget { get => weeklyBudget; set => Set(ref weeklyBudget, value); }
    public string WeeklyReset { get => weeklyReset; set => Set(ref weeklyReset, value); }
    public string WarnPercent { get => warnPercent; set => Set(ref warnPercent, value); }
    public string MaxSessions { get => maxSessions; set => Set(ref maxSessions, value); }
    // Ctrl+click on the selected pill deselects it, writing null: Save would store no default provider.
    public string Provider { get => provider; set { if (value is not null) Set(ref provider, value); } }
    public bool Autopilot { get => autopilot; set => Set(ref autopilot, value); }
    public bool Pacing { get => pacing; set => Set(ref pacing, value); }
    public string PaceTarget { get => paceTarget; set => Set(ref paceTarget, value); }
    /// <summary>The weekly target (M.ORCH-T680): 100 minus the weekly reserve (M.ORCH-T609), so it's saved as the reserve;
    /// every CLI on an account stops there, and weekly-aware pacing lands the week on it. 50 to 100.</summary>
    public string WeeklyTarget { get => weeklyTarget; set => Set(ref weeklyTarget, value); }
    /// <summary>One CLI per account (M.ORCH-T605): new Claude tabs default to the Auto login.</summary>
    public bool AutoLogin { get => autoLogin; set => Set(ref autoLogin, value); }
    /// <summary>Rebalance when an account runs low (M.ORCH-T608).</summary>
    public bool RebalanceLogins { get => rebalanceLogins; set => Set(ref rebalanceLogins, value); }
    /// <summary>How many times an orphaned ticket is redispatched (M.ORCH-T748), 0 to 20; 0 only shows it as orphaned.</summary>
    public string OrphanRedispatches { get => orphanRedispatches; set => Set(ref orphanRedispatches, value); }
    /// <summary>Minutes a dispatch's owner must be gone, nothing written in its worktree, before it's orphaned, 1 to 1440.</summary>
    public string OrphanQuiet { get => orphanQuiet; set => Set(ref orphanQuiet, value); }
    /// <summary>A busy person tab's play arrow blinks once per this many tokens (M.ORCH-T757), 1 to 10,000,000.</summary>
    public string TabBlinkTokens { get => tabBlinkTokens; set => Set(ref tabBlinkTokens, value); }
    public string ContextPercent { get => contextPercent; set => Set(ref contextPercent, value); }
    /// <summary>The commands exempt from pacing (M.ORCH-T198), comma-separated; blank exempts none.</summary>
    public string ExemptCommands { get => exemptCommands; set => Set(ref exemptCommands, value); }
    // A member's own use outside the orchestrator (M.ORCH-T682): each number, the MIN-MAX its per-login learned value stays in,
    // and what each login has learned (read-only, beside the setting).
    private string outsideCeiling = "", outsideCeilingBounds = "", outsideQuiet = "", outsideQuietBounds = "", outsideMargin = "",
        outsideMarginBounds = "", outsideMarginShare = "", outsideHeadroom = "", outsideHeadroomBounds = "";
    public string OutsideCeiling { get => outsideCeiling; set => Set(ref outsideCeiling, value); }
    public string OutsideCeilingBounds { get => outsideCeilingBounds; set => Set(ref outsideCeilingBounds, value); }
    public string OutsideQuiet { get => outsideQuiet; set => Set(ref outsideQuiet, value); }
    public string OutsideQuietBounds { get => outsideQuietBounds; set => Set(ref outsideQuietBounds, value); }
    public string OutsideMargin { get => outsideMargin; set => Set(ref outsideMargin, value); }
    public string OutsideMarginBounds { get => outsideMarginBounds; set => Set(ref outsideMarginBounds, value); }
    public string OutsideMarginShare { get => outsideMarginShare; set => Set(ref outsideMarginShare, value); }
    public string OutsideHeadroom { get => outsideHeadroom; set => Set(ref outsideHeadroom, value); }
    public string OutsideHeadroomBounds { get => outsideHeadroomBounds; set => Set(ref outsideHeadroomBounds, value); }

    // Psst on outside use (M.ORCH-T702): the watched logins, comma-separated, and who's texted.
    private string outsidePsstLogins = "", outsidePsstTo = "", loadedPsstLogins = "", loadedPsstTo = "";
    /// <summary>The logins whose outside use is texted: emails, the parts before the @, or names, comma-separated; blank none.</summary>
    public string OutsidePsstLogins { get => outsidePsstLogins; set => Set(ref outsidePsstLogins, value); }
    /// <summary>Who's texted: a Psst contact or a US number; blank nobody.</summary>
    public string OutsidePsstTo { get => outsidePsstTo; set => Set(ref outsidePsstTo, value); }

    /// <summary>Psst's contact book, for checking the recipient on Save; tests point it at their own.</summary>
    internal string? PsstContactsPath { get; init; }

    /// <summary>"Learned: Honey 88% · Ryan 92%" for each number; "Learned: none yet" until a login has.</summary>
    public string OutsideCeilingLearned { get; private set; } = "";
    public string OutsideQuietLearned { get; private set; } = "";
    public string OutsideMarginLearned { get; private set; } = "";
    public string OutsideHeadroomLearned { get; private set; } = "";

    private string[] loadedOutside = [];
    private string[] OutsideCurrent() => [OutsideCeiling, OutsideCeilingBounds, OutsideQuiet, OutsideQuietBounds, OutsideMargin, OutsideMarginBounds,
        OutsideMarginShare, OutsideHeadroom, OutsideHeadroomBounds];

    private static string Bounds(int min, int max) => $"{min}-{max}";

    private void LoadOutside(OrchestratorSettings s)
    {
        OutsideCeiling = s.OutsideUseCeilingPercent.ToString(CultureInfo.InvariantCulture);
        OutsideCeilingBounds = Bounds(s.OutsideUseCeilingMinPercent, s.OutsideUseCeilingMaxPercent);
        OutsideQuiet = s.OutsideUseQuietMinutes.ToString(CultureInfo.InvariantCulture);
        OutsideQuietBounds = Bounds(s.OutsideUseQuietMinMinutes, s.OutsideUseQuietMaxMinutes);
        OutsideMargin = s.OutsideUseMarginPoints.ToString(CultureInfo.InvariantCulture);
        OutsideMarginBounds = Bounds(s.OutsideUseMarginMinPoints, s.OutsideUseMarginMaxPoints);
        OutsideMarginShare = s.OutsideUseMarginSharePercent.ToString(CultureInfo.InvariantCulture);
        OutsideHeadroom = s.OutsideUseHeadroomPoints.ToString(CultureInfo.InvariantCulture);
        OutsideHeadroomBounds = Bounds(s.OutsideUseHeadroomMinPoints, s.OutsideUseHeadroomMaxPoints);
        loadedOutside = OutsideCurrent();
        OutsidePsstLogins = loadedPsstLogins = string.Join(", ", s.OutsideUsePsstLogins);
        OutsidePsstTo = loadedPsstTo = s.OutsideUsePsstTo ?? "";
        var logins = Sessions.OutsideUseWatch.Load(store);
        static string Say(string text) => "Learned: " + (text.Length == 0 ? "none yet" : text);
        OutsideCeilingLearned = Say(Sessions.OutsideUseWatch.Learned(logins, l => l.LearnedCeiling, "%"));
        OutsideQuietLearned = Say(Sessions.OutsideUseWatch.Learned(logins, l => l.LearnedQuietMinutes, " min"));
        OutsideMarginLearned = Say(Sessions.OutsideUseWatch.Learned(logins, l => l.LearnedMargin, " points"));
        OutsideHeadroomLearned = Say(Sessions.OutsideUseWatch.Learned(logins, l => l.LearnedHeadroom, " points"));
        Raise(nameof(OutsideCeilingLearned));
        Raise(nameof(OutsideQuietLearned));
        Raise(nameof(OutsideMarginLearned));
        Raise(nameof(OutsideHeadroomLearned));
    }

    /// <summary>The outside-use fields edited here applied to <paramref name="s"/>; null (and <see cref="Error"/> said) when one
    /// doesn't parse. Only edited fields are checked or saved, as with the rest of the panel.</summary>
    private OrchestratorSettings? SaveOutside(OrchestratorSettings s)
    {
        var was = loadedOutside.Length == 9 ? loadedOutside : OutsideCurrent();
        int? Number(string text, string old, int lo, int hi, string what)
        {
            if (text == old) return null;
            if (int.TryParse(text, out var n) && n >= lo && n <= hi) return n;
            Error = $"{what}: {lo} to {hi}.";
            throw new FormatException();
        }
        (int Min, int Max)? Range(string text, string old, int lo, int hi, string what)
        {
            if (text == old) return null;
            if (Sessions.OutsideTuning.ParseBounds(text, lo, hi) is { } b) return b;
            Error = $"{what}: MIN-MAX, each {lo} to {hi}, like 70-95.";
            throw new FormatException();
        }
        try
        {
            if (Number(OutsideCeiling, was[0], 1, 100, "Outside-use ceiling") is { } c) s = s with { OutsideUseCeilingPercent = c };
            if (Range(OutsideCeilingBounds, was[1], 1, 100, "Outside-use ceiling bounds") is { } cb) s = s with { OutsideUseCeilingMinPercent = cb.Min, OutsideUseCeilingMaxPercent = cb.Max };
            if (Number(OutsideQuiet, was[2], 1, 240, "Quiet period") is { } q) s = s with { OutsideUseQuietMinutes = q };
            if (Range(OutsideQuietBounds, was[3], 1, 240, "Quiet period bounds") is { } qb) s = s with { OutsideUseQuietMinMinutes = qb.Min, OutsideUseQuietMaxMinutes = qb.Max };
            if (Number(OutsideMargin, was[4], 0, 20, "Noise margin") is { } m) s = s with { OutsideUseMarginPoints = m };
            if (Range(OutsideMarginBounds, was[5], 0, 20, "Noise margin bounds") is { } mb) s = s with { OutsideUseMarginMinPoints = mb.Min, OutsideUseMarginMaxPoints = mb.Max };
            if (Number(OutsideMarginShare, was[6], 0, 100, "Noise margin share") is { } ms) s = s with { OutsideUseMarginSharePercent = ms };
            if (Number(OutsideHeadroom, was[7], 0, 50, "Headroom") is { } h) s = s with { OutsideUseHeadroomPoints = h };
            if (Range(OutsideHeadroomBounds, was[8], 0, 50, "Headroom bounds") is { } hb) s = s with { OutsideUseHeadroomMinPoints = hb.Min, OutsideUseHeadroomMaxPoints = hb.Max };
            if (OutsidePsstLogins != loadedPsstLogins) s = s with { OutsideUsePsstLogins = OrchestratorSettings.ParseLogins(OutsidePsstLogins) };
            if (OutsidePsstTo != loadedPsstTo)
            {
                var to = OutsidePsstTo.Trim();
                if (to.Length > 0 && !Reminders.PsstClient.Addressable(to, PsstContactsPath))
                {
                    Error = $"Text to: \"{to}\" isn't a Psst contact or a US number (add it with psst contacts add <name> <phone>), or blank for nobody.";
                    throw new FormatException();
                }
                s = s with { OutsideUsePsstTo = to };
            }
            return s;
        }
        catch (FormatException) { return null; }
    }

    public string Error { get => error; private set => Set(ref error, value); }
    public IReadOnlyList<string> Providers => Sessions.SessionControl.Providers;
    public string FilePath => store.Describe(JsonStore.SettingsName);
    public ICommand SaveCommand { get; }

    /// <summary>Autopilot as the panel read it: the CLI pauses and resumes it too while the panel is open, and a Save that
    /// didn't touch the toggle mustn't put back what it read.</summary>
    private bool loadedAutopilot;

    /// <summary>Every field as the panel read it. The CLI and agents change settings too while it's open (budget, pace,
    /// settings --max-sessions, --exempt): a Save writes only the fields edited here, not what it read back then (M.ORCH-T338).</summary>
    private Fields loaded = new("", "", "", "", "", "", false, "", "", "", true, "", true, "", "", "");

    private sealed record Fields(string Block, string Weekly, string Reset, string Warn, string Max, string Provider, bool Pacing, string Target,
        string Context, string Exempt, bool AutoLogin, string WeekTarget, bool Rebalance, string Orphans, string OrphanQuiet, string Blink);

    private Fields Current() => new(BlockBudget, WeeklyBudget, WeeklyReset, WarnPercent, MaxSessions, Provider, Pacing, PaceTarget,
        ContextPercent, ExemptCommands, AutoLogin, WeeklyTarget, RebalanceLogins, OrphanRedispatches, OrphanQuiet, TabBlinkTokens);

    public void Load()
    {
        var s = store.LoadSettings();
        BlockBudget = s.BlockBudget is { } b ? ExactTokens(b) : "";
        WeeklyBudget = s.WeeklyBudget is { } w ? ExactTokens(w) : "";
        WeeklyReset = Budget.WeeklyReset.TryParse(s.WeeklyReset, out var r) && r is { } reset ? reset.Display : "";
        WarnPercent = s.WarnPercent.ToString(CultureInfo.InvariantCulture);
        MaxSessions = s.MaxConcurrentSessions.ToString(CultureInfo.InvariantCulture);
        Provider = s.DefaultProvider;
        Autopilot = loadedAutopilot = s.Autopilot;
        Pacing = s.Pacing;
        PaceTarget = s.PaceTargetPercent.ToString(CultureInfo.InvariantCulture);
        WeeklyTarget = s.WeeklyAwareTarget.ToString(CultureInfo.InvariantCulture);
        AutoLogin = s.AutoLogin;
        RebalanceLogins = s.RebalanceLogins;
        // No field shows it (context cycling is off), so an out-of-range hand edit mustn't make every Save fail on it.
        ContextPercent = Math.Clamp(s.ContextCyclePercent, 10, 95).ToString(CultureInfo.InvariantCulture);
        ExemptCommands = string.Join(", ", s.ExemptCommands);
        OrphanRedispatches = s.OrphanRedispatches.ToString(CultureInfo.InvariantCulture);
        OrphanQuiet = s.OrphanQuietMinutes.ToString(CultureInfo.InvariantCulture);
        TabBlinkTokens = s.TabBlinkTokens.ToString(CultureInfo.InvariantCulture);
        loaded = Current();
        LoadOutside(s);
        Error = "";
    }

    private void Save()
    {
        // Only what was edited here is checked, as only that is saved (M.ORCH-T338): a value the CLI set outside the panel's
        // range (settings --max-sessions 60) mustn't make every Save fail on a field nobody touched (M.ORCH-T349).
        var was = loaded;
        if (!TryTokens(BlockBudget, out var block) && BlockBudget != was.Block) { Error = "5-hour budget: a number like 20M, 500k or blank."; return; }
        if (!TryTokens(WeeklyBudget, out var weekly) && WeeklyBudget != was.Weekly) { Error = "Weekly budget: a number like 150M or blank."; return; }
        if (!Budget.WeeklyReset.TryParse(WeeklyReset, out var reset) && WeeklyReset != was.Reset) { Error = "Week resets: a day and a time like Mon 9:00 AM, or blank for a rolling 7 days."; return; }
        if (!(int.TryParse(WarnPercent, out var warn) && warn is >= 1 and <= 100) && WarnPercent != was.Warn) { Error = "Warn at: 1 to 100."; return; }
        if (!(int.TryParse(MaxSessions, out var max) && max is >= 1 and <= 50) && MaxSessions != was.Max) { Error = "Max sessions: 1 to 50."; return; }
        if (!(int.TryParse(PaceTarget, out var target) && target is >= 1 and <= 100) && PaceTarget != was.Target) { Error = "Pace target: 1 to 100."; return; }
        // Weekly pacing's numbers are checked together (M.ORCH-T680): the line must sit below the target, so a changed target
        // is checked against the line too, as saved or as typed; one not edited here is taken as saved.
        var weeklyEdited = WeeklyTarget != was.WeekTarget;
        int weekTarget = 0;
        if (weeklyEdited)
        {
            var onDisk = store.LoadSettings();
            if (!WeeklyNumber(WeeklyTarget, was.WeekTarget, onDisk.WeeklyAwareTarget, out weekTarget)) { Error = $"Weekly target: {100 - OrchestratorSettings.MaxWeeklyReservePercent} to 100."; return; }
            if (OrchestratorSettings.WeeklyTargetError(weekTarget) is { } weeklyError) { Error = weeklyError; return; }
        }
        if (!(int.TryParse(ContextPercent, out var cycleAt) && cycleAt is >= 10 and <= 95) && ContextPercent != was.Context) { Error = "Cycle context at: 10 to 95."; return; }

        if (!(int.TryParse(OrphanRedispatches, out var orphans) && orphans is >= 0 and <= 20) && OrphanRedispatches != was.Orphans) { Error = "Orphan redispatches: 0 to 20."; return; }
        if (!(int.TryParse(OrphanQuiet, out var orphanQuiet) && orphanQuiet is >= 1 and <= 1440) && OrphanQuiet != was.OrphanQuiet) { Error = "Orphan quiet minutes: 1 to 1440."; return; }

        if (!(int.TryParse(TabBlinkTokens, out var blink) && blink is >= OrchestratorSettings.MinTabBlinkTokens and <= OrchestratorSettings.MaxTabBlinkTokens) && TabBlinkTokens != was.Blink)
        { Error = $"Blink once per: {OrchestratorSettings.MinTabBlinkTokens} to {OrchestratorSettings.MaxTabBlinkTokens:N0} tokens."; return; }

        if (SaveOutside(store.LoadSettings()) is null) return;
        try
        {
            var s = SaveOutside(store.LoadSettingsToChange())!;
            store.SaveSettings(s with
            {
                BlockBudget = BlockBudget == was.Block ? s.BlockBudget : block,
                WeeklyBudget = WeeklyBudget == was.Weekly ? s.WeeklyBudget : weekly,
                WeeklyReset = WeeklyReset == was.Reset ? s.WeeklyReset : reset?.ToString(),
                WarnPercent = WarnPercent == was.Warn ? s.WarnPercent : warn,
                MaxConcurrentSessions = MaxSessions == was.Max ? s.MaxConcurrentSessions : max,
                DefaultProvider = Provider == was.Provider ? s.DefaultProvider : Provider,
                Autopilot = Autopilot == loadedAutopilot ? s.Autopilot : Autopilot,
                Pacing = Pacing == was.Pacing ? s.Pacing : Pacing,
                PaceTargetPercent = PaceTarget == was.Target ? s.PaceTargetPercent : target,
                WeeklyReservePercent = WeeklyTarget == was.WeekTarget ? s.WeeklyReservePercent : 100 - weekTarget,
                AutoLogin = AutoLogin == was.AutoLogin ? s.AutoLogin : AutoLogin,
                RebalanceLogins = RebalanceLogins == was.Rebalance ? s.RebalanceLogins : RebalanceLogins,
                ContextCyclePercent = ContextPercent == was.Context ? s.ContextCyclePercent : cycleAt,
                OrphanRedispatches = OrphanRedispatches == was.Orphans ? s.OrphanRedispatches : orphans,
                OrphanQuietMinutes = OrphanQuiet == was.OrphanQuiet ? s.OrphanQuietMinutes : orphanQuiet,
                TabBlinkTokens = TabBlinkTokens == was.Blink ? s.TabBlinkTokens : blink,
                ExemptCommands = ExemptCommands == was.Exempt ? s.ExemptCommands : OrchestratorSettings.ParseCommands(ExemptCommands),
            });
        }
        // settings.json held past the store's retries: said here, not left to close the dashboard. The fields keep what was typed.
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.IO.InvalidDataException)
        {
            Error = $"Couldn't save: {ex.Message}";
            return;
        }
        Load();
        saved();
    }

    /// <summary>A weekly-pacing field as a number: what was typed, or the saved value when it wasn't edited here.</summary>
    private static bool WeeklyNumber(string typed, string loaded, int saved, out int value)
    {
        if (typed == loaded) { value = saved; return true; }
        return int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>The short form (20M) when it reads back as exactly <paramref name="tokens"/>, else every digit: the field is
    /// saved as it reads, so a rounded 12.3M would quietly replace a budget of 12,345,678 at the next Save.</summary>
    internal static string ExactTokens(long tokens)
    {
        var shortForm = Formatting.Tokens(tokens);
        return TryTokens(shortForm, out var back) && back == tokens ? shortForm : tokens.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Accepts 20000000, 20M, 1.5B, 500k; blank means "not set".</summary>
    internal static bool TryTokens(string text, out long? value)
    {
        value = null;
        var t = text.Trim().Replace(",", "").Replace("_", "");
        if (t.Length == 0) return true;
        var scale = char.ToUpperInvariant(t[^1]) switch { 'K' => 1e3, 'M' => 1e6, 'B' => 1e9, _ => 1 };
        if (scale != 1) t = t[..^1];
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n) || n <= 0) return false;
        // Under one token (0.5, 0.0004k) would be saved as 0, which is refused as typed (M.ORCH-T339).
        if (n * scale < 1) return false;
        value = (long)(n * scale);
        return true;
    }
}
