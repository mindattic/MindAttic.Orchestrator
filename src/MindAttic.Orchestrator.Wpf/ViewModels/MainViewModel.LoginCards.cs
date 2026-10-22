using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The Accounts view (M.ORCH-T603): one card per Claude login on this machine, the global <c>~/.claude</c> login first, then
/// each per-CLI login under <c>~/.claude-accounts</c>, showing who's signed in, the plan, the 5-hour and weekly use with
/// their resets, the daemon's pace for it and every live CLI running on it (by the config folder the context gate recorded
/// for its session). A per-CLI login nobody's signed in to offers Sign in, and Add account signs in a new one, both as
/// <c>orchestrator login --account</c> does (<see cref="OwnLogins.SignIn"/>), off the UI thread. Sign in all (M.ORCH-T604)
/// sits in the view's toolbar beside Add account.
/// </summary>
public sealed partial class MainViewModel
{
    private readonly ContextCycler loginCycler = new();
    private bool loadingLoginCards;
    private bool signingIn;
    private string newAccountEmail = "";
    private string accountsMessage = "";

    public ObservableCollection<LoginCardViewModel> LoginCards { get; } = [];

    /// <summary>"3 logins · 4 CLIs": the view's heading line.</summary>
    public string LoginCardsSummary => LoginsSummary(LoginCards.Count, LoginCards.Sum(c => c.Clis.Count));

    internal static string LoginsSummary(int logins, int clis) =>
        $"{logins} login{(logins == 1 ? "" : "s")} · {clis} live CLI{(clis == 1 ? "" : "s")}";

    /// <summary>The email Add account signs in a login of its own for.</summary>
    public string NewAccountEmail { get => newAccountEmail; set => Set(ref newAccountEmail, value ?? ""); }

    /// <summary>What the last Sign in or Add account did, or is doing.</summary>
    public string AccountsMessage { get => accountsMessage; private set => Set(ref accountsMessage, value); }

    public bool IsSigningIn { get => signingIn; private set => Set(ref signingIn, value); }

    private ICommand? signInLoginCommand, addAccountCommand;

    /// <summary>Signs the card's per-CLI login in (the parameter is its <see cref="LoginCardViewModel"/>).</summary>
    public ICommand SignInLoginCommand => signInLoginCommand ??= new RelayCommand(async p =>
    {
        if (p is LoginCardViewModel { CanSignIn: true, Email: { } email }) await SignInOwnAsync(email);
    });

    /// <summary>Signs a new per-CLI login in for <see cref="NewAccountEmail"/>.</summary>
    public ICommand AddAccountCommand => addAccountCommand ??= new RelayCommand(async () => await AddAccountAsync());

    private async Task AddAccountAsync()
    {
        if (AccountDirs.Normalize(NewAccountEmail) is not { } email)
        {
            AccountsMessage = NewAccountEmail.Trim().Length == 0 ? "Type the account's email to add a login for it."
                : $"\"{NewAccountEmail.Trim()}\" isn't an email address: a login of its own takes the account's whole email.";
            return;
        }
        if (await SignInOwnAsync(email)) NewAccountEmail = "";
    }

    /// <summary>Runs <see cref="OwnLogins.SignIn"/> for <paramref name="email"/> off the UI thread (it waits on the browser login),
    /// one at a time, then refreshes the cards. True when it signed in.</summary>
    private async Task<bool> SignInOwnAsync(string email)
    {
        if (IsSigningIn) return false;
        IsSigningIn = true;
        AccountsMessage = $"Opening {email}'s login in the browser…";
        try
        {
            var dirs = OwnLoginDirs();
            var result = await Task.Run(() => OwnLogins.SignIn(dirs, email, sso: false));
            AccountsMessage = result.Message;
            if (!result.Ok) Toasts.Insert(0, new ToastViewModel($"{email} isn't signed in", result.Message, Palette.Over, null));
            return result.Ok;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AccountsMessage = $"Couldn't sign {email} in: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "per-CLI sign-in failed");
            return false;
        }
        finally
        {
            IsSigningIn = false;
            RefreshLogins();
            await RefreshLoginCardsAsync();
        }
    }

    /// <summary>Reads every login's card off the UI thread (a login's <c>.claude.json</c> can run to megabytes) and shows them.
    /// Only while the Accounts view is open: the normal refresh calls it, and so does opening the view.</summary>
    internal async Task RefreshLoginCardsAsync()
    {
        if (loadingLoginCards || !IsAccountsView) return;
        loadingLoginCards = true;
        try
        {
            var now = DateTime.UtcNow;
            var agents = snapshot?.Agents ?? [];
            var current = accountRows.FirstOrDefault(r => r.Account == currentAccount);
            var signedIn = snapshot?.Account is { } a && a != ClaudeAccount.ApiKey;
            var dirs = OwnLoginDirs();
            var cycler = loginCycler;
            var cards = await Task.Run(() => CardsOf(dirs, current, signedIn, agents, cycler.OnOwnLogin(), now));
            ApplyLoginCards(cards, now);
        }
        // Fire-and-forget from the refresh: any failure is shown, never thrown into the dispatcher.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AccountsMessage = $"Couldn't read the logins: {ex.Message}";
        }
        finally { loadingLoginCards = false; }
    }

    /// <summary>Every login's card: the global one (<paramref name="current"/> is the roster's row for whoever's signed in to
    /// it), then each per-CLI login in <paramref name="dirs"/>, each with its live CLIs.</summary>
    internal static IReadOnlyList<LoginCard> CardsOf(AccountDirs dirs, AccountRow? current, bool signedIn,
        IReadOnlyList<AgentBoardRow> agents, IReadOnlyDictionary<string, string> onOwnLogin, DateTime nowUtc) =>
        LoginBoard.Build(LoginBoard.Global(dirs.ClaudeDir, current, signedIn, Path.Combine(dirs.ClaudeDir, "rl-status.json"), nowUtc),
            dirs, agents, onOwnLogin, nowUtc);

    internal void ApplyLoginCards(IReadOnlyList<LoginCard> cards, DateTime nowUtc)
    {
        LoginCards.SyncBy([.. cards], c => c.Email ?? "", v => v.Key,
            c => { var v = new LoginCardViewModel(c.Email ?? ""); v.Update(c, nowUtc); return v; },
            (v, c) => v.Update(c, nowUtc));
        Raise(nameof(LoginCardsSummary));
    }
}
