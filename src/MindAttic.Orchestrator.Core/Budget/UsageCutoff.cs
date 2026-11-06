using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One member's own usage cutoffs (M.ORCH-T707): the percent of its week and of its 5-hour window at which that login's
/// access is cut off. Set only with <c>orchestrator org limit &lt;who&gt; --weekly &lt;pct&gt; --5h &lt;pct&gt;</c>.</summary>
public sealed class UsageCutoff
{
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public int WeeklyPercent { get; set; } = UsageCutoffs.DefaultWeeklyPercent;
    public int FiveHourPercent { get; set; } = UsageCutoffs.DefaultFiveHourPercent;
    /// <summary>True when this member never set a weekly cutoff of their own (only a 5-hour one): <see cref="WeeklyPercent"/>
    /// is then a placeholder and the login follows the live global weekly target. Absent (false) on rows written before
    /// 2026-11-02, which keep both numbers as set.</summary>
    public bool WeeklyFollowsGlobal { get; set; }
    /// <summary>True when this member never set a 5-hour cutoff of their own (only a weekly one): <see cref="FiveHourPercent"/>
    /// is then a placeholder and the login follows the live global pace target.</summary>
    public bool FiveHourFollowsGlobal { get; set; }
}

/// <summary>What the database keeps (<see cref="UsageCutoffs.StateName"/>): only the members who set a cutoff of their own (even one
/// equal to the defaults: an explicit cutoff is pinned, while a member without one follows the live global dial).</summary>
public sealed class UsageCutoffBook
{
    public List<UsageCutoff> Cutoffs { get; set; } = [];
}

/// <summary>
/// Per-user usage cutoffs (M.ORCH-T707). Every login's cutoffs are <see cref="DefaultWeeklyPercent"/> of its week and
/// <see cref="DefaultFiveHourPercent"/> of its 5-hour window unless the user set others for that member (<c>org limit</c>).
/// Below a cutoff nothing throttles; at it, the login's access is cut off and the owner is asked (<c>org allow</c>).
/// </summary>
public static class UsageCutoffs
{
    public const string StateName = "usage-cutoffs";
    public const int DefaultWeeklyPercent = 90, DefaultFiveHourPercent = 96;
    /// <summary>The lowest weekly cutoff (the weekly reserve's own limit, 50) and the lowest 5-hour one.</summary>
    public const int MinWeeklyPercent = 100 - OrchestratorSettings.MaxWeeklyReservePercent, MinFiveHourPercent = 1;

    public static UsageCutoffBook LoadBook(JsonStore? store)
    {
        if (store is null) return new();
        try { return store.Load<UsageCutoffBook>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return new(); }
    }

    public static IReadOnlyList<UsageCutoff> Load(JsonStore? store) => LoadBook(store).Cutoffs;

