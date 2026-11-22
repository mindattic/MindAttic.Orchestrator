using System.Data.Common;
using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>
/// One owner-facing reminder that nags (M.ORCH-T1864, its store M.ORCH-T1932): a <c>reminders</c> row. Unlike the one-shot
/// <see cref="Reminder"/> (texted once at its time), a nag comes back again and again on a backoff schedule until it's
/// dismissed or its linked condition is met: <see cref="UntilTicket"/> closed (or won't-do). The first nag is due one
/// <see cref="Every"/> after it's added; after the k-th, the next is <see cref="Every"/> × <see cref="Backoff"/>^k later,
/// held to <see cref="MaxEvery"/> (<see cref="NagSchedule.Gap"/>).
/// </summary>
public sealed record Nag
{
    /// <summary>Its id: six hex characters, as the one-shot reminders have.</summary>
    public string Id { get; init; } = "";
    /// <summary>What it says.</summary>
    public string Message { get; init; } = "";
    /// <summary>The gap before the first nag, and the base the backoff grows from.</summary>
    public TimeSpan Every { get; init; }
    /// <summary>How much longer each gap is than the one before (1: the same every time).</summary>
    public double Backoff { get; init; } = NagSchedule.DefaultBackoff;
    /// <summary>The longest a gap grows to; null: <see cref="NagSchedule.LongestGap"/>.</summary>
    public TimeSpan? MaxEvery { get; init; }
    /// <summary>The ticket whose closing ends it (its id, e.g. <c>IP-T603</c>); null: it runs until dismissed.</summary>
    public string? UntilTicket { get; init; }
    /// <summary>When it was added.</summary>
    public DateTime CreatedUtc { get; init; }
    /// <summary>When it next nags.</summary>
    public DateTime NextUtc { get; init; }
    /// <summary>How many times it has nagged.</summary>
    public int Nags { get; init; }
    /// <summary>When it last nagged; null: not yet.</summary>
    public DateTime? LastNagUtc { get; init; }
    /// <summary>When it ended (dismissed, or its condition met); null while it's active.</summary>
    public DateTime? EndedUtc { get; init; }
    /// <summary>Why it ended: <see cref="NagSchedule.Dismissed"/>, or what met its condition ("IP-T603 is closed").</summary>
    public string? EndedWhy { get; init; }

    /// <summary>Whether it still nags.</summary>
    public bool Active => EndedUtc is null;
}

/// <summary>What <see cref="NagSchedule.Evaluate"/> makes of a nag at a moment.</summary>
public enum NagVerdict
{
    /// <summary>Active, its next nag still to come.</summary>
    Wait,
    /// <summary>Active and its time has come: nag now.</summary>
    Due,
    /// <summary>Its linked condition is met (its ticket closed): end it, don't nag.</summary>
    Met,
    /// <summary>Already ended (dismissed or met).</summary>
    Ended,
}

/// <summary>A nag's verdict, when it's next due (for <see cref="NagVerdict.Wait"/> and <see cref="NagVerdict.Due"/>) and why it
/// ends (for <see cref="NagVerdict.Met"/> and <see cref="NagVerdict.Ended"/>).</summary>
public readonly record struct NagCheck(NagVerdict Verdict, DateTime? NextUtc = null, string? Why = null);

/// <summary>
/// The backoff and until-ticket rules (M.ORCH-T1932), pure: no store, no clock, the ticket's status handed in, so the delivery
/// side (M.ORCH-T1934) and the CLI (M.ORCH-T1933) read a nag the same way.
/// </summary>
public static class NagSchedule
{
    /// <summary>The backoff a nag gets when none is given: each gap twice the last.</summary>
    public const double DefaultBackoff = 2;

    /// <summary>The longest any gap grows to when the nag names no <see cref="Nag.MaxEvery"/>: a nag is meant to come back.</summary>
    public static readonly TimeSpan LongestGap = TimeSpan.FromDays(7);

    /// <summary>The shortest <see cref="Nag.Every"/> taken: a nag is intermittent, never a tight loop.</summary>
    public static readonly TimeSpan ShortestEvery = TimeSpan.FromMinutes(1);

