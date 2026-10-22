using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The Auto login (M.ORCH-T605): one CLI per account, most headroom first, the global login when every account has
/// a CLI or none is signed in, and <c>orchestrator accounts pick</c>'s one-line contract. Scratch folders only, never ~/.claude*.</summary>
[NonParallelizable] // the CLI tests swap Ctx.StoreDirectory, the console and the commands' overrides, which are process-wide
public sealed class LoginPickerTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();
    private string dir = "";
    private AccountDirs dirs = null!;
    private List<string?> live = [];

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        live = [];
    }

    [TearDown]
    public void TearDown()
    {
        AccountsCommand.LoginsOverride = null;
        StartCommand.LiveOverride = null;
        temps.DeleteAll();
    }

    private LoginPicker Picker => new(dirs, () => live);

    private AccountDir Account(string email, double? fiveHour = null, double? week = null, bool signedIn = true)
    {
        var a = dirs.For(email);
        Directory.CreateDirectory(a.Directory);
        if (signedIn)
            File.WriteAllText(a.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "id-" + email, emailAddress = email } }));
        if (fiveHour is not null || week is not null)
        {
            var limits = new Dictionary<string, object>();
            var reset = new DateTimeOffset(Now.AddHours(2)).ToUnixTimeSeconds();
            if (fiveHour is { } f) limits["five_hour"] = new { used_percentage = f, resets_at = reset };
            if (week is { } w) limits["seven_day"] = new { used_percentage = w, resets_at = new DateTimeOffset(Now.AddDays(3)).ToUnixTimeSeconds() };
            File.WriteAllText(a.UsagePath, JsonSerializer.Serialize(new { rate_limits = limits, at = Now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ") }));
        }
        return a;
    }

    [Test]
    public void No_signed_in_account_is_the_global_login()
    {
        Account("a@example.com", signedIn: false);
        var pick = Picker.Pick(Now);
        Assert.That(pick.Account, Is.Null);
        Assert.That(pick.ConfigDir, Is.Empty);
    }

    [Test]
    public void The_free_account_with_the_most_headroom_is_picked()
    {
        Account("a@example.com", fiveHour: 60, week: 10);
        Account("b@example.com", fiveHour: 10, week: 30);
        Account("c@example.com", fiveHour: 20, week: 50);
        // Headroom is the tighter limit's room: a 40, b 70, c 50.
        Assert.That(Picker.Pick(Now).Account?.Email, Is.EqualTo("b@example.com"));
    }

    [Test]
    public void An_account_never_read_counts_as_untouched()
    {
        Account("a@example.com", fiveHour: 5, week: 5);
        Account("b@example.com");
        Assert.That(Picker.Pick(Now).Account?.Email, Is.EqualTo("b@example.com"));
    }

    [Test]
    public void An_account_with_a_live_CLI_is_skipped()
    {
        var a = Account("a@example.com");
        Account("b@example.com", fiveHour: 50);
        live.Add(a.Directory + Path.DirectorySeparatorChar);
        Assert.That(Picker.Pick(Now).Account?.Email, Is.EqualTo("b@example.com"));
    }

    [Test]
    public void Every_account_busy_falls_back_to_the_global_login()
    {
        var a = Account("a@example.com");
        var b = Account("b@example.com");
        live.AddRange([a.Directory, b.Directory, null, Path.Combine(dir, ".claude")]);
        var pick = Picker.Pick(Now);
        Assert.That(pick.Account, Is.Null);
        Assert.That(pick.Reason, Does.Contain("already has a CLI"));
    }

    [Test]
    public void An_exhausted_account_is_not_picked()
    {
        Account("a@example.com", fiveHour: 100, week: 20);
        Assert.That(Picker.Pick(Now).Account, Is.Null);
    }

    [Test]
    public void An_account_at_its_weekly_stop_line_is_not_picked()
    {
        // M.ORCH-T609: its CLIs stop at 100 minus the weekly reserve (95% by default), so it has nothing left to give a new CLI.
        Account("a@example.com", fiveHour: 5, week: 95);
        Account("b@example.com", fiveHour: 70, week: 10);
        Assert.That(Picker.Pick(Now).Account?.Email, Is.EqualTo("b@example.com"), "a at 95% of its week is at the default line");
        Assert.That(LoginPicker.Headroom(Budget.ReportedUsage.Load(Now, dirs.For("a@example.com").UsagePath), Now), Is.Zero);
        live.Add(dirs.For("b@example.com").Directory);
        Assert.That(Picker.Pick(Now).Account, Is.Null, "with b busy, nothing is free below its line: the global login");
        Assert.That(Picker.Pick(Now, weeklyStopPercent: 98).Account?.Email, Is.EqualTo("a@example.com"), "a 2% reserve leaves a 3% of its week");
    }

    [Test]
    public void A_claimed_pick_holds_its_account_until_the_hold_runs_out()
    {
        Account("a@example.com");
        Account("b@example.com", fiveHour: 30);
        Assert.That(Picker.Pick(Now, claim: true).Account?.Email, Is.EqualTo("a@example.com"));
        Assert.That(Picker.Pick(Now.AddSeconds(5), claim: true).Account?.Email, Is.EqualTo("b@example.com"), "tabs started back to back spread");
        Assert.That(Picker.Pick(Now.AddSeconds(10)).Account, Is.Null, "both held");
        Assert.That(Picker.Pick(Now + LoginPicker.ClaimFor + TimeSpan.FromSeconds(1)).Account?.Email, Is.EqualTo("a@example.com"));
    }

    [Test]
    public void An_unclaimed_pick_holds_nothing()
    {
        Account("a@example.com");
        Assert.That(Picker.Pick(Now).Account?.Email, Is.EqualTo("a@example.com"));
        Assert.That(Picker.Pick(Now).Account?.Email, Is.EqualTo("a@example.com"));
    }

    [Test]
    public void A_new_tab_uses_Auto_only_for_Claude_and_only_while_it_is_on()
    {
        Account("a@example.com");
        Assert.That(Picker.ForNewTab("Codex", autoLogin: true, Now, out _), Is.Null);
        Assert.That(Picker.ForNewTab("Claude", autoLogin: false, Now, out _), Is.Null);
        var picked = Picker.ForNewTab("Claude", autoLogin: true, Now, out var note);
        Assert.That(picked?.Email, Is.EqualTo("a@example.com"), note);
        Assert.That(File.Exists(picked!.SettingsPath), Is.True, "the login is prepared as a Claude home");
    }

    [Test]
    public void Accounts_pick_prints_the_chosen_config_dir_then_an_empty_line_once_every_account_is_taken()
    {
        var a = Account("a@example.com");
        AccountsCommand.LoginsOverride = () => dirs;
        StartCommand.LiveOverride = () => live;
        var (code, text) = RunCli("accounts", "pick");
        Assert.That(code, Is.Zero);
        Assert.That(text, Is.EqualTo(a.Directory + Environment.NewLine), "exactly one line: the folder");

        (code, text) = RunCli("accounts", "pick");
        Assert.That(code, Is.Zero);
        Assert.That(text, Is.EqualTo(Environment.NewLine), "held for the CLI just started: the global login");

        (code, text) = RunCli("accounts", "pick", "--json");
        using var doc = JsonDocument.Parse(text);
        Assert.That(doc.RootElement.GetProperty("configDir").GetString(), Is.Empty);
    }

    [Test]
    public void Accounts_pick_is_the_global_login_while_Auto_is_off()
    {
        Account("a@example.com");
        AccountsCommand.LoginsOverride = () => dirs;
        StartCommand.LiveOverride = () => live;
        var (code, text) = RunCli(store => store.SaveSettings(store.LoadSettings() with { AutoLogin = false }), "accounts", "pick");
        Assert.That((code, text), Is.EqualTo((0, Environment.NewLine)));
    }

    [Test]
    public void Accounts_alias_names_a_login_and_says_its_name()
    {
        var a = Account("a@example.com");
        AccountsCommand.LoginsOverride = () => dirs;
        Assert.That(RunCli("accounts", "alias", "a@example.com"), Is.EqualTo((0, "a" + Environment.NewLine)), "the email's default until it's named");
        Assert.That(RunCli("accounts", "alias", "A@example.com", "Amy Reed").Code, Is.Zero);
        Assert.That(LoginLook.For(a.Directory, a.Email).Alias, Is.EqualTo("Amy Reed"));
        Assert.That(RunCli("accounts", "alias", "a@example.com").Text.Trim(), Is.EqualTo("Amy Reed"));
        Assert.That(RunCli("accounts", "alias", "a@example.com", "--reset").Code, Is.Zero);
        Assert.That(LoginLook.For(a.Directory, a.Email).Alias, Is.EqualTo("a"));
        Assert.That(RunCli("accounts", "alias", "nobody@example.com", "X").Code, Is.EqualTo(1), "no login of its own here, nor the global one");
        Assert.That(RunCli("accounts", "alias", "not-an-email", "X").Code, Is.EqualTo(1));
    }

    [Test]
    public void The_accounts_branch_still_takes_its_own_options_and_help()
    {
        Assert.That(RunCli("accounts", "alias", "--help").Code, Is.Zero);
        Assert.That(RunCli("accounts", "--help").Code, Is.Zero);
        Assert.That(RunCli("accounts", "pick", "--help").Code, Is.Zero);
    }

    private (int Code, string Text) RunCli(params string[] args) => RunCli(null, args);

    private (int Code, string Text) RunCli(Action<JsonStore>? setup, params string[] args)
    {
        var store = temps.New();
        var previousStore = Ctx.StoreDirectory;
        var previousConsole = AnsiConsole.Console;
        var previousOut = Console.Out;
        var output = new StringWriter();
        try
        {
            Ctx.StoreDirectory = store;
            setup?.Invoke(new JsonStore(store));
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(new StringWriter()),
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No,
            });
            Console.SetOut(output);
            var code = CliApp.Create().Run(args);
            return (code, output.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            AnsiConsole.Console = previousConsole;
            Ctx.StoreDirectory = previousStore;
        }
    }
}
