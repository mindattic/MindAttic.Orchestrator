using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// A person's login actions (M.ORCH-T763), on their own page now that the Accounts view is gone: Sign in (its own login, as
/// <c>orchestrator login --account</c> does, <see cref="OwnLogins.SignIn"/>), Rename (its name and terminal-title prefix,
/// M.ORCH-T633), Org limit (its own 5-hour and weekly cutoffs, as <c>orchestrator org limit</c> sets them, M.ORCH-T707) and
/// Make default; Move CLIs is the window's menu (<see cref="MoveTargetsAsync"/>). The org-wide ones, Add login, Sign in all
/// and Report, sit in the small toolbar above the person tabs, on the global login's tab.
/// </summary>
public sealed partial class MainViewModel
{
    private bool signingIn;
    private string newAccountEmail = "";
    private string accountsMessage = "";

    /// <summary>The email Add login signs in a login of its own for.</summary>
    public string NewAccountEmail { get => newAccountEmail; set => Set(ref newAccountEmail, value ?? ""); }

    /// <summary>What the last Sign in, Add login, Rename, Org limit or Move did, or is doing.</summary>
    public string AccountsMessage { get => accountsMessage; private set => Set(ref accountsMessage, value); }

    public bool IsSigningIn { get => signingIn; private set => Set(ref signingIn, value); }

    private ICommand? signInPersonCommand, addAccountCommand, renamePersonCommand, saveLoginAliasCommand, cancelRenameCommand;

    // ── Rename ──────────────────────────────────────────────────────────────

    /// <summary>The login the Rename dialog names: its folder and its account's email.</summary>
    private (string Folder, string Email)? renaming;
    private string renameName = "", renamePrefix = "";

    /// <summary>The Rename dialog is open (M.ORCH-T633), from the person's Rename.</summary>
    public bool IsRenameOpen
    {
        get => renaming is not null;
        set { if (!value && renaming is not null) { renaming = null; RaiseRename(); } }
    }

    /// <summary>The dialog's Email: the login's account, shown and never edited.</summary>
    public string RenameEmail => renaming?.Email ?? "";

    /// <summary>The dialog's Name: the login's alias, its tab's title (blank: the email's part before the @).</summary>
    public string RenameName { get => renameName; set => Set(ref renameName, value ?? ""); }

    /// <summary>The dialog's Prefix: the badge its terminal titles start with ("[JR] Repo [Claude]"), the login file's
    /// <c>initials</c>; blank derives it from the name.</summary>
    public string RenamePrefix { get => renamePrefix; set => Set(ref renamePrefix, value ?? ""); }

    /// <summary>This person's login's folder (the global login's <c>~/.claude</c> on its tab) and its account's email; null
    /// while the email isn't known (nobody's signed in to the global login yet).</summary>
    private (string Folder, string Email)? OwnLogin()
    {
        if (!IsPersonTab) return null;
        var dirs = OwnLoginDirs();
        if (ScopeEmail is { } email) return (dirs.For(email).Directory, email);
        return Root.workspace?.GlobalEmail is { Length: > 0 } global ? (dirs.ClaudeDir, global) : null;
    }

    /// <summary>Rename: opens the Rename dialog on this person's login (M.ORCH-T614, M.ORCH-T633).</summary>
    public ICommand RenamePersonCommand => renamePersonCommand ??= new RelayCommand(() =>
    {
        if (OwnLogin() is not { } login) return;
        BeginRename(login.Folder, login.Email);
    }, () => IsTabSignedIn);

