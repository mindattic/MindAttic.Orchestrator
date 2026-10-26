using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>TermHost (M.ORCH-T924): the pipe protocol's frames and names, and the host's command line.</summary>
public sealed class TermHostTests
{
    [Test]
    public async Task A_frame_reads_back_as_its_kind_and_payload()
    {
        using var stream = new MemoryStream();
        stream.Write(TermProtocol.Frame(TermFrameKind.Output, "hello"u8));
        stream.Write(TermProtocol.Frame(TermFrameKind.Live, []));
        stream.Position = 0;

        var (kind, payload) = await TermProtocol.ReadFrameAsync(stream);
        Assert.That(kind, Is.EqualTo(TermFrameKind.Output));
        Assert.That(payload, Is.EqualTo("hello"u8.ToArray()));
        (kind, payload) = await TermProtocol.ReadFrameAsync(stream);
        Assert.That(kind, Is.EqualTo(TermFrameKind.Live));
        Assert.That(payload, Is.Empty);
        Assert.ThrowsAsync<EndOfStreamException>(() => TermProtocol.ReadFrameAsync(stream));
    }

    [Test]
    public void An_oversized_frame_is_refused()
    {
        var head = new byte[5];
        head[0] = (byte)TermFrameKind.Input;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(1), TermProtocol.MaxPayload + 1u);
        Assert.ThrowsAsync<InvalidDataException>(() => TermProtocol.ReadFrameAsync(new MemoryStream(head)));
    }

    [Test]
    public void The_payloads_round_trip()
    {
        Assert.That(TermProtocol.ReadHello(TermProtocol.Hello(1, 120, 30)), Is.EqualTo(((ushort)1, (ushort)120, (ushort)30)));
        Assert.That(TermProtocol.ReadHello(new byte[4]), Is.Null);
        Assert.That(TermProtocol.ReadSize(TermProtocol.Size(80, 24)), Is.EqualTo(((ushort)80, (ushort)24)));
        Assert.That(TermProtocol.ReadExit(TermProtocol.Exit(-1073741510)), Is.EqualTo(-1073741510));
        Assert.That(TermProtocol.ReadModes(TermProtocol.Modes(TermModes.BracketedPaste | TermModes.AltScreen)),
            Is.EqualTo(TermModes.BracketedPaste | TermModes.AltScreen));
        Assert.That(TermProtocol.ReadTitle(TermProtocol.Title("RD: Repo ✳")), Is.EqualTo("RD: Repo ✳"));

        var replay = TermProtocol.ReadReplay(TermProtocol.Replay(100, 40, TermModes.CursorVisible, "screen"u8, "\u001b[?25h"u8));
        Assert.That(replay, Is.Not.Null);
        Assert.That((replay!.Cols, replay.Rows, replay.Modes), Is.EqualTo(((ushort)100, (ushort)40, TermModes.CursorVisible)));
        Assert.That(replay.Data, Is.EqualTo("screen"u8.ToArray()));
        Assert.That(replay.Reassert, Is.EqualTo("\u001b[?25h"u8.ToArray()));
    }

    [Test]
    public void A_replay_whose_length_overruns_it_is_malformed()
    {
        var payload = TermProtocol.Replay(1, 1, TermModes.None, "abc"u8, []);
        Assert.That(TermProtocol.ReadReplay(payload.AsSpan(0, payload.Length - 1)), Is.Null);
    }

    [Test]
    public void The_pipe_is_named_for_the_user_and_the_session()
    {
        Assert.That(TermProtocol.PipeName("a1b2c3", "Ryan D"), Is.EqualTo("mindattic-term-Ryan-D-a1b2c3"));
        Assert.That(TermProtocol.PipeName("../x", "me"), Is.EqualTo("mindattic-term-me----x"));
        Assert.That(TermProtocol.NewSessionId(), Does.Match("^[0-9a-f]{12}$"));
    }

    [Test]
    public void The_command_line_is_parsed()
    {
        var options = HostOptions.Parse(["--id", "s1", "--cwd", @"D:\Repo", "--env", "CLAUDE_CONFIG_DIR=C:\\a=b", "--env", "CLAUDE_CODE_OAUTH_TOKEN=",
            "--title", "RD: Repo", "--linger", "30", "--", "claude", "--resume"], out var error);

        Assert.That(error, Is.Null);
        Assert.That(options!.Id, Is.EqualTo("s1"));
        Assert.That(options.Cwd, Is.EqualTo(@"D:\Repo"));
        Assert.That(options.Env, Is.EqualTo(new[]
        {
            new KeyValuePair<string, string?>("CLAUDE_CONFIG_DIR", "C:\\a=b"),
            new KeyValuePair<string, string?>("CLAUDE_CODE_OAUTH_TOKEN", null),
        }));
        Assert.That(options.Title, Is.EqualTo("RD: Repo"));
        Assert.That(options.Linger, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(options.Command, Is.EqualTo(new[] { "claude", "--resume" }));
        Assert.That(options.PipeName, Does.EndWith("-s1"));
    }

    [Test]
    public void Without_an_id_a_new_one_is_made_and_the_linger_is_ten_minutes()
    {
        var options = HostOptions.Parse([], out _);
        Assert.That(options!.Id, Does.Match("^[0-9a-f]{12}$"));
        Assert.That(options.Linger, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [TestCase("--bogus")]
    [TestCase("--id")]
    [TestCase("--env", "NOEQUALS")]
    [TestCase("--linger", "soon")]
    [TestCase("--id", "///")]
    public void A_bad_command_line_is_an_error(params string[] args)
    {
        Assert.That(HostOptions.Parse(args, out var error), Is.Null);
        Assert.That(error, Is.Not.Null);
    }

    [Test]
    public void Launch_passes_everything_but_the_environment_to_the_detached_host()
    {
        var options = HostOptions.Parse(["--launch", "--id", "s1", "--env", "TOKEN=secret", "--title", "T", "--", "claude"], out _)!;
        var args = options.DetachedArguments();

        Assert.That(args, Does.Not.Contain("--launch").And.Not.Contain("--env").And.Not.Contain("TOKEN=secret"));
        Assert.That(args, Is.EqualTo(new[] { "--id", "s1", "--env-prepared", "--linger", "600", "--title", "T", "--", "claude" }));
        Assert.That(HostOptions.Parse(args, out _)!.EnvPrepared, Is.True);
    }

    [Test]
    public void With_no_command_it_runs_pwsh_else_windows_powershell()
    {
        var options = HostOptions.Parse([], out _)!;
        Assert.That(options.CommandLine(_ => ".exe"), Is.EqualTo("pwsh"));
        Assert.That(options.CommandLine(_ => null), Is.EqualTo("powershell.exe"));
    }

    [Test]
    public void A_cmd_script_on_path_runs_through_cmd_and_a_whole_command_line_runs_as_it_is()
    {
        Assert.That(HostOptions.Parse(["--", "claude", "--model", "x y"], out _)!.CommandLine(_ => ".cmd"),
            Is.EqualTo("cmd.exe /d /c claude --model \"x y\""));
        Assert.That(HostOptions.Parse(["--", "claude"], out _)!.CommandLine(_ => ".exe"), Is.EqualTo("claude"));
        Assert.That(HostOptions.Parse(["--", "powershell.exe -NoLogo -EncodedCommand AAAA"], out _)!.CommandLine(_ => null),
            Is.EqualTo("powershell.exe -NoLogo -EncodedCommand AAAA"));
        Assert.That(HostOptions.Parse(["--", @"C:\Program Files\x.exe", "a"], out _)!.CommandLine(_ => null),
            Is.EqualTo("\"C:\\Program Files\\x.exe\" a"));
    }
}
