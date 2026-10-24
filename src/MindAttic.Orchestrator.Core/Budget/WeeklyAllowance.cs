using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// One member's login let past its weekly target by the user (M.ORCH-T694, <c>orchestrator org allow</c>): until
/// <see cref="UntilUtc"/>, and never past the weekly reset it was given in (<see cref="WeekResetUtc"/>), that one login's CLIs
/// stop only at the week's wall instead of at <see cref="OrchestratorSettings.WeeklyStopPercent"/>.
/// </summary>
public sealed class WeeklyAllowance
{
    /// <summary>The login's email.</summary>
    public string Email { get; set; } = "";

    /// <summary>The member's name, as the report and the log say it.</summary>
    public string Name { get; set; } = "";

    public DateTime SinceUtc { get; set; }

    /// <summary>When it ends: the login's weekly reset by default, or the time the user gave.</summary>
    public DateTime UntilUtc { get; set; }

    /// <summary>The weekly reset it was given in, when known: it ends there at the latest.</summary>
    public DateTime? WeekResetUtc { get; set; }

    /// <summary>Who allowed it ("you": only the user can).</summary>
    public string By { get; set; } = WeeklyAllowances.ByYou;

    /// <summary>When it stops applying: <see cref="UntilUtc"/>, or the weekly reset when that comes first.</summary>
    public DateTime EndsUtc => WeekResetUtc is { } r && r < UntilUtc ? r : UntilUtc;

    public bool ActiveAt(DateTime nowUtc) => nowUtc >= SinceUtc.AddMinutes(-1) && nowUtc < EndsUtc;

    /// <summary>"Erin: allowed past the target by you until Tue 5:00 PM" (local time).</summary>
    public string Text => $"{Name}: allowed past the target by {By} until {WeeklyAllowances.When(EndsUtc)}";
}

/// <summary>One line of the allowances' log: given, taken back, or ended at its reset.</summary>
public sealed class WeeklyAllowanceEvent
{
    public DateTime AtUtc { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>What the database keeps (<see cref="WeeklyAllowances.StateName"/>): the allowances and their log.</summary>
public sealed class WeeklyAllowanceBook
{
    public List<WeeklyAllowance> Allowances { get; set; } = [];
    public List<WeeklyAllowanceEvent> Log { get; set; } = [];
}

/// <summary>
/// The weekly target's one exception (M.ORCH-T694). A login with less than its weekly reserve left (past
/// <see cref="OrchestratorSettings.WeeklyStopPercent"/>) takes no new work and its CLIs stop until its weekly reset; only the user
/// lifts that, for one login at a time, with <c>orchestrator org allow &lt;who&gt; [--until reset|time]</c>, and takes it back
/// with <c>org disallow &lt;who&gt;</c>. Kept in orchestrator.db, logged, shown in the usage report. Nothing else (rebalancing,
/// redelegation, allocate) writes here: they only read <see cref="Apply"/> and <see cref="StopFor"/>.
/// </summary>
public static class WeeklyAllowances
{
    /// <summary>The <c>state</c> row they're kept in.</summary>
    public const string StateName = "weekly-allowances";

    public const string ByYou = "you";

    /// <summary>How many log lines are kept.</summary>
    public const int KeepLog = 200;

    /// <summary>A local time as the report says it: "Tue 5:00 PM".</summary>
    public static string When(DateTime utc) => utc.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>What <paramref name="store"/> keeps; empty when nothing is, or it can't be read now.</summary>
    public static WeeklyAllowanceBook Load(JsonStore? store)
    {
        if (store is null) return new();
        try { return store.Load<WeeklyAllowanceBook>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return new(); }
    }

    /// <summary>The allowances in force at <paramref name="nowUtc"/>.</summary>
    public static IReadOnlyList<WeeklyAllowance> Active(JsonStore? store, DateTime nowUtc) =>
        [.. Load(store).Allowances.Where(a => a.ActiveAt(nowUtc))];

    /// <summary><paramref name="email"/>'s allowance in force at <paramref name="nowUtc"/>; null when it has none.</summary>
    public static WeeklyAllowance? For(IEnumerable<WeeklyAllowance>? allowances, string? email, DateTime nowUtc) =>
        email is not { Length: > 0 } ? null
        : allowances?.FirstOrDefault(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase) && a.ActiveAt(nowUtc));

    /// <summary>
    /// <paramref name="settings"/> for <paramref name="email"/>'s login: while it's allowed past the target, its week stops at its
    /// wall (no weekly reserve kept), with why for its pace note; else as they are.
    /// </summary>
    public static (OrchestratorSettings Settings, string? Why) Apply(IEnumerable<WeeklyAllowance>? allowances, string? email,
        OrchestratorSettings settings, DateTime nowUtc)
    {
        if (For(allowances, email, nowUtc) is not { } a) return (settings, null);
        return (settings with { WeeklyReservePercent = 0 },
            $"Allowed past the {settings.WeeklyStopPercent}% weekly target by {a.By} until {When(a.EndsUtc)}.");
    }

    /// <summary>Where <paramref name="email"/>'s week stops: <paramref name="settings"/>' line, or the wall while it's allowed.</summary>
    public static int StopFor(IEnumerable<WeeklyAllowance>? allowances, string? email, OrchestratorSettings settings, DateTime nowUtc) =>
        Apply(allowances, email, settings, nowUtc).Settings.WeeklyStopPercent;

    /// <summary>Lets <paramref name="email"/>'s login past the weekly target until <paramref name="untilUtc"/> (at most its
    /// <paramref name="weekResetUtc"/>), replacing any allowance it had; logged. Returns the allowance and its log line.</summary>
    public static (WeeklyAllowance Allowance, string Line) Allow(JsonStore store, string email, string name, DateTime untilUtc,
        DateTime? weekResetUtc, int weeklyTargetPercent, DateTime nowUtc)
    {
        var allowance = new WeeklyAllowance
        {
            Email = email, Name = name, SinceUtc = nowUtc, UntilUtc = untilUtc, WeekResetUtc = weekResetUtc, By = ByYou,
        };
        var line = $"{name} ({email}) allowed past the {weeklyTargetPercent}% weekly target by {ByYou} until {When(allowance.EndsUtc)}" +
                   (weekResetUtc is { } r && r <= untilUtc ? " (its weekly reset)" : "") + ".";
        store.Update<WeeklyAllowanceBook>(StateName, book =>
        {
            Expire(book, nowUtc);
            book.Allowances.RemoveAll(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));
            book.Allowances.Add(allowance);
            Say(book, nowUtc, line);
        });
        return (allowance, line);
    }

