using System.ComponentModel;
using System.IO.Pipes;
using System.Text;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator clis send-keys &lt;TARGET&gt; &lt;TEXT&gt;</c> (M.ORCH-T2262): types TEXT then presses Enter in an
/// already-open CLI's input, from outside it -- the way a person pasting TEXT and then pressing Enter would -- so any
/// automation (psst, a scheduled task, a UI button), not just someone at the keyboard, can hand a CLI a command. TARGET is a
/// pid or a repo name, matched against the same roster <c>orchestrator clis</c> lists; it must resolve to exactly one open
/// CLI running under a TermHost (a dashboard-hosted tab, M.ORCH-T924), or the command fails with a clear message rather than
/// guessing.
/// <para>No idle/focus gating: it fires the moment it is called. M.ORCH-T864 moved automated nudges away from typing
/// straight into a CLI's input because that used to land mid-sentence over whatever the user was live-typing; this command
/// reintroduces direct injection by explicit design (confirmed with the user, 2026-11-26) for intentional, caller-initiated
/// use, never background nudging -- the same trust boundary the local pipe itself already carries (HostPipes.cs,
/// ElevatedPipeSecurity: same user, same session).</para>
/// </summary>
internal sealed class SendKeysCommand : AsyncCommand<SendKeysCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TARGET>")]
        [Description("A CLI's pid, or its repo name (orchestrator clis lists both); must match exactly one open, hosted CLI.")]
        public string Target { get; init; } = "";

        [CommandArgument(1, "<TEXT>")]
        [Description("Typed into the CLI's input, then Enter -- as if it were pasted and Enter pressed right after.")]
        public string Text { get; init; } = "";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var cycler = new ContextCycler();
        var now = DateTime.UtcNow;
        var roster = CliRoster.Read(Ctx.Store().Db) is { } r && now - r.WrittenUtc <= CliRoster.FreshFor
            ? r
            : new CliRosterFile(now, CliRoster.Build(cycler, new BillingSwitch(cycler.Directory), new HostSessionScanner(OrchestratorOptions.Load()).Scan(), now));

        var (row, cliError) = SendKeysTarget.ResolveCli(roster.Clis, settings.Target);
        if (row is null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(cliError!)}[/]"); return 1; }

        var (session, sessionError) = SendKeysTarget.ResolveSession(row, TermSessions.Enumerate());
        if (session is null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(sessionError!)}[/]"); return 1; }

        try
        {
            await SendKeys.SendAsync(session.Id, settings.Text).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or InvalidDataException or EndOfStreamException or UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine($"[red]Couldn't send to {Markup.Escape(row.Repo)} (pid {row.Pid}): {Markup.Escape(ex.Message)}.[/]");
            return 1;
        }
        AnsiConsole.MarkupLine($"[green]Sent to {Markup.Escape(row.Repo)} (pid {row.Pid}).[/]");
        return 0;
    }
}

/// <summary>Resolves <c>send-keys</c>'s TARGET to one open CLI and the hosted TermHost session it runs under (M.ORCH-T2262).</summary>
internal static class SendKeysTarget
{
    /// <summary>The roster row TARGET names: its pid when it parses as one, else its repo (case-insensitive, exact); null with
    /// why when it matches none or more than one open CLI.</summary>
    internal static (CliRow? Row, string? Error) ResolveCli(IReadOnlyList<CliRow> clis, string target)
    {
        target = target.Trim();
        if (target.Length == 0) return (null, "Name a target: a CLI's pid or repo (orchestrator clis lists them).");
        var matches = int.TryParse(target, out var pid)
            ? clis.Where(c => c.Pid == pid).ToList()
            : clis.Where(c => c.Repo.Equals(target, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            0 => (null, $"No open CLI matches {target} (orchestrator clis lists them)."),
            1 => (matches[0], null),
            _ => (null, $"{target} matches {matches.Count} open CLIs: name the pid instead."),
        };
    }

    /// <summary>The hosted TermHost session <paramref name="row"/>'s CLI runs under: the one whose host started it, directly
    /// or through processes between (its CLI is the host's ConPTY child). Null with why when none or more than one hosted
    /// session claims that pid.</summary>
    internal static (TermSessionRecord? Session, string? Error) ResolveSession(CliRow row, IReadOnlyList<TermSessionRecord> sessions,
        IReadOnlyDictionary<int, int>? parents = null)
    {
        var map = parents ?? ProcessTree.ParentMap();
        var matches = sessions.Where(s => ProcessTree.IsAncestor(s.HostPid, row.Pid, map)).ToList();
        return matches.Count switch
        {
            0 => (null, $"{row.Repo} (pid {row.Pid}) isn't running under a TermHost: send-keys only reaches a hosted CLI (a dashboard tab)."),
            1 => (matches[0], null),
            _ => (null, $"pid {row.Pid} matches {matches.Count} hosted sessions."),
        };
    }
}

/// <summary>
/// Connects to a TermHost session's pipe as a second viewer and types text then Enter into its CLI (M.ORCH-T2262): the same
/// frames a paste followed by a real Enter keystroke sends (<c>TerminalPaste</c>, M.ORCH-T868) -- TEXT as one Input frame
/// (bracketed when the CLI has bracketed paste on, from the Replay's modes), then a bare CR as an Input frame of its own,
/// never inside the bracket markers, so the CLI takes it as a submit rather than another pasted line
/// (<c>RelayInput.IsSubmit</c>).
/// </summary>
internal static class SendKeys
{
    /// <summary>How long connecting to the pipe, and the attach that follows, may each take.</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>How long, once attached, this waits for a Title or Exit frame the host already had queued behind Live (its
    /// Attach sends them right after it): long enough to catch an already-exited CLI, short enough never to wait on live
    /// output that may never come.</summary>
    internal static readonly TimeSpan TrailingFrameWindow = TimeSpan.FromMilliseconds(300);

    /// <summary>How this CLI names itself in its Hello (the host logs it).</summary>
    internal static string ViewerId => TermProtocol.LogSafeId($"send-keys-{Environment.ProcessId}");

    public static Task SendAsync(string sessionId, string text) => SendAsync(TermProtocol.PipeName(sessionId), text, ConnectPipeAsync);

    /// <summary>Connects (the seam <paramref name="connect"/> is a real pipe outside tests), attaches as an interactive
    /// viewer, and types <paramref name="text"/> then Enter.</summary>
    internal static async Task SendAsync(string pipeName, string text, Func<string, CancellationToken, Task<Stream>> connect)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        await using var stream = await connect(pipeName, timeout.Token).ConfigureAwait(false);

        // 0x0: never takes the session's size, and its typing can't apply one (ViewerPolicy.Join/OwnerSize, M.ORCH-T2302).
        await stream.WriteAsync(TermProtocol.Frame(TermFrameKind.Hello,
            TermProtocol.Hello(TermProtocol.Version, 0, 0, interactive: true, ViewerId)), timeout.Token).ConfigureAwait(false);

        var bracketed = false;
        var live = false;
        while (!live)
        {
            var (kind, payload) = await TermProtocol.ReadFrameAsync(stream, timeout.Token).ConfigureAwait(false);
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

        var pasted = TermProtocol.PastePayload(text, bracketed);
        if (pasted.Length > 0)
            await stream.WriteAsync(TermProtocol.Frame(TermFrameKind.Input, Encoding.UTF8.GetBytes(pasted)), timeout.Token).ConfigureAwait(false);
        await stream.WriteAsync(TermProtocol.Frame(TermFrameKind.Input, "\r"u8), timeout.Token).ConfigureAwait(false);
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
