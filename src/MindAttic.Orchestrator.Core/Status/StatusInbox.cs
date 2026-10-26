using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// The daemon's inbox on the status bus (M.ORCH-T872): a named pipe (<see cref="StatusStream.InboxPipeName"/>, this Windows user
/// only) that writers connect to, write one envelope a line, and leave, so what they did reaches the daemon the moment they
/// did it rather than at its next look at their files. A kind with a handler (<see cref="On"/>) is acted on here; any other is
/// handed to <see cref="Relay"/> (the status stream's <see cref="StatusStreamServer.Publish"/>), so an app can publish a kind of
/// its own to every subscriber (M.ORCH-T879: Tickets.Desktop's and Orchestrator.Desktop's links to each other). Several
/// instances listen at once so a burst of hooks rarely finds them all busy; a writer that does is told so at once and goes on
/// without (<see cref="TrySend"/>). Nothing here throws into its caller.
/// </summary>
public sealed class StatusInbox : IDisposable
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("StatusStream");
    private readonly Dictionary<string, Action<StatusEnvelope>> handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource stop = new();
    private readonly object creating = new();
    private int instances;

    /// <summary>A writer said a turn or a subagent ended: its tokens are in its transcript, so recount now.</summary>
    public const string Poke = "poke";

    /// <summary>A writer (a statusline, a probe) wrote a login's <c>claude-usage.json</c>: read it now.</summary>
    public const string UsageWritten = "usage-written";

    /// <summary>The pipe served; <see cref="StatusStream.InboxPipeName"/> by default.</summary>
    public string PipeName { get; init; } = StatusStream.InboxPipeName;

    /// <summary>How many connections are waited for at once.</summary>
    public int Listeners { get; init; } = 4;

    /// <summary>Where a kind with no handler goes; null drops it.</summary>
    public Action<StatusEnvelope>? Relay { get; init; }

    /// <summary>Acts on <paramref name="kind"/> with <paramref name="handler"/>, on the inbox's thread.</summary>
    public StatusInbox On(string kind, Action<StatusEnvelope> handler)
    {
        handlers[kind] = handler;
        return this;
    }

    /// <summary>Starts listening on the thread pool until disposed.</summary>
    public void Start()
    {
        for (var i = 0; i < Math.Max(1, Listeners); i++) _ = Task.Run(() => ListenAsync(stop.Token));
    }

    /// <summary>Hands one line to its handler or the relay. Never throws.</summary>
    internal void Take(string line)
    {
        if (line.Length == 0 || line.Length > StatusStream.MaxLine) return;
        try
        {
            if (StatusStream.Deserialize(line) is not { } envelope) return;
            if (handlers.TryGetValue(envelope.Kind, out var handle)) handle(envelope);
            else Relay?.Invoke(envelope);
        }
        catch (JsonException ex) { log.Debug("An unreadable inbox line was skipped: {Message:l}", ex.Message); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warning(ex, "Acting on an inbox message failed"); }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        var warned = false;
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            var counted = false;
            try
            {
                // The first instance claims the name, as the stream's does (M.ORCH-T1000): a second daemon (an `orchestrator daemon`
                // beside the logon one) joined this pipe and took about half of what writers sent. It listens once the first is gone.
                // Created one at a time, so no listener of this inbox joins another process's pipe while its first is being made.
                lock (creating)
                {
                    var first = instances == 0 ? PipeOptions.FirstPipeInstance : PipeOptions.None;
                    pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | first);
                    instances++;
                    counted = true;
                }
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                warned = false;
                // One writer at a time per listener; a writer that hangs open is let go after a few seconds.
                using var read = CancellationTokenSource.CreateLinkedTokenSource(ct);
                read.CancelAfter(TimeSpan.FromSeconds(5));
                using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
                while (await reader.ReadLineAsync(read.Token).ConfigureAwait(false) is { } line) Take(line.Trim());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { Release(); return; }
            catch (OperationCanceledException) { } // a writer held it open too long
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                // Taken by another process, or the writer went mid-line: said once while it keeps happening, retried.
                if (!warned) log.Information("The status inbox couldn't listen or read ({Message:l}); retrying", ex.Message);
                warned = true;
                // Released before the wait, so another listener here isn't told this inbox still holds the name.
                Release();
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false); } catch (OperationCanceledException) { return; }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { log.Warning(ex, "The status inbox failed; listening again"); }
            finally { Release(); }

            void Release()
            {
                pipe?.Dispose();
                pipe = null;
                if (!counted) return;
                counted = false;
                lock (creating) instances--;
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="envelope"/> to the inbox: best effort, for writers in .NET (the hooks are Python and write the same
    /// line themselves). No daemon, every listener busy, or a write that fails returns false at once; nothing is thrown.
    /// </summary>
    public static bool TrySend(StatusEnvelope envelope, string? pipeName = null, int timeoutMs = 100)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName ?? StatusStream.InboxPipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(timeoutMs);
            var bytes = Encoding.UTF8.GetBytes(StatusStream.Serialize(envelope) + "\n");
            pipe.Write(bytes);
            pipe.Flush();
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    public void Dispose() => stop.Cancel();
}