    /// <summary>Takes <paramref name="email"/>'s allowance back; logged. The line said, or null when it had none in force.</summary>
    public static string? Disallow(JsonStore store, string email, string name, int weeklyTargetPercent, DateTime nowUtc)
    {
        string? line = null;
        store.Update<WeeklyAllowanceBook>(StateName, book =>
        {
            Expire(book, nowUtc);
            if (book.Allowances.RemoveAll(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase)) == 0) return;
            line = $"{name} ({email}) no longer allowed past the {weeklyTargetPercent}% weekly target: taken back by {ByYou}.";
            Say(book, nowUtc, line);
        });
        return line;
    }

    /// <summary>Ends the allowances whose time (or weekly reset) has come, each logged; the lines said. Best effort.</summary>
    public static IReadOnlyList<string> Prune(JsonStore? store, DateTime nowUtc)
    {
        if (store is null || !Load(store).Allowances.Any(a => nowUtc >= a.EndsUtc)) return [];
        var said = new List<string>();
        try { store.Update<WeeklyAllowanceBook>(StateName, book => said.AddRange(Expire(book, nowUtc))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        return said;
    }

    private static List<string> Expire(WeeklyAllowanceBook book, DateTime nowUtc)
    {
        var said = new List<string>();
        foreach (var a in book.Allowances.Where(a => nowUtc >= a.EndsUtc).ToList())
        {
            book.Allowances.Remove(a);
            var line = $"{a.Name} ({a.Email}) no longer allowed past the weekly target: " +
                       (a.WeekResetUtc is { } r && r <= a.UntilUtc ? "its week reset" : "its time ran out") + $" at {When(a.EndsUtc)}.";
            Say(book, nowUtc, line);
            said.Add(line);
        }
        return said;
    }

    private static void Say(WeeklyAllowanceBook book, DateTime nowUtc, string line)
    {
        book.Log.Add(new WeeklyAllowanceEvent { AtUtc = nowUtc, Text = line });
        if (book.Log.Count > KeepLog) book.Log.RemoveRange(0, book.Log.Count - KeepLog);
        try { OrchestratorLog.For("Org").Information("{Allowance:l}", line); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
    }
}
