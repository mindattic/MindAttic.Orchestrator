using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// A CLI claiming one named ticket it starts by hand at the user's request (M.ORCH-T1696, <c>orchestrator claim &lt;ID&gt;</c>).
/// Without it a hand-started ticket has no <see cref="Assignment"/>: the daemon's SyncWorking cleared its "working" mark within
/// seconds and another CLI's /allocate handed it out again. The claim is the same Assignment <c>orchestrator next --claim</c>
/// records (this CLI, its session, its login, the repo's own checkout), so /allocate skips it (<see cref="QueueItem.IsAssigned"/>)
/// and it's released the same ways: the ticket closing (the daemon's reap), <c>orchestrator release</c>, or the CLI going away.
/// </summary>
public static class NamedClaim
{
    /// <summary>What a claim came to: <see cref="Claimed"/>, or why not (<see cref="Message"/> is for the user).</summary>
    public sealed record Result(bool Claimed, string Message);

    /// <summary>
    /// Records ticket <paramref name="ticketId"/> of <paramref name="repo"/> (checkout <paramref name="repoPath"/>) as claimed by the
    /// CLI <paramref name="cli"/> (<paramref name="session"/>, on <paramref name="login"/>). Refused when another live CLI, or a
    /// running background agent, already holds it, or when the holder can't be told (no CLI recorded on it: release it first).
    /// A claim its own CLI made is refreshed; one whose CLI is gone is taken over. The check and the write are one step under the
    /// assignments lock.
    /// </summary>
    public static Result Claim(JsonStore store, string ticketId, string repo, string repoPath, int? cli, string? session, string? login,
        Func<int, bool> cliRunning, Func<Assignment, bool> pilotRunning, DateTime nowUtc)
    {
        string? refused = null;
        store.UpdateAssignments(all =>
        {
            var held = all.FirstOrDefault(a => a.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase));
            if (held is not null && !Mine(held, cli, session))
            {
                refused = Refusal(held, ticketId, cliRunning, pilotRunning);
                if (refused is not null) return;
            }
            all.RemoveAll(a => a.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase));
            // Its own claim is updated in place: a pilot, log, model or review it already carries (from next --claim) isn't lost.
            var account = Sessions.AccountDirs.Normalize(login);
            all.Add(held is not null && Mine(held, cli, session)
                ? held with { Cli = cli ?? held.Cli, CliSession = session ?? held.CliSession, Account = account ?? held.Account }
                : new Assignment(ticketId, repo, repoPath, Providers.Claude, nowUtc, true) { Cli = cli, CliSession = session, Account = account });
        });
        return refused is null
            ? new(true, $"{ticketId} is this CLI's: claimed" + (cli is { } c ? $" for CLI {c}" : "") +
                ", so /allocate and every other CLI skip it until it closes or is released (orchestrator release " + ticketId + ").")
            : new(false, refused);
    }

    private static bool Mine(Assignment a, int? cli, string? session) =>
        (cli is not null && a.Cli == cli) || (session is not null && a.CliSession == session);

    /// <summary>Why <paramref name="held"/> can't be taken; null when it can (its CLI is gone and no agent of its runs).</summary>
    private static string? Refusal(Assignment held, string id, Func<int, bool> cliRunning, Func<Assignment, bool> pilotRunning)
    {
        if (held.Pilot is not null && pilotRunning(held))
            return $"{id} is already being worked by a background agent (pid {held.Pilot}); it isn't this CLI's to take.";
        if (held.Cli is { } other)
            return cliRunning(other)
                ? $"{id} is already claimed by CLI {other}" + (held.Account is { } who ? $" ({who})" : "") + ", still running; leave it to that CLI."
                : null;
        return $"{id} is already assigned (a tab or agent of unknown owner); run orchestrator release {id} first if it isn't really being worked.";
    }
}
