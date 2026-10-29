using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// A person's login actions (M.ORCH-T763), on their own page now that the Accounts view is gone: User Settings (M.ORCH-T1098), its
/// name and terminal-title prefix (M.ORCH-T633) and its own 5-hour and weekly cutoffs (as <c>orchestrator org limit</c> sets them,
/// M.ORCH-T707) in one dialog, from the person's tab's right-click menu (M.ORCH-T797). The org-wide ones, Add login, Sign in all and Report, sit in the small toolbar above the
/// person tabs, on the global login's tab. There's no Sign in among them (M.ORCH-T796): a login that gets disconnected opens
/// its own sign-in (<see cref="OwnLogins.SignIn"/>) by itself.
/// </summary>
public sealed partial class MainViewModel
{
    private bool signingIn;
    private string newAccountEmail = "";
    private string accountsMessage = "";

    /// <summary>The email Add login signs in a login of its own for.</summary>
    public string NewAccountEmail { get => newAccountEmail; set => Set(ref newAccountEmail, value ?? ""); }

    /// <summary>What the last Sign in, Add login, User Settings or Move did, or is doing.</summary>
    public string AccountsMessage { get => accountsMessage; private set => Set(ref accountsMessage, value); }

    public bool IsSigningIn { get => signingIn; private set => Set(ref signingIn, value); }

    private ICommand? addAccountCommand;

    // ── User Settings (M.ORCH-T1098: Rename and Org limit in one dialog) ────

    /// <summary>The login User Settings names: its folder and its account's email; null when its email isn't known.</summary>
    private (string Folder, string Email)? renaming;
    private string renameName = "", renamePrefix = "";
    private bool userSettingsOpen;
    private ICommand? userSettingsCommand, saveUserSettingsCommand, cancelUserSettingsCommand, resetLimitCommand;

    /// <summary>The person's User Settings dialog is open (from their tab's right-click menu): their login's name and prefix
    /// (M.ORCH-T633) and their own 5-hour and weekly cutoffs (M.ORCH-T707), one Save for both.</summary>
    public bool IsUserSettingsOpen
    {
        get => userSettingsOpen;
        set { if (!value && userSettingsOpen) CloseUserSettings(); }
    }

    /// <summary>Whether the dialog has the login's Name and Prefix to edit (its email is known).</summary>
    public bool HasRenameSection => renaming is not null;

    /// <summary>Whether the dialog has cutoffs to edit.</summary>
    public bool HasLimitSection => limiting is not null;

    /// <summary>The dialog's Email: the login's account, shown and never edited.</summary>
    public string RenameEmail => renaming?.Email ?? limiting ?? "";

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

