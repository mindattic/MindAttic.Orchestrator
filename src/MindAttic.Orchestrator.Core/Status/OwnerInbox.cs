using System.Globalization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// One thing waiting on the owner (M.ORCH-T1689): what kind it is, which one, what it says, since when, and the command or link
/// that answers it. <see cref="TicketId"/> is set when it is a ticket, so the dashboard opens it in Tickets Desktop.
/// </summary>
/// <param name="Kind">One of <see cref="OwnerInbox"/>'s kinds.</param>
/// <param name="Id">Which one within its kind: a ticket id, a login's email, a consent request's id.</param>
/// <param name="Title">What it is, in one line.</param>
/// <param name="Repo">The repo it belongs to; null for one that has none (a login, a consent request).</param>
/// <param name="SinceUtc">Since when it waits; null when that isn't known.</param>
/// <param name="Action">What the owner should do: a command to run, or where to answer it.</param>
public sealed record OwnerInboxItem(string Kind, string Id, string Title, string? Repo, DateTime? SinceUtc, string Action)
{
    /// <summary>The ticket it is about, opened in Tickets Desktop from the dashboard; null when it isn't a ticket.</summary>
    public string? TicketId { get; init; }

    /// <summary>Who it's assigned to now (<see cref="Snapshot.Assignees"/>, set in <see cref="OwnerInbox.Build"/> for every
    /// ticket item); null when it isn't a ticket or isn't assigned. What the Inbox's "Delegate" picker starts from.</summary>
    public string? Assignee { get; init; }

    /// <summary>The milestone it is about (M.ORCH-T1685); null when it isn't about one.</summary>
    public string? MilestoneId { get; init; }

    /// <summary>The ticket or epic waiting to join <see cref="MilestoneId"/>; null for anything but a pending addition.</summary>
    public string? MilestoneItem { get; init; }

    /// <summary>The figure an informational item reports, when it has one: a new figure is a new <see cref="Key"/>, so it is
    /// alerted once per change and not again while it stands.</summary>
    public string? Revision { get; init; }

    /// <summary>What names it across ticks: the alert is sent once per key (<see cref="OwnerInboxAlerts"/>).</summary>
    public string Key => Revision is null ? $"{Kind}|{Id}" : $"{Kind}|{Id}|{Revision}";
}

/// <summary>What the inbox is built from: all of it already loaded by the tick, the dashboard's refresh or the CLI, so building
/// the list reads nothing itself.</summary>
/// <param name="Snapshot">The workspace's snapshot.</param>
/// <param name="Plan">The plan made from it.</param>
/// <param name="NowUtc">When the list is built.</param>
public sealed record OwnerInboxInputs(Snapshot Snapshot, SchedulePlan Plan, DateTime NowUtc)
{
    /// <summary>The settings in force (how long a claim may sit quiet before it is stale).</summary>
    public OrchestratorSettings Settings { get; init; } = new();

    /// <summary>The <see cref="OrphanWatch.StoreName"/> row: tickets whose CLI or tab went dark mid-task.</summary>
    public IReadOnlyList<OrphanRecord> Orphans { get; init; } = [];

    /// <summary>The logins whose meter can't be read until the person signs in again (<see cref="UsageReport.NeedingSignIn"/>).</summary>
    public IReadOnlyCollection<string> SignInAgain { get; init; } = [];

    /// <summary>On a member's PC, the host's consent requests on their own CLIs (<c>orchestrator machine consents</c>); empty
    /// when the host wasn't asked.</summary>
    public IReadOnlyList<RemoteConsent> Consents { get; init; } = [];
}

/// <summary>
/// The owner inbox (M.ORCH-T1689, roadmap Phase 9): everything currently waiting on the owner, in one list, so nothing stalls
/// unseen. Each kind of item comes from one provider in <see cref="Providers"/>; a later phase adds a kind (an escalation, a
/// quarantined unit, a milestone acceptance) by adding a provider there. Providers only read <see cref="OwnerInboxInputs"/>.
/// </summary>
public static class OwnerInbox
{
    /// <summary>A ticket that went dark and isn't redispatched again, or has waited a stale period for a redispatch.</summary>
    public const string Orphaned = "orphaned";