    /// <summary>The <see cref="Nag.EndedWhy"/> of a nag the owner dismissed.</summary>
    public const string Dismissed = "dismissed";

    /// <summary>The ticket statuses that meet an until-ticket condition (MindAttic.Tickets' closed and won't-do).</summary>
    public static readonly IReadOnlySet<string> Done = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { MindAttic.Tickets.Log.TicketRules.Closed, MindAttic.Tickets.Log.TicketRules.WontDo };

    /// <summary>
    /// The gap after <paramref name="nags"/> nags: <see cref="Nag.Every"/> × <see cref="Nag.Backoff"/>^<paramref name="nags"/>,
    /// held to the nag's <see cref="Nag.MaxEvery"/> (or <see cref="LongestGap"/>) and never shorter than its <see cref="Nag.Every"/>.
    /// </summary>
    public static TimeSpan Gap(Nag nag, int nags) => Gap(nag.Every, nag.Backoff, nag.MaxEvery, nags);

    /// <inheritdoc cref="Gap(Nag, int)"/>
    public static TimeSpan Gap(TimeSpan every, double backoff, TimeSpan? maxEvery, int nags)
    {
        var cap = maxEvery is { } m && m > TimeSpan.Zero ? m : LongestGap;
        if (every >= cap) return every;
        var growth = Math.Pow(Math.Max(1, backoff), Math.Max(0, nags));
        // Computed in minutes as a double, so a long run of nags grows past the cap rather than overflowing a TimeSpan.
        var minutes = every.TotalMinutes * growth;
        return double.IsFinite(minutes) && minutes < cap.TotalMinutes ? TimeSpan.FromMinutes(minutes) : cap;
    }

    /// <summary>
    /// What <paramref name="nag"/> is at <paramref name="nowUtc"/>: <see cref="NagVerdict.Ended"/> once ended;
    /// <see cref="NagVerdict.Met"/> when it names a ticket <paramref name="statusOf"/> says is closed or won't-do (checked before
    /// the time, so a nag whose ticket closed never nags again); else <see cref="NagVerdict.Due"/> once its
    /// <see cref="Nag.NextUtc"/> has come, <see cref="NagVerdict.Wait"/> before. A ticket whose status isn't known
    /// (<paramref name="statusOf"/> null or answering null: the tickets store unreadable, the ticket not found) doesn't end it:
    /// a nag errs toward nagging.
    /// </summary>
    public static NagCheck Evaluate(Nag nag, DateTime nowUtc, Func<string, string?>? statusOf = null)
    {
        if (!nag.Active) return new(NagVerdict.Ended, null, nag.EndedWhy);
        if (nag.UntilTicket is { Length: > 0 } ticket && statusOf?.Invoke(ticket) is { } status && Done.Contains(status))
            return new(NagVerdict.Met, null, $"{ticket} is {status}");
        return new(nag.NextUtc <= nowUtc ? NagVerdict.Due : NagVerdict.Wait, nag.NextUtc);
    }

    /// <summary><paramref name="nag"/> as it is once it has nagged at <paramref name="nowUtc"/>: one more nag, and the next one
    /// a <see cref="Gap(Nag, int)"/> later.</summary>
    public static Nag Nagged(Nag nag, DateTime nowUtc) =>
        nag with { Nags = nag.Nags + 1, LastNagUtc = nowUtc, NextUtc = nowUtc + Gap(nag, nag.Nags + 1) };

    /// <summary>Why <paramref name="every"/>, <paramref name="backoff"/> and <paramref name="maxEvery"/> can't make a nag; null
    /// when they can.</summary>
    public static string? Problem(TimeSpan every, double backoff, TimeSpan? maxEvery) =>
        every < ShortestEvery ? $"--every must be at least {ShortestEvery.TotalMinutes:0} minute"
        : !double.IsFinite(backoff) || backoff < 1 ? "--backoff must be 1 or more (1: the same gap every time)"
        : maxEvery is { } m && m < every ? "--max-every can't be shorter than --every"
        : null;
}

