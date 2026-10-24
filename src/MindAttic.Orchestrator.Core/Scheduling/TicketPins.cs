using System.Globalization;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// Tickets assigned to a user (M.ORCH-T668). The assignment lives in the ticket database (<c>ticket_work</c>), where
/// <c>tickets ticket assign</c> and Tickets Desktop set, change or drop it ("Redelegate now" is <c>--redelegate</c> there), read here
/// through the engine into <see cref="Snapshot.Assignees"/> (M.ORCH-T762). An assigned ticket is a pin: <c>orchestrator next --claim</c>
/// hands it only to a CLI on that login (<see cref="MayTake"/>), Start opens its CLI only on that login, and its claim or tab's
/// <see cref="Assignment.Account"/> says so. While its user is out of room (<see cref="OutOfRoom"/>) it waits for them, its
/// card saying why ("Waiting for Ryan: week at 97%"), and the weekly-aware pacing's redelegation (M.ORCH-T666) leaves it alone
/// (<see cref="Redelegable"/>) until the pin is dropped in Tickets, so it's handed on like any unassigned ticket: to another user
/// with room, else the API key. An unassigned ticket keeps that redelegation.
/// </summary>
public static class TicketPins
{
    /// <summary>The assignments in <paramref name="snapshot"/> (the ticket database's), ticket id (any case) → the user's
    /// normalized email; an entry whose email isn't one is left out.</summary>
    public static IReadOnlyDictionary<string, string> Assignees(Snapshot snapshot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, email) in snapshot.Assignees)
            if (AccountDirs.Normalize(email) is { } e) map[id.Trim()] = e;
        return map;
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
