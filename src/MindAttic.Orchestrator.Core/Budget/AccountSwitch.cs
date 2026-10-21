using System.Diagnostics;

namespace MindAttic.Orchestrator.Budget;

/// <summary>What a switch did, line by line, and why it stopped when it did.</summary>
public sealed record AccountSwitchResult(IReadOnlyList<string> Lines, string? Error);

/// <summary>One account's row in a table (the dashboard's account menu, <c>orchestrator accounts</c>): its seat, this week's
/// used/left/reset and the 5-hour window's used/reset, and when it was last read. A reset or read cell is "" when there's
/// none to show (the account hasn't been read, or its reset isn't known).</summary>
public readonly record struct AccountColumns(string Seat, string WeekUsed, string WeekLeft, string WeekResets,
    string FiveHourUsed, string FiveHourResets, string LastRead);

/// <summary>
/// Switching the Claude account every CLI signs in with (M.ORCH-T492), from the dashboard's account menu or
/// <c>orchestrator login</c>: finding the roster row a name means, its seat, the account with the most headroom, and the
/// <c>claude auth login --email &lt;address&gt;</c> (with <c>--sso</c> when its org signs in through SSO) that opens the browser on
/// that account's login. The switch itself lands as any /login does, and the daemon and dashboard follow it (M.ORCH-T486).
/// </summary>
public static class AccountSwitch
{
    /// <summary>The row <paramref name="name"/> means: its email (any case), the part before the @ ("pdftransferjr"), the
    /// start of exactly one email, or the start of its account id. Null when none or more than one match.</summary>
    public static AccountRow? Find(IReadOnlyList<AccountRow> rows, string name) => Find(rows, name, out _);

    /// <summary>As <see cref="Find(IReadOnlyList{AccountRow}, string)"/>, tried in that order; the first step that matches
    /// anything decides. When it matches more than one, the name is ambiguous: null, with those rows in
    /// <paramref name="several"/> (empty otherwise), and no looser step is tried, which would pick an unrelated account
    /// ("d" starting two emails and a third account's id, M.ORCH-T553).</summary>
    public static AccountRow? Find(IReadOnlyList<AccountRow> rows, string name, out IReadOnlyList<AccountRow> several)
    {
        several = [];
        name = name.Trim();
        if (name.Length == 0) return null;
        Func<AccountRow, bool>[] steps =
        [
            r => string.Equals(r.Email, name, StringComparison.OrdinalIgnoreCase),
            r => r.Email is { } e && string.Equals(e.Split('@')[0], name, StringComparison.OrdinalIgnoreCase),
            r => r.Email?.StartsWith(name, StringComparison.OrdinalIgnoreCase) == true,
            r => r.Account.StartsWith(name, StringComparison.OrdinalIgnoreCase),
        ];
        foreach (var step in steps)
        {
            var matches = rows.Where(step).ToList();
            if (matches is [var only]) return only;
            if (matches.Count > 1) { several = matches; return null; }
        }
        return null;
    }

    /// <summary>"premium" or "standard": a Team or Enterprise seat's kind, by the Max tier its limits are sized by (a premium
    /// seat's are a Max tier's, a standard seat's aren't). Null for a personal plan, or before the plan is known.</summary>
    public static string? Seat(AccountRow row) => row.Plan switch
    {
        null => null,
        var p when !p.StartsWith("Team", StringComparison.Ordinal) && !p.StartsWith("Enterprise", StringComparison.Ordinal) => null,
        var p when p.Contains("Max", StringComparison.Ordinal) => "premium",
        _ => "standard",
    };

    /// <summary>The seat, or the personal plan ("Max 20x", "Pro") when there's no seat.</summary>
    public static string SeatOrPlan(AccountRow row) => Seat(row) ?? row.Plan ?? "";

    /// <summary>Whether its org signs in through SSO: an Enterprise org does; a Team or personal plan signs in by email.</summary>
    public static bool UsesSso(AccountRow row) => row.Plan?.StartsWith("Enterprise", StringComparison.Ordinal) == true;

    /// <summary>The account with the most of its week left (then of its 5-hour window), among those that can be signed in to
    /// (an email is known) and have been read. Null when none has.</summary>
    public static AccountRow? MostHeadroom(IReadOnlyList<AccountRow> rows, DateTime nowUtc) =>
        rows.Where(r => r.Email is not null && r.WeekLeftAt(nowUtc) is not null)
            .OrderByDescending(r => r.WeekLeftAt(nowUtc))
            .ThenBy(r => r.FiveHourUsedAt(nowUtc) ?? 0)
            .FirstOrDefault();

    /// <summary>One account's entry in the dashboard's account menu: "a@mindattic.com · premium · week 42% used, 58% left, resets
    /// Sun 4:00 PM · 5h 10%, resets 9:30 PM · read 3m ago", with "(signed in)" and "(most headroom)" where they apply.</summary>
    public static string MenuLine(AccountRow row, string? current, string? suggested, DateTime nowUtc)
    {
        var parts = new List<string> { row.Label + (row.Account == current ? " (signed in)" : "") + (row.Account == suggested ? " (most headroom)" : "") };
        if (SeatOrPlan(row) is { Length: > 0 } seat) parts.Add(seat);
        var week = row.WeekUsedAt(nowUtc) is null ? "week —" : $"week {AccountRoster.Percent(row.WeekUsedAt(nowUtc))} used, {AccountRoster.Percent(row.WeekLeftAt(nowUtc))} left";
        if (row.WeekResetAt(nowUtc) is { } wr) week += $", resets {Formatting.ResetTime(wr, nowUtc)}";
        parts.Add(week);
        var five = $"5h {AccountRoster.Percent(row.FiveHourUsedAt(nowUtc))}";
        if (row.FiveHourResetAt(nowUtc) is { } fr) five += $", resets {Formatting.ResetTime(fr, nowUtc)}";
        parts.Add(five);
        parts.Add(row.ReadUtc is { } r ? $"read {Formatting.Ago(r, nowUtc)}" : "not read yet");
        return string.Join(" · ", parts);
    }

