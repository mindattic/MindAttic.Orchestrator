using System.Text.Json;
using MindAttic.Orchestrator.Billing;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One open Claude CLI: where it runs, what it bills, whether it's in a Launcher tab, and whether /autopilot is on in it
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
/// The ready list of every open Claude CLI and whether /autopilot is on in it (M.ORCH-T474): the daemon rewrites
/// <c>~/.claude/session-context/clis.json</c> every tick, so anything that wants it reads one file instead of rescanning
/// every session's state and mark.
/// </summary>
public static class CliRoster
{
    public const string FileName = "clis.json";

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

    public static void Write(string directory, IReadOnlyList<CliRow> clis, DateTime nowUtc)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var tmp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(new CliRosterFile(nowUtc, clis), Json));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>The roster the daemon wrote, or null when there's none or it can't be read.</summary>
    public static CliRosterFile? Read(string directory)
    {
        try { return JsonSerializer.Deserialize<CliRosterFile>(File.ReadAllText(Path.Combine(directory, FileName))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
}
