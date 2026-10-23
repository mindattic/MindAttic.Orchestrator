using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Vault.Credentials;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Long-lived logins (M.ORCH-T696): <c>claude setup-token</c>'s one-year token, kept sealed in the MindAttic Vault and
/// set as <c>CLAUDE_CODE_OAUTH_TOKEN</c> in a per-CLI login's tabs. Every store is a scratch one; no test runs claude.</summary>
public sealed class LoginTokensTests
{
    private const string Token = "sk-ant-oat01-AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abcdefXYZ";
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDirs temps = new();
    private string vault = "";
    private LoginTokens tokens = null!;
    private AccountDirs dirs = null!;
    private Func<AccountDir, string?, string> realRun = null!;
    private Func<AccountDir, string, string?> realStatus = null!;
    private Func<LoginBrowser.Browser?> realFind = null!;

    [SetUp]
    public void SetUp()
    {
        vault = temps.New();
        tokens = new LoginTokens(new TokenStore(vault));
        var root = temps.New();
        dirs = new AccountDirs(Path.Combine(root, ".claude-accounts"), Path.Combine(root, ".claude"), Path.Combine(root, ".claude.json"));
        realRun = LoginTokens.Run;
        realStatus = LoginTokens.StatusEmail;
        realFind = LoginBrowser.Find;
        LoginTokens.Run = (_, _) => throw new AssertionException("no test runs claude setup-token");
        LoginTokens.StatusEmail = (_, _) => null;
        LoginBrowser.Find = () => null;
    }

    [TearDown]
    public void TearDown()
    {
        LoginTokens.Run = realRun;
        LoginTokens.StatusEmail = realStatus;
        LoginBrowser.Find = realFind;
        temps.DeleteAll();
    }

    [Test]
    public void A_token_is_kept_in_the_vault_sealed_never_as_plaintext_and_opens_again_for_this_user()
    {
        var kept = tokens.Save("B@Example.com", Token, Now);
        Assert.That(kept.Email, Is.EqualTo("b@example.com"));
        Assert.That(kept.ExpiresUtc, Is.EqualTo(Now.AddDays(365)), "one year");
        var onDisk = File.ReadAllText(Path.Combine(vault, TokenStore.TokensJsonFile));
        Assert.That(onDisk, Does.Not.Contain(Token).And.Not.Contain("sk-ant-oat"), "never plaintext");

        var read = tokens.For("b@example.com");
        Assert.That(read, Is.Not.Null);
        Assert.That(read!.MintedUtc, Is.EqualTo(Now));
        Assert.That(LoginTokens.Reveal(read), Is.EqualTo(Token));
        Assert.That(tokens.For("c@example.com"), Is.Null);
        Assert.That(LoginTokens.Reveal(read with { Protected = Convert.ToBase64String(new byte[] { 1, 2, 3 }) }), Is.Null, "a damaged seal opens to nothing");

        Assert.That(tokens.Forget("b@example.com"), Is.True);
        Assert.That(tokens.For("b@example.com"), Is.Null);
    }

    [Test]
    public void Saves_from_many_threads_keep_every_login_s_token()
    {
        var emails = Enumerable.Range(0, 12).Select(i => $"u{i}@example.com").ToList();
        Parallel.ForEach(emails, new ParallelOptions { MaxDegreeOfParallelism = 12 },
            e => new LoginTokens(new TokenStore(vault)).Save(e, Token, Now));
        foreach (var e in emails) Assert.That(tokens.For(e), Is.Not.Null, e);
    }