    /// <summary>A ticket parked with the <c>waiting-on-owner</c> label.</summary>
    public const string WaitingOnOwner = "waiting-on-owner";

    /// <summary>A stale claim nobody picked up again.</summary>
    public const string Stale = "stale";

    /// <summary>An open ticket that isn't a fix and isn't approved.</summary>
    public const string Approval = "approval";

    /// <summary>A login that must sign in again.</summary>
    public const string SignIn = "sign-in";

    /// <summary>A member's consent request on the host.</summary>
    public const string Consent = "consent";

    /// <summary>A ticket or epic waiting to join an active or accepting milestone, until the owner approves or declines it.</summary>
    public const string MilestoneAddition = "milestone-addition";

    /// <summary>An active or accepting milestone that has grown since it was activated: told, not asked.</summary>
    public const string MilestoneGrowth = "milestone-growth";

    /// <summary>A source of items.</summary>
    public delegate IEnumerable<OwnerInboxItem> Provider(OwnerInboxInputs inputs);

    /// <summary>Every source, most pressing first: a ticket two of them name is listed once, by the first.</summary>
    public static IReadOnlyList<Provider> Providers { get; } = [Orphans, OwnerWaiting, StaleClaims, Approvals, MilestoneAdditions, SignIns, Consents, MilestoneGrowths];

