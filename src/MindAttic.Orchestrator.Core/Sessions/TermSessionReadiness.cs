using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Whether a CLI the dispatcher opened under TermHost (M.ORCH-T2406, M.ORCH-T2411) actually came up, and submitting its
/// pre-filled prompt once it has (M.ORCH-T2409): the TermHost-hosted counterpart of the wt.exe tab's own wait-and-press-Enter
/// (retired, M.ORCH-T2414) -- here the tell is the session's registry record (<see cref="TermSessions.Get(string, string?)"/>)
/// reading <see cref="TermSessionStates.Running"/>, confirmed <see cref="StillUpAfter"/> later, and the Enter goes through the
/// TermHost pipe (<see cref="TermSendKeys.SendEnterAsync"/>) rather than a Launcher control pipe.
/// <para>The registry lookup, the clock and the send are seams (the internal overloads), so tests drive this with a fake
/// registry and an instant clock and never touch a real TermHost.</para>
/// </summary>
public static class TermSessionReadiness
{
    /// <summary>How long a session found <see cref="TermSessionStates.Running"/> must stay that way before
    /// <see cref="WaitUpAsync(string, TimeSpan, CancellationToken)"/> trusts it: a CLI whose agent fails to start can register
    /// and exit again within a second or two.</summary>
    public static readonly TimeSpan StillUpAfter = TimeSpan.FromSeconds(5);

    /// <summary>How often <see cref="WaitUpAsync(string, TimeSpan, CancellationToken)"/> polls the registry while waiting for
    /// the session to first appear <see cref="TermSessionStates.Running"/>.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>Whether session <paramref name="sessionId"/> is up: its registry record reads <see cref="TermSessionStates.Running"/>
    /// and still does <see cref="StillUpAfter"/> later. False when it never shows up Running, or has already gone by then,
    /// within <paramref name="timeout"/>.</summary>
    public static Task<bool> WaitUpAsync(string sessionId, TimeSpan timeout, CancellationToken ct = default) =>
        WaitUpAsync(sessionId, timeout, id => TermSessions.Get(id), () => DateTime.UtcNow, Task.Delay, ct);

    /// <summary><see cref="WaitUpAsync(string, TimeSpan, CancellationToken)"/> with its registry lookup, clock and delay as
    /// seams.</summary>
    internal static async Task<bool> WaitUpAsync(string sessionId, TimeSpan timeout, Func<string, TermSessionRecord?> lookup,
        Func<DateTime> now, Func<TimeSpan, CancellationToken, Task> delay, CancellationToken ct = default)
    {
        var deadline = now() + timeout;
        while (true)
        {
            if (lookup(sessionId) is { State: TermSessionStates.Running })
            {
                await delay(StillUpAfter, ct).ConfigureAwait(false);
                return lookup(sessionId) is { State: TermSessionStates.Running };
            }
            if (now() >= deadline) return false;
            await delay(PollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits for session <paramref name="sessionId"/> up (<see cref="WaitUpAsync(string, TimeSpan, CancellationToken)"/>), then
    /// <paramref name="settle"/> so its agent has loaded whatever it was started with pre-filled, then presses Enter through
    /// the TermHost pipe. False -- the Enter is never sent -- when the session never came up within <paramref name="timeout"/>.
    /// </summary>
    public static Task<bool> SubmitWhenReadyAsync(string sessionId, TimeSpan settle, TimeSpan timeout, CancellationToken ct = default) =>
        SubmitWhenReadyAsync(sessionId, settle, timeout, id => TermSessions.Get(id), () => DateTime.UtcNow, Task.Delay,
            TermSendKeys.SendEnterAsync, ct);

    /// <summary><see cref="SubmitWhenReadyAsync(string, TimeSpan, TimeSpan, CancellationToken)"/> with its registry lookup,
    /// clock, delay and send as seams.</summary>
    internal static async Task<bool> SubmitWhenReadyAsync(string sessionId, TimeSpan settle, TimeSpan timeout,
        Func<string, TermSessionRecord?> lookup, Func<DateTime> now, Func<TimeSpan, CancellationToken, Task> delay,
        Func<string, Task> send, CancellationToken ct = default)
    {
        if (!await WaitUpAsync(sessionId, timeout, lookup, now, delay, ct).ConfigureAwait(false)) return false;
        await delay(settle, ct).ConfigureAwait(false);
        await send(sessionId).ConfigureAwait(false);
        return true;
    }
}