    [Test]
    public void A_save_never_overwrites_a_tokens_file_it_could_not_read()
    {
        tokens.Save("a@example.com", Token, Now);
        var path = Path.Combine(vault, TokenStore.TokensJsonFile);
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => tokens.Save("b@example.com", Token, Now));
        File.WriteAllText(path, "{not json");
        Assert.Throws<IOException>(() => tokens.Save("b@example.com", Token, Now));
        Assert.That(File.ReadAllText(path), Is.EqualTo("{not json"), "left alone");
    }

    [Test]
    public void The_token_is_found_in_what_setup_token_printed_and_masked_when_said()
    {
        var output = $"\u001b[32m✓ Long-lived authentication token created successfully!\u001b[0m\r\n\r\nYour OAuth token (valid for 1 year):\r\n\r\n{Token}\r\n\r\nStore this token securely.";
        Assert.That(LoginTokens.Find(output), Is.EqualTo(Token));
        Assert.That(LoginTokens.Find("Login cancelled"), Is.Null);
        Assert.That(LoginTokens.Find(null), Is.Null);
        var masked = LoginTokens.Mask(output);
        Assert.That(masked, Does.Not.Contain(Token).And.Contain("sk-ant-oat01-A…fXYZ"));
        Assert.That(LoginTokens.ParseStatusEmail("""{"loggedIn":true,"email":"b@example.com"}"""), Is.EqualTo("b@example.com"));
        Assert.That(LoginTokens.ParseStatusEmail("""{"loggedIn":true,"authMethod":"oauth_token"}"""), Is.Null);
        Assert.That(LoginTokens.ParseStatusEmail("not json"), Is.Null);
    }

    [Test]
    public void A_token_wrapped_across_lines_is_joined_for_finding_and_masking()
    {
        var output = $"Your OAuth token:\r\n{Token[..40]}\r\n{Token[40..]}\r\n\r\nStore this token securely.";
        Assert.That(LoginTokens.Find(output), Is.EqualTo(Token));
        var masked = LoginTokens.Mask(output);
        Assert.That(masked, Does.Not.Contain(Token[40..]).And.Contain("sk-ant-oat01-A…fXYZ").And.Contain("Store this token securely."));
        Assert.That(LoginTokens.Find("sk-ant-oat01-AbCdEfGhIjKlMnOpQrStUvWx"), Is.Null, "a short fragment is no token");
    }

    [Test]
    public void Expiry_is_warned_thirty_days_ahead_and_an_expired_token_is_left_out_of_tabs()
    {
        tokens.Save("soon@example.com", Token, Now - TimeSpan.FromDays(340));
        tokens.Save("gone@example.com", Token, Now - TimeSpan.FromDays(366));
        tokens.Save("fresh@example.com", Token, Now - TimeSpan.FromDays(10));
        var warnings = tokens.Warnings(["soon@example.com", "gone@example.com", "fresh@example.com", "none@example.com", "SOON@example.com"], Now);
        Assert.That(warnings, Has.Count.EqualTo(2));
        Assert.That(warnings[0], Does.Contain("soon@example.com").And.Contain("25 days").And.Contain("--new-token"));
        Assert.That(warnings[1], Does.Contain("gone@example.com").And.Contain("expired"));

        Assert.That(tokens.NeedsMint("fresh@example.com", Now), Is.False);
        Assert.That(tokens.NeedsMint("soon@example.com", Now), Is.True, "within 30 days: minted again at the next sign-in");
        Assert.That(tokens.NeedsMint("gone@example.com", Now), Is.True);
        Assert.That(tokens.NeedsMint("none@example.com", Now), Is.True);

        Assert.That(tokens.TabEnv(dirs.For("gone@example.com").Directory, Now), Is.EqualTo("Remove-Item Env:CLAUDE_CODE_OAUTH_TOKEN -ErrorAction SilentlyContinue; "));
        Assert.That(tokens.TabEnv(dirs.For("soon@example.com").Directory, Now), Does.Contain("$env:CLAUDE_CODE_OAUTH_TOKEN ="), "still good until it expires");
    }

    [Test]
    public void A_tab_on_a_login_with_a_token_gets_it_beside_its_folder_and_nothing_that_outranks_it()
    {
        var b = dirs.For("b@example.com");
        Directory.CreateDirectory(b.Directory);
        tokens.Save(b.Email, Token, Now);
        var env = tokens.TabEnv(b.Directory, Now);
        Assert.That(env, Does.Not.Contain(Token), "only the sealed form is ever on a command line");
        Assert.That(env, Does.StartWith("Remove-Item Env:ANTHROPIC_API_KEY,Env:ANTHROPIC_AUTH_TOKEN"));
        Assert.That(tokens.TabEnv(null, Now), Is.EqualTo("Remove-Item Env:CLAUDE_CODE_OAUTH_TOKEN -ErrorAction SilentlyContinue; "), "the global login: never a stray token");
        Assert.That(tokens.TabEnv(dirs.For("c@example.com").Directory, Now), Does.StartWith("Remove-Item Env:CLAUDE_CODE_OAUTH_TOKEN"));

        // Run the tab's own command as Windows Terminal would: the Launcher (a stub) sees the folder and the unsealed token.
        var stub = Path.Combine(temps.New(), "launcher stub.cmd");
        File.WriteAllText(stub, "@echo %CLAUDE_CONFIG_DIR%^|%CLAUDE_CODE_OAUTH_TOKEN%^|%ANTHROPIC_API_KEY%^|%ANTHROPIC_AUTH_TOKEN%>\"%~dp0out.txt\"\r\n");
        var control = new SessionControl(new OrchestratorOptions { LauncherExe = stub })
        {
            Look = d => LoginLook.For(d ?? @"C:\nowhere\global", d is null ? "jordan@example.com" : "b@example.com"),
            TokenEnv = d => tokens.TabEnv(d, Now),
        };
        var args = control.StartArguments("Repo", b.Directory, "Claude", null, b.Directory).ToList();
        var encoded = args[args.IndexOf("-EncodedCommand") + 1];
        var script = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
        Assert.That(script, Does.StartWith($"$env:CLAUDE_CONFIG_DIR = '{b.Directory}'; Remove-Item Env:ANTHROPIC_API_KEY"));
        Assert.That(script, Does.Not.Contain(Token));

        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded }) psi.ArgumentList.Add(a);
        psi.Environment["ANTHROPIC_API_KEY"] = "sk-ant-api-inherited";
        psi.Environment["ANTHROPIC_AUTH_TOKEN"] = "gateway";
        psi.Environment["CLAUDE_CODE_OAUTH_TOKEN"] = "sk-ant-oat01-someone-elses";
        using (var p = Process.Start(psi)!) Assert.That(p.WaitForExit(60_000), Is.True);
        var seen = File.ReadAllText(Path.Combine(Path.GetDirectoryName(stub)!, "out.txt")).Trim();
        Assert.That(seen, Is.EqualTo($"{b.Directory}|{Token}||"),
            "the folder and its own token; the API key and gateway token that would outrank it are gone");

        // A pane on the global login clears an inherited token as well.
        var pane = control.PaneRequestFor("Repo", b.Directory, "Claude", null, null, "abc-123");
        var paneScript = Encoding.Unicode.GetString(Convert.FromBase64String(HostSessionScanner.SplitCommandLine(pane.CommandLine)[^1]));
        Assert.That(paneScript, Does.StartWith("Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction SilentlyContinue; Remove-Item Env:CLAUDE_CODE_OAUTH_TOKEN"));
    }

    [Test]
    public void Minting_keeps_the_folders_own_token_and_never_one_for_somebody_else()
    {
        var b = dirs.For("b@example.com");
        Directory.CreateDirectory(b.Directory);
        string? ranIn = null;
        LoginTokens.Run = (dir, opener) => { ranIn = dir.Directory; return $"Your OAuth token (valid for 1 year):\n{Token}\n"; };

        LoginTokens.StatusEmail = (_, token) => token == Token ? "other@example.com" : null;
        var wrong = tokens.MintFor(b, null, Now);
        Assert.That(wrong.Outcome, Is.EqualTo(MintOutcome.WrongAccount));
        Assert.That(wrong.Message, Does.Contain("other@example.com").And.Not.Contain(Token));
        Assert.That(tokens.For(b.Email), Is.Null, "a token for anybody else is never kept");
        Assert.That(ranIn, Is.EqualTo(b.Directory));

        LoginTokens.StatusEmail = (_, _) => null; // inference-only: Claude Code can't say who
        var minted = tokens.MintFor(b, null, Now);
        Assert.That(minted.Ok, Is.True, minted.Message);
        Assert.That(minted.Message, Does.Contain("/status").And.Contain("2027-10-23").And.Not.Contain(Token));
        Assert.That(LoginTokens.Reveal(tokens.For(b.Email)!), Is.EqualTo(Token));

        LoginTokens.StatusEmail = (_, _) => "B@example.com";
        Assert.That(tokens.MintFor(b, null, Now).Message, Does.Contain("confirmed as B@example.com"));

        // Its screen showed the token where the pipe didn't: the person pastes it.
        LoginTokens.Run = (_, _) => "";
        Assert.That(tokens.MintFor(b, null, Now).Outcome, Is.EqualTo(MintOutcome.NoToken));
        Assert.That(tokens.MintFor(b, null, Now, () => "  " + Token + "  ").Ok, Is.True);

        LoginTokens.Run = (_, _) => throw new System.ComponentModel.Win32Exception("claude not found");
        Assert.That(tokens.MintFor(b, null, Now).Outcome, Is.EqualTo(MintOutcome.Failed));
    }

    [Test]
    public void Signing_a_login_in_mints_its_token_once_and_again_only_when_asked()
    {
        var realLogin = AccountSwitch.LoginInto;
        var mints = new List<string>();
        LoginTokens.Run = (dir, _) => { mints.Add(dir.Email); return Token; };
        try
        {
            AccountSwitch.LoginInto = (email, _, configDir) =>
            {
                File.WriteAllText(Path.Combine(configDir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-" + email[0], emailAddress = email } }));
                return 0;
            };
            var minting = new TokenMinting(tokens);
            var result = OwnLogins.SignIn(dirs, "b@example.com", sso: false, minting: minting);
            Assert.That(result.Outcome, Is.EqualTo(OwnLoginOutcome.SignedIn));
            Assert.That(result.Token?.Ok, Is.True, result.Token?.Message);
            Assert.That(OwnLogins.SignIn(dirs, "b@example.com", sso: false).Token, Is.Null, "no minting asked for: none");

            // Sign in all: b is signed in with a good token (left alone); c is signed in without one (gets one).
            var c = dirs.For("c@example.com");
            dirs.Prepare(c);
            File.WriteAllText(c.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-c", emailAddress = c.Email } }));
            AccountRow[] rows = [new() { Account = "acct-b/org", Email = "b@example.com" }, new() { Account = "acct-c/org", Email = "c@example.com" }];
            var all = OwnLogins.SignInAll(dirs, rows, minting: minting);
            Assert.That(all.Select(r => r.Outcome), Is.EqualTo(new[] { OwnLoginOutcome.AlreadySignedIn, OwnLoginOutcome.AlreadySignedIn }));
            Assert.That(all[0].Token, Is.Null);
            Assert.That(all[1].Token?.Ok, Is.True);
            Assert.That(mints, Is.EqualTo(new[] { "b@example.com", "c@example.com" }));

            OwnLogins.SignInAll(dirs, rows, minting: minting with { Renew = true });
            Assert.That(mints, Has.Count.EqualTo(4), "a new token asked for: minted again even though one is kept");
        }
        finally { AccountSwitch.LoginInto = realLogin; }
    }

    [Test]
    public void Login_account_on_a_seat_already_signed_in_as_itself_skips_the_browser_login()
    {
        var realLogin = AccountSwitch.LoginInto;
        var realOverride = LoginCommand.TokensOverride;
        var mints = new List<string>();
        LoginTokens.Run = (dir, _) => { mints.Add(dir.Email); return Token; };
        var logins = 0;
        AccountSwitch.LoginInto = (_, _, _) => { logins++; return 0; };
        LoginCommand.TokensOverride = () => tokens;
        try
        {
            var b = dirs.For("b@example.com");
            dirs.Prepare(b);
            File.WriteAllText(b.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-b", emailAddress = b.Email } }));
            Assert.That(LoginCommand.SignInOwn(dirs, "b@example.com", sso: false), Is.EqualTo(0));
            Assert.That(logins, Is.EqualTo(0), "already signed in as itself: no browser login");
            Assert.That(mints, Is.EqualTo(new[] { "b@example.com" }), "no good token yet: one is minted");
            Assert.That(LoginCommand.SignInOwn(dirs, "b@example.com", sso: false), Is.EqualTo(0));
            Assert.That(logins, Is.EqualTo(0));
            Assert.That(mints, Has.Count.EqualTo(1), "a good token is kept");
        }
        finally
        {
            AccountSwitch.LoginInto = realLogin;
            LoginCommand.TokensOverride = realOverride;
        }
    }
}
