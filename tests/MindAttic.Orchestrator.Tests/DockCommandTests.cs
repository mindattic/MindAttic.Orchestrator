using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary><c>orchestrator dock</c> (M.ORCH-Tnnn): pulls a standalone Claude tab into the dashboard's Terminals panel by
/// walking its own process ancestry to its Launcher host, then asking that host's control pipe to end it and reopen it
/// resumed. Every piece it reads or writes (the ancestry walk, the snapshot scan, the dashboard's pipe, the lock store) is
/// faked; nothing here touches a real process, a real pipe, or the live orchestrator.db.</summary>
[NonParallelizable] // DockCommand.Override and AnsiConsole.Console are process-wide
public sealed class DockCommandTests
{
    private const int LauncherPid = 500;
    private static readonly DateTime Now = new(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc);
    private static readonly HostSession ClaudeHost = new("Claude", LauncherPid, "mindattic-host-claude-500", @"D:\W\A");

    private readonly TempDirs temps = new();
    private IAnsiConsole previousConsole = null!;
    private StringWriter output = null!;
    private AccountDirs dirs = null!;
    private JsonStore store = null!;
    private readonly List<(HostSession Host, string Message)> sent = [];
    private bool sendOk;
    private bool listening;
    private IReadOnlyList<(int Pid, string Exe)> ancestors = [];
    private IReadOnlyList<HostSession> hosts = [];
    private List<CliBilling> live = [];
    private Dictionary<string, string> onOwn = new(StringComparer.Ordinal);

    [SetUp]
    public void SetUp()
    {
        sent.Clear();
        sendOk = true;
        listening = true;
        var dir = temps.New();
        store = new JsonStore(Path.Combine(dir, "store"));
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        onOwn = new Dictionary<string, string>(StringComparer.Ordinal);
        ancestors = [(600, "conhost.exe"), (LauncherPid, "MindAttic.Launcher.exe"), (700, "explorer.exe")];
        hosts = [ClaudeHost];
        live = [new CliBilling(4242, "sess-1", @"D:\W\A", "subscription", ClaudeHost)];

        DockCommand.Override = () => new DockCommand.Deps(
            () => ancestors,
            () => hosts,
            new AccountMove(dirs) { OnOwnLogin = () => onOwn, LiveOverride = (_, _) => live },
            () => listening,
            (h, m) => { sent.Add((h, m)); return Task.FromResult(sendOk); },
            new RelocateLocks(store));

        previousConsole = AnsiConsole.Console;
        output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
        AnsiConsole.Console.Profile.Width = 240; // wide enough that a long message's words aren't wrapped onto another line
    }

    [TearDown]
    public void TearDown()
    {
        DockCommand.Override = null;
        AnsiConsole.Console = previousConsole;
        temps.DeleteAll();
    }

    private static int Run() => CliApp.Create().Run(["dock"]);

    [Test]
    public void Not_running_inside_a_launcher_tab_at_all_is_refused()
    {
        ancestors = [(600, "conhost.exe"), (700, "explorer.exe")];

        Assert.That(Run(), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("there's nothing to dock"));
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void A_host_not_yet_in_the_snapshot_is_refused()
    {
        hosts = [];

        Assert.That(Run(), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("isn't in the orchestrator's snapshot yet"));
    }

    [Test]
    public void A_non_claude_host_is_refused()
    {
        hosts = [new HostSession("Codex", LauncherPid, "mindattic-host-codex-500", @"D:\W\A")];

        Assert.That(Run(), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("Dock only supports Claude tabs, not Codex"));
    }

    [Test]
    public void A_claude_host_with_no_matching_billing_cli_yet_is_refused()
    {
        live = [];

        Assert.That(Run(), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("isn't in the orchestrator's snapshot yet"));
    }

    [Test]
    public void No_dashboard_listening_refuses_before_anything_is_locked_or_sent()
    {
        listening = false;

        Assert.That(Run(), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("MindAttic.Orchestrator isn't running"));
        Assert.That(sent, Is.Empty);
        Assert.That(new RelocateLocks(store).All(), Is.Empty, "nothing was changed");
    }

    [Test]
    public void A_session_already_relocating_is_refused()
    {
        new RelocateLocks(store).TryBegin(new RelocateLock
        {
            SessionId = "sess-1", Pid = 4242, HostPid = LauncherPid, Repo = "A", StartedUtc = Now, Direction = RelocateDirection.Dock,
        }, out _);

        Assert.That(Run(), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("already being relocated"));
        Assert.That(sent, Is.Empty);
    }

    [Test]
    public void A_refused_control_pipe_unlocks_the_session_and_says_how_to_reopen_it_yourself()
    {
        sendOk = false;

        Assert.That(Run(), Is.EqualTo(1));
        var said = output.ToString();
        Assert.That(said, Does.Contain("Couldn't reach this tab's control pipe"));
        Assert.That(said, Does.Contain("claude --resume sess-1"));
        Assert.That(new RelocateLocks(store).All(), Is.Empty, "unlocked so a retry isn't refused as already relocating");
    }

    [Test]
    public void The_success_path_locks_the_session_sends_the_relocate_message_and_returns_at_once()
    {
        Assert.That(Run(), Is.EqualTo(0));
        var said = output.ToString();
        Assert.That(said, Does.Contain("Docking requested"));
        Assert.That(said, Does.Contain("global login's dashboard tab"));
        Assert.That(said, Does.Not.Contain("[red]"), "not an error: default color");

        Assert.That(sent, Has.Count.EqualTo(1));
        Assert.That(sent[0].Host, Is.EqualTo(ClaudeHost));
        var message = JsonNode.Parse(sent[0].Message)!.AsObject();
        Assert.That((string?)message["type"], Is.EqualTo("restart-resume"));
        Assert.That((string?)message["reason"], Is.EqualTo("relocate"));
        Assert.That((string?)message["session"], Is.EqualTo("sess-1"));
        Assert.That((int?)message["cliPid"], Is.EqualTo(4242));

        var locked = new RelocateLocks(store).All().Single();
        Assert.That(locked.SessionId, Is.EqualTo("sess-1"));
        Assert.That(locked.HostPid, Is.EqualTo(LauncherPid));
        Assert.That(locked.Repo, Is.EqualTo("A"), "named from its cwd's folder, as CliBilling.Where names it");
        Assert.That(locked.Direction, Is.EqualTo(RelocateDirection.Dock));
        Assert.That(locked.Email, Is.Null, "the global login");
    }

    [Test]
    public void A_per_cli_login_carries_its_email_and_config_folder_onto_the_lock()
    {
        onOwn["sess-1"] = dirs.For("b@x.com").Directory;

        Assert.That(Run(), Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("b@x.com's dashboard tab"));

        var locked = new RelocateLocks(store).All().Single();
        Assert.That(locked.Email, Is.EqualTo("b@x.com"));
        Assert.That(locked.ConfigDir, Is.EqualTo(dirs.For("b@x.com").Directory));
    }
}
