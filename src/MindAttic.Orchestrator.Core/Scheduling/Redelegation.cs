using System.Globalization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>A login a ticket may be redelegated to (M.ORCH-T666): a signed-in per-CLI login, or the fallback login.</summary>
/// <param name="Email">The per-CLI login's email; null for the fallback login.</param>
/// <param name="ConfigDir">Its <c>CLAUDE_CONFIG_DIR</c>; null for the fallback login (~/.claude).</param>
/// <param name="RoomTokens">What a new ticket's estimate must fit there (<see cref="Pace.FitRoomTokens"/>); null when it isn't paced
/// (the fallback on an API key, or a login with no limit learned yet), which fits anything.</param>
/// <param name="Headroom">Percent left before its tighter limit, to pick the roomiest first.</param>
/// <param name="Open">It may start new work now (not stopped).</param>
public sealed record LoginRoom(string? Email, string? ConfigDir, double? RoomTokens, double Headroom, bool Open)
{
    public bool IsFallback => Email is null;

    /// <summary>The login in words: its email, or "the API key".</summary>
    public string Label => Email ?? "the API key";

    /// <summary>Whether <paramref name="item"/>'s estimate fits here: anything while unpaced, a ticket with no estimate while
    /// there's any room, else its estimate within the room.</summary>
    public bool Fits(QueueItem item) => Open && (RoomTokens is not { } room || (item.Estimate is { } e ? e.Fresh <= room : room > 0));

    /// <summary>A per-CLI login's room as its own pace says (<see cref="AccountPacer"/>).</summary>
    public static LoginRoom Of(AccountPace a, OrchestratorSettings s)
    {
        var p = a.Pace;
        var b = a.Budget;
        var five = b.BlockLimitIsPeak || b.CurrentBlock is null ? 0 : b.BlockPercent;
        // The login's own weekly line, as its budget was worked out: the wall while the user lets it past the target (M.ORCH-T694).
        var headroom = Math.Max(0, Math.Min(Math.Clamp(s.PaceTargetPercent, 1, 100) - five, b.WeeklyStopPercent - (b.WeeklyPercent ?? 0)));
        var open = !p.Stop && p.Mode != PaceMode.Over;
        return new LoginRoom(a.Email, a.Directory, p.IsPaced ? p.FitRoomTokens : null, headroom, open);
    }

    /// <summary>The fallback login (~/.claude): the API key, or whatever it's signed in to, taken to have room.</summary>
    public static LoginRoom Fallback { get; } = new(null, null, null, 0, true);
}

/// <summary>
/// Where <c>next --claim</c> sends a ticket the calling CLI's login can't fit (M.ORCH-T666, weekly-aware pacing): to another
/// signed-in login whose room fits its estimate (the most headroom first), else to the fallback login (the API key), so tickets
/// never wait on a full account.
/// </summary>
public static class Redelegation
{
    /// <summary>The login to send <paramref name="item"/> to: the roomiest of <paramref name="others"/> it fits that isn't
    /// <paramref name="taken"/> this round (one new tab per login per claim), else <paramref name="fallback"/> when given and not taken;
    /// null when none can have it.</summary>
    public static LoginRoom? Choose(QueueItem item, IEnumerable<LoginRoom> others, LoginRoom? fallback, ISet<string> taken) =>
        others.Where(o => !o.IsFallback && !taken.Contains(o.Email!) && o.Fits(item))
            .OrderByDescending(o => o.Headroom).ThenBy(o => o.Email, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
        ?? (fallback is not null && !taken.Contains(FallbackKey) && fallback.Fits(item) ? fallback : null);

    /// <summary>The key the fallback login is held by in <c>taken</c>.</summary>
    public const string FallbackKey = "(fallback)";

    /// <summary>Why it went where it did: said by <c>next --claim</c> and shown on the dashboard's queue row.</summary>
    public static string Text(QueueItem item, string? from, Pace? here, LoginRoom to)
    {
        var cost = item.Estimate is { } e ? $"~{Formatting.Tokens(e.Fresh)}" : "it";
        var why = here switch
        {
            { Stop: true } => "is at its limit",
            { FitRoomTokens: { } room } => $"has {Formatting.Tokens((long)room)} left, too little for {cost}",
            _ => "has no room for it",
        };
        if (here?.Weekly is { WeekPercent: { } w }) why += $" (its week at {w.ToString("0", CultureInfo.InvariantCulture)}%)";
        var who = from ?? "this login";
        return to.IsFallback
            ? $"fell back to the API key: {who} {why} and no signed-in login has room"
            : $"redelegated from {who} to {to.Email}: {who} {why}";
    }

    /// <summary>The <c>state</c> row holding each redelegated ticket's <see cref="Text"/> (ticket → text) for the dashboard.</summary>
    public const string StateName = "delegations";

    /// <summary>Each redelegated ticket's text, ticket → text; empty when none, or unreadable.</summary>
    public static IReadOnlyDictionary<string, string> Load(JsonStore store)
    {
        try { return store.Load<Dictionary<string, string>>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return new Dictionary<string, string>(); }
    }

    /// <summary>How long a ticket sent to the fallback login holds it from the next (a tab per call would pile up while the first
    /// one's CLI starts and its spend shows), as a pick holds a per-CLI login (<see cref="Sessions.LoginPicker.ClaimFor"/>).</summary>
    public static TimeSpan FallbackHold => Sessions.LoginPicker.ClaimFor;

    /// <summary>Whether a ticket went to the fallback login within <see cref="FallbackHold"/> of <paramref name="nowUtc"/>.</summary>
    public static bool FallbackHeld(JsonStore store, DateTime nowUtc) =>
        Load(store).TryGetValue(FallbackKey, out var text) &&
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) &&
        nowUtc - at.ToUniversalTime() < FallbackHold && at.ToUniversalTime() <= nowUtc + TimeSpan.FromMinutes(1);

    /// <summary>Records <paramref name="text"/> for <paramref name="ticket"/>, dropping tickets no longer assigned, and when it went
    /// to the fallback login (<paramref name="fellBackUtc"/>). Best effort.</summary>
    public static void Record(JsonStore store, string ticket, string text, DateTime? fellBackUtc = null)
    {
        try
        {
            var assigned = store.LoadAssignments().Select(a => a.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            store.Update<Dictionary<string, string>>(StateName, all =>
            {
                foreach (var gone in all.Keys.Where(k => k != FallbackKey && !assigned.Contains(k) && !k.Equals(ticket, StringComparison.OrdinalIgnoreCase)).ToList())
                    all.Remove(gone);
                all[ticket] = text;
                if (fellBackUtc is { } at) all[FallbackKey] = at.ToString("O", CultureInfo.InvariantCulture);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }
}
