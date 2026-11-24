using System.Data.Common;
using System.Globalization;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// One <c>agents</c> row (M.ORCH-T1742, roadmap Phase 1): an agent by its stable id (<see cref="AgentIds"/>), the role it plays,
/// the login it runs on, its runtime, its state, its last heartbeat and the unit it's on now.
/// </summary>
public sealed record Agent
{
    /// <summary>Its stable id, <c>agt-</c> and 16 hex digits.</summary>
    public string Id { get; init; } = "";
    /// <summary>Its role, by name (a role file's <c>name</c>, e.g. <c>reviewer</c>).</summary>
    public string Role { get; init; } = "";
    /// <summary>The login it runs on, by account email.</summary>
    public string Owner { get; init; } = "";
    /// <summary>What runs it: <c>claude</c>, <c>codex</c>, ...</summary>
    public string Runtime { get; init; } = AgentBook.DefaultRuntime;
    /// <summary>One of <see cref="AgentBook.States"/>.</summary>
    public string State { get; init; } = AgentBook.Idle;
    /// <summary>When it last said it's alive; null: not yet.</summary>
    public DateTime? HeartbeatUtc { get; init; }
    /// <summary>The unit it's on now (a ticket id); null: none.</summary>
    public string? Unit { get; init; }
    /// <summary>When it was made.</summary>
    public DateTime CreatedUtc { get; init; }
    /// <summary>When its state last changed.</summary>
    public DateTime StateUtc { get; init; }

    /// <summary>Whether it's retired (<see cref="AgentBook.Terminated"/>): it takes no more work.</summary>
    public bool Terminated => State == AgentBook.Terminated;
}

/// <summary>
/// The <c>agents</c> table (M.ORCH-T1742) in <c>orchestrator.db</c> (SQL Server once configured, else the SQLite file, as
/// <see cref="OrchestratorDb.OpenData"/> picks), behind <c>orchestrator agents new|list|pause|resume|retire</c> (M.ORCH-T1744).
/// A retired agent's row is kept, state <see cref="Terminated"/>, so its id still names it. Not in the dump: heartbeats churn.
/// </summary>
public sealed class AgentBook(OrchestratorDb db)
{
    /// <summary>The orchestrator database table the agents are kept in.</summary>
    public const string Table = "agents";

    /// <summary>The states an agent is in.</summary>
    public const string Idle = "idle", Running = "running", Paused = "paused", Terminated = "terminated";

    /// <summary>Every state, in the order they're listed.</summary>
    public static readonly IReadOnlyList<string> States = [Running, Idle, Paused, Terminated];

    /// <summary>The runtime an agent gets when none is named.</summary>
    public const string DefaultRuntime = "claude";

    private const string Columns = "id, role, owner, runtime, state, heartbeat_utc, unit, created_utc, state_utc";

    /// <summary>
    /// Adds an <see cref="Idle"/> agent playing <paramref name="role"/> on <paramref name="owner"/>'s login under
    /// <paramref name="runtime"/>, by a new id (or <paramref name="id"/> when given: an id a started CLI already carries).
    /// Throws <see cref="ArgumentException"/> for a blank role or owner, a bad id, or an id already taken.
    /// </summary>
    public Agent New(string role, string owner, DateTime nowUtc, string? runtime = null, string? unit = null, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (id is not null && AgentIds.Normalize(id) is null) throw new ArgumentException($"\"{id}\" isn't an agent id.");
        nowUtc = Utc(nowUtc);
        Agent added = null!;
        db.WriteData((connection, tx) =>
        {
            var chosen = AgentIds.Normalize(id);
            if (chosen is not null && Find(connection, tx, chosen) is not null) throw new ArgumentException($"{chosen} is already an agent.");
            if (chosen is null)
                do chosen = AgentIds.New();
                while (Find(connection, tx, chosen) is not null);
            added = new Agent
            {
                Id = chosen,
                Role = role.Trim().ToLowerInvariant(),
                Owner = owner.Trim().ToLowerInvariant(),
                Runtime = string.IsNullOrWhiteSpace(runtime) ? DefaultRuntime : runtime.Trim().ToLowerInvariant(),
                State = Idle,
                Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim().ToUpperInvariant(),
                CreatedUtc = nowUtc,
                StateUtc = nowUtc,
            };
            Save(connection, tx, added);
        }, dump: false);
        return added;
    }

    /// <summary>The agent <paramref name="id"/> (any case); null when there's none.</summary>
    public Agent? Find(string id)
    {
        using var connection = db.OpenData();
        return Find(connection, null, id);
    }

