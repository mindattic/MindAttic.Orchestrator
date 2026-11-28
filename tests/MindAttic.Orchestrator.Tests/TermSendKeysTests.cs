using System.IO.Pipes;
using System.Text;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary><see cref="TermSendKeys"/> (M.ORCH-T2262, moved from the Cli project's <c>send-keys</c> command to Core Sessions
/// by M.ORCH-T2409): the frames sent over a session's TermHost pipe.</summary>
public sealed class TermSendKeysTests
{
    [Test]
    public async Task The_text_goes_bracketed_when_the_replay_says_the_cli_has_bracketed_paste_on_then_a_bare_enter()
    {
        var name = "mindattic-test-sendkeys-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var accept = server.WaitForConnectionAsync();

        var send = TermSendKeys.SendAsync(name, "hello\nworld", async (pipeName, ct) =>
        {
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct).ConfigureAwait(false);
            return client;
        });
        await accept;

        var hello = await TermProtocol.ReadFrameAsync(server);
        Assert.That(hello.Kind, Is.EqualTo(TermFrameKind.Hello));
        Assert.That(TermProtocol.ReadViewerHello(hello.Payload)!.Interactive, Is.True);

        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.BracketedPaste, [], [])));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Live, []));

        var text = await TermProtocol.ReadFrameAsync(server);
        Assert.That(text.Kind, Is.EqualTo(TermFrameKind.Input));
        Assert.That(Encoding.UTF8.GetString(text.Payload), Is.EqualTo("\u001b[200~hello\rworld\u001b[201~"), "bracketed, never splitting the Enter into it");

        var enter = await TermProtocol.ReadFrameAsync(server);
        Assert.That(enter.Kind, Is.EqualTo(TermFrameKind.Input));
        Assert.That(enter.Payload, Is.EqualTo("\r"u8.ToArray()), "a bare CR, a submit (RelayInput.IsSubmit), never inside the brackets");

        await send;
    }

    [Test]
    public async Task With_bracketed_paste_off_the_text_goes_plain()
    {
        var name = "mindattic-test-sendkeys-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var accept = server.WaitForConnectionAsync();

        var send = TermSendKeys.SendAsync(name, "go", async (pipeName, ct) =>
        {
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct).ConfigureAwait(false);
            return client;
        });
        await accept;

        await TermProtocol.ReadFrameAsync(server); // the viewer's Hello
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.None, [], [])));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Live, []));

        var text = await TermProtocol.ReadFrameAsync(server);
        Assert.That(Encoding.UTF8.GetString(text.Payload), Is.EqualTo("go"));
        var enter = await TermProtocol.ReadFrameAsync(server);
        Assert.That(enter.Payload, Is.EqualTo("\r"u8.ToArray()));

        await send;
    }

    [Test]
    public async Task An_already_exited_cli_is_reported_rather_than_typed_into()
    {
        var name = "mindattic-test-sendkeys-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var accept = server.WaitForConnectionAsync();

        var send = TermSendKeys.SendAsync(name, "go", async (pipeName, ct) =>
        {
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct).ConfigureAwait(false);
            return client;
        });

        await accept;
        await TermProtocol.ReadFrameAsync(server); // the viewer's Hello
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.None, [], [])));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Live, []));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Exit, TermProtocol.Exit(-1)));

        var ex = Assert.ThrowsAsync<IOException>(async () => await send);
        Assert.That(ex!.Message, Does.Contain("already exited"));
    }

    [Test]
    public async Task SendEnterAsync_writes_no_text_frame_only_the_bare_enter()
    {
        var name = "mindattic-test-sendkeys-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var accept = server.WaitForConnectionAsync();

        var send = TermSendKeys.SendAsync(name, "", async (pipeName, ct) =>
        {
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(ct).ConfigureAwait(false);
            return client;
        });
        await accept;

        await TermProtocol.ReadFrameAsync(server); // the viewer's Hello
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.BracketedPaste, [], [])));
        await server.WriteAsync(TermProtocol.Frame(TermFrameKind.Live, []));

        var enter = await TermProtocol.ReadFrameAsync(server);
        Assert.That(enter.Kind, Is.EqualTo(TermFrameKind.Input));
        Assert.That(enter.Payload, Is.EqualTo("\r"u8.ToArray()), "nothing pasted first, bracketed or not");

        await send;
    }
}
