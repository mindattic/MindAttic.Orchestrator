using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Per-CLI Claude logins (M.ORCH-T559): each account's own config folder, the tabs started on it, its statusline,
/// its own pace, and the commands that sign it in and plan on it. Every folder is a scratch one, never ~/.claude.</summary>
public sealed class AccountDirsTests
{
    private readonly TempDirs temps = new();
    private string dir = "";
    private string ClaudeDir => Path.Combine(dir, ".claude");
    private string HomeConfig => Path.Combine(dir, ".claude.json");
    private AccountDirs dirs = null!;

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        Directory.CreateDirectory(Path.Combine(ClaudeDir, "commands"));
        File.WriteAllText(Path.Combine(ClaudeDir, "commands", "do.md"), "do it");
        Directory.CreateDirectory(Path.Combine(ClaudeDir, "skills"));
        File.WriteAllText(Path.Combine(ClaudeDir, "CLAUDE.md"), "# global instructions");
        File.WriteAllText(Path.Combine(ClaudeDir, "settings.json"), """
            {
              "env": { "A": "1" },
              "hooks": {
                "UserPromptSubmit": [ { "matcher": "", "hooks": [
                  { "type": "command", "command": "python C:/Users/x/.claude/rl-check.py" },
                  { "type": "command", "command": "python \"C:/Users/x/.claude/context-gate.py\"" } ] } ],
                "PreToolUse": [ { "matcher": "", "hooks": [ { "type": "command", "command": "python \"C:/Users/x/.claude/pace-gate.py\"" } ] } ]
              },
              "statusLine": { "type": "command", "command": "powershell -File \"D:/shared/statusline.ps1\"", "refreshInterval": 60 }
            }
            """);
        File.WriteAllText(HomeConfig, """{"oauthAccount":{"accountUuid":"global"},"userID":"u1","hasCompletedOnboarding":true,"projects":{"D:/Projects/A":{"hasTrustDialogAccepted":true}}}""");
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), ClaudeDir, HomeConfig);
    }

    private Func<LoginBrowser.Browser?> realFind = null!;

    [SetUp]
    public void NoRealBrowser()
    {
        realFind = LoginBrowser.Find;
        // No test mints a real long-lived token (M.ORCH-T696): LoginTokensTests covers minting, with stubs.
        LoginCommand.TokensOverride = () => null;
        LoginBrowser.Find = () => new LoginBrowser.Browser("Edge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", "--inprivate");
    }

    [TearDown]
    public void TearDown()
    {
        LoginBrowser.Find = realFind;
        LoginCommand.DirsOverride = null;
        LoginCommand.TokensOverride = () => LoginTokens.Default;
        temps.DeleteAll();
    }

    [Test]
    public void An_accounts_sign_in_opens_in_a_private_window_of_its_own_browser_profile()
    {
        var b = dirs.For("b@example.com");
        var opener = LoginBrowser.Prepare(b, out var note);
        Assert.That(note, Is.Null);
        Assert.That(opener, Is.EqualTo(b.LoginOpenerPath));
        var script = File.ReadAllText(opener!);
        Assert.That(script, Does.Contain("start \"\" \"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe\""));
        Assert.That(script, Does.Contain($"--user-data-dir=\"{b.BrowserProfileDirectory}\"").And.Contain("--inprivate %1"),
            "its own profile, a private window, and the sign-in URL claude passes (already quoted)");
        Assert.That(LoginBrowser.Script(new("Chrome", "chrome.exe", "--incognito"), b), Does.Contain("--incognito %1"));

        LoginBrowser.Find = () => null;
        Assert.That(LoginBrowser.Prepare(b, out note), Is.Null);
        Assert.That(note, Does.Contain("default browser").And.Contain("b@example.com"));
        Assert.That(File.Exists(b.LoginOpenerPath), Is.False, "an opener for a browser that's gone is removed");
    }

    [Test]
    public void Login_with_account_opens_through_the_accounts_opener_and_a_wrong_account_is_signed_out_again()
    {
        var real = AccountSwitch.LoginInto;
        var previous = AnsiConsole.Console;
        var output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No });
        AnsiConsole.Profile.Width = 600;
        LoginCommand.DirsOverride = () => dirs;
        try
        {
            var b = dirs.For("b@example.com");
            AccountSwitch.LoginInto = (_, _, configDir) =>
            {
                Assert.That(File.Exists(Path.Combine(configDir, "open-login.cmd")), Is.True, "the opener is there before claude runs");
                File.WriteAllText(Path.Combine(configDir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-x", emailAddress = "other@example.com" }, projects = new { } }));
                File.WriteAllText(Path.Combine(configDir, ".credentials.json"), "{}");
                return 0;
            };
            Assert.That(CliApp.Create().Run(["login", "--account", "b@example.com"]), Is.EqualTo(1));
            Assert.That(b.SignedIn, Is.False);
            Assert.That(File.Exists(b.CredentialsPath), Is.False, "its token is gone");
            Assert.That(File.ReadAllText(b.ConfigPath), Does.Contain("projects").And.Not.Contain("oauthAccount"), "the rest of its config is kept");
            Assert.That(output.ToString(), Does.Contain("private browser window").And.Contain($"BROWSER={b.LoginOpenerPath}")
                .And.Contain("Signed in as other@example.com, not b@example.com"));

            var result = OwnLogins.SignIn(dirs, "b@example.com", sso: false);
            Assert.That(result.Outcome, Is.EqualTo(OwnLoginOutcome.SignedInAsOther));
            Assert.That(result.Ok, Is.False);
            Assert.That(OwnLogins.Summary([result]), Is.EqualTo("1 signed in as someone else (signed out again)."));
        }
        finally
        {
            AccountSwitch.LoginInto = real;
            AnsiConsole.Console = previous;
        }
    }

    [Test]
    public void The_global_logins_tab_starts_on_the_global_login_and_offers_auto_only_while_its_on()
    {
        // M.ORCH-T613: every other login has a dashboard tab of its own to start from, so none is listed here.
        Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.GlobalTabLogins(auto: true), Is.EqualTo(new[] { "Global login", "Auto" }));
        Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.GlobalTabLogins(auto: false), Is.EqualTo(new[] { "Global login" }));
    }

    private static void SignIn(AccountDir account, string uuid = "acct-b") =>
        File.WriteAllText(account.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = uuid, emailAddress = account.Email } }));

    [Test]
    public void An_account_folder_is_named_by_its_email_and_found_again_from_its_config_dir()
    {
        Assert.That(AccountDirs.Normalize(" B@Example.com "), Is.EqualTo("b@example.com"));
        Assert.That(AccountDirs.Normalize("nobody"), Is.Null);
        Assert.That(AccountDirs.Normalize("a@b@c"), Is.Null);
        Assert.That(AccountDirs.Normalize("a b@c.com"), Is.Null);
        Assert.Throws<ArgumentException>(() => dirs.For("not-an-email"));

        var b = dirs.For("B@Example.com");
        Assert.That(b.Directory, Is.EqualTo(Path.Combine(dir, ".claude-accounts", "b@example.com")));
        Assert.That(dirs.Of(b.Directory + Path.DirectorySeparatorChar)?.Email, Is.EqualTo("b@example.com"));
        Assert.That(dirs.Of(ClaudeDir), Is.Null, "the global login's folder isn't a per-CLI login");
        Assert.That(dirs.Of(null), Is.Null);
        Assert.That(dirs.Of(Path.Combine(dir, "elsewhere", "b@example.com")), Is.Null, "only folders under the accounts root");
    }

    [Test]
    public void A_prepared_folder_shares_your_claude_home_but_has_its_own_statusline_and_no_global_sign_in()
    {
        var b = dirs.For("b@example.com");
        dirs.Prepare(b);

        foreach (var shared in new[] { "commands", "skills", "projects" })
            Assert.That(new DirectoryInfo(Path.Combine(b.Directory, shared)).Attributes.HasFlag(FileAttributes.ReparsePoint), Is.True, $"{shared} is linked, not copied");
        Assert.That(File.ReadAllText(Path.Combine(b.Directory, "commands", "do.md")), Is.EqualTo("do it"), "your commands work there");
        Assert.That(Directory.Exists(Path.Combine(ClaudeDir, "projects")), Is.True, "the shared transcripts folder is made when missing");
        Assert.That(Directory.Exists(Path.Combine(b.Directory, "agents")), Is.False, "nothing to share, so no link");
        Assert.That(File.ReadAllText(Path.Combine(b.Directory, "CLAUDE.md")), Is.EqualTo("# global instructions"));

        var settings = JsonNode.Parse(File.ReadAllText(b.SettingsPath))!.AsObject();
        Assert.That(settings["statusLine"]!["command"]!.GetValue<string>(), Does.Contain(b.StatuslinePath.Replace('\\', '/')));
        Assert.That(settings["statusLine"]!["refreshInterval"]!.GetValue<int>(), Is.EqualTo(60));
        var text = settings.ToJsonString();
        Assert.That(text, Does.Not.Contain("rl-check.py"), "the global login's pace notes aren't this account's");
        Assert.That(text, Does.Contain("pace-gate.py").And.Contain("context-gate.py"), "the gates still run");
        Assert.That(settings["env"]!["A"]!.GetValue<string>(), Is.EqualTo("1"));
        Assert.That(File.ReadAllText(b.StatuslinePath), Does.Contain("D:/shared/statusline.ps1"), "the shared statusline still draws the line");

        var config = JsonNode.Parse(File.ReadAllText(b.ConfigPath))!.AsObject();
        Assert.That(config.ContainsKey("oauthAccount"), Is.False, "it signs in on its own");
        Assert.That(config.ContainsKey("userID"), Is.False);
        Assert.That(config["projects"]!["D:/Projects/A"]!["hasTrustDialogAccepted"]!.GetValue<bool>(), Is.True, "your trusted folders carry over");
        Assert.That(b.SignedIn, Is.False);
        Assert.That(dirs.All().Select(a => a.Email), Is.EqualTo(new[] { "b@example.com" }));
        Assert.That(dirs.SignedIn(), Is.Empty);

        SignIn(b);
        Assert.That(dirs.Prepare(b), Is.Empty, "again: nothing to say");
        Assert.That(File.ReadAllText(b.ConfigPath), Does.Contain("acct-b"), "its own sign-in is never replaced");
        Assert.That(dirs.SignedIn().Single().Account, Is.EqualTo("acct-b"));
        Assert.That(b.SignedInEmail, Is.EqualTo("b@example.com"));
    }

    [Test]
    public void A_tab_on_its_own_login_starts_the_launcher_with_claude_config_dir_set()
    {
        var b = dirs.For("b@example.com");
        var stub = Path.Combine(dir, "launcher stub.cmd");
        File.WriteAllText(stub, "@echo %CLAUDE_CONFIG_DIR%^|%*>\"%~dp0out.txt\"\r\n");
        var control = new SessionControl(new OrchestratorOptions { LauncherExe = stub }) { Look = d => LoginLook.For(d ?? Path.Combine(dir, "global"), d is null ? "jordan@example.com" : "b@example.com") };
        var args = control.StartArguments("Repo A", dir, "Claude", "it's \"quoted\"; with a semicolon", b.Directory).ToList();

        Assert.That(args[..5], Is.EqualTo(new[] { "-w", "b", "new-tab", "--title", "B: Repo A [Claude]" }), "its own window, badged");
        Assert.That(args[5], Is.EqualTo("--tabColor"));
        Assert.That(args[7..9], Is.EqualTo(new[] { "-d", dir }));
        Assert.That(args, Does.Contain("powershell.exe"));
        var encoded = args[args.IndexOf("-EncodedCommand") + 1];
        var script = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
        Assert.That(script, Does.StartWith($"$env:CLAUDE_CONFIG_DIR = '{b.Directory}';"));

        // Run it as the tab would: the Launcher gets the folder in its environment and its usual arguments.
        var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded }) psi.ArgumentList.Add(a);
        psi.Environment.Remove("CLAUDE_CONFIG_DIR");
        using (var p = Process.Start(psi)!) Assert.That(p.WaitForExit(60_000), Is.True);
        var seen = File.ReadAllText(Path.Combine(dir, "out.txt")).Trim();
        Assert.That(seen, Does.StartWith(b.Directory + "|host --path"));
        Assert.That(seen, Does.Contain("--provider Claude").And.Contain("--prompt").And.Contain("it's 'quoted'; with a semicolon"));

        Assert.That(control.Start("Repo A", dir, "Codex", null, b.Directory), Does.Contain("Claude login"), "only Claude has a login to pick");
        var global = control.StartArguments("Repo A", dir, "Claude", null).ToList();
        Assert.That(global, Does.Contain("host").And.Not.Contain("powershell.exe"), "without one, the tab starts the Launcher directly");
        Assert.That(global[..5], Is.EqualTo(new[] { "-w", "jordan", "new-tab", "--title", "JO: Repo A [Claude]" }), "the global login's own window");
        Assert.That(control.StartArguments("Repo A", dir, "Codex", null).ToList()[..5], Is.EqualTo(new[] { "-w", "0", "new-tab", "--title", "Repo A [Codex]" }),
            "a provider with no login opens as it always did");
    }

    [Test]
    public void A_tabs_title_drops_the_repos_leading_prefix()
    {
        // M.ORCH-T632: "JR: EZ [Claude]", not "[JR] GTFO.EZ [Claude]"; the provider tag stays.
        var control = new SessionControl(new OrchestratorOptions { LauncherExe = "L.exe" }) { Look = _ => LoginLook.For(Path.Combine(dir, "global"), "jordan@example.com") };
        Assert.That(control.StartArguments("GTFO.EZ", dir, "Claude", null).ToList()[4], Is.EqualTo("JO: EZ [Claude]"));
        Assert.That(control.StartArguments("MindAttic.Tickets", dir, "Claude", null).ToList()[4], Is.EqualTo("JO: Tickets [Claude]"));
        Assert.That(control.StartArguments("Unity.Eve", dir, "Codex", null).ToList()[4], Is.EqualTo("Eve [Codex]"));
        Assert.That(control.StartArguments("UnityEve", dir, "Codex", null).ToList()[4], Is.EqualTo("UnityEve [Codex]"), "only the exact prefix, dot and all");
        Assert.That(control.StartArguments("REA.Bloodlines", dir, "Codex", null).ToList()[4], Is.EqualTo("REA.Bloodlines [Codex]"));
        Assert.That(SessionControl.ShortName("GTFO."), Is.EqualTo("GTFO."), "a name that is only the prefix stays whole");
    }

    [Test]
    public void A_logins_look_comes_from_its_folder_else_its_email()
    {
        var b = dirs.For("ryandebraal@example.com");
        Directory.CreateDirectory(b.Directory);
        var plain = LoginLook.For(b.Directory, b.Email);
        Assert.That((plain.Window, plain.Initials), Is.EqualTo(("ryandebraal", "RY")));
        Assert.That(plain.TabColor, Is.EqualTo(LoginLook.For(b.Directory, b.Email).TabColor), "the same login keeps its color");

        File.WriteAllText(Path.Combine(b.Directory, LoginLook.FileName), """{ "alias": "Ryan", "initials": "rd", "tabColor": "#123abc" }""");
        Assert.That(LoginLook.For(b.Directory, b.Email), Is.EqualTo(new LoginLook("Ryan", "RD", "#123ABC")));

        File.WriteAllText(Path.Combine(b.Directory, LoginLook.FileName), """{ "alias": "Amy Reed" }""");
        Assert.That(LoginLook.For(b.Directory, b.Email).Initials, Is.EqualTo("AR"), "two words give their first letters");
        Assert.That(LoginLook.For(b.Directory, b.Email).Window, Is.EqualTo("Amy-Reed"));

        File.WriteAllText(Path.Combine(b.Directory, LoginLook.FileName), "not json");
        Assert.That(LoginLook.For(b.Directory, b.Email).Initials, Is.EqualTo("RY"), "a broken file is ignored");

        File.WriteAllText(Path.Combine(b.Directory, LoginLook.FileName), """{ "alias": "Jordan" }""");
        Assert.That(LoginLook.For(b.Directory, b.Email).Initials, Is.EqualTo("JO"), "one word gives its own first two letters");

        Assert.That(LoginLook.WindowName("42"), Is.EqualTo("claude-42"), "a number would be a window id");
        Assert.That(LoginLook.WindowName("new"), Is.EqualTo("claude-new"));
    }

    [Test]
    public void A_logins_name_belongs_to_its_email_not_its_folder()
    {
        // The global login's folder still holds the name of whoever was signed in to it before a /login as someone else.
        File.WriteAllText(Path.Combine(ClaudeDir, LoginLook.FileName), """{ "alias": "Jordan", "initials": "JR" }""");
        var look = LoginLook.For(ClaudeDir, "ohleo199@example.com");
        Assert.That((look.Alias, look.Initials), Is.EqualTo(("ohleo199", "OH")), "a file with no email isn't trusted in the global folder");
        Assert.That(LoginLook.For(ClaudeDir, null).Alias, Is.EqualTo("claude"), "nor when nobody's known to be signed in");

        File.WriteAllText(Path.Combine(ClaudeDir, LoginLook.FileName), """{ "email": "PDF@example.com", "alias": "Jordan", "initials": "JR" }""");
        Assert.That(LoginLook.For(ClaudeDir, "pdf@example.com").Alias, Is.EqualTo("Jordan"), "the email it was given for");
        Assert.That(LoginLook.For(ClaudeDir, "ohleo199@example.com").Alias, Is.EqualTo("ohleo199"), "anyone else gets the default");

        // Naming the new sign-in replaces the other email's look, and keeps anything else the file holds.
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(ClaudeDir, LoginLook.FileName)))!.AsObject();
        json["launcher"] = "kept";
        File.WriteAllText(Path.Combine(ClaudeDir, LoginLook.FileName), json.ToJsonString());
        Assert.That(LoginLook.SetAlias(ClaudeDir, "ohleo199@example.com", "Ryan"), Is.EqualTo("Ryan"));
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(ClaudeDir, LoginLook.FileName)))!.AsObject();
        Assert.That((string?)saved["email"], Is.EqualTo("ohleo199@example.com"));
        Assert.That((string?)saved["alias"], Is.EqualTo("Ryan"));
        Assert.That(saved.ContainsKey("initials"), Is.False, "JR was Jordan's");
        Assert.That((string?)saved["launcher"], Is.EqualTo("kept"));
        Assert.That(LoginLook.For(ClaudeDir, "ohleo199@example.com").Initials, Is.EqualTo("RY"));

        Assert.That(LoginLook.SetAlias(ClaudeDir, "ohleo199@example.com", "  "), Is.EqualTo("ohleo199"), "a blank name goes back to the default");

        // M.ORCH-T633: initials given are kept even as the name changes; blank goes back to the name's.
        LoginLook.SetAlias(ClaudeDir, "ohleo199@example.com", "Ryan", "rdb");
        Assert.That(LoginLook.For(ClaudeDir, "ohleo199@example.com").Initials, Is.EqualTo("RDB"));
        LoginLook.SetAlias(ClaudeDir, "ohleo199@example.com", "Ryan D", "rdb");
        Assert.That(LoginLook.For(ClaudeDir, "ohleo199@example.com").Initials, Is.EqualTo("RDB"));
        LoginLook.SetAlias(ClaudeDir, "ohleo199@example.com", "Ryan D", " ");
        Assert.That(LoginLook.For(ClaudeDir, "ohleo199@example.com").Initials, Is.EqualTo("RD"));
    }

    [Test]
    public void Naming_an_email_names_each_folder_that_is_its_login()
    {
        var b = dirs.For("b@example.com");
        Directory.CreateDirectory(b.Directory);
        File.WriteAllText(HomeConfig, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "g", emailAddress = "b@example.com" } }));

        var written = LoginLook.SetAlias(dirs, "B@example.com", "Bee");
        Assert.That(written, Is.EquivalentTo(new[] { b.Directory, ClaudeDir }), "its per-CLI login and the global one signed in as it");
        Assert.That(LoginLook.For(b.Directory, b.Email).Alias, Is.EqualTo("Bee"));
        Assert.That(LoginLook.For(ClaudeDir, "b@example.com").Alias, Is.EqualTo("Bee"));

        Assert.That(LoginLook.SetAlias(dirs, "nobody@example.com", "X"), Is.Empty, "an email with no login here names nothing");
        Assert.That(File.Exists(Path.Combine(dirs.For("nobody@example.com").Directory, LoginLook.FileName)), Is.False);

        // The cards show the name, and the email for the tooltip.
        var card = LoginBoard.Own(b, DateTime.UtcNow);
        Assert.That((card.Alias, card.AliasEmail), Is.EqualTo(("Bee", "b@example.com")));
        var global = LoginBoard.Global(ClaudeDir, new AccountRow { Account = "g", Email = "b@example.com" }, true, Path.Combine(ClaudeDir, "rl-status.json"), DateTime.UtcNow);
        Assert.That((global.Alias, global.AliasEmail), Is.EqualTo(("Bee", "b@example.com")));
    }

    [Test]
    public void The_accounts_statusline_keeps_its_meter_in_its_own_folder_and_never_hands_it_to_the_shared_one()
    {
        var b = dirs.For("b@example.com");
        var reset = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        Directory.CreateDirectory(b.Directory);
        File.WriteAllText(b.StatuslinePath, AccountDirs.StatuslineScript(b, "more"));
        string Draw(double percent)
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
            foreach (var a in new[] { "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", b.StatuslinePath }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.StandardInput.Write(JsonSerializer.Serialize(new { session_id = "s1", rate_limits = new { five_hour = new { used_percentage = percent, resets_at = reset } } }));
            p.StandardInput.Close();
            var output = p.StandardOutput.ReadToEnd();
            Assert.That(p.WaitForExit(60_000), Is.True);
            return output;
        }

        var line = Draw(42);
        Assert.That(line, Does.Contain("s1"), "the shared statusline got the rest of the status");
        Assert.That(line, Does.Not.Contain("rate_limits"), "but not the account's meter, which it would write as the global login's");
        Assert.That(line, Does.Contain("[b]"), "the line names the account, by its email's part before the @ until it has a name");
        Assert.That(ReportedUsage.Load(DateTime.UtcNow, b.UsagePath)?.FiveHour?.Percent, Is.EqualTo(42));
        Draw(30);
        Assert.That(ReportedUsage.Load(DateTime.UtcNow, b.UsagePath)?.FiveHour?.Percent, Is.EqualTo(42), "an idle tab's lower reading never lowers it");

        LoginLook.SetAlias(b.Directory, b.Email, "Bee Dee");
        Assert.That(Draw(50), Does.Contain("[Bee Dee]"), "a name shows at once, read from the login's own folder");
        File.WriteAllText(Path.Combine(b.Directory, LoginLook.FileName), """{ "email": "other@example.com", "alias": "Other" }""");
        Assert.That(Draw(50), Does.Contain("[b]"), "another email's name isn't this login's");
    }

    [Test]
    public void Each_account_is_paced_on_its_own_meter_sessions_and_subagents()
    {
        var b = dirs.For("b@example.com");
        dirs.Prepare(b);
        SignIn(b);
        var now = DateTime.UtcNow;
        File.WriteAllText(b.UsagePath, JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = 60, resets_at = new DateTimeOffset(now.AddHours(2)).ToUnixTimeSeconds() } },
            at = now.AddSeconds(-5).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        Directory.CreateDirectory(Path.Combine(b.MarksDirectory, "subagents"));
        foreach (var m in new[] { "s1", "s2" }) File.WriteAllText(Path.Combine(b.MarksDirectory, "subagents", m), "");

        static TranscriptSession Session(string id, long fresh, DateTime at) =>
            new(id, @"D:\W\A", at, default, null) { Events = [new UsageEvent(at, new TokenUsage(fresh, 0, 0, 0))] };
        var sessions = new[] { Session("own", 1_000, now.AddMinutes(-2)), Session("global", 900_000, now.AddMinutes(-1)) };
        var pacer = new AccountPacer { Dirs = dirs, Probe = false, ProjectsDir = Path.Combine(dir, "no-projects") };
        var paces = pacer.Tick(sessions, new Dictionary<string, string> { ["own"] = b.Directory }, new OrchestratorSettings(), now, now.Date, null, write: true);

        var pace = paces.Single();
        Assert.That(pace.Email, Is.EqualTo("b@example.com"));
        Assert.That(pace.Account, Is.EqualTo("acct-b"));
        Assert.That(pace.Sessions, Is.EqualTo(1), "only the session on this login");
        Assert.That(pace.Budget.Today.Fresh, Is.EqualTo(1_000), "the global login's tokens aren't this account's");
        Assert.That(pace.Budget.BlockPercent, Is.InRange(60, 62), "from its own meter");
        Assert.That(pace.Pace.Subagents, Is.EqualTo(2), "its own subagents only");
        Assert.That(File.Exists(b.StatusPath), Is.True, "written for its CLIs' hooks");
        Assert.That(AccountPacer.LoadSamples(b), Has.Count.EqualTo(1), "its reading is kept to size its limit and grade its windows");

        File.Delete(b.ConfigPath);
        Assert.That(pacer.Tick(sessions, new Dictionary<string, string>(), new OrchestratorSettings(), now, now.Date, null, write: false), Is.Empty,
            "an account signed out of its folder isn't paced");
    }

    [Test]
    public void Planning_from_a_cli_on_its_own_login_uses_its_accounts_pace()
    {
        var now = DateTime.UtcNow;
        var global = new Pace(PaceMode.Open, 4, 1, 1, null, null, null, "global") { SubagentAllowance = 8 };
        var own = new Pace(PaceMode.Open, 4, 1, 1, null, null, null, "own") { SubagentAllowance = 2 };
        var budget = BudgetCalculator.Compute([], new OrchestratorSettings(), now, now.Date);
        var snapshot = new Snapshot(now, [], []) { Pace = global, Budget = budget, Accounts = [new AccountPace("b@example.com", "x", "acct-b", budget, own, null, 1)] };

        Assert.That(Ctx.OnOwnLogin(snapshot, null).Pace, Is.SameAs(global), "the global login plans as before");
        var mine = Ctx.OnOwnLogin(snapshot, dirs.For("B@example.com"));
        Assert.That(mine.Pace, Is.SameAs(own));
        Assert.That(mine.Account, Is.EqualTo("acct-b"));
        Assert.That(Ctx.OnOwnLogin(snapshot, dirs.For("c@example.com")).Pace, Is.Null, "an account not paced yet never plans on the global login's room");
    }

    [Test]
    public void Login_with_account_signs_in_that_accounts_own_folder_and_leaves_the_global_login()
    {
        var calls = new List<(string Email, bool Sso, string Dir)>();
        var real = AccountSwitch.LoginInto;
        var previous = AnsiConsole.Console;
        var output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No });
        AnsiConsole.Profile.Width = 600;
        LoginCommand.DirsOverride = () => dirs;
        try
        {
            AccountSwitch.LoginInto = (email, sso, configDir) =>
            {
                calls.Add((email, sso, configDir));
                File.WriteAllText(Path.Combine(configDir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-b", emailAddress = email } }));
                return 0;
            };
            Assert.That(CliApp.Create().Run(["login", "--account", "B@Example.com", "--sso"]), Is.Zero);
            var b = dirs.For("b@example.com");
            Assert.That(calls.Single(), Is.EqualTo(("b@example.com", true, b.Directory)));
            Assert.That(b.SignedIn, Is.True);
            Assert.That(File.ReadAllText(HomeConfig), Does.Contain("\"global\""), "the global login is left as it is");
            Assert.That(output.ToString(), Does.Contain("orchestrator start <repo> --account b@example.com"));

            AccountSwitch.LoginInto = (_, _, _) => 0; // finished, but nobody signed in
            Assert.That(CliApp.Create().Run(["login", "--account", "c@example.com"]), Is.EqualTo(1));
            Assert.That(CliApp.Create().Run(["login", "--account", "not-an-email"]), Is.EqualTo(1));
            Assert.That(CliApp.Create().Run(["login", "b", "--account", "b@example.com"]), Is.EqualTo(1), "one login at a time");
        }
        finally
        {
            AccountSwitch.LoginInto = real;
            AnsiConsole.Console = previous;
        }
    }

    [Test]
    public void Login_refuses_new_token_where_it_cannot_apply_and_a_failed_mint_is_a_failure()
    {
        var previous = AnsiConsole.Console;
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(new StringWriter()), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No });
        try
        {
            Assert.That(CliApp.Create().Run(["login", "--new-token"]), Is.EqualTo(1));
            Assert.That(CliApp.Create().Run(["login", "b", "--new-token"]), Is.EqualTo(1));
            var ok = new OwnLoginResult("b", "b@example.com", "d", OwnLoginOutcome.SignedIn, "m", []);
            Assert.That(LoginCommand.MintFailed(ok), Is.False, "no mint tried");
            Assert.That(LoginCommand.MintFailed(ok with { Token = new MintResult(MintOutcome.Minted, "m") }), Is.False);
            foreach (var outcome in new[] { MintOutcome.Failed, MintOutcome.NoToken, MintOutcome.WrongAccount })
                Assert.That(LoginCommand.MintFailed(ok with { Token = new MintResult(outcome, "m") }), Is.True, outcome.ToString());
        }
        finally { AnsiConsole.Console = previous; }
    }

    [Test]
    public void Sign_in_all_gives_every_roster_account_without_its_own_login_one_in_turn()
    {
        var calls = new List<(string Email, bool Sso, string Dir)>();
        var real = AccountSwitch.LoginInto;
        var previous = AnsiConsole.Console;
        var output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No });
        AnsiConsole.Profile.Width = 600;
        var c = dirs.For("c@example.com");
        Directory.CreateDirectory(c.Directory);
        SignIn(c, "acct-c");
        AccountRow[] rows =
        [
            new() { Account = "acct-b/org", Email = "B@Example.com", Plan = "Team · Max 5x" },
            new() { Account = "acct-b/other", Email = "b@example.com", Plan = "Team" }, // the same email in another org: once
            new() { Account = "acct-c/org", Email = "c@example.com", Plan = "Team" },   // already has its own login
            new() { Account = "acct-d-uuid/org" },                                      // no email yet
            new() { Account = "acct-e/org", Email = "e@example.com", Plan = "Enterprise" },
            new() { Account = "acct-f/org", Email = "f@example.com", Plan = "Team" },
        ];
        try
        {
            AccountSwitch.LoginInto = (email, sso, configDir) =>
            {
                calls.Add((email, sso, configDir));
                if (email == "f@example.com") return 1; // the user closed the browser
                File.WriteAllText(Path.Combine(configDir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-" + email[0], emailAddress = email } }));
                return 0;
            };
            var results = OwnLogins.SignInAll(dirs, rows);
            Assert.That(calls, Is.EqualTo(new[]
            {
                ("b@example.com", false, dirs.For("b@example.com").Directory),
                ("e@example.com", true, dirs.For("e@example.com").Directory),
                ("f@example.com", false, dirs.For("f@example.com").Directory),
            }), "one after another, each in its own folder, SSO for the Enterprise org");
            Assert.That(results.Select(r => r.Outcome), Is.EqualTo(new[]
            {
                OwnLoginOutcome.SignedIn, OwnLoginOutcome.AlreadySignedIn, OwnLoginOutcome.NoEmail, OwnLoginOutcome.SignedIn, OwnLoginOutcome.Failed,
            }));
            Assert.That(dirs.For("b@example.com").SignedIn && dirs.For("e@example.com").SignedIn, Is.True);
            Assert.That(File.ReadAllText(HomeConfig), Does.Contain("\"global\""), "the global login is left as it is");
            Assert.That(OwnLogins.Summary(results), Is.EqualTo("2 signed in, 1 already had its own login, 1 failed, 1 with no email yet."));

            calls.Clear();
            Assert.That(LoginCommand.SignInAll(dirs, rows), Is.EqualTo(1), "f still fails");
            Assert.That(calls.Select(x => x.Email), Is.EqualTo(new[] { "f@example.com" }), "only the one still without a login");
            Assert.That(output.ToString(), Does.Contain("acct-d-u").And.Contain("no email yet").And.Contain("f@example.com isn't signed in"));
            Assert.That(CliApp.Create().Run(["login", "--all", "--account", "b@example.com"]), Is.EqualTo(1), "--all goes on its own");
        }
        finally
        {
            AccountSwitch.LoginInto = real;
            AnsiConsole.Console = previous;
        }
    }

    [Test]
    public void Start_on_an_account_without_its_own_login_yet_signs_it_in_first()
    {
        var real = AccountSwitch.LoginInto;
        var previous = AnsiConsole.Console;
        var output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Interactive = InteractionSupport.No });
        AnsiConsole.Profile.Width = 600;
        try
        {
            AccountSwitch.LoginInto = (_, _, _) => 0; // finished, but nobody signed in
            Assert.That(StartCommand.OwnLogin(dirs, "b@example.com", "Claude"), Is.Null);
            Assert.That(output.ToString(), Does.Contain("No tab opened"));
            Assert.That(StartCommand.OwnLogin(dirs, "b@example.com", "Codex"), Is.Null);

            AccountSwitch.LoginInto = (email, _, configDir) =>
            {
                File.WriteAllText(Path.Combine(configDir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-x", emailAddress = "someone-else@example.com" } }));
                return 0;
            };
            Assert.That(StartCommand.OwnLogin(dirs, "c@example.com", "Claude"), Is.Null, "signed in as another account: no tab");
            Assert.That(dirs.For("c@example.com").SignedIn, Is.False, "and the folder is signed out again (M.ORCH-T610)");
            Assert.That(output.ToString(), Does.Contain("Signed in as someone-else@example.com, not c@example.com: signed c@example.com's login out again"));

            var calls = 0;
            AccountSwitch.LoginInto = (email, _, configDir) =>
            {
                calls++;
                File.WriteAllText(Path.Combine(configDir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-b", emailAddress = email } }));
                return 0;
            };
            var b = dirs.For("b@example.com");
            Assert.That(StartCommand.OwnLogin(dirs, "B@example.com", "Claude")?.Directory, Is.EqualTo(b.Directory));
            Assert.That(StartCommand.OwnLogin(dirs, "b@example.com", "Claude")?.Directory, Is.EqualTo(b.Directory));
            Assert.That(calls, Is.EqualTo(1), "signed in once, then reused");
            Assert.That(File.Exists(b.SettingsPath), Is.True, "brought up to date as the tab starts");
        }
        finally
        {
            AccountSwitch.LoginInto = real;
            AnsiConsole.Console = previous;
        }
    }

    [Test]
    public void Ps_doubles_every_single_quote_form_PowerShell_accepts()
    {
        Assert.That(AccountDirs.Ps("don't"), Is.EqualTo("'don''t'"));
        Assert.That(AccountDirs.Ps("don’t ‘x‚‛"), Is.EqualTo("'don’’t ‘‘x‚‚‛‛'"));
    }

    [Test]
    public void MklinkArguments_QuoteBothPaths_AndRefuseCharactersCmdExpandsAnyway()
    {
        Assert.That(AccountDirs.TryMklinkArguments(@"C:\a&b\x", @"C:\t^|", out var line), Is.True);
        Assert.That(line, Is.EqualTo("/d /c \"mklink /J \"C:\\a&b\\x\" \"C:\\t^|\"\""));
        Assert.That(AccountDirs.TryMklinkArguments(@"C:\%PATH%", @"C:\t", out _), Is.False);
        Assert.That(AccountDirs.TryMklinkArguments(@"C:\a", "C:\\q\"x", out _), Is.False);
    }

    [Test]
    public void MklinkArguments_AJunctionWithAmpersandInItsNameIsMadeAndNothingElseRuns()
    {
        var root = temps.New();
        var target = Path.Combine(root, "t");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(root, "pwned");
        var link = Path.Combine(root, "a&md pwned&b");
        Assert.That(AccountDirs.TryMklinkArguments(link, target, out var line), Is.True);
        using (var p = Process.Start(new ProcessStartInfo("cmd.exe", line) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root })!)
            p.WaitForExit();
        Assert.That(Directory.Exists(link), Is.True);
        Assert.That(File.Exists(marker), Is.False);
    }
}