    /// <summary>The items waiting on the owner, in <paramref name="providers"/>' order (<see cref="Providers"/> when null), each
    /// provider's own order kept; a ticket is listed once and so is a key. A provider that throws adds nothing.</summary>
    public static IReadOnlyList<OwnerInboxItem> Build(OwnerInboxInputs inputs, IEnumerable<Provider>? providers = null)
    {
        var items = new List<OwnerInboxItem>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tickets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers ?? Providers)
        {
            List<OwnerInboxItem> found;
            try { found = [.. provider(inputs)]; }
            catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
            foreach (var item in found)
            {
                if (item.TicketId is { } ticket && !tickets.Add(ticket)) continue;
                // Every ticket item gets its current assignee (M.ORCH-T1732), so the Delegate picker starts from who has it
                // instead of always "Unassigned": one lookup here, not one per provider.
                var withAssignee = item.TicketId is { } tid ? item with { Assignee = inputs.Snapshot.Assignees.GetValueOrDefault(tid) } : item;
                if (keys.Add(withAssignee.Key)) items.Add(withAssignee);
            }
        }
        return items;
    }

    /// <summary>The inputs for <paramref name="snapshot"/> and <paramref name="plan"/>, with the two state rows the inbox reads
    /// (the orphaned tickets and the meter probe's) loaded from <paramref name="store"/>. Never throws: a row that can't be read
    /// reads as empty.</summary>
    public static OwnerInboxInputs Read(JsonStore store, Snapshot snapshot, SchedulePlan plan, OrchestratorSettings settings, DateTime nowUtc,
        IReadOnlyList<RemoteConsent>? consents = null)
    {
        IReadOnlyList<OrphanRecord> orphans = [];
        try { orphans = OrphanWatch.Load(store); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        return new OwnerInboxInputs(snapshot, plan, nowUtc)
        {
            Settings = settings, Orphans = orphans, SignInAgain = UsageReport.NeedingSignIn(store.Db), Consents = consents ?? [],
        };
    }

    /// <summary>How long a stale or dark ticket may wait to be picked up again before it is the owner's: the stale period itself
    /// (<see cref="OrchestratorSettings.StaleAfterHours"/>), so no number of the inbox's own.</summary>
    internal static TimeSpan PickupGrace(OrchestratorSettings settings) =>
        TimeSpan.FromHours(Math.Clamp(settings.StaleAfterHours, 0, 24 * 36_525));

    /// <summary>Tickets that went dark mid-task (<see cref="OrphanWatch"/>): one the daemon gave up on at once; one still waiting
    /// for its redispatch once it has waited <see cref="PickupGrace"/> (allocate paused, no login with room, redispatching off).</summary>
    internal static IEnumerable<OwnerInboxItem> Orphans(OwnerInboxInputs i)
    {
        var open = OpenTickets(i.Snapshot);
        foreach (var r in i.Orphans.OrderBy(r => r.FirstSeenUtc))
        {
            if (!open.ContainsKey(r.TicketId) && !i.Snapshot.UnknownRepos.Contains(r.Repo)) continue; // closed since
            var waited = r.State == OrphanRecord.Orphaned && r.Why is not null && i.NowUtc - r.FirstSeenUtc >= PickupGrace(i.Settings);
            if (r.State != OrphanRecord.NeedsYou && !waited) continue;
            yield return new OwnerInboxItem(Orphaned, r.TicketId, $"{Title(i.Snapshot, open, r.TicketId)}: {OneLine(r.Why ?? "it went dark mid-task")}", r.Repo, r.FirstSeenUtc,
                $"orchestrator release {r.TicketId}") { TicketId = r.TicketId };
        }
    }

    /// <summary>Open tickets labelled <c>waiting-on-owner</c> (<see cref="Snapshot.OwnerWaiting"/>): held back until the label goes.</summary>
    internal static IEnumerable<OwnerInboxItem> OwnerWaiting(OwnerInboxInputs i)
    {
        var open = OpenTickets(i.Snapshot);
        foreach (var id in i.Snapshot.OwnerWaiting.Order(StringComparer.OrdinalIgnoreCase))
            if (open.TryGetValue(id, out var t))
                yield return new OwnerInboxItem(WaitingOnOwner, t.Ticket.Id, Title(i.Snapshot, open, id), t.Repo, Opened(t.Ticket.Date),
                    $"tickets ticket label remove {t.Ticket.Id} {OpenTicketSource.WaitingOnOwnerLabel}") { TicketId = t.Ticket.Id };
    }

    /// <summary>Stale claims (<see cref="SchedulePlan.Stale"/>) still not picked up <see cref="PickupGrace"/> after they went
    /// stale: a stale ticket is back in the queue, so one an /allocate CLI takes again within that time never shows here.
    /// <see cref="QueueItem.StaleSinceUtc"/> is the last activity, a stale period before the claim went stale, so the wait
    /// from it is the stale period and then the grace; the grace alone would list every stale claim at once (M.ORCH-T1805).</summary>
    internal static IEnumerable<OwnerInboxItem> StaleClaims(OwnerInboxInputs i)
    {
        var after = PickupGrace(i.Settings);
        foreach (var q in i.Plan.Stale.OrderBy(q => q.StaleSinceUtc))
        {
            if (q.StaleSinceUtc is not { } quietSince || i.NowUtc - quietSince < after + after) continue;
            yield return new OwnerInboxItem(Stale, q.Id, $"{TitleOf(q)}: its session went quiet and nobody has picked it up again", q.Repo, quietSince,
                $"orchestrator release {q.Id}") { TicketId = q.Id };
        }
    }

    /// <summary>Open tickets that aren't fixes and aren't approved (what <c>tickets ticket approve</c> with no ids lists), newest
    /// first as the queue orders them.</summary>
    internal static IEnumerable<OwnerInboxItem> Approvals(OwnerInboxInputs i)
    {
        foreach (var q in i.Plan.AwaitingApproval)
        {
            if (q.MayWork) continue; // held for another reason (its epic, the waiting-on-owner label), not for its own approval
            yield return new OwnerInboxItem(Approval, q.Id, TitleOf(q), q.Repo, Opened(q.Ticket.Date), $"tickets ticket approve {q.Id}") { TicketId = q.Id };
        }
    }

    /// <summary>Additions waiting for a locked milestone's owner (<see cref="Snapshot.MilestoneScopes"/>, M.ORCH-T1685), oldest
    /// first. Not a ticket item even when what waits is a ticket: the same ticket may also wait for its own approval.</summary>
    internal static IEnumerable<OwnerInboxItem> MilestoneAdditions(OwnerInboxInputs i)
    {
        var waiting = (i.Snapshot.MilestoneScopes ?? []).SelectMany(m => m.Waiting.Select(w => (Milestone: m, Waiting: w)));
        foreach (var (m, w) in waiting.OrderBy(x => x.Waiting.SinceUtc ?? DateTime.MaxValue))
        {
            var name = i.Snapshot.Names?.GetValueOrDefault(w.Item) is { Length: > 0 } named ? $" ({OneLine(named)})" : "";
            yield return new OwnerInboxItem(MilestoneAddition, $"{m.Id}/{w.Item}",
                $"{w.Item}{name} is waiting to join milestone {m.Id} ({m.Name}){(w.By is { } by ? $", proposed by {OneLine(by)}" : "")}", null, w.SinceUtc,
                $"tickets milestone approve|decline {m.Id} {w.Item} --by <WHO>") { MilestoneId = m.Id, MilestoneItem = w.Item };
        }
    }

    /// <summary>Active and accepting milestones that have grown since they were activated (M.ORCH-T1685), in milestone order:
    /// for the owner to know, with nothing to answer. The figure is its <see cref="OwnerInboxItem.Revision"/>.</summary>
    internal static IEnumerable<OwnerInboxItem> MilestoneGrowths(OwnerInboxInputs i)
    {
        foreach (var m in i.Snapshot.MilestoneScopes ?? [])
        {
            if (m.Grown <= 0) continue;
            var more = m.WaitingTickets > 0 ? $", {m.WaitingTickets} more waiting for approval" : "";
            yield return new OwnerInboxItem(MilestoneGrowth, m.Id,
                $"Milestone {m.Id} ({m.Name}) has grown by {m.Grown} ticket{(m.Grown == 1 ? "" : "s")} since it was activated{more}", null, null,
                "orchestrator milestone list") { MilestoneId = m.Id, Revision = $"+{m.Grown}/{m.WaitingTickets}" };
        }
    }

    /// <summary>Logins whose meter can't be read until the person signs in again (M.ORCH-T1119).</summary>
    internal static IEnumerable<OwnerInboxItem> SignIns(OwnerInboxInputs i)
    {
        foreach (var login in i.SignInAgain.Order(StringComparer.OrdinalIgnoreCase))
        {
            var email = login.Contains('@') ? login : null;
            yield return new OwnerInboxItem(SignIn, login, $"{login} must sign in again: Claude's meter can't be read until then", null, null,
                email is null ? "orchestrator login" : $"orchestrator login --account {email}");
        }
    }

    /// <summary>On a member's PC, who is asking to type into one of their CLIs on the host; one past its deadline is gone.</summary>
    internal static IEnumerable<OwnerInboxItem> Consents(OwnerInboxInputs i)
    {
        foreach (var c in i.Consents.OrderBy(c => c.DeadlineUtc))
        {
            if (c.DeadlineUtc <= i.NowUtc) continue;
            yield return new OwnerInboxItem(Consent, c.Id, $"{c.Viewer} asks to type into your CLI {c.Session} on the host (answer by {c.DeadlineUtc.ToLocalTime():HH:mm:ss})",
                null, c.AskedUtc, $"orchestrator machine consent {c.Id} allow|deny");
        }
    }

    /// <summary>What the kind reads as in a list.</summary>
    public static string KindLabel(string kind) => kind switch
    {
        Approval => "Approve",
        WaitingOnOwner => "Waiting on you",
        Stale => "Stale",
        Orphaned => "Orphaned",
        SignIn => "Sign in",
        Consent => "Consent",
        MilestoneAddition => "Milestone addition",
        MilestoneGrowth => "Milestone growth",
        _ => kind,
    };

    private static Dictionary<string, (Ticket Ticket, string Repo)> OpenTickets(Snapshot snapshot)
    {
        var open = new Dictionary<string, (Ticket, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in snapshot.Repos)
            foreach (var t in r.OpenTickets)
                open.TryAdd(t.Id, (t, r.Name));
        return open;
    }

    private static string Title(Snapshot snapshot, Dictionary<string, (Ticket Ticket, string Repo)> open, string id) =>
        snapshot.Names?.GetValueOrDefault(id) is { Length: > 0 } name ? name
        : open.TryGetValue(id, out var t) && t.Ticket.Summary.Length > 0 ? OneLine(t.Ticket.Summary) : id;

    private static string TitleOf(QueueItem q) => q.Name is { Length: > 0 } name ? name : q.Ticket.Summary.Length > 0 ? OneLine(q.Ticket.Summary) : q.Id;

    private static string OneLine(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 160 ? line : line[..159] + "…";
    }

    /// <summary>A ticket's opened date (<c>yyyy-MM-dd</c>, local) as a time; null when it isn't one.</summary>
    private static DateTime? Opened(string date) =>
        DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
}