/// <summary>
/// The <c>reminders</c> table (M.ORCH-T1932) in <c>orchestrator.db</c> (SQL Server once configured, else the SQLite file, as
/// <see cref="OrchestratorDb.OpenData"/> picks): one row per <see cref="Nag"/>, kept after it ends so the dump shows when and
/// why. In the dump: a nag changes only when it nags, is added or ends.
/// </summary>
public sealed class NagBook(OrchestratorDb db)
{
    /// <summary>The table's name.</summary>
    public const string Table = "reminders";

    private const string Columns =
        "id, message, every_minutes, backoff, max_every_minutes, until_ticket, created_utc, next_utc, nags, last_nag_utc, ended_utc, ended_why";

    /// <summary>
    /// Adds a nag saying <paramref name="message"/>, first due one <paramref name="every"/> after <paramref name="nowUtc"/>,
    /// its gaps growing by <paramref name="backoff"/> up to <paramref name="maxEvery"/>, ended by <paramref name="untilTicket"/>
    /// closing when one is named. Throws <see cref="ArgumentException"/> for what <see cref="NagSchedule.Problem"/> refuses.
    /// </summary>
    public Nag Add(string message, TimeSpan every, DateTime nowUtc, double backoff = NagSchedule.DefaultBackoff,
        TimeSpan? maxEvery = null, string? untilTicket = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (NagSchedule.Problem(every, backoff, maxEvery) is { } problem) throw new ArgumentException(problem);
        nowUtc = Utc(nowUtc);
        Nag added = null!;
        db.WriteData((connection, tx) =>
        {
            var taken = Sql.Read(connection, tx, $"SELECT id FROM {Table};", r => r.GetString(0)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string id;
            do id = Guid.NewGuid().ToString("N")[..6];
            while (taken.Contains(id));
            added = new Nag
            {
                Id = id,
                Message = message.Trim(),
                Every = every,
                Backoff = backoff,
                MaxEvery = maxEvery,
                UntilTicket = string.IsNullOrWhiteSpace(untilTicket) ? null : untilTicket.Trim().ToUpperInvariant(),
                CreatedUtc = nowUtc,
                NextUtc = nowUtc + every,
            };
            Save(connection, tx, added);
        });
        return added;
    }

    /// <summary>The nag <paramref name="id"/> (any case); null when there's none.</summary>
    public Nag? Find(string id)
    {
        using var connection = db.OpenData();
        return Find(connection, null, id);
    }

    /// <summary>Every nag, active ones first, each group soonest (or latest ended) first.</summary>
    public IReadOnlyList<Nag> All()
    {
        using var connection = db.OpenData();
        return [.. Sql.Read(connection, null, $"SELECT {Columns} FROM {Table};", Row)
            .OrderBy(n => n.Active ? 0 : 1).ThenBy(n => n.Active ? n.NextUtc.Ticks : -(n.EndedUtc?.Ticks ?? 0)).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

    /// <summary>The nags still active, soonest first.</summary>
    public IReadOnlyList<Nag> Active()
    {
        using var connection = db.OpenData();
        return Sql.Read(connection, null, $"SELECT {Columns} FROM {Table} WHERE ended_utc IS NULL ORDER BY next_utc, id;", Row);
    }

    /// <summary>
    /// Evaluates every active nag at <paramref name="nowUtc"/> (<see cref="NagSchedule.Evaluate"/>): ends each whose condition
    /// is met, and returns the ones due now, soonest first, for the delivery side to send and then mark with
    /// <see cref="MarkNagged"/>. <paramref name="ended"/> gets each nag this ended, with why.
    /// </summary>
    public IReadOnlyList<Nag> Due(DateTime nowUtc, Func<string, string?>? statusOf, Action<Nag>? ended = null)
    {
        nowUtc = Utc(nowUtc);
        var due = new List<Nag>();
        foreach (var nag in Active())
            switch (NagSchedule.Evaluate(nag, nowUtc, statusOf))
            {
                case { Verdict: NagVerdict.Met, Why: var why }:
                    if (End(nag.Id, nowUtc, why!) is { } done) ended?.Invoke(done);
                    break;
                case { Verdict: NagVerdict.Due }:
                    due.Add(nag);
                    break;
            }
        return due;
    }

    /// <summary>Records that <paramref name="id"/> nagged at <paramref name="nowUtc"/> (<see cref="NagSchedule.Nagged"/>), on
    /// the row as it is now; the nag as saved, or null when it's gone or ended meanwhile.</summary>
    public Nag? MarkNagged(string id, DateTime nowUtc)
    {
        nowUtc = Utc(nowUtc);
        Nag? saved = null;
        db.WriteDataIf((connection, tx) =>
        {
            if (Find(connection, tx, id) is not { Active: true } nag) return false;
            saved = NagSchedule.Nagged(nag, nowUtc);
            Save(connection, tx, saved);
            return true;
        });
        return saved;
    }

    /// <summary>Dismisses the active nag <paramref name="id"/> (any case); the nag as ended, or null when there's no active one.</summary>
    public Nag? Dismiss(string id, DateTime nowUtc) => End(id, nowUtc, NagSchedule.Dismissed);

    /// <summary>Ends the active nag <paramref name="id"/> for <paramref name="why"/>; the nag as ended, or null when there's no
    /// active one.</summary>
    public Nag? End(string id, DateTime nowUtc, string why)
    {
        nowUtc = Utc(nowUtc);
        Nag? saved = null;
        db.WriteDataIf((connection, tx) =>
        {
            if (Find(connection, tx, id) is not { Active: true } nag) return false;
            saved = nag with { EndedUtc = nowUtc, EndedWhy = why };
            Save(connection, tx, saved);
            return true;
        });
        return saved;
    }

    /// <summary>Drops the nag <paramref name="id"/>'s row outright, active or not; false when there's none.</summary>
    public bool Delete(string id)
    {
        var changed = 0;
        db.WriteDataIf((connection, tx) =>
        {
            changed = Find(connection, tx, id) is { } nag ? Sql.Run(connection, tx, $"DELETE FROM {Table} WHERE id = @id;", ("@id", nag.Id)) : 0;
            return changed > 0;
        });
        return changed > 0;
    }

    private static Nag? Find(DbConnection connection, DbTransaction? tx, string id) =>
        Sql.Read(connection, tx, $"SELECT {Columns} FROM {Table} WHERE LOWER(id) = @id;", Row, ("@id", id.Trim().ToLowerInvariant())).FirstOrDefault();

    private static void Save(DbConnection connection, DbTransaction tx, Nag n) =>
        Sql.Upsert(connection, tx, Table, ["id"],
            ("id", n.Id), ("message", n.Message), ("every_minutes", n.Every.TotalMinutes), ("backoff", n.Backoff),
            ("max_every_minutes", n.MaxEvery?.TotalMinutes), ("until_ticket", n.UntilTicket), ("created_utc", Text(n.CreatedUtc)),
            ("next_utc", Text(n.NextUtc)), ("nags", (long)n.Nags), ("last_nag_utc", n.LastNagUtc is { } l ? Text(l) : null),
            ("ended_utc", n.EndedUtc is { } e ? Text(e) : null), ("ended_why", n.EndedWhy));

    private static Nag Row(DbDataReader r) => new()
    {
        Id = r.GetString(0),
        Message = r.GetString(1),
        Every = TimeSpan.FromMinutes(Sql.Real(r, 2)),
        Backoff = Sql.Real(r, 3),
        MaxEvery = Sql.OptionalReal(r, 4) is { } m ? TimeSpan.FromMinutes(m) : null,
        UntilTicket = Sql.OptionalText(r, 5),
        CreatedUtc = Parse(r.GetString(6)),
        NextUtc = Parse(r.GetString(7)),
        Nags = Sql.Int(r, 8),
        LastNagUtc = Sql.OptionalText(r, 9) is { } l ? Parse(l) : null,
        EndedUtc = Sql.OptionalText(r, 10) is { } e ? Parse(e) : null,
        EndedWhy = Sql.OptionalText(r, 11),
    };

    /// <summary>A time as given, read as UTC when its kind isn't said.</summary>
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
