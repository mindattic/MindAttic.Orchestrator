using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One entry in the account menu: what it switches to (<see cref="Id"/>: an account key, or
/// <see cref="MainViewModel.ApiKeyItem"/>), its line, its icon-font glyph and its tooltip.</summary>
public sealed record AccountMenuItem(string Id, string Label, string Icon, string ToolTip, string Account,
    bool IsCurrent, string WeekUsage, string WeekResets, string FiveHourUsed,
    string FiveHourResets, string LastRead, bool IsApiKey);

/// <summary>
/// The account menu in the upper right (M.ORCH-T492, M.ORCH-T566): every Claude account signed in to here, how much of its
/// week and 5-hour window it has left and when each resets, read from the roster the daemon keeps, as a table. The corner
/// names the signed-in account ("API key" while the Vault's key bills); its menu lists every account, the signed-in one
/// marked and the one with the most headroom suggested, and picking one opens its login. The menu's last row bills the API
/// key instead, as the Claude Team / API key toggle did (M.ORCH-T436).
/// </summary>
public sealed partial class MainViewModel
{
    public const string ApiKeyItem = "use-api-key";

    private IReadOnlyList<AccountRow> accountRows = [];
    private string? currentAccount;
    private bool switchingAccount;

    /// <summary>The upper-right corner: "API key" while the Vault's key bills, else the signed-in account's email.</summary>
    public string AccountCorner => CornerOf(billing?.Mode, accountRows, currentAccount);

    internal static string CornerOf(string? mode, IReadOnlyList<AccountRow> rows, string? current) =>
        mode == BillingModes.Api ? "API key"
            : rows.FirstOrDefault(r => r.Account == current)?.Label ?? (current is null ? "Not signed in" : "Signed in");

    /// <summary>The menu's rows, built as it opens: every account, then "Use API key (emergencies)".</summary>
    public IReadOnlyList<AccountMenuItem> AccountMenu() => MenuOf(accountRows, currentAccount, billing?.Mode, DateTime.UtcNow, weeklyStopPercent);

    /// <summary>Each account's weekly stop line (M.ORCH-T609), from the weekly reserve setting as of the last refresh.</summary>
    private int weeklyStopPercent = MindAttic.Orchestrator.Settings.OrchestratorSettings.WeeklyStopFor(MindAttic.Orchestrator.Settings.OrchestratorSettings.DefaultWeeklyReservePercent);

    /// <summary>Bindable: the table the account menu's popup renders, rebuilt whenever the roster, the signed-in account or
    /// the billing mode changes.</summary>
    public IReadOnlyList<AccountMenuItem> AccountMenuItems => AccountMenu();

    internal static IReadOnlyList<AccountMenuItem> MenuOf(IReadOnlyList<AccountRow> rows, string? current, string? mode, DateTime nowUtc,
        int? weeklyStopPercent = null)
    {
        var onKey = mode == BillingModes.Api;
        var suggested = AccountSwitch.MostHeadroom(rows, nowUtc, weeklyStopPercent)?.Account;
        var items = new List<AccountMenuItem>();
        foreach (var r in rows)
        {
            var signedIn = r.Account == current && !onKey;
            var icon = signedIn ? "" : ""; // only the signed-in row gets an icon
            var tip = signedIn ? "Signed in: every CLI bills this account."
                : r.Email is null ? "Its email shows once it's signed in to again (/login in a Claude CLI)."
                : r.Account == current ? "Bill this account again instead of the API key: every CLI switches back."
                : $"Sign every CLI in to {r.Email}: opens its login in the browser ({AccountSwitch.LoginCommand(r.Email, AccountSwitch.UsesSso(r))}).";
            if (weeklyStopPercent is { } stop)
                tip += AccountSwitch.AtWeeklyStop(r, nowUtc, stop)
                    ? $" Its week is at its {stop}% stop line (the weekly reserve): its CLIs are stopped until the week resets."
                    : $" Its CLIs stop at {stop}% of its week (the weekly reserve).";
            var c = AccountSwitch.Columns(r, nowUtc, weeklyStopPercent);
            items.Add(new AccountMenuItem(r.Account, AccountSwitch.MenuLine(r, signedIn ? current : null, suggested, nowUtc, weeklyStopPercent), icon, tip,
                r.Label, signedIn, c.WeekUsage, c.WeekResets, c.FiveHourUsed, c.FiveHourResets, c.LastRead, false));
        }
        var apiLabel = "Use API key (emergencies)" + (onKey ? " (in use)" : "");
        items.Add(new AccountMenuItem(ApiKeyItem, apiLabel, onKey ? "" : "",
            "Bill every CLI to the MindAttic Vault's API key, for emergencies only. Open Launcher tabs restart with claude --resume at their next idle point.",
            apiLabel, onKey, "", "", "", "", "", true));
        return items;
    }

    private void ApplyAccounts(Snapshot next, DateTime nowUtc)
    {
        // The roster is read once a refresh, by the global login's tab, which is applied first; every login's tab shows the same.
        if (root is not null)
        {
            accountRows = root.accountRows;
            weeklyStopPercent = root.weeklyStopPercent;
        }
        else
        {
            try { accountRows = AccountRoster.Load(aggregator.Db, next.Account); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { return; }
            try { weeklyStopPercent = store.LoadSettings().WeeklyStopPercent; }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.IO.InvalidDataException) { }
        }
        currentAccount = next.Account == ClaudeAccount.ApiKey ? null : next.Account;
        Raise(nameof(AccountCorner));
        Raise(nameof(AccountMenuItems));
    }

    /// <summary>Picks a menu entry: the API key switches every CLI to it; an account opens its login (billing goes back to
    /// Claude Team first if the key was billing). The dashboard follows the switch at the next refresh after it lands.</summary>
    public async Task ChooseAccountAsync(string id)
    {
        if (id == ApiKeyItem)
        {
            if (billing?.Mode != BillingModes.Api && !switchingBilling) await SwitchBillingAsync(BillingModes.Api);
            return;
        }
        if (switchingAccount || switchingBilling || accountRows.FirstOrDefault(r => r.Account == id) is not { } row) return;
        switchingAccount = true;
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
                Toasts.Insert(0, new ToastViewModel("Account not switched", result.Error, Palette.Over, null));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            billingMessage = $"Couldn't switch account: {ex.Message}";
            billingMessageUtc = DateTime.UtcNow;
            OrchestratorLog.For("Dashboard").Error(ex, "account switch failed");
        }
        finally
        {
            NoteLocalChange();
            switchingAccount = false;
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
                (dir, _) => ((IProgress<string>)opening).Report(dir.Email), r => ((IProgress<OwnLoginResult>)finished).Report(r)));
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
