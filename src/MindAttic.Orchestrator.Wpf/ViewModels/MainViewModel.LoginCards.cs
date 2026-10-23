using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

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

    private ICommand? signInLoginCommand, addAccountCommand, renameLoginCommand, saveLoginAliasCommand, cancelRenameCommand;

    private LoginCardViewModel? renaming;
    private string renameName = "", renamePrefix = "";

    /// <summary>The Rename dialog is open (M.ORCH-T633): opened from a card's right-click menu.</summary>
    public bool IsRenameOpen
    {
        get => renaming is not null;
        set { if (!value && renaming is not null) { renaming = null; RaiseRename(); } }
    }

    /// <summary>The dialog's Email: the login's account, shown and never edited.</summary>
    public string RenameEmail => renaming?.AliasEmail ?? "";

    /// <summary>The dialog's Name: the login's alias, its card's title (blank: the email's part before the @).</summary>
    public string RenameName { get => renameName; set => Set(ref renameName, value ?? ""); }

    /// <summary>The dialog's Prefix: the badge its terminal titles start with ("[JR] Repo [Claude]"), the login file's
    /// <c>initials</c>; blank derives it from the name.</summary>
    public string RenamePrefix { get => renamePrefix; set => Set(ref renamePrefix, value ?? ""); }

    /// <summary>Opens the Rename dialog on a card's login (M.ORCH-T614, M.ORCH-T633; the parameter is its <see cref="LoginCardViewModel"/>).</summary>
    public ICommand RenameLoginCommand => renameLoginCommand ??= new RelayCommand(p =>
    {
        if (p is LoginCardViewModel { CanRename: true } card) BeginRename(card);
    });

    internal void BeginRename(LoginCardViewModel card)
    {
        renaming = card;
        RenameName = card.Card.Alias ?? LoginLook.DefaultAlias(card.AliasEmail) ?? "";
        RenamePrefix = LoginLook.InitialsOverride(card.Card.Directory, card.AliasEmail) ?? "";
        RaiseRename();
    }

    private void RaiseRename()
    {
        Raise(nameof(IsRenameOpen)); Raise(nameof(RenameEmail));
    }

    /// <summary>Closes the Rename dialog without saving (Cancel, Esc, its close button, the backdrop).</summary>
    public ICommand CancelRenameCommand => cancelRenameCommand ??= new RelayCommand(() => { renaming = null; RaiseRename(); });

    /// <summary>Saves the dialog's Name and Prefix for its login, and the member's name in the members table (M.ORCH-T670).</summary>
    public ICommand SaveLoginAliasCommand => saveLoginAliasCommand ??= new RelayCommand(async () =>
    {
        if (renaming is not { AliasEmail: { } email } card) return;
        AccountsMessage = SaveAlias(OwnLoginDirs(), card.Card.Directory, email, RenameName, RenamePrefix, aggregator.Db);
        renaming = null;
        RaiseRename();
        await RefreshLoginCardsAsync();
    });

    /// <summary>Names <paramref name="email"/>'s logins <paramref name="alias"/> with the badge <paramref name="initials"/>
    /// (null leaves it; blank derives it from the name): every folder that's its login
    /// (<see cref="LoginLook.SetAlias(AccountDirs, string, string?, string?)"/>) and the card's own <paramref name="folder"/>;
    /// and, when <paramref name="db"/> holds the email as a member, the member's name in step (blank clears it, back to the
    /// alias). Says what it did.</summary>
    internal static string SaveAlias(AccountDirs dirs, string folder, string email, string alias, string? initials = null, OrchestratorDb? db = null)
    {
        try
        {
            LoginLook.Rename(dirs, folder, email, alias, initials, db);
            var look = LoginLook.For(folder, email);
            return $"{email} is now called {look.Alias} [{look.Initials}].";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Data.Common.DbException)
        {
            return $"Couldn't rename {email}: {ex.Message}";
        }
    }

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
        // Every tab's light: the one signing in goes purple while it does (M.ORCH-T630).
        SetSigningInEmail(email);
        CommandManager.InvalidateRequerySuggested();
        // Its own login screen (M.ORCH-T610): a private window of the account's own browser profile, prefilled with its email.
        AccountsMessage = $"Opening {email}'s own sign-in in a private browser window: sign in there as {email}…";
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
            SetSigningInEmail(null);
            CommandManager.InvalidateRequerySuggested();
            RefreshLogins();
            await RefreshLoginCardsAsync();
        }
    }

    /// <summary>Reads every login's card off the UI thread (a login's <c>.claude.json</c> can run to megabytes) and shows them.
    /// Only while the Accounts view is open: the normal refresh calls it, and so does opening the view.</summary>
    private void SetLoadingLoginCards(bool on) { loadingLoginCards = on; Raise(nameof(IsViewBusy)); }

    internal async Task RefreshLoginCardsAsync()
    {
        if (loadingLoginCards || !IsAccountsView) return;
        SetLoadingLoginCards(true);
        try
        {
            var now = DateTime.UtcNow;
            // Every login's, whichever tab it's opened in: the Accounts view is the overview of them all (M.ORCH-T613).
            var agents = full?.Agents ?? [];
            var current = accountRows.FirstOrDefault(r => r.Account == currentAccount);
            var signedIn = full?.Account is { } a && a != ClaudeAccount.ApiKey;
            var dirs = OwnLoginDirs();
            var cycler = loginCycler;
            var cards = await Task.Run(() => CardsOf(dirs, current, signedIn, agents, cycler.OnOwnLogin(), now));
            // Each member's own use outside the orchestrator, as the daemon keeps it (M.ORCH-T682).
            var outside = await Task.Run(() => OutsideUseWatch.Stats(OutsideUseWatch.Load(store), store.LoadSettings()));
            ApplyLoginCards(cards, now, outside);
        }
        // Fire-and-forget from the refresh: any failure is shown, never thrown into the dispatcher.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AccountsMessage = $"Couldn't read the logins: {ex.Message}";
        }
        finally { SetLoadingLoginCards(false); }
    }

    /// <summary>Every login's card: the global one (<paramref name="current"/> is the roster's row for whoever's signed in to
    /// it), then each per-CLI login in <paramref name="dirs"/>, each with its live CLIs.</summary>
    internal static IReadOnlyList<LoginCard> CardsOf(AccountDirs dirs, AccountRow? current, bool signedIn,
        IReadOnlyList<AgentBoardRow> agents, IReadOnlyDictionary<string, string> onOwnLogin, DateTime nowUtc) =>
        LoginBoard.Build(LoginBoard.Global(dirs.ClaudeDir, current, signedIn, Path.Combine(dirs.ClaudeDir, "rl-status.json"), nowUtc),
            dirs, agents, onOwnLogin, nowUtc);

    internal void ApplyLoginCards(IReadOnlyList<LoginCard> cards, DateTime nowUtc, IReadOnlyList<OutsideUseStats>? outside = null)
    {
        OutsideUseStats? OutsideOf(LoginCard c) =>
            outside?.FirstOrDefault(o => string.Equals(o.Key, c.Email ?? "", StringComparison.OrdinalIgnoreCase));
        // Who the global login is signed in to: that per-CLI login's card is the default already (no Make default).
        var defaultEmail = IsOnApiKey ? null : cards.FirstOrDefault(c => c.IsGlobal)?.SignedInAs;
        LoginCards.SyncBy([.. cards], c => c.Email ?? "", v => v.Key,
            c => { var v = new LoginCardViewModel(c.Email ?? ""); v.Update(c, nowUtc, defaultEmail, IsOnApiKey, OutsideOf(c)); return v; },
            (v, c) => v.Update(c, nowUtc, defaultEmail, IsOnApiKey, OutsideOf(c)));
        Raise(nameof(LoginCardsSummary));
    }
}
