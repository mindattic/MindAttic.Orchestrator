using System.Globalization;
using System.Windows.Input;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The settings panel: text fields that are parsed on Save, so a half-typed number is never applied.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly JsonStore store;
    private readonly Action saved;
    private string blockBudget = "", weeklyBudget = "", weeklyReset = "", warnPercent = "", maxSessions = "", provider = "Claude", error = "", paceTarget = "",
        maxDelay = "", contextPercent = "", exemptCommands = "", paceProfile = OrchestratorSettings.LinearProfile, paceCurveK = "";
    private bool autopilot, pacing, throttleAgents;

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
    /// <summary>The pace curve (M.ORCH-T234): linear or front-loaded; switchable mid-window.</summary>
    // Ctrl+click deselects this pill too: Save stored linear in place of the profile you had (M.ORCH-T431).
    public string PaceProfile { get => paceProfile; set { if (value is not null) Set(ref paceProfile, value); } }
    public IReadOnlyList<string> PaceProfiles => OrchestratorSettings.PaceProfiles;
    /// <summary>The front-loaded curve's exponent (0.1 to 1).</summary>
    public string PaceCurveK { get => paceCurveK; set => Set(ref paceCurveK, value); }
    public bool ThrottleAgents { get => throttleAgents; set => Set(ref throttleAgents, value); }
    public string MaxDelay { get => maxDelay; set => Set(ref maxDelay, value); }
    public string ContextPercent { get => contextPercent; set => Set(ref contextPercent, value); }
    /// <summary>The commands exempt from pacing (M.ORCH-T198), comma-separated; blank exempts none.</summary>
    public string ExemptCommands { get => exemptCommands; set => Set(ref exemptCommands, value); }
    public string Error { get => error; private set => Set(ref error, value); }
    public IReadOnlyList<string> Providers => Sessions.SessionControl.Providers;
    public string FilePath => System.IO.Path.Combine(store.Directory, "settings.json");
    public ICommand SaveCommand { get; }

    /// <summary>Autopilot as the panel read it: the tray pauses and resumes it too while the panel is open, and a Save that
    /// didn't touch the toggle mustn't put back what it read.</summary>
    private bool loadedAutopilot;

    /// <summary>Every field as the panel read it. The CLI and agents change settings too while it's open (budget, pace,
    /// settings --max-sessions, --exempt): a Save writes only the fields edited here, not what it read back then (M.ORCH-T338).</summary>
    private Fields loaded = new("", "", "", "", "", "", false, "", false, "", "", "", "", "");

    private sealed record Fields(string Block, string Weekly, string Reset, string Warn, string Max, string Provider, bool Pacing, string Target,
        bool Throttle, string Delay, string Context, string Exempt, string Profile, string CurveK);

    private Fields Current() => new(BlockBudget, WeeklyBudget, WeeklyReset, WarnPercent, MaxSessions, Provider, Pacing, PaceTarget,
        ThrottleAgents, MaxDelay, ContextPercent, ExemptCommands, PaceProfile, PaceCurveK);

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
        PaceProfile = OrchestratorSettings.ParsePaceProfile(s.PaceProfile) ?? OrchestratorSettings.LinearProfile;
        PaceCurveK = s.PaceCurveK.ToString(CultureInfo.InvariantCulture);
        ThrottleAgents = s.ThrottleAgents;
        MaxDelay = s.MaxAgentDelaySeconds.ToString(CultureInfo.InvariantCulture);
        // No field shows it (context cycling is off), so an out-of-range hand edit mustn't make every Save fail on it.
        ContextPercent = Math.Clamp(s.ContextCyclePercent, 10, 95).ToString(CultureInfo.InvariantCulture);
        ExemptCommands = string.Join(", ", s.ExemptCommands);
        loaded = Current();
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
        if (!(double.TryParse(PaceCurveK, NumberStyles.Float, CultureInfo.InvariantCulture, out var curveK) && curveK is >= 0.1 and <= 1) && PaceCurveK != was.CurveK) { Error = "Front-loaded curve exponent: 0.1 to 1."; return; }
        if (!(int.TryParse(MaxDelay, out var delay) && delay >= 0 && delay <= Setup.AgentGate.MaxHoldSeconds) && MaxDelay != was.Delay) { Error = $"Longest hold: 0 to {Setup.AgentGate.MaxHoldSeconds} seconds."; return; }
        if (!(int.TryParse(ContextPercent, out var cycleAt) && cycleAt is >= 10 and <= 95) && ContextPercent != was.Context) { Error = "Cycle context at: 10 to 95."; return; }

        try
        {
            var s = store.LoadSettingsToChange();
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
                PaceProfile = PaceProfile == was.Profile ? s.PaceProfile : OrchestratorSettings.ParsePaceProfile(PaceProfile) ?? OrchestratorSettings.LinearProfile,
                PaceCurveK = PaceCurveK == was.CurveK ? s.PaceCurveK : curveK,
                ThrottleAgents = ThrottleAgents == was.Throttle ? s.ThrottleAgents : ThrottleAgents,
                MaxAgentDelaySeconds = MaxDelay == was.Delay ? s.MaxAgentDelaySeconds : delay,
                ContextCyclePercent = ContextPercent == was.Context ? s.ContextCyclePercent : cycleAt,
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
