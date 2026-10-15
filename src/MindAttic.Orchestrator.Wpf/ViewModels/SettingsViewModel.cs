using System.Globalization;
using System.Windows.Input;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The settings panel: text fields that are parsed on Save, so a half-typed number is never applied.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly JsonStore store;
    private readonly Action saved;
    private string blockBudget = "", weeklyBudget = "", weeklyReset = "", warnPercent = "", maxSessions = "", provider = "Claude", error = "", paceTarget = "";
    private bool autoDispatch, pacing;

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
    public string Provider { get => provider; set => Set(ref provider, value); }
    public bool AutoDispatch { get => autoDispatch; set => Set(ref autoDispatch, value); }
    public bool Pacing { get => pacing; set => Set(ref pacing, value); }
    public string PaceTarget { get => paceTarget; set => Set(ref paceTarget, value); }
    public string Error { get => error; private set => Set(ref error, value); }
    public IReadOnlyList<string> Providers => Sessions.SessionControl.Providers;
    public string FilePath => System.IO.Path.Combine(store.Directory, "settings.json");
    public ICommand SaveCommand { get; }

    public void Load()
    {
        var s = store.LoadSettings();
        BlockBudget = s.BlockBudget is { } b ? Formatting.Tokens(b) : "";
        WeeklyBudget = s.WeeklyBudget is { } w ? Formatting.Tokens(w) : "";
        WeeklyReset = Budget.WeeklyReset.TryParse(s.WeeklyReset, out var r) && r is { } reset ? reset.Display : "";
        WarnPercent = s.WarnPercent.ToString(CultureInfo.InvariantCulture);
        MaxSessions = s.MaxConcurrentSessions.ToString(CultureInfo.InvariantCulture);
        Provider = s.DefaultProvider;
        AutoDispatch = s.AutoDispatch;
        Pacing = s.Pacing;
        PaceTarget = s.PaceTargetPercent.ToString(CultureInfo.InvariantCulture);
        Error = "";
    }

    private void Save()
    {
        if (!TryTokens(BlockBudget, out var block)) { Error = "5-hour budget: a number like 20M, 500k or blank."; return; }
        if (!TryTokens(WeeklyBudget, out var weekly)) { Error = "Weekly budget: a number like 150M or blank."; return; }
        if (!Budget.WeeklyReset.TryParse(WeeklyReset, out var reset)) { Error = "Week resets: a day and a time like Mon 9:00 AM, or blank for a rolling 7 days."; return; }
        if (!int.TryParse(WarnPercent, out var warn) || warn is < 1 or > 100) { Error = "Warn at: 1 to 100."; return; }
        if (!int.TryParse(MaxSessions, out var max) || max is < 1 or > 50) { Error = "Max sessions: 1 to 50."; return; }
        if (!int.TryParse(PaceTarget, out var target) || target is < 1 or > 100) { Error = "Pace target: 1 to 100."; return; }

        var s = store.LoadSettings();
        store.SaveSettings(s with
        {
            BlockBudget = block, WeeklyBudget = weekly, WeeklyReset = reset?.ToString(), WarnPercent = warn, MaxConcurrentSessions = max,
            DefaultProvider = Provider, AutoDispatch = AutoDispatch, Pacing = Pacing, PaceTargetPercent = target,
        });
        Load();
        saved();
    }

    /// <summary>Accepts 20000000, 20M, 1.5B, 500k; blank means "not set".</summary>
    internal static bool TryTokens(string text, out long? value)
    {
        value = null;
        var t = text.Trim().Replace(",", "").Replace("_", "");
        if (t.Length == 0) return true;
        var scale = char.ToUpperInvariant(t[^1]) switch { 'K' => 1e3, 'M' => 1e6, 'B' => 1e9, _ => 1 };
        if (scale != 1) t = t[..^1];
        if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n <= 0) return false;
        value = (long)(n * scale);
        return true;
    }
}
