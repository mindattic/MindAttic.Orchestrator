using System.Globalization;
using MindAttic.Orchestrator.Status;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// The daemon's reader of the ticket database's change feed (M.ORCH-T879, <see cref="TicketsDb.Activity"/>): every change any writer
/// makes (the tickets CLI, Tickets.Desktop, the Orchestrator) becomes a <see cref="TicketBusEvent"/> on the app bus within a couple of
/// seconds, so no writer has to publish for every app to hear it. It starts at the feed's newest event (what came before isn't
/// news), looks again only when the database's files moved (or every <see cref="ForceEvery"/>, for a database on SQL Server), and
/// skips a change the Orchestrator already published as it acted (<see cref="Published"/>). Not thread-safe: one poll at a time.
/// Nothing here throws into its caller.
/// </summary>
public sealed class TicketActivityFeed
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TicketBus");

    /// <summary>The most events one look reads; a burst past it is read on the next look.</summary>
    public const int Batch = 500;

    /// <summary>How often the feed is read even when no file moved (a database on SQL Server changes no file here).</summary>
    public static readonly TimeSpan ForceEvery = TimeSpan.FromSeconds(30);

    /// <summary>How long a change the Orchestrator published itself keeps the feed's copy of it from going out again.</summary>
    public static readonly TimeSpan EchoWindow = TimeSpan.FromMinutes(2);

    private readonly Dictionary<(string Id, string Event), DateTime> published = [];
    private long? after;
    private (DateTime, DateTime, long) stamp;
    private DateTime readUtc = DateTime.MinValue;
    private string? lastError;

    /// <summary>The newest event read so far; null until the first look.</summary>
    public long? After => after;

    /// <summary>Notes that the Orchestrator published <paramref name="change"/> itself, so the feed's copy is skipped.</summary>
    public void Published(TicketBusEvent change)
    {
        lock (published) published[Key(change)] = DateTime.UtcNow;
    }

    /// <summary>
    /// Reads what changed in <paramref name="workspaceRoot"/>'s ticket database since the last look and hands each change, oldest first,
    /// to <paramref name="publish"/>. The first look only notes where the feed is. Returns how many were published.
    /// </summary>
    public int Poll(string? workspaceRoot, Action<TicketBusEvent> publish, DateTime nowUtc)
    {
        try
        {
            var path = TicketsDb.DefaultPath(workspaceRoot);
            if (!File.Exists(path)) return 0;
            var now = Stamp(path);
            if (after is not null && now == stamp && nowUtc - readUtc < ForceEvery) return 0;
            using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
            if (db is null) return 0;
            var events = db.Activity(after ?? 0, after is null ? 1 : Batch);
            stamp = now;
            readUtc = nowUtc;
            lastError = null;
            if (after is null)
            {
                after = events.Count > 0 ? events[0].Seq : 0;
                return 0;
            }
            var sent = 0;
            // Newest first from the feed: published oldest first. A burst past the batch (an import) sends only its newest
            // changes, which is what a subscriber needs to refresh.
            var batch = events.OrderBy(e => e.Seq).ToList();
            foreach (var e in batch)
            {
                after = Math.Max(after ?? 0, e.Seq);
                var change = Of(e);
                if (WasPublished(change, nowUtc)) continue;
                publish(change);
                sent++;
            }
            return sent;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (ex.Message != lastError) log.Warning(ex, "Reading the ticket database's change feed failed; trying again");
            lastError = ex.Message;
            return 0;
        }
    }

    private bool WasPublished(TicketBusEvent change, DateTime nowUtc)
    {
        lock (published)
        {
            foreach (var old in published.Where(p => nowUtc - p.Value > EchoWindow).Select(p => p.Key).ToList()) published.Remove(old);
            return published.Remove(Key(change));
        }
    }

    private static (string, string) Key(TicketBusEvent c) => (c.Id.ToUpperInvariant(), c.Event);

    private static (DateTime, DateTime, long) Stamp(string path)
    {
        var wal = path + "-wal";
        return (File.GetLastWriteTimeUtc(path), File.Exists(wal) ? File.GetLastWriteTimeUtc(wal) : default, File.Exists(wal) ? new FileInfo(wal).Length : 0);
    }

    /// <summary>One change of the feed as a bus <see cref="TicketBusEvent"/>.</summary>
    public static TicketBusEvent Of(ActivityEvent e)
    {
        var (login, session) = e.Field == "working" ? ParseWorker(e.New) : (null, null);
        return new TicketBusEvent
        {
            Id = e.TicketId, Event = EventOf(e.Field, e.Old, e.New), Repo = e.Repo, Summary = e.Summary, Field = e.Field,
            Old = e.Old, New = e.New, Login = login, Session = session, Seq = e.Seq, Source = "tickets-db",
            AtUtc = DateTime.TryParse(e.At, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ? at : null,
        };
    }

    /// <summary>What a change to <paramref name="field"/> means (<see cref="TicketEvents"/>).</summary>
    public static string EventOf(string field, string? old, string? value) => field switch
    {
        "created" => TicketEvents.Filed,
        "approval" => value switch
        {
            TicketsDb.Approved => TicketEvents.Approved,
            TicketsDb.Rejected => TicketEvents.Rejected,
            _ => TicketEvents.Unapproved,
        },
        "status" => value switch
        {
            TicketRules.Closed => TicketEvents.Closed,
            TicketRules.WontDo => TicketEvents.WontDo,
            TicketRules.Open when old is not null && old != TicketRules.Open => TicketEvents.Reopened,
            _ => TicketEvents.Changed,
        },
        "working" => value is null ? TicketEvents.Released : TicketEvents.Claimed,
        "summary" => TicketEvents.Reworded,
        "assignee" => TicketEvents.Assigned,
        "priority" => TicketEvents.Prioritized,
        "link" => TicketEvents.Linked,
        _ => TicketEvents.Changed,
    };

    /// <summary>The engine's "login (session)", "login" or "session" (who works a ticket) split back into its parts.</summary>
    public static (string? Login, string? Session) ParseWorker(string? who)
    {
        if (string.IsNullOrWhiteSpace(who)) return (null, null);
        who = who.Trim();
        var open = who.IndexOf(" (", StringComparison.Ordinal);
        if (open > 0 && who.EndsWith(')')) return (who[..open], who[(open + 2)..^1]);
        return who.Contains('@') ? (who, null) : (null, who);
    }
}