    /// <summary>The same facts as <see cref="MenuLine"/>, as plain table cells (M.ORCH-T566): the dashboard's account menu
    /// renders these as columns instead of one joined sentence. Each reset or read cell is "" when there's none to show.</summary>
    public static AccountColumns Columns(AccountRow row, DateTime nowUtc) => new(
        SeatOrPlan(row),
        AccountRoster.Percent(row.WeekUsedAt(nowUtc)),
        row.WeekUsedAt(nowUtc) is null ? "—" : AccountRoster.Percent(row.WeekLeftAt(nowUtc)),
        row.WeekResetAt(nowUtc) is { } wr ? Formatting.ResetTime(wr, nowUtc) : "",
        AccountRoster.Percent(row.FiveHourUsedAt(nowUtc)),
        row.FiveHourResetAt(nowUtc) is { } fr ? Formatting.ResetTime(fr, nowUtc) : "",
        row.ReadUtc is { } r ? Formatting.Ago(r, nowUtc) : "not read yet");

    /// <summary><c>auth login --email &lt;address&gt;</c>, and <c>--sso</c> when <paramref name="sso"/>.</summary>
    public static IReadOnlyList<string> LoginArgs(string email, bool sso) =>
        sso ? ["auth", "login", "--email", email, "--sso"] : ["auth", "login", "--email", email];

    /// <summary>"claude auth login --email a@b.com --sso", as it's shown and run.</summary>
    public static string LoginCommand(string email, bool sso) => "claude " + string.Join(' ', LoginArgs(email, sso));

    /// <summary>Starts <c>claude auth login</c> for <paramref name="email"/>, which opens the browser on that account's login.
    /// From a terminal it runs in that terminal; from the dashboard (which has no console) it gets a console window of its
    /// own, where the login says what it's doing. Returns the process, for the caller to wait on; throws
    /// <see cref="System.ComponentModel.Win32Exception"/> when claude isn't found.</summary>
    public static Process Start(string email, bool sso, string? configDir = null)
    {
        var psi = new ProcessStartInfo("claude") { UseShellExecute = false, CreateNoWindow = false };
        foreach (var a in LoginArgs(email, sso)) psi.ArgumentList.Add(a);
        // A per-CLI login (M.ORCH-T559) signs in to its own folder, leaving the global login as it is.
        if (configDir is not null) psi.Environment[Sessions.AccountDirs.EnvVar] = configDir;
        return Process.Start(psi) ?? throw new InvalidOperationException("claude didn't start");
    }

    /// <summary>The seam <c>orchestrator login --account</c> signs a per-CLI login's folder in through (M.ORCH-T559): runs
    /// <c>claude auth login --email</c> with <c>CLAUDE_CONFIG_DIR</c> set to the folder and returns its exit code. Tests replace it.</summary>
    public static Func<string, bool, string, int> LoginInto { get; set; } = (email, sso, configDir) =>
    {
        using var p = Start(email, sso, configDir);
        p.WaitForExit();
        return p.ExitCode;
    };

    /// <summary>
    /// Signs every CLI in to <paramref name="row"/>'s account (by <paramref name="by"/>: "dashboard", "cli"): billing on the API
    /// key goes back to Claude Team first (as the toggle does: every CLI switches, Launcher tabs restart at their next idle
    /// point); then, unless it's the account already signed in (<paramref name="current"/>), <see cref="Login"/> runs
    /// <c>claude auth login --email</c> for it, with <c>--sso</c> when <paramref name="sso"/> says so or its org uses SSO.
    /// </summary>
    public static AccountSwitchResult SignIn(AccountRow row, string? current, Billing.BillingSwitch billing,
        IReadOnlyList<HostSession> hosts, string by, bool sso, DateTime nowUtc)
    {
        var lines = new List<string>();
        var onKey = billing.Mode(out _) == Billing.BillingModes.Api;
        if (row.Account == current && !onKey) return new([$"Already signed in as {row.Label}."], null);
        if (row.Account != current && row.Email is null)
            return new([], $"{row.Label}'s email isn't known yet, so its login can't be opened: run /login in a Claude CLI and pick it there.");
        if (onKey)
        {
            var result = billing.Switch(Billing.BillingModes.Team, by, hosts, nowUtc);
            if (result.Error is { } error) return new(result.Lines, error);
            lines.AddRange(result.Lines);
        }
        if (row.Account == current) return new(lines, null);
        var email = row.Email!;
        sso = sso || UsesSso(row);
        lines.Add($"Opening {email}'s login in the browser ({LoginCommand(email, sso)}).");
        int code;
        try { code = Login(email, sso); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(lines, $"Couldn't run {LoginCommand(email, sso)}: {ex.Message}");
        }
        if (code != 0) return new(lines, $"{LoginCommand(email, sso)} ended with exit code {code}; the account wasn't switched.");
        lines.Add($"Signed in as {email}: every CLI follows at its next turn.");
        return new(lines, null);
    }

    /// <summary>The seam <c>orchestrator login</c> and the dashboard start the login through: runs it and returns its exit code
    /// (the dashboard runs it off the UI thread). Tests replace it, so no test ever opens a browser.</summary>
    public static Func<string, bool, int> Login { get; set; } = (email, sso) =>
    {
        using var p = Start(email, sso);
        p.WaitForExit();
        return p.ExitCode;
    };
}
