using System.Globalization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The active and accepting milestones' forecasts, read and recorded (M.ORCH-T1688). <see cref="Read"/> gathers what
/// <see cref="MilestoneForecast"/> needs and writes nothing:
/// <list type="bullet">
/// <item>throughput is <see cref="TicketStats"/>' (what <c>tickets ticket stats</c> shows): tickets closed per day in the
/// milestone's repos over the last <see cref="MilestoneForecast.WindowDays"/> days, without those filed already closed (work
/// logged after it was done never waited in a queue);</item>
/// <item>the average tokens per completed ticket is the Orchestrator's own measured cost (<see cref="TicketCosts"/>, what
/// <c>orchestrator tokens</c> reports) of the closed tickets in the milestone's repos, or of every closed ticket when fewer
/// than <see cref="TicketCosts.MinBasis"/> of those have one;</item>
/// <item>growth is the tickets on it now that joined after activation (a <c>member</c> event since then, on the ticket or
/// on something it's part of, or a ticket filed since then) against the tickets on it closed since then.</item>
/// </list>
/// <see cref="RecordDue"/> is the daemon's: it keeps each milestone's first projected date and its latest in the store
/// (<see cref="StateName"/>), which is the baseline a slip is measured against. Nothing else writes it.
/// </summary>
public sealed class MilestoneForecasts(JsonStore store)
{
    /// <summary>The store's record of each milestone's first and latest projection, by milestone ID.</summary>
    public const string StateName = "milestone-forecasts";

    /// <summary>How often the daemon works the forecasts out and records them.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromHours(1);

    /// <summary>A milestone's first projection and its latest, as the daemon recorded them.</summary>
    public sealed class Recorded
    {
        /// <summary>Gets or sets the date first projected.</summary>
        public DateTime? FirstProjectedUtc { get; set; }
        /// <summary>Gets or sets when the first projection was made.</summary>
        public DateTime? FirstAtUtc { get; set; }
        /// <summary>Gets or sets the date last projected.</summary>
        public DateTime? LatestProjectedUtc { get; set; }
        /// <summary>Gets or sets when the last projection was made.</summary>
        public DateTime? LatestAtUtc { get; set; }
        /// <summary>Gets or sets whether it was at risk when last recorded.</summary>
        public bool AtRisk { get; set; }

        /// <summary>The baseline a slip is measured against; null until a date has been projected.</summary>
        public MilestoneForecast.Baseline? Baseline =>
            FirstProjectedUtc is { } first ? new MilestoneForecast.Baseline(first, FirstAtUtc ?? first) : null;

        /// <summary>Takes <paramref name="forecast"/> as the latest: the first projected date is kept once there is one, and
        /// a forecast with no date (unknown, or nothing left) leaves both dates as they were.</summary>
        public void Take(MilestoneForecast.Result forecast, DateTime nowUtc)
        {
            if (forecast.ProjectedUtc is { } projected)
            {
                if (FirstProjectedUtc is null) (FirstProjectedUtc, FirstAtUtc) = (projected, nowUtc);
                (LatestProjectedUtc, LatestAtUtc) = (projected, nowUtc);
            }
            AtRisk = forecast.AtRisk;
        }
    }

    /// <summary>A milestone, its forecast now, and what the daemon has recorded of it (null: nothing yet).</summary>
    public sealed record Row(Milestone Milestone, MilestoneForecast.Result Forecast, Recorded? Record);

    private bool mayHaveRecords = true;

