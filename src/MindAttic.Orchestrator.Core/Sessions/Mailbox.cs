using System.Data.Common;
using System.Globalization;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// One <c>agent_messages</c> row (M.ORCH-T1745, roadmap Phase 1): a durable, addressed handoff to an agent by its stable id.
/// </summary>
public sealed record AgentMessage
{
    /// <summary>Its id, <c>msg-</c> and 16 hex digits.</summary>
    public string Id { get; init; } = "";
    /// <summary>The agent it's for (an <c>agt-</c> id).</summary>
    public string To { get; init; } = "";
    /// <summary>Who sent it: an agent id, a login, or a name.</summary>
    public string From { get; init; } = "";
    /// <summary>The unit it's about (a ticket id); null: none.</summary>
    public string? Unit { get; init; }
    /// <summary>What it says.</summary>
    public string Body { get; init; } = "";
    /// <summary>When it was sent.</summary>
    public DateTime SentUtc { get; init; }
    /// <summary>When its agent was handed it; null: unread.</summary>
    public DateTime? ReadUtc { get; init; }

    /// <summary>Whether its agent has been handed it.</summary>
    public bool Read => ReadUtc is not null;
}

/// <summary>
/// The agent mailbox (M.ORCH-T1745): the <c>agent_messages</c> table in <c>orchestrator.db</c> (SQL Server once configured, else the
/// SQLite file, as <see cref="OrchestratorDb.OpenData"/> picks). <c>orchestrator mail send</c> leaves a message for an agent; the
/// daemon hands each live agent's unread mail to its CLI as a <c>nudge-&lt;pid&gt;-mail.txt</c> file (<see cref="CliNudges"/>),
/// which its next prompt takes, and marks it read once taken. It replaces model-addressed SendMessage for pipeline traffic.
/// </summary>
public sealed class Mailbox(OrchestratorDb db)
{
    /// <summary>The table's name.</summary>
    public const string Table = "agent_messages";

    /// <summary>The prefix every message id starts with.</summary>
    public const string Prefix = "msg-";

    /// <summary>The longest body a message takes, so one handoff can't flood a turn's context.</summary>
    public const int MaxBody = 8000;

    private const string Columns = "id, to_agent, from_agent, unit, body, sent_utc, read_utc";

    /// <summary>
    /// Leaves <paramref name="body"/> for agent <paramref name="to"/> from <paramref name="from"/>, about <paramref name="unit"/> when
    /// given. Throws <see cref="ArgumentException"/> for a bad agent id, a blank sender or body, or a body over <see cref="MaxBody"/>.
    /// </summary>
    public AgentMessage Send(string to, string from, string body, DateTime nowUtc, string? unit = null)
    {
        var agent = AgentIds.Normalize(to) ?? throw new ArgumentException($"\"{to}\" isn't an agent id.");
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (body.Length > MaxBody) throw new ArgumentException($"A message holds at most {MaxBody} characters; this one has {body.Length}.");
        var message = new AgentMessage
        {
            Id = Prefix + Guid.NewGuid().ToString("N")[..16],
            To = agent.ToLowerInvariant(),
            From = from.Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim().ToUpperInvariant(),
            Body = body.Trim(),
            SentUtc = Utc(nowUtc),
        };
        db.WriteData((connection, tx) => Sql.Upsert(connection, tx, Table, ["id"],
            ("id", message.Id), ("to_agent", message.To), ("from_agent", message.From), ("unit", message.Unit),
            ("body", message.Body), ("sent_utc", Text(message.SentUtc)), ("read_utc", null)), dump: false);
        return message;
    }

    /// <summary>Agent <paramref name="to"/>'s unread messages, oldest first.</summary>
    public IReadOnlyList<AgentMessage> Unread(string to)
    {
        using var connection = db.OpenData();
        return [.. Sql.Read(connection, null, $"SELECT {Columns} FROM {Table} WHERE to_agent = @to AND read_utc IS NULL;", Row,
                ("@to", to.Trim().ToLowerInvariant()))
            .OrderBy(m => m.SentUtc).ThenBy(m => m.Id, StringComparer.Ordinal)];
    }

    /// <summary>Every unread message, by the agent it's for, each oldest first.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<AgentMessage>> UnreadByAgent()
    {
        using var connection = db.OpenData();
        return Sql.Read(connection, null, $"SELECT {Columns} FROM {Table} WHERE read_utc IS NULL;", Row)
            .GroupBy(m => m.To, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AgentMessage>)[.. g.OrderBy(m => m.SentUtc).ThenBy(m => m.Id, StringComparer.Ordinal)],
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The messages, newest first: <paramref name="to"/>'s only when given, the read ones too with <paramref name="all"/>,
    /// at most <paramref name="limit"/>.</summary>
    public IReadOnlyList<AgentMessage> List(string? to = null, bool all = false, int limit = 50)
    {
        using var connection = db.OpenData();
        var where = new List<string>();
        var args = new List<(string, object?)>();
        if (!string.IsNullOrWhiteSpace(to)) { where.Add("to_agent = @to"); args.Add(("@to", to.Trim().ToLowerInvariant())); }
        if (!all) where.Add("read_utc IS NULL");
        var sql = $"SELECT {Columns} FROM {Table}" + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") + ";";
        return [.. Sql.Read(connection, null, sql, Row, [.. args])
            .OrderByDescending(m => m.SentUtc).ThenByDescending(m => m.Id, StringComparer.Ordinal).Take(Math.Max(1, limit))];
    }

    /// <summary>Marks the messages <paramref name="ids"/> read at <paramref name="nowUtc"/> (those already read keep their time);
    /// how many were marked.</summary>
    public int MarkRead(IEnumerable<string> ids, DateTime nowUtc)
    {
        var list = ids.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (list.Count == 0) return 0;
        var marked = 0;
        db.WriteData((connection, tx) =>
        {
            foreach (var id in list)
                marked += Sql.Run(connection, tx, $"UPDATE {Table} SET read_utc = @at WHERE id = @id AND read_utc IS NULL;",
                    ("@at", Text(Utc(nowUtc))), ("@id", id));
        }, dump: false);
        return marked;
    }

    /// <summary>What a CLI is handed for <paramref name="messages"/>: one line each, sender, unit and body, under a heading.</summary>
    public static string Nudge(IReadOnlyList<AgentMessage> messages)
    {
        var lines = new List<string>
        {
            $"Mail for this agent ({messages.Count} message{(messages.Count == 1 ? "" : "s")}, from `orchestrator mail`; reply with `orchestrator mail send <agent> \"...\"`):",
        };
        foreach (var m in messages)
            lines.Add($"- [{m.Id}] from {m.From}{(m.Unit is { } u ? $" about {u}" : "")}: {m.Body.ReplaceLineEndings(" ")}");
        return string.Join(Environment.NewLine, lines);
    }

    private static AgentMessage Row(DbDataReader r) => new()
    {
        Id = r.GetString(0),
        To = r.GetString(1),
        From = r.GetString(2),
        Unit = Sql.OptionalText(r, 3),
        Body = r.GetString(4),
        SentUtc = Parse(r.GetString(5)),
        ReadUtc = Sql.OptionalText(r, 6) is { } t ? Parse(t) : null,
    };

    private static DateTime Utc(DateTime t) => t.Kind switch
    {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
    };

    private static string Text(DateTime t) => Utc(t).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static DateTime Parse(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}
