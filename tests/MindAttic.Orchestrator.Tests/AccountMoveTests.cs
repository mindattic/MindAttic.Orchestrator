using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Move to account (M.ORCH-T607): a running CLI's ownership moves to another signed-in login through the Launcher's
/// restart-resume request with that login's folder. Every folder is a scratch one, and nothing is sent to a real tab.</summary>
[NonParallelizable] // MoveCommand.Override and AnsiConsole.Console are process-wide
public sealed class AccountMoveTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 16, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();
    private string dir = "";
    private AccountDirs dirs = null!;
    private readonly List<(HostSession Host, string Message)> sent = [];
    private bool globalSignedIn = true;
    private bool tabTakes = true;
    private Dictionary<string, string> onOwn = new(StringComparer.Ordinal);
    private List<CliBilling> live = [];
    private static readonly HostSession Tab = new("Claude", 77, "mindattic-host-claude-77", @"D:\Projects\Prose");

    [SetUp]
    public void SetUp()
    {
        // One fixture instance runs every test: what a test changed is put back.
        sent.Clear();
        globalSignedIn = true;
        tabTakes = true;
        onOwn = new(StringComparer.Ordinal);
        dir = temps.New();
        Directory.CreateDirectory(Path.Combine(dir, ".claude"));
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        live = [new CliBilling(4242, "sess-1", @"D:\Projects\Prose", "subscription", Tab)];
    }

    [TearDown]
    public void TearDown()
    {
        MoveCommand.Override = null;
        temps.DeleteAll();
    }

    private AccountMove Mover() => new(dirs)
    {
        Send = (h, m) => { sent.Add((h, m)); return Task.FromResult(tabTakes); },
        GlobalSignedIn = () => globalSignedIn,
        OnOwnLogin = () => onOwn,
        LiveOverride = (_, _) => live,
    };

    private AccountDir SignedIn(string email)
    {
        var d = dirs.For(email);
        Directory.CreateDirectory(d.Directory);
        File.WriteAllText(d.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "u-" + email, emailAddress = email } }));
        return d;
    }

    [Test]
    public void The_menu_lists_every_other_signed_in_login_with_the_global_one_first()
    {
        var a = SignedIn("a@x.com");
        var b = SignedIn("b@x.com");
        Directory.CreateDirectory(dirs.For("c@x.com").Directory); // nobody signed in: never a target

        Assert.That(Mover().TargetsFor("a@x.com").Select(t => t.Label), Is.EqualTo(new[] { AccountMove.GlobalLabel, "b@x.com" }));
        Assert.That(Mover().TargetsFor(null).Select(t => t.ConfigDir), Is.EqualTo(new[] { a.Directory, b.Directory }), "on the global login: only the per-CLI ones");
        globalSignedIn = false;
        Assert.That(Mover().TargetsFor("b@x.com").Select(t => t.Label), Is.EqualTo(new[] { "a@x.com" }), "a global login nobody's signed in to isn't offered");
    }

    [Test]
    public void The_request_is_the_launchers_restart_resume_with_the_logins_folder_and_no_billing()
    {
        var message = JsonNode.Parse(AccountMove.Message(live[0], @"C:\u\.claude-accounts\b@x.com", Now))!.AsObject();
        Assert.That((string?)message["type"], Is.EqualTo("restart-resume"));
        Assert.That((string?)message["configDir"], Is.EqualTo(@"C:\u\.claude-accounts\b@x.com"));
        Assert.That((string?)message["session"], Is.EqualTo("sess-1"));
        Assert.That((int?)message["cliPid"], Is.EqualTo(4242));
        Assert.That(message.ContainsKey("billing"), Is.False, "a move keeps the billing mode: the Launcher doesn't wait on it");
        Assert.That((string?)JsonNode.Parse(AccountMove.Message(live[0], "", Now))!["configDir"], Is.EqualTo(""), "the global login is an empty configDir");
    }

    [Test]
    public void A_cli_is_found_by_pid_session_or_repo()
    {
        List<CliBilling> clis = [.. live, new CliBilling(5, "sess-2", @"D:\Projects\Other", null, null), new CliBilling(6, "sess-3", @"D:\Projects\Other\", null, null)];
        Assert.That(AccountMove.Find(clis, "4242").Single().SessionId, Is.EqualTo("sess-1"));
        Assert.That(AccountMove.Find(clis, "sess-2").Single().Pid, Is.EqualTo(5));
        Assert.That(AccountMove.Find(clis, "prose").Single().Pid, Is.EqualTo(4242));
        Assert.That(AccountMove.Find(clis, @"D:\Projects\Other").Select(c => c.Pid), Is.EqualTo(new[] { 5, 6 }));
        Assert.That(AccountMove.Find(clis, "nope"), Is.Empty);
    }

    [Test]
    public async Task Moving_sends_the_tab_the_request_with_the_target_logins_folder()
    {
        var b = SignedIn("b@x.com");
        var result = await Mover().MoveAsync(live[0], "B@x.com", Now);

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(sent.Single().Host, Is.EqualTo(Tab));
        Assert.That((string?)JsonNode.Parse(sent.Single().Message)!["configDir"], Is.EqualTo(b.Directory));
        Assert.That(File.Exists(b.SettingsPath), "the login is prepared (its settings follow ~/.claude's) before a CLI moves to it");
    }

    [Test]
    public async Task Moving_to_the_global_login_sends_an_empty_folder()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        var result = await Mover().MoveAsync(live[0], null, Now);

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That((string?)JsonNode.Parse(sent.Single().Message)!["configDir"], Is.EqualTo(""));
    }

    [Test]
    public async Task A_move_is_refused_to_the_same_login_one_not_signed_in_or_a_cli_outside_the_launcher()
    {
        var a = SignedIn("a@x.com");
        onOwn["sess-1"] = a.Directory;
        Assert.That((await Mover().MoveAsync(live[0], "a@x.com", Now)).Message, Does.Contain("already on a@x.com"));
        Assert.That((await Mover().MoveAsync(live[0], "nobody@x.com", Now)).Message, Does.Contain("isn't signed in"));
        Assert.That((await Mover().MoveAsync(live[0], "not-an-email", Now)).Ok, Is.False);
        globalSignedIn = false;
        Assert.That((await Mover().MoveAsync(live[0], null, Now)).Message, Does.Contain("global login"));

        globalSignedIn = true;
        var outside = live[0] with { Host = null };
        Assert.That((await Mover().MoveAsync(outside, null, Now)).Message, Does.Contain("isn't in a Launcher tab").And.Contain("claude --resume sess-1"));
        Assert.That(sent, Is.Empty);

        tabTakes = false;
        var refused = await Mover().MoveAsync(live[0], null, Now);
        Assert.That(refused.Ok, Is.False);
        Assert.That(refused.Message, Does.Contain("no control pipe"));
    }

    [Test]
    public void The_move_command_moves_a_cli_named_by_its_repo()
    {
        SignedIn("b@x.com");
        MoveCommand.Override = () => (Mover(), []);
        var previous = AnsiConsole.Console;
        var output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors });
        try
        {
            Assert.That(CliApp.Create().Run(["move", "Prose", "--account", "b@x.com"]), Is.EqualTo(0), output.ToString());
            Assert.That(output.ToString(), Does.Contain("Asked the Launcher to move the CLI in Prose"));
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(CliApp.Create().Run(["move", "Nowhere", "--global"]), Is.EqualTo(1));
            Assert.That(CliApp.Create().Run(["move", "Prose"]), Is.EqualTo(1), "one of --account or --global is needed");
        }
        finally { AnsiConsole.Console = previous; }
    }
}
