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

    /// <summary>A browser sign-in is under way (one at a time): Start and another sign-in wait for it.</summary>
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
    /// its cutoffs, each in its bounds (refused, with why, otherwise, and nothing is saved). A cutoff the member never set and
    /// left at the live global value it was shown with keeps following that global dial (not pinned); anything else is stored
    /// as set, even a value equal to the defaults.</summary>
    public ICommand SaveUserSettingsCommand => saveUserSettingsCommand ??= new RelayCommand(async () =>
    {
        if (!userSettingsOpen) return;
        int weekly = 0, fiveHour = 0;
        if (limiting is not null && ParseCutoffs(LimitWeekly, LimitFiveHour, out weekly, out fiveHour) is { } why) { LimitError = why; return; }
        var said = new List<string>();
        // The name the cutoffs are kept and said under: the one just saved (M.ORCH-T1438), not the tab's title from before the Save.
        var name = TabTitle;
        if (renaming is { } login)
        {
            said.Add(SaveAlias(OwnLoginDirs(), login.Folder, login.Email, RenameName, RenamePrefix, aggregator.Db));
            name = LoginLook.For(login.Folder, login.Email).Alias;
        }
        if (limiting is { } email)
        {
            try
            {
                int? setWeekly = UsageCutoffs.HasWeekly(Root.cutoffs, email) || weekly != Root.weeklyStopPercent ? weekly : null;
                int? setFiveHour = UsageCutoffs.HasFiveHour(Root.cutoffs, email) || fiveHour != (int)Root.paceTargetPercent ? fiveHour : null;
                var set = UsageCutoffs.Set(store, email, name, setWeekly, setFiveHour);
                // Applied here at once (M.ORCH-T1438), not left for the refresh below to read back: every tab's Cutoffs tile and
                // disabled icon show the new numbers the moment the dialog closes, whatever that refresh meets.
                Root.cutoffs = UsageCutoffs.Load(store);
                foreach (var tab in Root.LoginTabs) tab.RaiseOwnNumbers();
                said.Add($"{name}'s cutoffs: {UsageCutoffs.Text(set, Root.weeklyStopPercent, (int)Root.paceTargetPercent)}.");
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

    /// <summary>Defaults: puts the dialog's cutoffs back to <see cref="UsageCutoffs.DefaultWeeklyPercent"/> of the week and
    /// <see cref="UsageCutoffs.DefaultFiveHourPercent"/> of the 5-hour window, for Save to keep.</summary>
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

    /// <summary>The bounds the dialog says: "50 to 100" and "1 to 100", the defaults 90 and 96.</summary>
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

    // ── Login / Log out, from a login's right-click menu (M.ORCH-T1820, M.ORCH-T1821) ──────

    private ICommand? loginCommand, logOutCommand;

    /// <summary>
    /// "Login", on every real login's right-click menu (<c>MainWindow.ShowLoginMenu</c>), not just the API key pseudo-account's
    /// (which has Buy API credits instead): re-authenticates this one account without opening the Logins modal or touching any
    /// other login. Always runnable, not only once signed out (<see cref="SignInTabCommand"/> is only that), so a login nearing
    /// its token's expiry can be renewed ahead of it. A per-CLI login runs the exact sign-in <see cref="SignInOwnAsync"/> already
    /// uses; the global login's tab (no <see cref="ScopeEmail"/>) runs <see cref="AccountSwitch.Login"/> for it instead, the same
    /// <c>claude auth login --email</c> <c>orchestrator login &lt;account&gt;</c> itself runs.
    /// </summary>
    public ICommand LoginCommand => loginCommand ??= new RelayCommand(async () =>
    {
        if (IsApiKeyTab) return;
        if (ScopeEmail is { } email) { if (await SignInOwnAsync(email)) await RefreshAsync(); }
        else if (Root.workspace?.GlobalEmail is { Length: > 0 } global) await RenewGlobalLoginAsync(global);
        // Logged out (M.ORCH-T1821), the global login has no email left to name: a plain claude auth login picks it.
        else if (!IsTabSignedIn) { await RenewGlobalLoginAsync(null); await RefreshAsync(); }
    }, () => !IsApiKeyTab && !IsSigningIn && (ScopeEmail is not null || Root.workspace?.GlobalEmail is { Length: > 0 } || !IsTabSignedIn));

    /// <summary>Opens <paramref name="email"/>'s login again for the global login's tab: the same seam <see cref="AccountSwitch.Login"/>
    /// and <c>orchestrator login &lt;account&gt;</c> run, off the UI thread (it waits on the browser), with no roster row or
    /// billing switch to weigh in here (the global login is never the API key). A null <paramref name="email"/> (the global
    /// login logged out, M.ORCH-T1821, so no email is known for it) runs a plain <c>claude auth login</c>.</summary>
    private async Task RenewGlobalLoginAsync(string? email)
    {
        if (IsSigningIn) return;
        IsSigningIn = true;
        SetSigningInEmail(email);
        CommandManager.InvalidateRequerySuggested();
        var who = email ?? "the global login";
        var command = AccountSwitch.LoginCommand(email ?? "", sso: false);
        AccountsMessage = $"Opening {who}'s login in the browser ({command}).";
        try
        {
            var code = await Task.Run(() => AccountSwitch.Login(email ?? "", false));
            AccountsMessage = code == 0
                ? $"Signed in as {who}: every CLI follows at its next turn."
                : $"{command} ended with exit code {code}; the account wasn't switched.";
            if (code != 0) Toasts.Insert(0, new ToastViewModel($"{who} not signed in", AccountsMessage, Palette.Over, null));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AccountsMessage = $"Couldn't run {command}: {ex.Message}";
            Toasts.Insert(0, new ToastViewModel($"{who} not signed in", AccountsMessage, Palette.Over, null));
        }
        finally
        {
            IsSigningIn = false;
            SetSigningInEmail(null);
            CommandManager.InvalidateRequerySuggested();
        }
        NoteLocalChange();
        await RefreshAsync();
    }

    /// <summary>
    /// "Log out" (M.ORCH-T1821), on a signed-in login's right-click menu: signs this one account out, so its CLIs can't be
    /// used until Login (above) signs it back in. A per-CLI login's own folder (<see cref="AccountDirs.SignOut(AccountDir)"/>)
    /// goes through the exact file state <see cref="IsTabSignedIn"/> already reads back (the tree's "Disabled: signed out"
    /// badge and the sign-in-only panel a roster account not yet signed in here shows): no flag of its own, so it grays out
    /// and its menu reduces to just Login the same way, once its tab has no live CLI of its own left to keep it looking
    /// signed in. The global login's tab (<see cref="AccountDirs.SignOutGlobal"/>) signs out the same way, and
    /// <c>LoginScope.Tabs</c> reads it back (<see cref="AccountDirs.GlobalSignedOut"/>), so its tab grays and its menu reduces
    /// to Login too.
    /// </summary>
    public ICommand LogOutCommand => logOutCommand ??= new RelayCommand(() =>
    {
        if (IsApiKeyTab || !IsTabSignedIn) return;
        string label;
        string? error;
        if (ScopeEmail is { } email)
        {
            label = email;
            error = AccountDirs.SignOut(OwnLoginDirs().For(email));
        }
        else
        {
            var dirs = OwnLoginDirs();
            label = Root.workspace?.GlobalEmail ?? TabTitle;
            error = AccountDirs.SignOutGlobal(dirs.ClaudeDir, dirs.HomeConfigPath);
        }
        AccountsMessage = error is null
            ? $"{label} is signed out: its CLIs are inaccessible until Login (above) signs it back in."
            : $"Couldn't sign {label} out: {error}";
        if (error is not null) Toasts.Insert(0, new ToastViewModel($"Couldn't sign {label} out", error, Palette.Over, null));
        NoteLocalChange();
        _ = RefreshAsync();
    }, () => !IsApiKeyTab && IsTabSignedIn);

    // ── Lock / Unlock, from a login's right-click menu (M.ORCH-T2118) ──────

    private ICommand? toggleLockCommand;

    /// <summary>The email <see cref="IsLoginLocked"/> and <see cref="ToggleLockCommand"/> act on: this tab's own login
    /// (<see cref="ScopeEmail"/>), or the global login's email on its own tab (<see cref="OwnLogin"/>'s fallback); null for
    /// the API key tab, or while the global login isn't known here yet.</summary>
    private string? LockEmail => ScopeEmail ?? Root.workspace?.GlobalEmail;

    /// <summary>Whether <paramref name="email"/> counts as locked for the menu: never the API key. Gated first by
    /// <paramref name="liveLocked"/> (<see cref="LoginLocks.LiveLocked"/>'s cached set, like <c>OwnLock</c>), so the common
    /// unlocked login never reads the lock book; only one that set names is confirmed against <paramref name="store"/>. With
    /// no set, the store is read directly. What <see cref="IsLoginLocked"/> calls with this tab's own state.</summary>
    internal static bool IsLocked(JsonStore store, string? email, bool isApiKeyTab, IReadOnlySet<string>? liveLocked = null) =>
        !isApiKeyTab && AccountDirs.Normalize(email) is { } normalized
        && (liveLocked is null || liveLocked.Contains(normalized))
        && LoginLocks.IsLocked(LoginLocks.Load(store), normalized);

    /// <summary>Whether this tab's login is locked (M.ORCH-T2108): false for the API key tab.</summary>
    internal bool IsLoginLocked => IsLocked(store, LockEmail, IsApiKeyTab, LoginLocks.LiveLocked());

    /// <summary>Whether this login's "+ Add" buttons are enabled (M.ORCH-T2185): signed in and not locked. Raised with the
    /// tab status (<see cref="RaiseTabStatus"/>), so a lock pushed over the bus greys them at once.</summary>
    public bool CanAddCli => CanAddCliOf(IsTabSignedIn, IsLoginLocked);

    /// <summary>"+ Add"'s tooltip while locked (M.ORCH-T2185); null otherwise, so each button keeps its own wording.</summary>
    public string? AddCliLockedTip => AddCliLockedTipOf(IsLoginLocked);

    internal static bool CanAddCliOf(bool signedIn, bool locked) => signedIn && !locked;

    internal static string? AddCliLockedTipOf(bool locked) => locked ? "Locked: right-click > Unlock to use this login" : null;

    /// <summary>
    /// "Lock"/"Unlock" (M.ORCH-T2118), on every real login's right-click menu (never the API key's): a locked login can't
    /// open a CLI or take dispatched or moved work until it's unlocked again (<see cref="LoginLocks"/>, M.ORCH-T2122).
    /// Unlocking a login whose lock came from its own weekly cutoff (<see cref="LoginLocks.ReasonWeeklyCutoff"/>) confirms
    /// first, showing its current weekly usage, since the same cutoff may still apply once it's unlocked.
    /// </summary>
    public ICommand ToggleLockCommand => toggleLockCommand ??= new RelayCommand(async () =>
    {
        if (IsApiKeyTab || LockEmail is not { } email) return;
        var root = Root;
        var locking = true;
        // async void: the DB read and anything thrown below must stay inside the try, or it crashes the app (M.ORCH-T1970).
        try
        {
            var existing = LoginLocks.For(LoginLocks.Load(store), email);
            if (existing is { Reason: LoginLocks.ReasonWeeklyCutoff })
            {
                var percent = WeekUsed is { } used ? $"{used:0}%" : "an unknown amount";
                if (MindAttic.Components.Wpf.Controls.MaMessageBox.Show(System.Windows.Application.Current?.MainWindow,
                        $"Unlock {TabTitle} ({email})?\n\nIt was locked at its own weekly cutoff, and it's currently at {percent} of its week used. " +
                        "Unlocking it lets it open a CLI and take work again, but its weekly cutoff still stops its pace until its weekly reset unless it's allowed past it (org allow).",
                        "Unlock login?", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No) != System.Windows.MessageBoxResult.Yes) return;
            }
            locking = existing is null;
            if (locking) await Task.Run(() => LoginLocks.Lock(store, email, TabTitle, LoginLocks.ReasonManual, "you", null, DateTime.UtcNow));
            else { var reset = WeekReset; await Task.Run(() => LoginLocks.Unlock(store, email, TabTitle, "you", DateTime.UtcNow, reset)); }
            LoginLocks.InvalidateLiveCache();
            AccountsMessage = locking
                ? $"{email} is locked: it can't open a CLI or take dispatched or moved work until it's unlocked again."
                : $"{email} is unlocked again.";
            NoteLocalChange();
            await root.RefreshAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AccountsMessage = $"Couldn't {(locking ? "lock" : "unlock")} {email}: {ex.Message}";
            Toasts.Insert(0, new ToastViewModel($"{email} not changed", AccountsMessage, Palette.Over, null));
        }
    }, () => !IsApiKeyTab && LockEmail is not null);

    // ── Delete, from a login's right-click menu (M.ORCH-T1953) ──────

    private ICommand? deleteLoginCommand;

    /// <summary>The logins being deleted (M.ORCH-T1953): no Launcher or General Prompt is reopened for one while its tabs close
    /// and its folder moves away, which would otherwise make the folder again.</summary>
    private readonly HashSet<string> deletingLogins = new(StringComparer.OrdinalIgnoreCase);

    internal bool IsBeingDeleted(string? email) => AccountDirs.Normalize(email) is { } e && Root.deletingLogins.Contains(e);

    /// <summary>
    /// "Delete" (M.ORCH-T1953), on a per-CLI login's right-click menu (never the API key's or the global login's): removes the
    /// member for good, after a confirm. Its open tabs (CLIs, Launcher, General Prompt) are closed, its
    /// <c>~/.claude-accounts\&lt;email&gt;</c> folder is moved to a dated backup (<c>.claude-accounts\.deleted\&lt;email&gt;-yyyyMMddHHmmss</c>,
    /// which no node is made from, M.ORCH-T1950 only reading folders named as emails) rather than deleted, and it is
    /// taken off the member roster the way <c>orchestrator members remove &lt;email&gt;</c> does
    /// (<see cref="MemberRoster.Remove"/>). Its node then drops at the refresh. A folder that can't be moved (still in use)
    /// stops it there, the roster untouched, so the node never comes back half-deleted.
    /// </summary>
    public ICommand DeleteLoginCommand => deleteLoginCommand ??= new RelayCommand(async () =>
    {
        if (IsApiKeyTab || AccountDirs.Normalize(ScopeEmail) is not { } email) return;
        var dirs = OwnLoginDirs();
        var backup = Path.Combine(dirs.Root, ".deleted", $"{email}-{DateTime.Now:yyyyMMddHHmmss}");
        if (MindAttic.Components.Wpf.Controls.MaMessageBox.Show(System.Windows.Application.Current?.MainWindow,
                $"Delete {TabTitle} ({email})?\n\nIts open CLIs, Launcher and General Prompt close, it's removed from the member roster " +
                $"(orchestrator members remove {email}), and its node leaves the tree.\n\nIts login folder isn't deleted: it moves to\n{backup}\n" +
                "so its credentials and history can be restored by moving it back (and adding the member again).",
                "Delete login?", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No) != System.Windows.MessageBoxResult.Yes) return;
        var root = Root;
        root.deletingLogins.Add(email);
        try
        {
            foreach (var pane in root.AllTerminals.Panes.Where(p => string.Equals(TreeLoginKey(p), email, StringComparison.OrdinalIgnoreCase)).ToList())
                root.AllTerminals.Close(pane);
            var folder = dirs.For(email).Directory;
            var error = await Task.Run(() => MoveToBackup(folder, backup));
            if (error is not null)
            {
                AccountsMessage = $"Couldn't delete {email}: its folder couldn't be moved to {backup} ({error}). Nothing else was changed; close what holds it and try again.";
                Toasts.Insert(0, new ToastViewModel($"{email} not deleted", AccountsMessage, Palette.Over, null));
                return;
            }
            var removed = await Task.Run(() => MemberRoster.Remove(root.aggregator.Db, email));
            AccountsMessage = $"Deleted {email}: " + (removed ? "removed from the member roster, " : "") +
                (Directory.Exists(backup) ? $"its folder moved to {backup}." : "it had no login folder here.");
            NoteLocalChange();
            await root.RefreshAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AccountsMessage = $"Couldn't delete {email}: {ex.Message}";
            Toasts.Insert(0, new ToastViewModel($"{email} not deleted", AccountsMessage, Palette.Over, null));
            OrchestratorLog.For("Dashboard").Error(ex, "deleting a login failed");
        }
        finally { root.deletingLogins.Remove(email); }
    }, () => !IsApiKeyTab && ScopeEmail is not null && !IsSigningIn);

    /// <summary>Moves <paramref name="folder"/> to <paramref name="backup"/>, retrying briefly while a just-ended CLI still holds a
    /// file in it; null when moved (or there was no folder), else why not.</summary>
    private static string? MoveToBackup(string folder, string backup)
    {
        if (!Directory.Exists(folder)) return null;
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        for (var attempt = 1; ; attempt++)
        {
            try { Directory.Move(folder, backup); return null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 10) return ex.Message;
                Thread.Sleep(500);
            }
        }
    }
}