    /// <summary>Every agent, retired ones last, each group newest first.</summary>
    public IReadOnlyList<Agent> All()
    {
        using var connection = db.OpenData();
        return [.. Sql.Read(connection, null, $"SELECT {Columns} FROM {Table};", Row)
            .OrderBy(a => a.Terminated ? 1 : 0).ThenByDescending(a => a.CreatedUtc).ThenBy(a => a.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Moves agent <paramref name="id"/> to <paramref name="state"/>, when <see cref="Problem"/> allows it; the agent as saved and
    /// null for the problem, or null and why not.
    /// </summary>
    public (Agent? Agent, string? Problem) SetState(string id, string state, DateTime nowUtc)
    {
        nowUtc = Utc(nowUtc);
        Agent? saved = null;
        string? problem = null;
        db.WriteDataIf((connection, tx) =>
        {
            if (Find(connection, tx, id) is not { } agent) { problem = $"There's no agent {id.Trim()}."; return false; }
            if (Problem(agent.State, state) is { } why) { problem = $"{agent.Id} {why}."; return false; }
            saved = agent with { State = state, StateUtc = nowUtc, Unit = state == Terminated ? null : agent.Unit };
            Save(connection, tx, saved);
            return false; // not in the dump
        });
        return (saved, problem);
    }

    /// <summary>Why an agent in <paramref name="from"/> can't move to <paramref name="to"/>; null when it can. Paused only from
    /// idle or running, resumed (to idle) only from paused, retired from anything but retired.</summary>
    public static string? Problem(string from, string to) => to switch
    {
        Paused when from is Idle or Running => null,
        Paused => from == Paused ? "is already paused" : "is retired",
        Idle when from == Paused => null,
        Idle => from == Terminated ? "is retired" : "isn't paused",
        Terminated when from != Terminated => null,
        Terminated => "is already retired",
        _ => $"can't be set to {to}",
    };

    /// <summary>
    /// The roles an agent can play: the <c>roles</c> table's names and the role files under
    /// <c>&lt;workspace&gt;\mindattic-agent-standard\shared\roles\*.md</c> (M.ORCH-T1931, its README left out), lowercased.
    /// Empty when neither can be read.
    /// </summary>
    public IReadOnlySet<string> Roles(string? workspaceRoot)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var connection = db.OpenData();
            foreach (var name in Sql.Read(connection, null, "SELECT name FROM roles;", r => r.GetString(0))) roles.Add(name.Trim().ToLowerInvariant());
        }
        catch (DbException) { }
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            try
            {
                var folder = RolesFolder(workspaceRoot);
                if (Directory.Exists(folder))
                    foreach (var file in Directory.EnumerateFiles(folder, "*.md"))
                        if (Path.GetFileNameWithoutExtension(file) is var name && !name.Equals("README", StringComparison.OrdinalIgnoreCase))
                            roles.Add(name.ToLowerInvariant());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return roles;
    }

    /// <summary>Where the role files are in <paramref name="workspaceRoot"/>.</summary>
    public static string RolesFolder(string workspaceRoot) => Path.Combine(workspaceRoot, "mindattic-agent-standard", "shared", "roles");

    private static Agent? Find(DbConnection connection, DbTransaction? tx, string id) =>
        Sql.Read(connection, tx, $"SELECT {Columns} FROM {Table} WHERE LOWER(id) = @id;", Row, ("@id", id.Trim().ToLowerInvariant())).FirstOrDefault();

    private static void Save(DbConnection connection, DbTransaction tx, Agent a) =>
        Sql.Upsert(connection, tx, Table, ["id"],
            ("id", a.Id), ("role", a.Role), ("owner", a.Owner), ("runtime", a.Runtime), ("state", a.State),
            ("heartbeat_utc", a.HeartbeatUtc is { } h ? Text(h) : null), ("unit", a.Unit),
            ("created_utc", Text(a.CreatedUtc)), ("state_utc", Text(a.StateUtc)));

    private static Agent Row(DbDataReader r) => new()
    {
        Id = r.GetString(0),
        Role = r.GetString(1),
        Owner = r.GetString(2),
        Runtime = r.GetString(3),
        State = r.GetString(4),
        HeartbeatUtc = Sql.OptionalText(r, 5) is { } h ? Parse(h) : null,
        Unit = Sql.OptionalText(r, 6),
        CreatedUtc = Parse(r.GetString(7)),
        StateUtc = Parse(r.GetString(8)),
    };

    private static DateTime Utc(DateTime t) => t.Kind switch
    {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
    };

    private static string Text(DateTime t) => Utc(t).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static DateTime Parse(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