    internal void BeginRename(string folder, string email)
    {
        renaming = (folder, email);
        RenameName = LoginLook.For(folder, email).Alias;
        RenamePrefix = LoginLook.InitialsOverride(folder, email) ?? "";
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
        if (renaming is not { } login) return;
        AccountsMessage = SaveAlias(OwnLoginDirs(), login.Folder, login.Email, RenameName, RenamePrefix, aggregator.Db);
        renaming = null;
        RaiseRename();
        NoteLocalChange();
        await RefreshAsync();
    });

    /// <summary>Names <paramref name="email"/>'s logins <paramref name="alias"/> with the badge <paramref name="initials"/>
    /// (null leaves it; blank derives it from the name): every folder that's its login
    /// (<see cref="LoginLook.SetAlias(AccountDirs, string, string?, string?)"/>) and the login's own <paramref name="folder"/>;
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

    // ── Org limit ───────────────────────────────────────────────────────────

    private string? limiting;
    private string limitWeekly = "", limitFiveHour = "", limitError = "";
    private ICommand? orgLimitCommand, saveLimitCommand, resetLimitCommand, cancelLimitCommand;

    /// <summary>The Org limit dialog is open, on this person's login.</summary>
    public bool IsLimitOpen
    {
        get => limiting is not null;
        set { if (!value && limiting is not null) { limiting = null; RaiseLimit(); } }
    }

    /// <summary>Whose cutoffs the dialog sets.</summary>
    public string LimitEmail => limiting ?? "";

    /// <summary>The dialog's weekly cutoff, % of the week.</summary>
    public string LimitWeekly { get => limitWeekly; set => Set(ref limitWeekly, value ?? ""); }

    /// <summary>The dialog's 5-hour cutoff, % of the 5-hour window.</summary>
    public string LimitFiveHour { get => limitFiveHour; set => Set(ref limitFiveHour, value ?? ""); }

    /// <summary>Why the dialog's numbers can't be saved; empty when they can.</summary>
    public string LimitError { get => limitError; private set => Set(ref limitError, value); }

    /// <summary>The bounds the dialog says: "50 to 100" and "1 to 100", the defaults 90 and 98.</summary>
    public string LimitHint => $"Weekly {UsageCutoffs.MinWeeklyPercent} to 100 ({UsageCutoffs.DefaultWeeklyPercent} by default); 5-hour " +
        $"{UsageCutoffs.MinFiveHourPercent} to 100 ({UsageCutoffs.DefaultFiveHourPercent} by default). At either, this login's access is cut off " +
        "until it resets or you allow it past (orchestrator org allow).";

    /// <summary>Org limit: opens the dialog on this person's own cutoffs (orchestrator org limit &lt;who&gt; --weekly --5h).</summary>
    public ICommand OrgLimitCommand => orgLimitCommand ??= new RelayCommand(() =>
    {
        if (CutoffEmail is not { Length: > 0 } email) return;
        limiting = email;
        LimitWeekly = UsageCutoffs.WeeklyFor(Root.cutoffs, email).ToString(System.Globalization.CultureInfo.InvariantCulture);
        LimitFiveHour = UsageCutoffs.FiveHourFor(Root.cutoffs, email).ToString(System.Globalization.CultureInfo.InvariantCulture);
        LimitError = "";
        RaiseLimit();
    }, () => IsTabSignedIn && CutoffEmail is { Length: > 0 });

    public ICommand CancelLimitCommand => cancelLimitCommand ??= new RelayCommand(() => { limiting = null; RaiseLimit(); });

    /// <summary>Saves the dialog's cutoffs, each in its bounds; refused, with why, otherwise.</summary>
    public ICommand SaveLimitCommand => saveLimitCommand ??= new RelayCommand(async () =>
    {
        if (limiting is not { } email) return;
        if (ParseCutoffs(LimitWeekly, LimitFiveHour, out var weekly, out var fiveHour) is { } why) { LimitError = why; return; }
        await ChangeLimitAsync(email, () => UsageCutoffs.Set(store, email, TabTitle, weekly, fiveHour));
    });

    /// <summary>Back to the defaults for this person.</summary>
    public ICommand ResetLimitCommand => resetLimitCommand ??= new RelayCommand(async () =>
    {
        if (limiting is not { } email) return;
        await ChangeLimitAsync(email, () => UsageCutoffs.Reset(store, email, TabTitle));
    });

    private async Task ChangeLimitAsync(string email, Func<UsageCutoff> change)
    {
        try
        {
            var set = change();
            AccountsMessage = $"{TabTitle}'s cutoffs: {UsageCutoffs.Text(set.WeeklyPercent, set.FiveHourPercent)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException)
        {
            LimitError = $"Couldn't save {email}'s cutoffs: {ex.Message}";
            return;
        }
        limiting = null;
        RaiseLimit();
        NoteLocalChange();
        await RefreshAsync();
    }

    /// <summary>The dialog's two numbers as cutoffs in their bounds; why not, else null.</summary>
    internal static string? ParseCutoffs(string weeklyText, string fiveHourText, out int weekly, out int fiveHour)
    {
        fiveHour = 0;
        if (!int.TryParse(weeklyText.Trim().TrimEnd('%'), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out weekly)
            || weekly < UsageCutoffs.MinWeeklyPercent || weekly > 100)
            return $"The weekly cutoff is a whole number from {UsageCutoffs.MinWeeklyPercent} to 100.";
        if (!int.TryParse(fiveHourText.Trim().TrimEnd('%'), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out fiveHour)
            || fiveHour < UsageCutoffs.MinFiveHourPercent || fiveHour > 100)
            return $"The 5-hour cutoff is a whole number from {UsageCutoffs.MinFiveHourPercent} to 100.";
        return null;
    }

    private void RaiseLimit()
    {
        Raise(nameof(IsLimitOpen)); Raise(nameof(LimitEmail));
    }

    // ── Sign in ─────────────────────────────────────────────────────────────

    /// <summary>Sign in: this person's own login, in a private window of its own browser profile (again, to fix a mismatch or
    /// an expired one). The global login signs in with /login in any CLI, so its tab has none.</summary>
    public ICommand SignInPersonCommand => signInPersonCommand ??= new RelayCommand(async () =>
    {
        if (ScopeEmail is not { } email) return;
        if (await SignInOwnAsync(email)) await RefreshAsync();
    }, () => IsPersonTab && ScopeEmail is not null && !IsSigningIn);

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
        if (await SignInOwnAsync(email))
        {
            NewAccountEmail = "";
            await RefreshAsync();
        }
    }

    /// <summary>Runs <see cref="OwnLogins.SignIn"/> for <paramref name="email"/> off the UI thread (it waits on the browser login),
    /// one at a time. True when it signed in.</summary>
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
            // Its long-lived token too (M.ORCH-T696): claude setup-token, kept in the Vault for the login's tabs.
            var result = await Task.Run(() => OwnLogins.SignIn(dirs, email, sso: false, minting: new TokenMinting(LoginTokens.Default)));
            AccountsMessage = result.Token is { } token ? $"{result.Message} {token.Message}" : result.Message;
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
        }
    }
}
