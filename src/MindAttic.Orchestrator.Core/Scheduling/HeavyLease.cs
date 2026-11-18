using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>Where a heavy-process lease stands (<see cref="HeavyLease.State"/>).</summary>
public enum HeavyLeaseState
{
    /// <summary>Nothing holds it any more: the next heavy command takes it.</summary>
    Free,
    /// <summary>Just taken: the command is about to start, or its process hasn't been looked for yet.</summary>
    Starting,
    /// <summary>A process running the command is alive.</summary>
    Running,
    /// <summary>Taken a while ago with no process known for it yet: the next heavy command's hook looks for one, and frees
    /// the lease when there is none.</summary>
    Unconfirmed,
}

/// <summary>One heavy-process lease as the pace gate wrote it (the <c>holder</c> JSON of a <c>heavy-N</c> row of <c>leases</c>).</summary>
/// <param name="Name">The row's name, <c>heavy-1</c> up to the limit.</param>
/// <param name="Session">The CLI session that took it.</param>
/// <param name="Agent">The subagent that took it; empty for the session's main agent.</param>
/// <param name="AgentType">The subagent's type, when the hook was told it.</param>
/// <param name="CliPid">The CLI process the command runs under.</param>
/// <param name="CliStarted">When that process started (epoch seconds), so a reused pid isn't taken for it.</param>
/// <param name="GatePid">The pace gate process that took it, while it may still be holding the call.</param>
/// <param name="GateStarted">When that process started.</param>
/// <param name="Repo">The repo the command was run in.</param>
/// <param name="Cwd">The folder the command was run in.</param>
/// <param name="Login">The Claude login the CLI is on, when known.</param>
/// <param name="Command">The command.</param>
/// <param name="Pattern">The name of the heavy pattern it matched.</param>
/// <param name="Phase"><c>starting</c> until a process running the command is found, then <c>running</c>.</param>
/// <param name="At">When the lease was taken, or the gate let the call go (epoch seconds).</param>
/// <param name="Procs">The processes found running the command, each with its start time.</param>
public sealed record HeavyHolder(
    string Name, string Session, string Agent, string AgentType, long? CliPid, double? CliStarted, long? GatePid, double? GateStarted,
    string Repo, string Cwd, string Login, string Command, string Pattern, string Phase, double At,
    IReadOnlyList<(long Pid, double? Started)> Procs)
{
    /// <summary>Who holds it, in words: its repo, login, session, subagent and CLI.</summary>
    public string Who => string.Join(", ", new[]
    {
        Repo.Length > 0 ? "repo " + Repo : null,
        Login.Length > 0 ? "login " + Login : null,
        Session.Length > 0 ? "session " + (Session.Length > 8 ? Session[..8] : Session) : null,
        Agent.Length > 0 ? "subagent " + (AgentType.Length > 0 ? AgentType : Agent) : "its main agent",
        CliPid is { } cli ? "CLI pid " + cli : null,
    }.Where(x => x is not null));
}

/// <summary>
/// The heavy-process lease (M.ORCH-T1680): the agent standard's "one game player, engine build or long soak per machine at a
/// time", enforced. The pace gate takes it (<see cref="Setup.HeavyGate"/>) before a heavy shell command runs, as a
/// <c>heavy-N</c> row of orchestrator.db's <c>leases</c> (N up to <see cref="OrchestratorSettings.HeavyProcessLimit"/>); this
/// is the orchestrator's reading of those rows, for <c>orchestrator clis</c>. Like <see cref="DispatchLease"/>, a lease is
/// held by processes named by pid and start time, so one that exited or crashed holds nothing. Nothing here takes, frees or
/// kills anything: the hook does the first two, and nobody the third.
/// </summary>
public static class HeavyLease
{
    /// <summary>What every heavy lease's row name starts with.</summary>
    public const string Prefix = "heavy-";

    /// <summary>The default seconds a lease just taken counts as held before a process running its command must be found;
    /// live, it's <see cref="OrchestratorSettings.HeavyProcessGraceSeconds"/>.</summary>
    public const int GraceSeconds = OrchestratorSettings.DefaultHeavyProcessGraceSeconds;

    /// <summary>The default seconds a lease past its grace still counts as held while the machine's processes can't be listed;
    /// live, it's <see cref="OrchestratorSettings.HeavyProcessUnknownMaxSeconds"/>.</summary>
    public const int UnknownMaxSeconds = OrchestratorSettings.DefaultHeavyProcessUnknownMaxSeconds;

    /// <summary>The default seconds a lease's process may have started before the lease's own time (clocks read apart); live,
    /// it's <see cref="OrchestratorSettings.HeavyProcessSlackSeconds"/>.</summary>
    public const int SlackSeconds = OrchestratorSettings.DefaultHeavyProcessSlackSeconds;