/// <summary>
/// The owner inbox's alerts (M.ORCH-T1689): which items are new since the last look, each said once and never again. What was
/// already said is kept in the store's <see cref="StateName"/> row, so a restart (or a second daemon) doesn't say it twice. The
/// first look only notes what is there: a backlog that predates the inbox is not texted.
/// </summary>
public sealed class OwnerInboxAlerts(JsonStore store)
{
    /// <summary>The <c>state</c> row that remembers which items were alerted.</summary>
    public const string StateName = "owner-inbox-alerted";

    /// <summary>How long an item no longer in the inbox is remembered: one that comes back within it isn't alerted again.</summary>
    internal static readonly TimeSpan Remembered = TimeSpan.FromDays(90);

    /// <summary>The most items one text names; the rest are counted.</summary>
    internal const int Named = 3;

    /// <summary>The row: each alerted item's key and when it was first seen.</summary>
    public sealed class Alerted
    {
        /// <summary>False until the first look has noted what was already waiting.</summary>
        public bool Seeded { get; set; }

        /// <summary>Item key (<see cref="OwnerInboxItem.Key"/>) → when it was first seen.</summary>
        public Dictionary<string, DateTime> Keys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The items in <paramref name="items"/> not seen before, each returned exactly once across ticks, restarts and processes
    /// (they are claimed in one write transaction). The first look returns none. Whether a text is sent for them is the caller's
    /// setting: they are noted as seen either way, so turning alerts on later doesn't text a backlog. Empty when the row can't be
    /// read or written now (they are found again at the next look).
    /// </summary>
    public IReadOnlyList<OwnerInboxItem> Fresh(IReadOnlyList<OwnerInboxItem> items, DateTime nowUtc)
    {
        try
        {
            // The usual look changes nothing: no write then.
            var has = store.Has(StateName);
            if (has && (!store.TryLoad<Alerted>(StateName, out var seen) || !Changes(seen, items, nowUtc))) return [];
            List<OwnerInboxItem> fresh = [];
            store.Update<Alerted>(StateName, state =>
            {
                state.Keys = new Dictionary<string, DateTime>(state.Keys, StringComparer.OrdinalIgnoreCase);
                var current = items.Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var gone in state.Keys.Where(k => !current.Contains(k.Key) && nowUtc - k.Value > Remembered).Select(k => k.Key).ToList())
                    state.Keys.Remove(gone);
                foreach (var item in items)
                    if (state.Keys.TryAdd(item.Key, nowUtc) && state.Seeded) fresh.Add(item);
                state.Seeded = true;
            });
            return fresh;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException)
        {
            return [];
        }
    }

