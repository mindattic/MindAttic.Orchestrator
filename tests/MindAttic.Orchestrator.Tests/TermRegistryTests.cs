using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The TermHost session registry, its term-session bus kind and TermLauncher's arguments (M.ORCH-T925).</summary>
public sealed class TermRegistryTests
{
    private static readonly DateTime HostStart = new(2026, 10, 26, 12, 0, 0, 123, DateTimeKind.Utc);
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "mindattic-term-registry-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (DirectoryNotFoundException) { }
    }

    private static TermSessionRecord Record(string id, int pid = 4242, DateTime? startedUtc = null) => new()
    {
        Id = id, HostPid = pid, HostStartUtc = HostStart, Pipe = TermProtocol.PipeName(id, "tester"), Title = "RD: Repo [Claude]",
        Login = "someone@example.com", ConfigDir = @"C:\Users\x\.claude-accounts\someone@example.com", Repo = "MindAttic.Orchestrator",
        Cwd = @"D:\Projects\Repo", CommandLine = "powershell.exe -NoLogo", Cols = 120, Rows = 30, State = TermSessionStates.Running,
        StartedUtc = startedUtc ?? HostStart.AddSeconds(1), Protocol = TermProtocol.Version,
    };

    /// <summary>A probe that knows only <paramref name="processes"/>: pid to start time.</summary>
    private static Func<int, DateTime?> Processes(params (int Pid, DateTime Start)[] processes) =>
        pid => processes.Where(p => p.Pid == pid).Select(p => (DateTime?)p.Start).FirstOrDefault();

    [Test]
    public void A_written_record_reads_back_whole()
    {
        var record = Record("abc123") with { State = TermSessionStates.Exited, ExitCode = 7 };
        Assert.That(TermSessions.Write(record, dir), Is.True);

        var read = TermSessions.Read(TermSessions.PathOf("abc123", dir));
        Assert.That(read, Is.EqualTo(record));
        Assert.That(read!.Exited, Is.True);
    }

    [Test]
    public void A_write_replaces_the_file_and_leaves_no_temporary_behind()
    {
        TermSessions.Write(Record("abc123"), dir);
        TermSessions.Write(Record("abc123") with { Title = "new title", Cols = 100, Rows = 40 }, dir);

        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName), Is.EqualTo(new[] { "abc123.json" }));
        var read = TermSessions.Read(TermSessions.PathOf("abc123", dir))!;
        Assert.That((read.Title, read.Cols, read.Rows), Is.EqualTo(("new title", 100, 40)));
    }

    [Test]
    public void A_reader_racing_many_writes_always_sees_a_whole_record()
    {
        TermSessions.Write(Record("race"), dir);
        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() =>
        {
            for (var i = 0; !stop.IsCancellationRequested && i < 400; i++)
                TermSessions.Write(Record("race") with { Title = new string('x', i % 50 + 1), Cols = 80 + i % 40 }, dir);
        });
        var misses = 0;
        for (var i = 0; i < 400; i++)
            if (TermSessions.Read(TermSessions.PathOf("race", dir)) is not { Id: "race" }) misses++;
        stop.Cancel();
        writer.Wait();
        Assert.That(misses, Is.Zero);
    }

    [Test]
    public void A_bus_event_is_written_as_the_record_alone()
    {
        TermSessions.Write(new TermSessionEvent(Record("abc123"), TermSessionEvents.Changed), dir);
        var json = File.ReadAllText(TermSessions.PathOf("abc123", dir));
        Assert.That(json, Does.Not.Contain("\"event\""));
        Assert.That(TermSessions.Read(TermSessions.PathOf("abc123", dir)), Is.EqualTo(Record("abc123")));
    }

    [Test]
    public void Enumerate_keeps_live_hosts_and_prunes_a_gone_one_and_a_reused_pid()
    {
        TermSessions.Write(Record("live", pid: 100), dir);
        TermSessions.Write(Record("gone", pid: 200), dir);
        TermSessions.Write(Record("reused", pid: 300), dir);
        var probe = Processes((100, HostStart), (300, HostStart.AddMinutes(5)));

        var live = TermSessions.Enumerate(dir, probe);

        Assert.That(live.Select(r => r.Id), Is.EqualTo(new[] { "live" }));
        Assert.That(File.Exists(TermSessions.PathOf("live", dir)), Is.True);
        Assert.That(File.Exists(TermSessions.PathOf("gone", dir)), Is.False, "a host whose process is gone is pruned");
        Assert.That(File.Exists(TermSessions.PathOf("reused", dir)), Is.False, "a pid now running another process is pruned");
    }

    [Test]
    public void A_start_time_off_only_by_rounding_is_the_same_host()
    {
        TermSessions.Write(Record("live", pid: 100), dir);
        var live = TermSessions.Enumerate(dir, Processes((100, HostStart.AddTicks(5000))));
        Assert.That(live, Has.Count.EqualTo(1));
    }

    [Test]
    public void Enumerate_with_real_processes_keeps_this_one_and_prunes_one_that_exited()
    {
        var self = TermSessions.HostStartOf(Environment.ProcessId);
        Assert.That(self, Is.Not.Null);
        TermSessions.Write(Record("self", pid: Environment.ProcessId) with { HostStartUtc = self!.Value }, dir);

        using var gone = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/d /c exit 0") { CreateNoWindow = true, UseShellExecute = false })!;
        var goneStart = gone.StartTime.ToUniversalTime();
        gone.WaitForExit();
        TermSessions.Write(Record("exited", pid: gone.Id) with { HostStartUtc = goneStart }, dir);

        var live = TermSessions.Enumerate(dir);

        Assert.That(live.Select(r => r.Id), Is.EqualTo(new[] { "self" }));
        Assert.That(File.Exists(TermSessions.PathOf("exited", dir)), Is.False);
    }

    [Test]
    public void Enumerate_lists_the_oldest_first_and_includes_an_exited_cli_whose_host_lingers()
    {
        TermSessions.Write(Record("b", pid: 1, startedUtc: HostStart.AddMinutes(2)), dir);
        TermSessions.Write(Record("a", pid: 2, startedUtc: HostStart.AddMinutes(1)) with { State = TermSessionStates.Exited, ExitCode = 0 }, dir);
        var live = TermSessions.Enumerate(dir, Processes((1, HostStart), (2, HostStart)));
        Assert.That(live.Select(r => r.Id), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(live[0].Exited, Is.True);
    }

    [Test]
    public void Unreadable_and_leftover_files_are_deleted_only_once_old()
    {
        Directory.CreateDirectory(dir);
        var oldJunk = Path.Combine(dir, "old.json");
        var newJunk = Path.Combine(dir, "new.json");
        var oldTemp = Path.Combine(dir, "x.json.1234abcd.tmp");
        var log = Path.Combine(dir, "x.log");
        File.WriteAllText(oldJunk, "{ not json");
        File.WriteAllText(newJunk, "{ not json");
        File.WriteAllText(oldTemp, "{");
        File.WriteAllText(log, "a host's log");
        File.SetLastWriteTimeUtc(oldJunk, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(oldTemp, DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddMinutes(-5));

        Assert.That(TermSessions.Enumerate(dir, Processes()), Is.Empty);

        Assert.That(File.Exists(oldJunk), Is.False);
        Assert.That(File.Exists(oldTemp), Is.False);
        Assert.That(File.Exists(newJunk), Is.True, "a fresh file is left alone");
        Assert.That(File.Exists(log), Is.True, "logs aren't the registry's");
    }

    [Test]
    public void Enumerate_of_a_missing_folder_is_empty()
    {
        Assert.That(TermSessions.Enumerate(dir, Processes()), Is.Empty);
        Assert.That(Directory.Exists(dir), Is.False);
    }

    [Test]
    public void Get_returns_a_live_session_and_prunes_a_stale_one()
    {
        TermSessions.Write(Record("live", pid: 100), dir);
        TermSessions.Write(Record("stale", pid: 200), dir);
        var probe = Processes((100, HostStart));

        Assert.That(TermSessions.Get("live", dir, probe)?.Id, Is.EqualTo("live"));
        Assert.That(TermSessions.Get("stale", dir, probe), Is.Null);
        Assert.That(File.Exists(TermSessions.PathOf("stale", dir)), Is.False);
        Assert.That(TermSessions.Get("missing", dir, probe), Is.Null);
    }

    [Test]
    public void Delete_removes_the_file()
    {
        TermSessions.Write(Record("abc123"), dir);
        Assert.That(TermSessions.Delete("abc123", dir), Is.True);
        Assert.That(File.Exists(TermSessions.PathOf("abc123", dir)), Is.False);
    }

    // The term-session bus kind.

    [Test]
    public void The_envelope_carries_the_record_and_the_event()
    {
        var at = new DateTime(2026, 10, 26, 13, 0, 0, DateTimeKind.Utc);
        var line = TermBus.Envelope(new TermSessionEvent(Record("abc123"), TermSessionEvents.Started), at, 777);

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("kind").GetString(), Is.EqualTo("term-session"));
        Assert.That(root.GetProperty("v").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("pid").GetInt32(), Is.EqualTo(777));
        Assert.That(root.GetProperty("at").GetDateTime(), Is.EqualTo(at));
        var payload = root.GetProperty("payload");
        Assert.That(payload.GetProperty("event").GetString(), Is.EqualTo("started"));
        Assert.That(payload.GetProperty("id").GetString(), Is.EqualTo("abc123"));
        Assert.That(payload.GetProperty("hostPid").GetInt32(), Is.EqualTo(4242));
        Assert.That(payload.GetProperty("pipe").GetString(), Is.EqualTo(TermProtocol.PipeName("abc123", "tester")));
        Assert.That(payload.GetProperty("state").GetString(), Is.EqualTo("running"));
        Assert.That(payload.GetProperty("cols").GetInt32(), Is.EqualTo(120));
        Assert.That(payload.TryGetProperty("exitCode", out _), Is.False, "nulls are left out");
    }

    [Test]
    public void The_envelope_parses_back_and_other_kinds_or_newer_versions_are_skipped()
    {
        var sent = new TermSessionEvent(Record("abc123") with { State = TermSessionStates.Exited, ExitCode = 3 }, TermSessionEvents.Changed);
        var line = TermBus.Envelope(sent, DateTime.UtcNow, 1);

        Assert.That(TermBus.Parse(line), Is.EqualTo(sent));
        Assert.That(TermBus.Parse(line.Replace("\"term-session\"", "\"ticket\"")), Is.Null);
        Assert.That(TermBus.Parse(line.Replace("\"v\":1", "\"v\":2")), Is.Null);
        Assert.That(TermBus.Parse("not json"), Is.Null);
        Assert.That(sent.ToRecord(), Is.EqualTo(Record("abc123") with { State = TermSessionStates.Exited, ExitCode = 3 }));
    }

    [Test]
    public void Cores_status_bus_reads_the_envelope()
    {
        var sent = new TermSessionEvent(Record("abc123"), TermSessionEvents.Ended);
        var envelope = StatusStream.Deserialize(TermBus.Envelope(sent, DateTime.UtcNow, 1));
        Assert.That(envelope, Is.Not.Null);

        TermSessionEvent? got = null;
        new StatusBus().On<TermSessionEvent>(TermBus.Kind, (_, e) => got = e, TermBus.Version).Dispatch(envelope!);
        Assert.That(got, Is.EqualTo(sent));
    }

    [Test]
    public void The_host_writes_to_the_daemons_inbox()
    {
        Assert.That(TermBus.InboxPipeName, Is.EqualTo(StatusStream.InboxPipeName));
    }

    [Test]
    public void Sending_with_no_daemon_fails_at_once()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var sent = TermBus.TrySend(new TermSessionEvent(Record("abc123"), TermSessionEvents.Started), "mindattic-no-such-pipe-" + Guid.NewGuid().ToString("N"));
        Assert.That(sent, Is.False);
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
    }

    // Starting a hosted CLI.

    [Test]
    public void A_pane_request_launches_as_its_tab_would()
    {
        var request = new PaneRequest("RD: Repo [Claude]", @"D:\Projects\Repo") { Command = ["claude"], Repo = "Repo" };
        var spec = TermLaunchSpec.From(request, "pane-pipe");

        Assert.That(spec.CommandLine, Is.EqualTo(request.ProcessCommandLine("pane-pipe")));
        Assert.That((spec.Directory, spec.Title, spec.Repo, spec.Login), Is.EqualTo((@"D:\Projects\Repo", "RD: Repo [Claude]", "Repo", (string?)null)));
        Assert.That(spec.Environment, Is.Empty);
        Assert.That(spec.IsMenu, Is.False, "not the Launcher's own menu");
    }

    [Test]
    public void The_launchers_own_menu_pane_carries_IsMenu_through_the_spec_and_arguments()
    {
        var request = new PaneRequest("Launcher", @"D:\Projects\Repo") { Command = ["claude"], Repo = "Launcher", IsMenu = true };
        var spec = TermLaunchSpec.From(request);
        Assert.That(spec.IsMenu, Is.True);

        var args = TermLauncher.Arguments(spec, "abc123");
        Assert.That(args, Does.Contain("--menu"));
    }

    [Test]
    public void The_launch_arguments_carry_the_command_line_as_one_argument_and_no_environment()
    {
        var spec = new TermLaunchSpec
        {
            CommandLine = "powershell.exe -NoLogo -EncodedCommand abc", Directory = @"D:\Projects\Repo", Title = "RD: Repo [Claude]",
            Login = "someone@example.com", Repo = "Repo", Linger = TimeSpan.FromSeconds(30),
            Environment = new Dictionary<string, string?> { ["CLAUDE_CODE_OAUTH_TOKEN"] = "secret" },
        };

        var args = TermLauncher.Arguments(spec, "abc123");

        Assert.That(args, Is.EqualTo(new[]
        {
            "--launch", "--id", "abc123", "--cwd", @"D:\Projects\Repo", "--title", "RD: Repo [Claude]", "--login", "someone@example.com",
            "--repo", "Repo", "--linger", "30", "--", "powershell.exe -NoLogo -EncodedCommand abc",
        }));
        Assert.That(string.Join(' ', args), Does.Not.Contain("secret"));
        // The host reads them back as the same session.
        var options = HostOptions.Parse(args, out var error);
        Assert.That(error, Is.Null);
        Assert.That((options!.Id, options.Login, options.Repo, options.Command.Single()),
            Is.EqualTo(("abc123", "someone@example.com", "Repo", "powershell.exe -NoLogo -EncodedCommand abc")));
        Assert.That(options.DetachedArguments(), Does.Contain("--login").And.Contain("--repo"));
    }

    [Test]
    public void The_launch_line_is_read_from_the_hosts_output()
    {
        Assert.That(TermLauncher.ReadLaunch("{\"id\":\"abc123\",\"pid\":42,\"pipe\":\"mindattic-term-x-abc123\"}\r\n"),
            Is.EqualTo(("abc123", 42, "mindattic-term-x-abc123")));
        Assert.That(TermLauncher.ReadLaunch("TermHost failed: boom"), Is.Null);
        Assert.That(TermLauncher.ReadLaunch(""), Is.Null);
    }

    [Test]
    public void A_file_that_cant_be_read_is_unknown_not_gone()
    {
        TermSessions.Write(Record("held", pid: 100), dir);
        var probe = Processes((100, HostStart));
        using (new FileStream(TermSessions.PathOf("held", dir), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.That(TermSessions.TryGet("held", dir, probe, out var record), Is.False, "M.ORCH-T1035: a read failure is unknown");
            Assert.That(record, Is.Null);
        }
        Assert.That(File.Exists(TermSessions.PathOf("held", dir)), Is.True, "nothing is deleted on a read failure");
        Assert.That(TermSessions.TryGet("held", dir, probe, out var again), Is.True);
        Assert.That(again?.Id, Is.EqualTo("held"));
        Assert.That(TermSessions.TryGet("missing", dir, probe, out var none), Is.True);
        Assert.That(none, Is.Null);
    }

    [Test]
    public void Enumerate_says_it_is_incomplete_when_a_record_cant_be_read()
    {
        // bughunt 2026-10-27: a caller pruning by the listing mustn't drop a live session it couldn't read.
        TermSessions.Write(Record("held", pid: 100), dir);
        TermSessions.Write(Record("ok", pid: 100), dir);
        var probe = Processes((100, HostStart));
        using (new FileStream(TermSessions.PathOf("held", dir), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var live = TermSessions.Enumerate(dir, probe, out var complete);
            Assert.That(live.Select(r => r.Id), Is.EqualTo(new[] { "ok" }));
            Assert.That(complete, Is.False);
        }
        Assert.That(TermSessions.Enumerate(dir, probe, out var after), Has.Count.EqualTo(2));
        Assert.That(after, Is.True);
        TermSessions.Enumerate(Path.Combine(dir, "missing"), probe, out var missing);
        Assert.That(missing, Is.True, "no folder: no session");
    }
}
