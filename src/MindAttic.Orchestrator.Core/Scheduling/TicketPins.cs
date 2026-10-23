using System.Globalization;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// Tickets assigned to a user (M.ORCH-T668). Assigning a ticket (or reassigning it to another user) records that user's login,
/// by its account email, in <see cref="OrchestratorSettings.Assignees"/>; from then on it's a pin: <c>orchestrator next --claim</c>
/// hands it only to a CLI on that login (<see cref="MayTake"/>), Start opens its CLI only on that login, and its claim or tab's
/// <see cref="Assignment.Account"/> says so. While its user is out of room (<see cref="OutOfRoom"/>) it waits for them, its
/// card saying why ("Waiting for Ryan: week at 97%"), and the weekly-aware pacing's redelegation (M.ORCH-T666) leaves it alone
/// (<see cref="Redelegable"/>), unless "Redelegate now" (<see cref="Redelegate"/>) drops the pin, so it's handed on like any
/// unassigned ticket: to another user with room, else the API key. An unassigned ticket keeps that redelegation.
/// </summary>
public static class TicketPins
{
    /// <summary>A change to a ticket's assignment: who it was assigned to before and who it is now (null: nobody).</summary>
    public sealed record Change(string TicketId, string? From, string? To)
    {
        /// <summary>What it did, in words, by the users' emails.</summary>
        public string Text => (From, To) switch
        {
            (null, null) => $"{TicketId} wasn't assigned to anyone.",
            (null, { } to) => $"{TicketId} is assigned to {to}: it's dispatched only on that login.",
            ({ } from, null) => $"{TicketId} is no longer assigned to {from}: it's handed out like any unassigned ticket.",
            ({ } from, { } to) when from == to => $"{TicketId} was already assigned to {to}.",
            ({ } from, { } to) => $"{TicketId} is reassigned from {from} to {to}: it's dispatched only on {to}'s login.",
        };
    }

    /// <summary>The assignments in <paramref name="settings"/>, ticket id (any case) → the user's normalized email; an entry
    /// whose email isn't one is left out.</summary>
    public static IReadOnlyDictionary<string, string> Assignees(OrchestratorSettings settings)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, email) in settings.Assignees)
            if (AccountDirs.Normalize(email) is { } e) map[id.Trim()] = e;
        return map;
    }

    /// <summary>Who ticket <paramref name="ticketId"/> is assigned to, or null.</summary>
    public static string? AssigneeOf(OrchestratorSettings settings, string ticketId) => Assignees(settings).GetValueOrDefault(ticketId.Trim());

    /// <summary>Assigns ticket <paramref name="ticketId"/> to the user signed in as <paramref name="email"/>, or reassigns it from
    /// whoever had it: records their login, in one write so another process's change isn't lost. Throws
    /// <see cref="ArgumentException"/> when <paramref name="email"/> isn't an email address.</summary>
    public static Change Assign(JsonStore store, string ticketId, string email)
    {
        var to = AccountDirs.Normalize(email) ?? throw new ArgumentException($"\"{email}\" isn't an email address.", nameof(email));
        return Set(store, ticketId, to);
    }

    /// <summary>Takes ticket <paramref name="ticketId"/> off whoever it was assigned to.</summary>
    public static Change Unassign(JsonStore store, string ticketId) => Set(store, ticketId, null);

    /// <summary>"Redelegate now" on a pinned ticket waiting for its user: the pin is dropped, so it's handed on like an unassigned
    /// ticket (another user with room, else the API key), and the pacing's redelegation may move it again.</summary>
    public static Change Redelegate(JsonStore store, string ticketId) => Set(store, ticketId, null);

    private static Change Set(JsonStore store, string ticketId, string? to)
    {
        var id = ticketId.Trim().ToUpperInvariant();
        string? from = null;
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s =>
        {
            foreach (var key in s.Assignees.Keys.Where(k => k.Trim().Equals(id, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                from ??= AccountDirs.Normalize(s.Assignees[key]);
                s.Assignees.Remove(key);
            }
            if (to is not null) s.Assignees[id] = to;
        });
        return new Change(id, from, to);
    }

    /// <summary>Whether two logins are the same account, by email whatever its case; false when either isn't known.</summary>
    public static bool Same(string? a, string? b) => AccountDirs.Normalize(a) is { } x && x == AccountDirs.Normalize(b);

    /// <summary>Whether a CLI on <paramref name="login"/> (its account email; null when not known) may be handed
    /// <paramref name="item"/>: any unassigned ticket, and a pinned one only on its user's own login.</summary>
    public static bool MayTake(QueueItem item, string? login) => item.Assignee is not { } who || Same(who, login);

    /// <summary>Whether the pacing may redelegate <paramref name="item"/> to another login when its own has no room (M.ORCH-T666):
    /// an unassigned ticket may; a pinned one waits for its user until "Redelegate now" drops the pin.</summary>
    public static bool Redelegable(QueueItem item) => !item.IsPinned;

    /// <summary>
    /// Why a user can't take their pinned tickets right now, or null while they have room: not signed in on this machine; their
    /// week at or past its stop line (<paramref name="weeklyStopPercent"/>, the weekly reserve's); their 5-hour window at or past
    /// its target (<paramref name="fiveHourStopPercent"/>, the pace target). A window never read counts as untouched.
    /// </summary>
    public static string? OutOfRoom(bool signedIn, double? fiveHourUsedPercent, double? weekUsedPercent, double fiveHourStopPercent, double weeklyStopPercent)
    {
        if (!signedIn) return "not signed in on this machine";
        if (weekUsedPercent is { } week && week >= weeklyStopPercent) return $"week at {Percent(week)}";
        if (fiveHourUsedPercent is { } five && five >= fiveHourStopPercent) return $"5-hour window at {Percent(five)}";
        return null;
    }

    /// <summary>A pinned ticket's card while its user is out of room: "Waiting for Ryan: week at 97%".</summary>
    public static string WaitingText(string name, string why) => $"Waiting for {name}: {why}";

    private static string Percent(double p) => Math.Floor(p).ToString("0", CultureInfo.InvariantCulture) + "%";
}