    /// <summary>What the daemon has recorded, by milestone ID; empty when there's nothing or the store can't be read now.</summary>
    public IReadOnlyDictionary<string, Recorded> Records() =>
        store.TryLoad<Dictionary<string, Recorded>>(StateName, out var state) ? new Dictionary<string, Recorded>(state, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, Recorded>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The forecast of each active and accepting milestone (or of <paramref name="only"/>), against its recorded
    /// baseline. With no such milestone it's one read of the ticket database. Nothing is written.</summary>
    public IReadOnlyList<Row> Read(TicketsDb db, DateTime nowUtc, Milestone? only = null)
    {
        List<Milestone> milestones = only is not null ? [only]
            : [.. db.Milestones().Where(m => m.State is MilestoneStates.Active or MilestoneStates.Accepting)];
        if (milestones.Count == 0) return [];

        var records = Records();
        var graph = db.Graph();
        var estimates = db.Estimates();
        var tickets = db.Tickets();
        var history = db.StatusHistory();
        var byTicket = history.ToLookup(e => e.TicketId, StringComparer.OrdinalIgnoreCase);
        var repoOf = tickets.GroupBy(t => t.Ticket.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        List<(string Repo, long Fresh)> costed = store.Db.TryLoadTicketCosts(out var sessions)
            ? [.. new TicketCosts(sessions).Tickets
                .Where(c => c.Fresh > 0 && repoOf.TryGetValue(c.TicketId, out var t) && t.Ticket.Status == TicketRules.Closed)
                .Select(c => (repoOf[c.TicketId].Repo, c.Fresh))]
            : [];

        var rows = new List<Row>();
        foreach (var m in milestones)
        {
            var repos = m.Repos.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var metrics = TicketStats.Compute(tickets.Where(t => repos.Contains(t.Repo)), history, StatsPeriod.Day, MilestoneForecast.WindowDays, nowUtc);
            var closed = Math.Max(0, metrics.Periods.Sum(p => p.Closed) - metrics.FiledClosed);
            var theirs = costed.Where(c => repos.Contains(c.Repo)).ToList();
            var pool = theirs.Count >= TicketCosts.MinBasis ? theirs : costed;
            var measured = new MilestoneForecast.Throughput(closed, Math.Max(1, (nowUtc - metrics.Periods[0].Start).TotalDays),
                pool.Count == 0 ? null : (long)pool.Average(c => (double)c.Fresh));

            var members = db.MilestoneMembers(m.Id, graph).Where(x => x.Counts).ToList();
            var open = members.Where(x => !x.IsDone)
                .Select(x => new MilestoneForecast.OpenTicket(x.Id, estimates.TryGetValue(x.Id, out var e) ? e : null)).ToList();
            var activated = Parse(m.Activated);
            var record = records.GetValueOrDefault(m.Id);
            rows.Add(new Row(m, MilestoneForecast.Compute(open, measured, nowUtc, activated, record?.Baseline,
                activated is { } since ? GrowthOf(db, m, members, graph, byTicket, since) : null), record));
        }
        return rows;
    }

    private static MilestoneForecast.Growth GrowthOf(TicketsDb db, Milestone m, List<MilestoneMember> members, TicketGraph graph,
        ILookup<string, TicketEvent> byTicket, DateTime since)
    {
        var joined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = db.MilestoneHistory(m.Id).Where(e => e.Field == "member" && Parse(e.At) > since)
            .GroupBy(e => e.New ?? e.Old ?? "", StringComparer.OrdinalIgnoreCase).Where(g => g.Key.Length > 0 && g.Last().New is not null);
        foreach (var item in added)
        {
            joined.Add(item.Key);
            joined.UnionWith(graph.Reach(item.Key, TicketsDb.PartOf, down: true));
        }
        var grown = members.Count(x => joined.Contains(x.Id)
            || byTicket[x.Id].FirstOrDefault(e => e.Field == "created") is { } filed && Parse(filed.At) > since);
        var closed = members.Count(x => x.IsDone
            && byTicket[x.Id].LastOrDefault(e => e.Field == "status" && e.New == TicketRules.Closed) is { } done && Parse(done.At) >= since);
        return new MilestoneForecast.Growth(grown, closed);
    }

    /// <summary>
    /// The daemon's pass: works each active and accepting milestone's forecast out and records its first and latest projected
    /// date; a line for each milestone whose at-risk mark changed. With no such milestone it's one read of the ticket database
    /// (and, once, the store's record cleared). Nothing at all when the ticket database or the store can't be read.
    /// </summary>
    public IReadOnlyList<MilestoneAcceptance.Line> RecordDue(Func<TicketsDb?> openTickets, DateTime nowUtc)
    {
        var lines = new List<MilestoneAcceptance.Line>();
        try
        {
            using var db = openTickets();
            if (db is null) return lines;
            var rows = Read(db, nowUtc);
            if (rows.Count == 0 && !mayHaveRecords) return lines;
            if (!store.TryLoad<Dictionary<string, Recorded>>(StateName, out var loaded) && store.Has(StateName)) return lines;
            var state = new Dictionary<string, Recorded>(loaded, StringComparer.OrdinalIgnoreCase);
            mayHaveRecords = rows.Count > 0;
            if (rows.Count == 0 && state.Count == 0) return lines;

            foreach (var gone in state.Keys.Where(id => !rows.Any(r => r.Milestone.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).ToList())
                state.Remove(gone);
            foreach (var row in rows)
            {
                if (!state.TryGetValue(row.Milestone.Id, out var record)) state[row.Milestone.Id] = record = new Recorded();
                var was = record.AtRisk;
                record.Take(row.Forecast, nowUtc);
                if (record.AtRisk != was)
                    lines.Add(new MilestoneAcceptance.Line(record.AtRisk, record.AtRisk
                        ? $"milestone forecast: {row.Milestone.Id} is at risk: {row.Forecast.Reason}"
                        : $"milestone forecast: {row.Milestone.Id} is no longer at risk"));
            }
            store.Save(StateName, state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or KeyNotFoundException or System.Data.Common.DbException) { }
        return lines;
    }

    private static DateTime? Parse(string? at) =>
        DateTime.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
}