    private static bool Changes(Alerted seen, IReadOnlyList<OwnerInboxItem> items, DateTime nowUtc)
    {
        var known = seen.Keys.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!seen.Seeded || items.Any(i => !known.Contains(i.Key))) return true;
        var current = items.Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return seen.Keys.Any(k => !current.Contains(k.Key) && nowUtc - k.Value > Remembered);
    }

    /// <summary>The one text for <paramref name="fresh"/> (not empty): a single item with what to do about it; several named up to
    /// <see cref="Named"/>, the rest counted, and where the list is.</summary>
    public static string Text(IReadOnlyList<OwnerInboxItem> fresh)
    {
        if (fresh.Count == 1) return $"Orchestrator inbox: {Line(fresh[0])}. {fresh[0].Action}";
        var named = string.Join("; ", fresh.Take(Named).Select(Line));
        var more = fresh.Count > Named ? $" (+{fresh.Count - Named} more)" : "";
        return $"Orchestrator inbox: {fresh.Count} new items wait on you: {named}{more}. orchestrator inbox lists them.";
    }

    private static string Line(OwnerInboxItem item)
    {
        var title = item.Title.Length <= 90 ? item.Title : item.Title[..89] + "…";
        return item.TicketId is { } id ? $"{OwnerInbox.KindLabel(item.Kind)} {id} ({title})" : $"{OwnerInbox.KindLabel(item.Kind)}: {title}";
    }
}