    /// <summary>User Settings: opens the dialog on this person's login, its Name and Prefix (M.ORCH-T614, M.ORCH-T633) and its own
    /// cutoffs (orchestrator org limit &lt;who&gt; --weekly --5h, M.ORCH-T707).</summary>
    public ICommand UserSettingsCommand => userSettingsCommand ??= new RelayCommand(() =>
    {
        renaming = OwnLogin();
        if (renaming is { } login)
        {
            RenameName = LoginLook.For(login.Folder, login.Email).Alias;
            RenamePrefix = LoginLook.InitialsOverride(login.Folder, login.Email) ?? "";
        }
        limiting = CutoffEmail is { Length: > 0 } email ? email : null;
        if (limiting is { } who)
        {
            LimitWeekly = UsageCutoffs.WeeklyFor(Root.cutoffs, who, Root.weeklyStopPercent).ToString(System.Globalization.CultureInfo.InvariantCulture);
            LimitFiveHour = UsageCutoffs.FiveHourFor(Root.cutoffs, who, (int)Root.paceTargetPercent).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        LimitError = "";
        if (renaming is null && limiting is null) return;
        userSettingsOpen = true;
        RaiseUserSettings();
    }, () => IsTabSignedIn && (OwnLogin() is not null || CutoffEmail is { Length: > 0 }));

    /// <summary>Closes User Settings without saving (Cancel, Esc, its close button, the backdrop).</summary>
    public ICommand CancelUserSettingsCommand => cancelUserSettingsCommand ??= new RelayCommand(CloseUserSettings);

    /// <summary>Saves the dialog: its login's Name and Prefix, with the member's name in the members table (M.ORCH-T670), and
    /// its cutoffs, each in its bounds (refused, with why, otherwise, and nothing is saved). Cutoffs left at the defaults are
    /// saved as the defaults (orchestrator org limit --reset), not as a custom pair that happens to match them.</summary>
    public ICommand SaveUserSettingsCommand => saveUserSettingsCommand ??= new RelayCommand(async () =>
    {
        if (!userSettingsOpen) return;
        int weekly = 0, fiveHour = 0;
        if (limiting is not null && ParseCutoffs(LimitWeekly, LimitFiveHour, out weekly, out fiveHour) is { } why) { LimitError = why; return; }
        var said = new List<string>();
        if (renaming is { } login) said.Add(SaveAlias(OwnLoginDirs(), login.Folder, login.Email, RenameName, RenamePrefix, aggregator.Db));
        if (limiting is { } email)
        {
            try
            {
                var set = weekly == UsageCutoffs.DefaultWeeklyPercent && fiveHour == UsageCutoffs.DefaultFiveHourPercent
                    ? UsageCutoffs.Reset(store, email, TabTitle)
                    : UsageCutoffs.Set(store, email, TabTitle, weekly, fiveHour);
                said.Add($"{TabTitle}'s cutoffs: {UsageCutoffs.Text(set.WeeklyPercent, set.FiveHourPercent)}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException)
            {
                AccountsMessage = string.Join(" ", said);
                LimitError = $"Couldn't save {email}'s cutoffs: {ex.Message}";
                return;
            }
        }
        AccountsMessage = string.Join(" ", said);
        CloseUserSettings();
        NoteLocalChange();
        await RefreshAsync();
    });

    /// <summary>Defaults: puts the dialog's cutoffs back to 90% of the week and 98% of the 5-hour window, for Save to keep.</summary>
    public ICommand ResetLimitCommand => resetLimitCommand ??= new RelayCommand(() =>
    {
        LimitWeekly = UsageCutoffs.DefaultWeeklyPercent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        LimitFiveHour = UsageCutoffs.DefaultFiveHourPercent.ToString(System.Globalization.CultureInfo.InvariantCulture);
        LimitError = "";
    }, () => limiting is not null);

    private void CloseUserSettings()
    {
        userSettingsOpen = false;
        renaming = null;
        limiting = null;
        RaiseUserSettings();
    }

    private void RaiseUserSettings()
    {
        Raise(nameof(IsUserSettingsOpen)); Raise(nameof(RenameEmail)); Raise(nameof(HasRenameSection)); Raise(nameof(HasLimitSection));
    }

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

    // ── User Settings' cutoffs ──────────────────────────────────────────────

    /// <summary>Whose cutoffs User Settings sets; null while it's closed or has no cutoffs to set.</summary>
    private string? limiting;
    private string limitWeekly = "", limitFiveHour = "", limitError = "";

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

    // ── Sign in ─────────────────────────────────────────────────────────────

    // No per-person Sign in button any more (M.ORCH-T796): a login that gets disconnected opens its own sign-in by itself.

    /// <summary>The logins waiting for their automatic sign-in, on the global login's tab, one at a time.</summary>
    private readonly Queue<string> autoSignIns = new();
    private bool autoSigningIn;

    /// <summary>The person tabs (by email) that just got disconnected (M.ORCH-T796): signed in at the last refresh
    /// (<paramref name="wasSignedIn"/>, by tab key) and signed out now. A tab seen for the first time, or signed out all along,
    /// isn't one, so opening the dashboard never pops a browser per member not signed in yet.</summary>
    internal static IReadOnlyList<string> NewlyDisconnected(IReadOnlyDictionary<string, bool> wasSignedIn, IEnumerable<LoginTab> now) =>
        [.. now.Where(t => t.Email is not null && !t.IsApiKey && !t.SignedIn
                           && wasSignedIn.TryGetValue(t.Key, out var was) && was)
               .Select(t => t.Email!)];

    /// <summary>Opens each just-disconnected login's own sign-in (M.ORCH-T796), as its tab's Sign in would, one after another:
    /// no waiting for you to click it. A sign-in you close or that fails leaves the tab on its Sign in panel.</summary>
    private async void AutoSignIn(IReadOnlyList<string> emails)
    {
        foreach (var e in emails)
            if (!autoSignIns.Contains(e, StringComparer.OrdinalIgnoreCase)) autoSignIns.Enqueue(e);
        if (autoSigningIn || autoSignIns.Count == 0) return;
        autoSigningIn = true;
        try
        {
            while (autoSignIns.TryDequeue(out var email))
            {
                var on = loginTabs.FirstOrDefault(t => string.Equals(t.ScopeEmail, email, StringComparison.OrdinalIgnoreCase));
                if (on is null || on.tab.SignedIn) continue;
                Toasts.Insert(0, new ToastViewModel($"{on.TabTitle} was signed out", $"Opening {email}'s own sign-in in a private browser window.", Palette.Other, null));
                if (await on.SignInOwnAsync(email)) await RefreshAsync();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "automatic sign-in failed");
        }
        finally { autoSigningIn = false; }
    }

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
