using System.IO.Pipes;
using System.Text;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Connects to a TermHost session's pipe as a second viewer and types text then Enter into its CLI (M.ORCH-T2262): the same
/// frames a paste followed by a real Enter keystroke sends (<c>TerminalPaste</c>, M.ORCH-T868) -- TEXT as one Input frame
/// (bracketed when the CLI has bracketed paste on, from the Replay's modes), then a bare CR as an Input frame of its own,
/// never inside the bracket markers, so the CLI takes it as a submit rather than another pasted line
/// (<c>RelayInput.IsSubmit</c>). Moved here from the Cli project's <c>send-keys</c> command (M.ORCH-T2409) so
/// <see cref="TermSessionReadiness"/>'s own bare-Enter submit, once a dispatched CLI comes up under TermHost (M.ORCH-T2406),
/// reuses the same attach dance rather than a second copy of it.
/// </summary>
public static class TermSendKeys
{
    /// <summary>How long connecting to the pipe (with its Hello), the attach that follows, and the final writes may each take.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>How long, once attached, this waits for a Title or Exit frame the host already had queued behind Live (its
    /// Attach sends them right after it): long enough to catch an already-exited CLI, short enough never to wait on live
    /// output that may never come.</summary>
    public static readonly TimeSpan TrailingFrameWindow = TimeSpan.FromMilliseconds(300);

    /// <summary>How this CLI names itself in its Hello (the host logs it).</summary>
    public static string ViewerId => TermProtocol.LogSafeId($"send-keys-{Environment.ProcessId}");

    /// <summary>Types <paramref name="text"/> into session <paramref name="sessionId"/>'s CLI, then presses Enter.</summary>
    public static Task SendAsync(string sessionId, string text) => SendAsync(TermProtocol.PipeName(sessionId), text, ConnectPipeAsync);

    /// <summary>Presses Enter in session <paramref name="sessionId"/>'s CLI with nothing typed first (<see cref="TermSessionReadiness"/>'s
    /// own submit, once a dispatched CLI is up and has settled): the same attach dance as <see cref="SendAsync(string, string)"/>, but
    /// with no text, so only the bare CR frame is written.</summary>
    public static Task SendEnterAsync(string sessionId) => SendAsync(sessionId, "");

    /// <summary>Connects (the seam <paramref name="connect"/> is a real pipe outside tests), attaches as an interactive
    /// viewer, and types <paramref name="text"/> then Enter.</summary>
    internal static async Task SendAsync(string pipeName, string text, Func<string, CancellationToken, Task<Stream>> connect)
    {
        using var connecting = new CancellationTokenSource(Timeout);
        await using var stream = await connect(pipeName, connecting.Token).ConfigureAwait(false);

        // 0x0: never takes the session's size, and its typing can't apply one (ViewerPolicy.Join/OwnerSize, M.ORCH-T2302).
        await stream.WriteAsync(TermProtocol.Frame(TermFrameKind.Hello,
            TermProtocol.Hello(TermProtocol.Version, 0, 0, interactive: true, ViewerId)), connecting.Token).ConfigureAwait(false);

        var bracketed = false;
        var live = false;
        using var attaching = new CancellationTokenSource(Timeout);
        while (!live)
        {
            var (kind, payload) = await TermProtocol.ReadFrameAsync(stream, attaching.Token).ConfigureAwait(false);
            switch (kind)
            {
                case TermFrameKind.Replay when TermProtocol.ReadReplay(payload) is { } replay:
                    bracketed = (replay.Modes & TermModes.BracketedPaste) != 0;
                    break;
                case TermFrameKind.Modes:
                    bracketed = (TermProtocol.ReadModes(payload) & TermModes.BracketedPaste) != 0;
                    break;
                case TermFrameKind.Exit:
                    throw new IOException($"That CLI has already exited (code {TermProtocol.ReadExit(payload)}).");
                case TermFrameKind.Live:
                    live = true;
                    break;
            }
        }

        using (var trailing = new CancellationTokenSource(TrailingFrameWindow))
        {
            try
            {
                while (true)
                {
                    var (kind, payload) = await TermProtocol.ReadFrameAsync(stream, trailing.Token).ConfigureAwait(false);
                    if (kind == TermFrameKind.Exit) throw new IOException($"That CLI has already exited (code {TermProtocol.ReadExit(payload)}).");
                }
            }
            // Nothing more was already queued within the window: the CLI is still running, and its live output isn't waited on.
            catch (OperationCanceledException) { }
        }

        using var writing = new CancellationTokenSource(Timeout);
        var pasted = TermProtocol.PastePayload(text, bracketed);
        if (pasted.Length > 0)
            await stream.WriteAsync(TermProtocol.Frame(TermFrameKind.Input, Encoding.UTF8.GetBytes(pasted)), writing.Token).ConfigureAwait(false);
        await stream.WriteAsync(TermProtocol.Frame(TermFrameKind.Input, "\r"u8), writing.Token).ConfigureAwait(false);
    }

    /// <summary>Connects to a TermHost pipe (current user only), giving up after <paramref name="ct"/> cancels (mirrors
    /// <c>RelayConnection.ConnectPipeAsync</c>, which the WPF dashboard's viewers use the same way).</summary>
    private static async Task<Stream> ConnectPipeAsync(string pipeName, CancellationToken ct)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(ct).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
