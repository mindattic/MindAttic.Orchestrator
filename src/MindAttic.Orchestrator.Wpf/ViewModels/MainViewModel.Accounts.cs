using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The account roster the daemon keeps (every Claude account signed in to here, how much of its week and 5-hour window it
/// has left and when each resets). There's no account menu any more (M.ORCH-T628): every person is signed in at once, each
/// with a tab of their own (M.ORCH-T613) whose stat tiles carry that login's own numbers (<see cref="OwnRow"/>), and what
/// only the menu did moved to the Accounts view: billing the API key for emergencies and back to Claude Team
/// (<see cref="ToggleBillingCommand"/>, M.ORCH-T436), and making a login the global default (<see cref="MakeDefaultCommand"/>).
/// </summary>
public sealed partial class MainViewModel
{
    private IReadOnlyList<AccountRow> accountRows = [];
    private string? currentAccount;
    private bool switchingAccount;

    /// <summary>Each account's weekly stop line (M.ORCH-T609), from the weekly reserve setting as of the last refresh.</summary>
    private int weeklyStopPercent = MindAttic.Orchestrator.Settings.OrchestratorSettings.WeeklyStopFor(MindAttic.Orchestrator.Settings.OrchestratorSettings.DefaultWeeklyReservePercent);

    /// <summary>The pace target (M.ORCH-T629's idle chart's target line when the login has no utilization of its own).</summary>
    private double paceTargetPercent = new MindAttic.Orchestrator.Settings.OrchestratorSettings().PaceTargetPercent;

    /// <summary>This tab's login's roster row (M.ORCH-T628): on the global login's tab, whoever's signed in to it; on a per-CLI
    /// login's tab, the account with its email. Null when the roster doesn't know it.</summary>
    internal AccountRow? OwnRow => RowFor(accountRows, ScopeEmail, currentAccount);

    internal static AccountRow? RowFor(IReadOnlyList<AccountRow> rows, string? email, string? current) => email is null
        ? rows.FirstOrDefault(r => r.Account == current)
        : rows.FirstOrDefault(r => string.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase));

    private void ApplyAccounts(Snapshot next, DateTime nowUtc)
    {
        // The roster is read once a refresh, by the global login's tab, which is applied first; every login's tab shows the same.
        if (root is not null)
        {
            accountRows = root.accountRows;
            weeklyStopPercent = root.weeklyStopPercent;
            paceTargetPercent = root.paceTargetPercent;
            // The global login's account is the global tab's: a per-CLI tab's snapshot names its own login's.
            currentAccount = root.currentAccount;
        }
        else
        {
            try { accountRows = AccountRoster.Load(aggregator.Db, next.Account); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Data.Common.DbException) { return; }
            try
            {
                var settings = store.LoadSettings();
                weeklyStopPercent = settings.WeeklyStopPercent; paceTargetPercent = settings.PaceTargetPercent;
                tabBlinkTokens = settings.TabBlinkTokens;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.IO.InvalidDataException) { }
            // Each login's own cutoffs and who's allowed past the weekly one, for the tabs' disabled icon (M.ORCH-T757); kept as
            // they were when the store can't be read now.
            try { cutoffs = UsageCutoffs.Load(store); allowances = WeeklyAllowances.Active(store, nowUtc); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
            currentAccount = next.Account == ClaudeAccount.ApiKey ? null : next.Account;
        }
        RaiseOwnNumbers();
    }

    private ICommand? makeDefaultCommand, toggleBillingCommand;

    /// <summary>"Make default", a person's login action (M.ORCH-T763; the Accounts view's card had it before): signs the global
    /// login in to this per-CLI login's account, so every CLI not started on a login of its own runs on it, as picking it in the
    /// old account menu did.</summary>
    public ICommand MakeDefaultCommand => makeDefaultCommand ??= new RelayCommand(async () =>
    {
        if (CanMakeDefault && ScopeEmail is { } email) await MakeDefaultAsync(email);
    }, () => CanMakeDefault && !switchingAccount && !switchingBilling);

    /// <summary>A signed-in per-CLI login the global login isn't signed in to already can become the global default.</summary>
    public bool CanMakeDefault => IsTabSignedIn && ScopeEmail is { } email &&
        !string.Equals(email, Root.workspace?.GlobalEmail, StringComparison.OrdinalIgnoreCase);

    /// <summary>The Accounts view's billing button: "Use API key (emergencies)" while Claude Team bills, "Back to Claude Team"
    /// while the Vault's key does (M.ORCH-T436). Switching every open CLI onto the organization's key is a break-glass action
    /// (M.ORCH-T773), so it asks first - one stray click on the corner icon must never flip billing by accident. Switching
    /// back to Claude Team doesn't ask: that direction is always the safe one, and it's often clicked in a hurry once the
    /// emergency that justified the key is over.</summary>
    public ICommand ToggleBillingCommand => toggleBillingCommand ??= new RelayCommand(async () =>
    {
        if (switchingBilling || switchingAccount) return;
        if (IsOnApiKey) { await SwitchBillingAsync(BillingModes.Team); return; }
        var confirmed = MindAttic.Components.Wpf.Controls.MaMessageBox.Show(
            System.Windows.Application.Current?.MainWindow,
            "This bills every open CLI to the MindAttic Vault's organization API key instead of anyone's personal Claude Team seat, "
            + "until you switch it back. Use it only for an emergency (a seat out of room, pacing gone wrong).",
            "Bill every CLI to the API key?",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No)
            == System.Windows.MessageBoxResult.Yes;
        if (!confirmed) return;
        await SwitchBillingAsync(BillingModes.Api);
    }, () => !switchingBilling && !switchingAccount);

    /// <summary>Whether the Vault's API key bills every CLI right now.</summary>
    public bool IsOnApiKey => billing?.Mode == BillingModes.Api;

    /// <summary>The billing button's label.</summary>
    public string BillingToggleText => IsOnApiKey ? "Back to Claude Team" : "Use API key (emergencies)";

    /// <summary>The emergency billing toggle's key glyph (M.ORCH-T770): MaText while Claude Team bills, MaGood while the API
    /// key does, as a view model's own live brush (<see cref="Palette"/>) rather than a Style's DataTrigger, so the icon needs
    /// no style of its own (M.ORCH-T775).</summary>
    public System.Windows.Media.Brush EmergencyKeyBrush => IsOnApiKey ? Palette.Working : Palette.Text;

    /// <summary>Signs the global login in to <paramref name="email"/>'s account (its roster row, or the email alone when the
    /// roster doesn't know it yet): billing goes back to Claude Team first if the key was billing. The dashboard follows the
    /// switch at the next refresh after it lands.</summary>
    public async Task MakeDefaultAsync(string email)
    {
        if (switchingAccount || switchingBilling) return;
        var row = accountRows.FirstOrDefault(r => string.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase))
                  ?? new AccountRow { Account = email, Email = email };
        switchingAccount = true;
        CommandManager.InvalidateRequerySuggested();
        billingMessage = row.Account == currentAccount ? "Switching every CLI back to Claude Team…" : $"Opening {row.Label}'s login in the browser…";
        billingMessageUtc = DateTime.UtcNow;
        Raise(nameof(BillingDetail));
        try
        {
            var hosts = full?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
            var current = currentAccount;
            var result = await Task.Run(() => AccountSwitch.SignIn(row, current, aggregator.BillingSwitch, hosts, "dashboard", sso: false, DateTime.UtcNow));
            billingMessage = result.Error ?? result.Lines.LastOrDefault() ?? "";
            billingMessageUtc = DateTime.UtcNow;
            if (result.Error is not null)
                Toasts.Insert(0, new ToastViewModel("Default login not switched", result.Error, Palette.Over, null));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            billingMessage = $"Couldn't switch the default login: {ex.Message}";
            billingMessageUtc = DateTime.UtcNow;
            OrchestratorLog.For("Dashboard").Error(ex, "account switch failed");
        }
        finally
        {
            NoteLocalChange();
            switchingAccount = false;
            CommandManager.InvalidateRequerySuggested();
            Raise(nameof(BillingDetail));
        }
        await RefreshAsync();
    }

    private bool signingInAll;
    private RelayCommand? signInAllCommand;

    /// <summary>"Sign in all" (M.ORCH-T604): every account in the roster with no per-CLI login yet gets its own folder and its
    /// login, in turn, so every Claude Team seat stays signed in side by side and the global login is only the default.</summary>
    public ICommand SignInAllCommand => signInAllCommand ??= new RelayCommand(async () => await SignInAllAsync(), () => !signingInAll);

    /// <summary>Where the per-CLI logins' folders are; tests point it elsewhere (never the real ~/.claude-accounts).</summary>
    internal Func<AccountDirs> OwnLoginDirs { get; set; } = () => new AccountDirs();

    /// <summary>Runs <see cref="OwnLogins.SignInAll"/> off the UI thread (each login waits on the browser), saying each account
    /// as its login opens and finishes, a toast for each that failed, and the tally at the end.</summary>
    public async Task SignInAllAsync()
    {
        if (signingInAll) return;
        signingInAll = true;
        CommandManager.InvalidateRequerySuggested();
        void Say(string text)
        {
            billingMessage = text;
            billingMessageUtc = DateTime.UtcNow;
            Raise(nameof(BillingDetail));
        }
        Say("Signing every account in to its own login…");
        var opening = new Progress<string>(email => Say($"Opening {email}'s login in the browser…"));
        var finished = new Progress<OwnLoginResult>(r =>
        {
            Say(r.Message);
            if (r.Outcome == OwnLoginOutcome.Failed)
                Toasts.Insert(0, new ToastViewModel($"{r.Label} not signed in", r.Message, Palette.Over, null));
        });
        try
        {
            var rows = accountRows;
            var dirs = OwnLoginDirs();
            var results = await Task.Run(() => OwnLogins.SignInAll(dirs, rows,
                (dir, _) => ((IProgress<string>)opening).Report(dir.Email), r => ((IProgress<OwnLoginResult>)finished).Report(r),
                new TokenMinting(LoginTokens.Default)));
            var summary = OwnLogins.Summary(results);
            Say(summary);
            Toasts.Insert(0, new ToastViewModel("Sign in all", summary,
                results.Any(r => r.Outcome == OwnLoginOutcome.Failed) ? Palette.Waiting : Palette.Working, null));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Say($"Couldn't sign the accounts in: {ex.Message}");
            OrchestratorLog.For("Dashboard").Error(ex, "sign in all failed");
        }
        finally
        {
            signingInAll = false;
            CommandManager.InvalidateRequerySuggested();
            NoteLocalChange();
        }
        await RefreshAsync();
    }
}