    /// <summary>Every heavy lease row, held or not; empty when the database can't be read or is from before the table.</summary>
    public static IReadOnlyList<HeavyHolder> Read(OrchestratorDb db)
    {
        var rows = new List<HeavyHolder>();
        try
        {
            using var connection = db.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, holder FROM leases WHERE name LIKE $prefix ORDER BY name;";
            command.Parameters.AddWithValue("$prefix", Prefix + "%");
            using var r = command.ExecuteReader();
            while (r.Read())
                if (Parse(r.GetString(0), r.GetString(1)) is { } holder) rows.Add(holder);
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or UnauthorizedAccessException) { }
        return rows;
    }

    /// <summary>The leases that are held now, each with where it stands.</summary>
    /// <param name="graceSeconds"><see cref="OrchestratorSettings.HeavyProcessGraceSeconds"/>; <see cref="GraceSeconds"/> when null.</param>
    public static IReadOnlyList<(HeavyHolder Holder, HeavyLeaseState State)> Held(OrchestratorDb db, DateTime nowUtc, int? graceSeconds = null) =>
        [.. Read(db).Select(h => (h, State(h, nowUtc, graceSeconds: graceSeconds))).Where(x => x.Item2 != HeavyLeaseState.Free)];

    /// <summary>A row's holder JSON as the hook wrote it; null when it isn't a JSON object (the hook frees such a row).</summary>
    internal static HeavyHolder? Parse(string name, string holderJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(holderJson);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object) return null;
            var procs = new List<(long, double?)>();
            if (o.TryGetProperty("procs", out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var p in list.EnumerateArray())
                    if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() >= 1 && p[0].ValueKind == JsonValueKind.Number)
                        procs.Add(((long)p[0].GetDouble(), p.GetArrayLength() > 1 && p[1].ValueKind == JsonValueKind.Number ? p[1].GetDouble() : null));
            return new HeavyHolder(name, Text(o, "session"), Text(o, "agent"), Text(o, "agent_type"),
                Number(o, "cli") is { } cli ? (long)cli : null, Number(o, "cli_started"),
                Number(o, "gate") is { } gate ? (long)gate : null, Number(o, "gate_started"),
                Text(o, "repo"), Text(o, "cwd"), Text(o, "login"), Text(o, "command"), Text(o, "pattern"), Text(o, "phase"),
                Number(o, "at") ?? 0, procs);
        }
        catch (JsonException) { return null; }
    }

    private static string Text(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double? Number(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>
    /// Where a lease stands, by the hook's own rule: running while a process it's known to run is alive; before one is known,
    /// starting while the gate that took it still runs (it may be holding the call for the pace), then for
    /// <see cref="OrchestratorSettings.HeavyProcessGraceSeconds"/> while its CLI is alive; free once that CLI is gone. Past the
    /// grace with no process known it's unconfirmed: only the hook lists the machine's processes to settle it, when the next
    /// heavy command asks.
    /// </summary>
    /// <param name="alive">Whether the process with this pid and start time still runs; <see cref="DispatchLease.Alive"/> when null.</param>
    /// <param name="graceSeconds">The grace; <see cref="GraceSeconds"/> when null.</param>
    public static HeavyLeaseState State(HeavyHolder holder, DateTime nowUtc, Func<long, double?, bool>? alive = null, int? graceSeconds = null)
    {
        alive ??= (pid, started) => DispatchLease.Alive(pid, started);
        if (holder.Procs.Any(p => alive(p.Pid, p.Started))) return HeavyLeaseState.Running;
        if (holder.Phase != "starting") return HeavyLeaseState.Free;
        if (holder.GatePid is { } gate && alive(gate, holder.GateStarted)) return HeavyLeaseState.Starting;
        if (holder.CliPid is { } cli && !alive(cli, holder.CliStarted)) return HeavyLeaseState.Free;
        var age = (nowUtc - DateTime.UnixEpoch).TotalSeconds - holder.At;
        return age < (graceSeconds ?? GraceSeconds) ? HeavyLeaseState.Starting : HeavyLeaseState.Unconfirmed;
    }

    /// <summary>One line for a held lease: what is running, since when, and who holds it.</summary>
    public static string Describe(HeavyHolder holder, HeavyLeaseState state, DateTime nowUtc)
    {
        var command = string.Join(' ', holder.Command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (command.Length > 120) command = command[..117] + "...";
        var minutes = (int)Math.Max(0, ((nowUtc - DateTime.UnixEpoch).TotalSeconds - holder.At) / 60);
        var what = holder.Pattern.Length > 0 ? holder.Pattern : "a heavy process";
        var stands = state switch
        {
            HeavyLeaseState.Running => "running",
            HeavyLeaseState.Starting => "starting",
            _ => "no process seen for it yet; the next heavy command checks and frees it if it's over",
        };
        return $"{holder.Name}: {what} ({stands}), `{command}`, taken {(minutes < 1 ? "under a minute" : minutes + " min")} ago by {holder.Who}";
    }
}
