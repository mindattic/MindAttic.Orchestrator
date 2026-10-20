using System.Diagnostics;
using MindAttic.Orchestrator.Settings;
using Microsoft.Data.Sqlite;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// Lets only one orchestrator process feed the /autopilot CLIs: the logon daemon and an open dashboard would
/// otherwise both type the same story into a CLI. It's a row of orchestrator.db's <c>leases</c> (M.ORCH-T526; it was a lock
/// file held open with no sharing) naming the process that holds it, by pid and start time: one that exited or crashed holds
/// nothing, so the next to ask takes it over, and a reused pid isn't taken for the holder. Taken in one write transaction, so
/// two processes asking at once can't both get it. Works from any thread.
/// </summary>
/// <param name="directory">The store's folder: where the old lock file was, and what finds the database (as <see cref="JsonStore"/> does).</param>
/// <param name="name">The lease: another name is a lease of its own (the daemons' <see cref="Sessions.LimitWaker"/> takes one).</param>
public sealed class DispatchLease(string directory, string name = "dispatch.lock", OrchestratorDb? db = null) : IDisposable
{
    private readonly string holder = $"{Environment.ProcessId}-{Guid.NewGuid():N}";
    private bool held;

    /// <summary>The lease's row in <c>leases</c>: its old lock file's name without <c>.lock</c>.</summary>
    public string Name { get; } = name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase) ? name[..^".lock".Length] : name;

    /// <summary>Where the lock file was: a process from before the move still holding it holds the lease.</summary>
    public string Path { get; } = System.IO.Path.Combine(directory, name);

    public OrchestratorDb Db { get; } = db ?? new OrchestratorDb(OrchestratorDb.StatePath(directory));

    public bool IsHeld => held;

    /// <summary>True when this process holds the lease, taking it if it's free. Asked again while held, it checks the row is
    /// still this one's (a database put back from git, say, can lose it), and says false once another live process has it.</summary>
    public bool TryAcquire()
    {
        try
        {
            if (held)
            {
                var row = Holder();
                if (row is null) return held = Take();
                if (row.Value.Holder == holder) return true;
                if (Alive(row.Value.Pid, row.Value.Started)) return held = false;
            }
            if (!OldLockFree()) return false;
            return held = Take();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            // The database held past its timeout: a holder keeps what it has, and nobody takes it now.
            return held;
        }
    }

    public void Dispose()
    {
        if (!held) return;
        held = false;
        try
        {
            Db.Write((connection, tx) => Run(connection, tx, "DELETE FROM leases WHERE name = $name AND holder = $holder;",
                ("$name", Name), ("$holder", holder)), dump: false);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException) { } // the next to ask sees this process gone
    }

    /// <summary>Takes the row unless a live process other than this one holds it; true when it's this one's.</summary>
    private bool Take()
    {
        var taken = false;
        Db.Write((connection, tx) =>
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = "SELECT holder, pid, started FROM leases WHERE name = $name;";
                command.Parameters.AddWithValue("$name", Name);
                using var r = command.ExecuteReader();
                if (r.Read() && r.GetString(0) != holder && Alive(r.GetInt64(1), r.IsDBNull(2) ? null : r.GetDouble(2))) return;
            }
            Run(connection, tx, "INSERT OR REPLACE INTO leases (name, holder, pid, started, taken_at) VALUES ($name, $holder, $pid, $started, $at);",
                ("$name", Name), ("$holder", holder), ("$pid", (long)Environment.ProcessId), ("$started", OwnStart),
                ("$at", (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds));
            taken = true;
        }, dump: false);
        return taken;
    }

    private (string Holder, long Pid, double? Started)? Holder()
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT holder, pid, started FROM leases WHERE name = $name;";
        command.Parameters.AddWithValue("$name", Name);
        using var r = command.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetInt64(1), r.IsDBNull(2) ? null : r.GetDouble(2)) : null;
    }

    /// <summary>Whether the process that took a lease still runs: its pid is live and started when the row says (within a
    /// second). This process's own pid is live: another lease object here holds it until it's disposed.</summary>
    internal static bool Alive(long pid, double? started)
    {
        // This process's own pid: another lease object here holds it, unless the row is from an earlier process that had
        // the same pid (a database carried over from before a restart).
        if (pid == Environment.ProcessId)
            return started is not { } then || OwnStart is not { } own || Math.Abs(own - then) < 1;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (process.HasExited) return false;
            return started is not { } at || Started(process) is not { } now || Math.Abs(now - at) < 1;
        }
        catch (ArgumentException) { return false; } // no such process
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return true; } // live, but not ours to look into
    }

    private static readonly double? OwnStart = Started(Process.GetCurrentProcess());

    private static double? Started(Process process)
    {
        try { return (process.StartTime.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds; }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { return null; }
    }

    /// <summary>The lock file the lease was before the move: deleted once nothing holds it; false while a process from before
    /// the move (an old daemon or dashboard not restarted yet) still holds it open, so the two can't both feed the CLIs.</summary>
    private bool OldLockFree()
    {
        try
        {
            if (File.Exists(Path)) File.Delete(Path);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void Run(SqliteConnection connection, SqliteTransaction tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (n, v) in parameters) command.Parameters.AddWithValue(n, v ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
}
