using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>How one per-CLI login's sign-in went (M.ORCH-T604).</summary>
public enum OwnLoginOutcome
{
    /// <summary>Signed in to the account it was opened for.</summary>
    SignedIn,
    /// <summary>The sign-in landed on another account than the one asked for, so the folder was signed out again (M.ORCH-T610).</summary>
    SignedInAsOther,
    /// <summary>It already had its own login: left alone.</summary>
    AlreadySignedIn,
    /// <summary>The roster row has no email yet (it shows once the account is signed in to again), so it has no folder.</summary>
    NoEmail,
    /// <summary>The login didn't run, didn't finish, or left nobody signed in.</summary>
    Failed,
}

/// <summary>One account's sign-in: who (<see cref="Label"/>, its <see cref="Email"/> when known), how it went, and what to say.
/// <see cref="Notes"/> are what preparing its folder had to say.</summary>
public sealed record OwnLoginResult(string Label, string? Email, string? Directory, OwnLoginOutcome Outcome, string Message, IReadOnlyList<string> Notes)
{
    public bool Ok => Outcome is OwnLoginOutcome.SignedIn or OwnLoginOutcome.AlreadySignedIn;

    /// <summary>How minting the login's long-lived token went (M.ORCH-T696); null when none was minted (one was kept already,
    /// or minting wasn't asked for).</summary>
    public MintResult? Token { get; init; }
}

/// <summary>Minting long-lived tokens along with the sign-ins (M.ORCH-T696): where they're kept, whether to mint one even
/// though a good one is kept (<see cref="Renew"/>), and who to ask for the token when <c>setup-token</c>'s output had none.</summary>
public sealed record TokenMinting(LoginTokens Tokens, bool Renew = false, Func<string?>? Paste = null);

/// <summary>
/// Signs per-CLI logins in (M.ORCH-T559, M.ORCH-T604): <see cref="SignIn"/> one account's own folder, or <see cref="SignInAll"/>
/// every account in the roster that has none yet, one after another, so every Claude Team seat stays signed in side by side
/// and the global login is only the default. Each runs <c>claude auth login --email</c> under the account's folder through
/// <see cref="AccountSwitch.LoginInto"/>; the global login is never touched.
/// </summary>
public static class OwnLogins
{
    /// <summary>Makes the API key's folder ready from the Vault's key: null when ready, else why not (seam).</summary>
    internal static Func<AccountDirs, string?> PrepareApiKey { get; set; } = d => Billing.ApiKeyPerson.Prepare(d, new Billing.BillingSwitch());

