using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// The ticket database's change version (M.ORCH-T1938): SQL Server's <c>CHANGE_TRACKING_CURRENT_VERSION()</c>, which every commit
/// to any table moves (change tracking is on for every table, M.TIX-T344). One scalar query, so a refresh that finds it where its
/// last read left it keeps that read instead of reading the whole tickets, links and labels tables again: those reads pegged the
/// Basic-tier Azure database at 100% CPU (M.ORCH-T1922). Null when there's no version to go by (the file is the store, change
/// tracking is off, or the server can't be asked): the caller then reads as it did before, on its file time and age.
/// </summary>
public static class TicketDbVersion
{
    /// <summary>How long a read stands while the version hasn't moved: a safety net for a change the version somehow missed.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    /// <summary>The version of the store kept for the database file at <paramref name="path"/>; null when there's none to go by.
    /// Read it before the rows it stands for, so a change landing mid-read moves it past them and is read next time.</summary>
    public static long? Of(string path)
    {
        try
        {
            if (TicketsServer.For(path) is not { } server) return null;
            using var connection = server.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CHANGE_TRACKING_CURRENT_VERSION()";
            return cmd.ExecuteScalar() is long version ? version : null;
        }
        // Anything at all (a sign-in, the network, a server that's resuming): no version, so the caller reads in full as before
        // and its own read reports what went wrong.
        catch (Exception) { return null; }
    }

    /// <summary>Whether a read made at <paramref name="readUtc"/> at version <paramref name="read"/> still stands at version
    /// <paramref name="now"/>: both known, equal, and younger than <see cref="MaxAge"/>.</summary>
    public static bool Unchanged(long? read, long? now, DateTime readUtc, DateTime nowUtc) =>
        read is { } r && now is { } n && r == n && nowUtc - readUtc < MaxAge;
}
