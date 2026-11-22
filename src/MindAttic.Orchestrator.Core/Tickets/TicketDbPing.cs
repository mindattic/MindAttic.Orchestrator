using System.Diagnostics;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>One timed read of the ticket database: <see cref="Ms"/> from opening it to its first answer, null when it didn't
/// answer, with <see cref="Error"/> saying why.</summary>
public sealed record TicketDbRead(double? Ms, string? Error = null);

/// <summary>
/// Times one small read of the workspace's ticket database (M.ORCH-T2032, for the links probe's <c>link:latency</c>
/// <c>-&gt;tickets-db</c>): <see cref="TicketsDb.OpenIfMigrated"/>, which opens the store and asks it for its repo count, the same
/// open every refresh starts with. A pegged Azure SQL database made those reads take minutes and stalled the daemon's tick
/// (M.ORCH-T1922); this one read shows it in milliseconds before that. Only reads.
/// </summary>
public static class TicketDbPing
{
    /// <summary>Opens the database for <paramref name="workspaceRoot"/> (the default workspace when null) and times it. Never
    /// throws: a failure is a read with no milliseconds and the reason.</summary>
    public static TicketDbRead Time(string? workspaceRoot)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
            return db is null
                ? new(null, "the workspace has no ticket database yet (its tickets are still in markdown)")
                : new(watch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return new(null, ex.Message); }
    }

    /// <summary><see cref="Time"/> off the caller's thread, given up on after <paramref name="timeout"/> (the read itself can't be
    /// stopped mid-query, so it finishes on its own and is dropped).</summary>
    public static async Task<TicketDbRead> TimeAsync(string? workspaceRoot, TimeSpan timeout, CancellationToken ct)
    {
        var read = Task.Run(() => Time(workspaceRoot), CancellationToken.None);
        try { return await read.WaitAsync(timeout, ct).ConfigureAwait(false); }
        catch (TimeoutException) { return new(null, $"no answer in {timeout.TotalSeconds:0} s"); }
    }
}