    /// <summary>Prepares <paramref name="email"/>'s own folder and runs its login there; <paramref name="opening"/> hears of it
    /// just before the browser opens (with the command run), since the login waits on the user.</summary>
    /// <remarks>With <paramref name="minting"/> (M.ORCH-T696) a signed-in login also gets its long-lived token
    /// (<c>claude setup-token</c>, <see cref="LoginTokens"/>) when it has no good one, so its tabs bill it for a year.</remarks>
    public static OwnLoginResult SignIn(AccountDirs dirs, string email, bool sso, Action<AccountDir, string>? opening = null, TokenMinting? minting = null)
    {
        if (AccountDirs.Normalize(email) is null)
            return new OwnLoginResult(email, null, null, OwnLoginOutcome.Failed, $"\"{email}\" isn't an email address.", []);
        var dir = dirs.For(email);
        // The API key is where the Team's usage credits are spent, not a person: it never signs in to claude.ai (M.ORCH-T1101).
        // Its folder is made ready from the Vault's key instead, which every CLI it starts bills.
        if (Billing.ApiKeyPerson.Is(email))
            return PrepareApiKey(dirs) is { } notReady
                ? new OwnLoginResult(dir.Email, dir.Email, dir.Directory, OwnLoginOutcome.Failed, notReady, [])
                : new OwnLoginResult(dir.Email, dir.Email, dir.Directory, OwnLoginOutcome.AlreadySignedIn,
                    $"{dir.Email} needs no sign-in: its CLIs bill the MindAttic Vault's Claude API key.", []);
        List<string> notes;
        try { notes = new List<string>(dirs.Prepare(dir)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new OwnLoginResult(dir.Email, dir.Email, dir.Directory, OwnLoginOutcome.Failed, $"Couldn't prepare {dir.Email}'s login ({ex.Message}).", []);
        }
        // Its own login screen (M.ORCH-T610): a private window of the account's own browser profile, not the default browser
        // already signed in to claude.ai as whoever, so the page asks for this person's email and code.
        var opener = LoginBrowser.Prepare(dir, out var browserNote);
        if (browserNote is not null) notes.Add(browserNote);
        var command = $"{AccountDirs.EnvVar}={dir.Directory} " + (opener is null ? "" : $"BROWSER={opener} ") + AccountSwitch.LoginCommand(dir.Email, sso);
        opening?.Invoke(dir, command);
        OwnLoginResult Done(OwnLoginOutcome outcome, string message) => new(dir.Email, dir.Email, dir.Directory, outcome, message, notes);
        int code;
        try { code = AccountSwitch.LoginInto(dir.Email, sso, dir.Directory); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Done(OwnLoginOutcome.Failed, $"Couldn't run {command}: {ex.Message}");
        }
        if (code != 0) return Done(OwnLoginOutcome.Failed, $"{command} ended with exit code {code}; {dir.Email} isn't signed in.");
        if (!dir.SignedIn) return Done(OwnLoginOutcome.Failed, $"The login finished but no Claude account is signed in to in {dir.Directory}.");
        // The folder is the chosen account's: one signed in as anybody else is signed out again, so no CLI spends their tokens.
        if (dir.SignedInEmail is { } signed && !signed.Equals(dir.Email, StringComparison.OrdinalIgnoreCase))
            return Done(OwnLoginOutcome.SignedInAsOther, AccountDirs.SignOut(dir) is { } why
                ? $"Signed in as {signed}, not {dir.Email}, and couldn't sign the folder out again ({why}): CLIs started with --account {dir.Email} would spend {signed}'s tokens."
                : $"Signed in as {signed}, not {dir.Email}: signed {dir.Email}'s login out again. Sign in once more as {dir.Email}.");
        return Done(OwnLoginOutcome.SignedIn, $"Signed in: {dir.Email} has its own login in {dir.Directory}.") with { Token = Mint(dir, opener, minting) };
    }

    /// <summary>Mints <paramref name="dir"/>'s long-lived token when <paramref name="minting"/> asks for tokens and it has no good
    /// one (or a new one is asked for); null when nothing was minted.</summary>
    public static MintResult? Mint(AccountDir dir, string? opener, TokenMinting? minting)
    {
        if (minting is null) return null;
        var now = DateTime.UtcNow;
        if (!minting.Renew && !minting.Tokens.NeedsMint(dir.Email, now)) return null;
        return minting.Tokens.MintFor(dir, opener, now, minting.Paste);
    }

    /// <summary>
    /// Every roster row with an email whose own folder isn't signed in yet gets its folder and its login, in turn (each login
    /// waits for the browser). Rows already signed in on their own are left alone; rows with no email yet are said and skipped.
    /// One email is signed in once, however many rows (orgs) carry it. <paramref name="done"/> hears of each as it finishes.
    /// </summary>
    public static IReadOnlyList<OwnLoginResult> SignInAll(AccountDirs dirs, IEnumerable<AccountRow> rows,
        Action<AccountDir, string>? opening = null, Action<OwnLoginResult>? done = null, TokenMinting? minting = null)
    {
        var results = new List<OwnLoginResult>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            OwnLoginResult result;
            if (row.Email is not { } email || AccountDirs.Normalize(email) is not { } normalized)
                result = new OwnLoginResult(row.Label, row.Email, null, OwnLoginOutcome.NoEmail,
                    $"{row.Label} has no email yet (it shows once it's signed in to again), so it's skipped.", []);
            else if (!seen.Add(normalized)) continue;
            else if (dirs.For(normalized) is { SignedIn: true } already)
                result = new OwnLoginResult(already.Email, already.Email, already.Directory, OwnLoginOutcome.AlreadySignedIn,
                    $"{already.Email} already has its own login.", []) { Token = MintSignedIn(already, opening, minting) };
            else result = SignIn(dirs, normalized, AccountSwitch.UsesSso(row), opening, minting);
            results.Add(result);
            done?.Invoke(result);
        }
        return results;
    }

    /// <summary>A login already signed in that has no good long-lived token gets one, through its own private browser window.</summary>
    public static MintResult? MintSignedIn(AccountDir dir, Action<AccountDir, string>? opening, TokenMinting? minting)
    {
        if (minting is null || (!minting.Renew && !minting.Tokens.NeedsMint(dir.Email, DateTime.UtcNow))) return null;
        var opener = LoginBrowser.Prepare(dir, out _);
        opening?.Invoke(dir, $"{AccountDirs.EnvVar}={dir.Directory} " + (opener is null ? "" : $"BROWSER={opener} ") + "claude setup-token");
        return Mint(dir, opener, minting);
    }

    /// <summary>"3 signed in, 1 already was, 1 failed, 1 with no email": the tally of a <see cref="SignInAll"/>.</summary>
    public static string Summary(IReadOnlyList<OwnLoginResult> results)
    {
        if (results.Count == 0) return "No accounts in the roster to sign in.";
        var parts = new List<string>();
        void Add(int n, string text) { if (n > 0) parts.Add($"{n} {text}"); }
        Add(results.Count(r => r.Outcome == OwnLoginOutcome.SignedIn), "signed in");
        Add(results.Count(r => r.Outcome == OwnLoginOutcome.AlreadySignedIn), "already had its own login");
        Add(results.Count(r => r.Outcome == OwnLoginOutcome.SignedInAsOther), "signed in as someone else (signed out again)");
        Add(results.Count(r => r.Outcome == OwnLoginOutcome.Failed), "failed");
        Add(results.Count(r => r.Outcome == OwnLoginOutcome.NoEmail), "with no email yet");
        return string.Join(", ", parts) + ".";
    }
}
