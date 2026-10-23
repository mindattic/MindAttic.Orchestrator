using System.Text.Json;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Settings;
using Microsoft.Data.Sqlite;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One open Claude CLI: where it runs, what it bills, whether it's in a Launcher tab, and whether /allocate is on in it
/// (with the repos it works, "*" for all).</summary>
public sealed record CliRow(
    int Pid,
    string SessionId,
    string Repo,
    string? Cwd,
    string? Bills,
    bool InLauncher,
    bool Autopilot,
    IReadOnlyList<string> AutopilotRepos,
    bool Idle,
    DateTime? LastTurnUtc);

/// <summary>The roster as written: when, and every open CLI.</summary>
public sealed record CliRosterFile(DateTime WrittenUtc, IReadOnlyList<CliRow> Clis);

/// <summary>
/// The ready list of every open Claude CLI and whether /allocate is on in it (M.ORCH-T474): the daemon rewrites its row in
/// orchestrator.db's <c>state</c> every tick (M.ORCH-T526; it was <c>~/.claude/session-context/clis.json</c>, which nothing
/// outside this repo read), so anything that wants it reads one row instead of rescanning every session's state and mark.
/// The row is left out of the dump: it changes every tick.
/// </summary>
public static class CliRoster
{
    /// <summary>The file the roster was before the move.</summary>
    public const string FileName = "clis.json";

    /// <summary>The roster's row in <c>state</c>.</summary>
    public const string StateName = "clis";

    /// <summary>A roster older than this wasn't written by a running daemon: build one instead of trusting it.</summary>
    public static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static IReadOnlyList<CliRow> Build(IReadOnlyList<CliBilling> clis, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilot,
        IEnumerable<SessionContext> sessions)
    {
        var turns = sessions.ToDictionary(s => s.SessionId, StringComparer.Ordinal);
        return [.. clis.Select(c =>
        {
            var on = autopilot.TryGetValue(c.Pid, out var repos);
            turns.TryGetValue(c.SessionId, out var turn);
            return new CliRow(c.Pid, c.SessionId, c.Where, c.Cwd, c.Bills, c.Host is not null, on, on ? repos! : [],
                turn?.Idle ?? false, turn is null || turn.TurnUtc == DateTime.MinValue ? null : turn.TurnUtc);
        })];
    }

    /// <summary>Every open CLI as this process sees it now.</summary>
    public static IReadOnlyList<CliRow> Build(ContextCycler cycler, BillingSwitch billing, IReadOnlyList<HostSession> hosts, DateTime nowUtc) =>
        Build(billing.LiveClis(hosts, nowUtc), cycler.AutopilotClis(), cycler.Load());

    /// <summary>Writes the roster as its <c>state</c> row; a <c>clis.json</c> left in <paramref name="oldDirectory"/> by a daemon
    /// from before the move is kept as <c>.bak</c> (what it held is a minute old by now, and this replaces it). A database that
    /// stays held past its timeout is an <see cref="IOException"/>, as a held file was.</summary>
    public static void Write(OrchestratorDb db, IReadOnlyList<CliRow> clis, DateTime nowUtc, string? oldDirectory = null)
    {
        try { db.SaveState(StateName, JsonSerializer.Serialize(new CliRosterFile(nowUtc, clis), Json)); }
        catch (System.Data.Common.DbException ex) { throw new IOException($"The orchestrator database couldn't be written: {ex.Message}", ex); }
        if (oldDirectory is null) return;
        var old = Path.Combine(oldDirectory, FileName);
        try { if (File.Exists(old)) File.Move(old, old + ".bak", overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // held now: next tick
    }

    /// <summary>The roster the daemon wrote, or null when there's none or it can't be read.</summary>
    public static CliRosterFile? Read(OrchestratorDb db)
    {
        try { return db.LoadState(StateName) is { } row ? JsonSerializer.Deserialize<CliRosterFile>(row.Value) : null; }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