    public static UsageCutoff? Custom(IEnumerable<UsageCutoff>? cutoffs, string? email) =>
        email is not { Length: > 0 } ? null
        : cutoffs?.FirstOrDefault(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Without a member override, this is <see cref="DefaultWeeklyPercent"/>; a caller with the live global weekly
    /// target in hand (<see cref="OrchestratorSettings.WeeklyStopPercent"/>) should pass it as <paramref name="fallback"/>
    /// instead, so a login with no override of its own tracks that global dial rather than a number frozen at startup.</summary>
    public static int WeeklyFor(IEnumerable<UsageCutoff>? cutoffs, string? email, int fallback = DefaultWeeklyPercent) =>
        Custom(cutoffs, email) is { WeeklyFollowsGlobal: false } c ? Math.Clamp(c.WeeklyPercent, MinWeeklyPercent, 100) : fallback;

    /// <summary>Without a member override, this is <see cref="DefaultFiveHourPercent"/>; a caller with the live global pace
    /// target in hand (<see cref="OrchestratorSettings.PaceTargetPercent"/>) should pass it as <paramref name="fallback"/>
    /// instead, so a login with no override of its own tracks that global dial rather than a number frozen at startup.</summary>
    public static int FiveHourFor(IEnumerable<UsageCutoff>? cutoffs, string? email, int fallback = DefaultFiveHourPercent) =>
        Custom(cutoffs, email) is { FiveHourFollowsGlobal: false } c ? Math.Clamp(c.FiveHourPercent, MinFiveHourPercent, 100) : fallback;

    /// <summary>True when <paramref name="email"/> set a weekly cutoff of their own.</summary>
    public static bool HasWeekly(IEnumerable<UsageCutoff>? cutoffs, string? email) => Custom(cutoffs, email) is { WeeklyFollowsGlobal: false };

    /// <summary>True when <paramref name="email"/> set a 5-hour cutoff of their own.</summary>
    public static bool HasFiveHour(IEnumerable<UsageCutoff>? cutoffs, string? email) => Custom(cutoffs, email) is { FiveHourFollowsGlobal: false };

    /// <summary><paramref name="settings"/> for <paramref name="email"/>'s login with its cutoffs in force: a member override
    /// wins outright; without one, <paramref name="settings"/>'s own global weekly/pace target stands, so changing that global
    /// dial actually takes effect for everyone who hasn't set a cutoff of their own. The dashboard's warn lines move with the
    /// cutoffs too (bughunt 2026-11-01): a member who moved their weekly or 5-hour cutoff away from the default previously still
    /// warned (and, unpaced, was held) at the org-wide <see cref="OrchestratorSettings.WarnPercent"/>, which could sit well past
    /// a lowered cutoff or well short of a raised one, so the local override didn't actually override anything past the stop line
    /// itself. Each warn line now keeps the same gap to its own cutoff that the org-wide default keeps to its own.</summary>
    public static OrchestratorSettings Apply(IEnumerable<UsageCutoff>? cutoffs, string? email, OrchestratorSettings settings)
    {
        var weekly = WeeklyFor(cutoffs, email, settings.WeeklyStopPercent);
        var fiveHour = FiveHourFor(cutoffs, email, settings.PaceTargetPercent);
        return settings with
        {
            WeeklyReservePercent = 100 - weekly,
            PaceTargetPercent = fiveHour,
            WeeklyWarnPercent = Math.Clamp(weekly - (settings.WeeklyStopPercent - settings.WarnPercent), 1, 100),
            FiveHourWarnPercent = Math.Clamp(fiveHour - (settings.PaceTargetPercent - settings.WarnPercent), 1, 100),
        };
    }

    /// <summary>Sets <paramref name="email"/>'s cutoffs (null keeps the one it has, or, if it has none of its own, keeps following
    /// the live global dial). A value given here is stored even when it equals the defaults, so it stays put if the global dial
    /// moves; setting only one of the two never pins the other. Returns the member's row as stored (both
    /// <c>*FollowsGlobal</c> when nothing is kept for it).</summary>
    public static UsageCutoff Set(JsonStore store, string email, string name, int? weekly, int? fiveHour)
    {
        UsageCutoff result = new() { Email = email, Name = name };
        store.Update<UsageCutoffBook>(StateName, book =>
        {
            var old = Custom(book.Cutoffs, email);
            var oldWeekly = old is { WeeklyFollowsGlobal: false } ? old.WeeklyPercent : (int?)null;
            var oldFiveHour = old is { FiveHourFollowsGlobal: false } ? old.FiveHourPercent : (int?)null;
            var newWeekly = weekly ?? oldWeekly;
            var newFiveHour = fiveHour ?? oldFiveHour;
            result.WeeklyPercent = newWeekly ?? DefaultWeeklyPercent;
            result.WeeklyFollowsGlobal = newWeekly is null;
            result.FiveHourPercent = newFiveHour ?? DefaultFiveHourPercent;
            result.FiveHourFollowsGlobal = newFiveHour is null;
            book.Cutoffs.RemoveAll(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
            if (newWeekly is not null || newFiveHour is not null) book.Cutoffs.Add(result);
        });
        return result;
    }

    /// <summary>Back to the defaults for <paramref name="email"/>.</summary>
    public static UsageCutoff Reset(JsonStore store, string email, string name)
    {
        store.Update<UsageCutoffBook>(StateName, book => book.Cutoffs.RemoveAll(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)));
        return new UsageCutoff { Email = email, Name = name, WeeklyFollowsGlobal = true, FiveHourFollowsGlobal = true };
    }

    /// <summary><see cref="Text(int,int)"/> for <paramref name="cutoff"/>'s login as it now stands: a half it didn't set shows the
    /// live global value (<paramref name="globalWeekly"/>, <paramref name="globalFiveHour"/>) it follows.</summary>
    public static string Text(UsageCutoff cutoff, int globalWeekly, int globalFiveHour) =>
        Text(cutoff.WeeklyFollowsGlobal ? globalWeekly : cutoff.WeeklyPercent, cutoff.FiveHourFollowsGlobal ? globalFiveHour : cutoff.FiveHourPercent);

    public static string Text(int weekly, int fiveHour) => $"{weekly}% of the week, {fiveHour}% of the 5-hour window";
}
