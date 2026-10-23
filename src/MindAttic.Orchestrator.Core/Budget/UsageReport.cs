using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One login's latest reading of Claude's meter for <see cref="UsageReport.Build"/>: a per-CLI login's
/// <c>claude-usage.json</c>, or the global login's, put on the account signed in to it.</summary>
/// <param name="Email">Whose reading it is.</param>
/// <param name="Account">Its account key (<see cref="ClaudeAccount.Current"/>'s), to find its spend in the usage history; null when unknown.</param>
/// <param name="Alias">The login's name (<see cref="LoginLook.Alias"/>); null when it has none.</param>
public sealed record LoginReading(string Email, string? Account, string? Alias, ReportedUsage? Reading);

/// <summary>One member's row of the usage report (M.ORCH-T685). Percents are null while never read (or, for the 5-hour
/// window, while the window read has since reset: <see cref="FiveHourUnknown"/>); tokens are uncached tokens (input + cache
/// writes + output) the orchestrator's CLIs on this machine spent on the member's login.</summary>
public sealed record UsageReportRow
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    /// <summary>Whether the email is in the members table; false for a login on this machine nobody entered as a member.</summary>
    public bool IsMember { get; init; } = true;
    /// <summary>The global (fallback) login, <c>~/.claude</c>, is signed in as this member.</summary>
    public bool IsGlobalLogin { get; init; }
    public double? FiveHourUsed { get; init; }
    /// <summary>The 5-hour window last read has reset since, and nothing newer is known: its use now is unknown, not 0.</summary>
    public bool FiveHourUnknown { get; init; }
    public DateTime? FiveHourResetUtc { get; init; }
    public double? WeekUsed { get; init; }
    public double? WeekLeft => WeekUsed is { } u ? Math.Max(0, 100 - u) : null;
    /// <summary>Points left before the weekly target (<see cref="UsageReport.WeeklyTargetPercent"/>); negative once past it.</summary>
    public double? LeftToTarget { get; init; }
    public DateTime? WeekResetUtc { get; init; }
    /// <summary>When Claude's meter was last read for this login; null when it never was.</summary>
    public DateTime? ReadUtc { get; init; }
    /// <summary>The reading is older than <see cref="UsageReport.StaleAfter"/> (or there's none).</summary>
    public bool Stale { get; init; }
    /// <summary>Why the reading is stale, when that's known.</summary>
    public string? StaleReason { get; init; }
    /// <summary><see cref="UsageReport.Meter"/> or <see cref="UsageReport.Estimated"/>; null with no reading at all.</summary>
    public string? Source { get; init; }
    public long CliToday { get; init; }
    public long CliWeek { get; init; }
    /// <summary>Since <see cref="UsageReport.Since"/>; null when no --since was given.</summary>
    public long? CliSince { get; init; }
    /// <summary>The member's own use of Claude outside the orchestrator (M.ORCH-T682, <see cref="UsageReport.OutsideText"/>); null until it's measured.</summary>
    public string? OutsideUse { get; init; }
}

/// <summary>
/// The usage report (M.ORCH-T685): one row per member with the 5-hour and weekly use of Claude and their resets, the points
/// left to the weekly target, how old each reading is and whether it's still accurate, then a line each for who's at the
/// target, who weekly-aware pacing is limiting, who has room, and why any reading is stale. A second table has what the
/// orchestrator's CLIs spent on each login against the member's own outside use, and the API key's spend. Printed by
/// <c>orchestrator report</c> (as plain text, CSV or JSON) and written as Markdown or HTML to share, from the CLI or the
/// dashboard's Accounts view; anything else that shows the table (<c>orchestrator org usage</c>) renders it from here.
/// </summary>
public sealed record UsageReport
{
    /// <summary>A reading older than this is marked stale.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    public const string Meter = "Claude's meter";
    public const string Estimated = "estimated";

    /// <summary>What an unmeasured outside use reads as.</summary>
    public const string NotMeasured = "not measured yet";

    public DateTime GeneratedUtc { get; init; }
    public DateOnly? Since { get; init; }
    public int WeeklyTargetPercent { get; init; }
    /// <summary>Where weekly-aware pacing starts taking the week in (<see cref="OrchestratorSettings.WeeklyAwareLine"/>).</summary>
    public int PacingLinePercent { get; init; } = OrchestratorSettings.DefaultWeeklyAwareLinePercent;
    /// <summary>Whether weekly-aware pacing is on (<see cref="OrchestratorSettings.WeeklyAwarePacing"/>).</summary>
    public bool PacingOn { get; init; }
    public IReadOnlyList<UsageReportRow> Rows { get; init; } = [];
    /// <summary>Tokens billed to the API key (or any other off-subscription billing) today, over the last 7 days, and since <see cref="Since"/>.</summary>
    public long ApiKeyToday { get; init; }
    public long ApiKeyWeek { get; init; }
    public long? ApiKeySince { get; init; }

    /// <summary>
    /// Builds the report as of <paramref name="nowUtc"/>. Each member's reading is the latest of its per-CLI login's
    /// (<paramref name="logins"/>) and the roster's rows for its email (<paramref name="roster"/>, the global login's last
    /// readings). A stale reading is raised to <paramref name="estimates"/>' budget for the account (Claude's last percent plus
    /// what the transcripts recorded since) when it has a real limit, and then its source says estimated. A login on this
    /// machine whose email isn't a member gets a row too, so no spend goes unshown.
    /// </summary>
    /// <param name="estimates">Account key → its budget as the status refresh computed it.</param>
    /// <param name="globalAccount">The account the global login is signed in as; its member is marked.</param>
    /// <param name="outsideUse">Email → the member's measured outside use (M.ORCH-T682); null, or null for an email, is not measured yet.</param>
    /// <param name="staleReason">Email → why its reading may be old, when known.</param>
    public static UsageReport Build(IReadOnlyList<Member> members, IReadOnlyList<AccountRow> roster, IReadOnlyList<LoginReading> logins,
        IReadOnlyList<DayUsage> history, IReadOnlyDictionary<string, BudgetStatus> estimates, int weeklyTargetPercent, DateTime nowUtc,
        DateOnly? since = null, string? globalAccount = null, Func<string, string?>? outsideUse = null, Func<string, string?>? staleReason = null)
    {
        var people = members.Select(m => (m.Email, m.Name, IsMember: true)).ToList();
        var known = people.Select(p => p.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var email in logins.Select(l => l.Email).Concat(roster.Select(r => r.Email).OfType<string>()))
            if (AccountDirs.Normalize(email) is { } e && known.Add(e)) people.Add((e, null, false));

        var today = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        var claude = history.Where(d => d.Provider == Providers.Claude).ToList();
        var rows = new List<UsageReportRow>();
        foreach (var (email, name, isMember) in people)
        {
            var mine = logins.Where(l => string.Equals(l.Email, email, StringComparison.OrdinalIgnoreCase)).ToList();
            var rosterRows = roster.Where(r => string.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase)).ToList();
            var keys = mine.Select(l => l.Account).Concat(rosterRows.Select(r => r.Account)).OfType<string>()
                .Where(k => k != ClaudeAccount.ApiKey).ToHashSet(StringComparer.Ordinal);
            var candidates = mine.Where(l => l.Reading is not null).Select(l => new AccountRow
            {
                Account = l.Account ?? email,
                Email = email,
                FiveHourPercent = l.Reading!.FiveHour?.Percent,
                FiveHourResetUtc = l.Reading.FiveHour?.ResetUtc,
                WeekPercent = l.Reading.Week?.Percent,
                WeekResetUtc = l.Reading.Week?.ResetUtc,
                ReadUtc = l.Reading.AtUtc,
            }).Concat(rosterRows);
            var row = candidates.OrderByDescending(r => r.ReadUtc ?? DateTime.MinValue).FirstOrDefault();

            // A 5-hour window that has reset since it was read says nothing about the one running now (one may not even have started).
            var fiveUnknown = row is { FiveHourPercent: not null, FiveHourResetUtc: { } fr } && fr <= nowUtc;
            double? five = fiveUnknown ? null : row?.FiveHourUsedAt(nowUtc), week = row?.WeekUsedAt(nowUtc);
            DateTime? fiveReset = row?.FiveHourResetAt(nowUtc), weekReset = row?.WeekResetAt(nowUtc);
            var read = row?.ReadUtc;
            var stale = read is not { } r || nowUtc - r > StaleAfter;
            string? source = read is null ? null : Meter;
            if (stale && keys.Select(k => estimates.GetValueOrDefault(k)).OfType<BudgetStatus>().FirstOrDefault() is { } budget)
            {
                if (budget.BlockLimitSource is LimitSource.Reported or LimitSource.Learned or LimitSource.Manual &&
                    budget.CurrentBlock is { } block && block.IsActive(nowUtc) && (five is null || budget.BlockPercent > five))
                {
                    (five, fiveReset, fiveUnknown, source) = (budget.BlockPercent, block.EndUtc, false, Estimated);
                }
                if (budget.WeeklyPercent is { } w && budget.WeeklyLimitSource is not null && (week is null || w > week))
                {
                    (week, source) = (w, Estimated);
                    if (budget.WeekResetUtc is { } wr && wr > nowUtc) weekReset = wr;
                }
            }

            // This week is the login's own, from its weekly reset; a login whose reset isn't known counts the last 7 days.
            var weekStart = weekReset is { } reset ? reset.AddDays(-7) : nowUtc.ToLocalTime().Date.AddDays(-6).ToUniversalTime();
            var spent = claude.Where(d => d.Account is { } a && keys.Contains(a)).ToList();
            var alias = mine.Select(l => l.Alias).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            rows.Add(new UsageReportRow
            {
                Name = name ?? alias ?? LoginLook.DefaultAlias(email) ?? email,
                Email = email,
                IsMember = isMember,
                IsGlobalLogin = globalAccount is not null && keys.Contains(globalAccount),
                FiveHourUsed = five,
                FiveHourUnknown = fiveUnknown,
                FiveHourResetUtc = fiveReset,
                WeekUsed = week,
                LeftToTarget = week is { } used ? weeklyTargetPercent - used : null,
                WeekResetUtc = weekReset,
                ReadUtc = read,
                Stale = stale,
                StaleReason = stale ? staleReason?.Invoke(email) ?? (read is null ? "Claude's meter was never read for this login here" : null) : null,
                Source = source,
                CliToday = spent.Where(d => d.Day == today).Sum(d => d.Fresh),
                CliWeek = spent.Where(d => InWeek(d, weekStart)).Sum(d => d.Fresh),
                CliSince = since is { } s ? spent.Where(d => d.Day >= s).Sum(d => d.Fresh) : null,
                OutsideUse = outsideUse?.Invoke(email),
            });
        }

        var billed = claude.Where(d => UsageHistory.IsBilledElsewhere(d.Account)).ToList();
        return new UsageReport
        {
            GeneratedUtc = nowUtc,
            Since = since,
            WeeklyTargetPercent = weeklyTargetPercent,
            Rows = [.. rows.OrderBy(r => !r.IsMember).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)],
            ApiKeyToday = billed.Where(d => d.Day == today).Sum(d => d.Fresh),
            ApiKeyWeek = billed.Where(d => d.Day >= today.AddDays(-6)).Sum(d => d.Fresh),
            ApiKeySince = since is { } from ? billed.Where(d => d.Day >= from).Sum(d => d.Fresh) : null,
        };

        // The day the week reset on counts from the reset: its tokens kept apart as before it (M.ORCH-T388) were last week's.
        static bool InWeek(DayUsage d, DateTime weekStartUtc)
        {
            var startDay = DateOnly.FromDateTime(weekStartUtc.ToLocalTime());
            return d.Day > startDay || (d.Day == startDay && !(d.BeforeUtc is { } before && before <= weekStartUtc.AddMinutes(1)));
        }
    }

    /// <summary>
    /// The report from what's on this machine: the members table and account roster in <paramref name="db"/>, each per-CLI
    /// login's reading in <paramref name="dirs"/>, the global login's <paramref name="globalReading"/> (put on the account the
    /// snapshot says is signed in to it), <paramref name="snapshot"/>'s usage history and budgets (the global login's and each
    /// per-CLI login's) for the spend and the estimates, the weekly target and pacing line from <paramref name="settings"/>, and
    /// the meter probe's last word for each login in <paramref name="stateDb"/> for why a reading is stale, and each login's
    /// <paramref name="outside"/> use as the daemon measures it (M.ORCH-T682; keyed by email, "" for the global login).
    /// </summary>
    public static UsageReport Gather(OrchestratorDb db, Snapshot snapshot, AccountDirs dirs, OrchestratorSettings settings, DateTime nowUtc,
        DateOnly? since = null, ReportedUsage? globalReading = null, OrchestratorDb? stateDb = null, IReadOnlyList<OutsideUseStats>? outside = null)
    {
        var members = MemberRoster.Load(db);
        var current = snapshot.Account is { } key && key != ClaudeAccount.ApiKey ? key : null;
        var roster = AccountRoster.Load(db, current);
        var logins = new List<LoginReading>();
        var reasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var probe = ProbeNotes(stateDb);
        foreach (var dir in dirs.All())
        {
            string? alias = null;
            try { alias = LoginLook.For(dir.Directory, dir.Email).Alias; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            logins.Add(new LoginReading(dir.Email, dir.Account, alias, ReportedUsage.LoadAnyAge(dir.UsagePath)));
            if (!dir.SignedIn) reasons[dir.Email] = $"its login here isn't signed in (orchestrator login --account {dir.Email})";
            else if (probe.GetValueOrDefault(dir.Email) is { } note) reasons[dir.Email] = note;
        }
        var globalEmail = current is null ? null : roster.FirstOrDefault(r => r.Account == current)?.Email;
        if (globalEmail is not null && globalReading is not null && (globalReading.Account is null || globalReading.Account == current))
            logins.Add(new LoginReading(globalEmail, current, null, globalReading));
        if (globalEmail is not null && !reasons.ContainsKey(globalEmail))
            reasons[globalEmail] = "no Claude tab on the ~/.claude login has drawn its statusline lately, and the meter wasn't asked since";
        foreach (var r in roster)
            if (r.Email is { } email && !reasons.ContainsKey(email) && logins.All(l => !string.Equals(l.Email, email, StringComparison.OrdinalIgnoreCase)))
                reasons[email] = "not signed in on this machine: the reading is from when it last was";

        var estimates = new Dictionary<string, BudgetStatus>(StringComparer.Ordinal);
        if (current is not null && snapshot.Budget is { } global) estimates[current] = global;
        foreach (var a in snapshot.Accounts)
            if (a.Account is { } k) estimates[k] = a.Budget;
        string? OutsideOf(string email) => outside?.FirstOrDefault(o => string.Equals(o.Key, email, StringComparison.OrdinalIgnoreCase)
            || (o.Key.Length == 0 && string.Equals(globalEmail, email, StringComparison.OrdinalIgnoreCase))) is { } o ? OutsideText(o) : null;
        return Build(members, roster, logins, snapshot.History, estimates, settings.WeeklyStopPercent, nowUtc, since, current, OutsideOf,
                email => reasons.GetValueOrDefault(email)) with
        {
            PacingLinePercent = settings.WeeklyAwareLine,
            PacingOn = settings.WeeklyAwarePacing,
        };
    }

    /// <summary>A member's outside use for the spend table: "using Claude now; 3% a window, 6% a week on average (12 windows)";
    /// null (not measured yet) before any window was measured.</summary>
    public static string? OutsideText(OutsideUseStats o)
    {
        var now = o.InUse ? "using Claude now" : null;
        if (o.WindowCount == 0) return now;
        static string P(double v) => v.ToString(v < 10 ? "0.#" : "0", CultureInfo.InvariantCulture) + "%";
        var average = $"{P(o.PerWindow)} a window" + (o.PerWeek is { } w ? $", {P(w)} a week" : "") +
            $" on average ({o.WindowCount} window{(o.WindowCount == 1 ? "" : "s")})";
        return now is null ? average : $"{now}; {average}";
    }

    /// <summary>Each login's meter probe trouble from its <see cref="AccountPacer.ProbeStateName"/> row: why it isn't asking, or
    /// that its last asks failed.</summary>
    internal static Dictionary<string, string> ProbeNotes(OrchestratorDb? stateDb)
    {
        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (stateDb is null || !File.Exists(stateDb.Path) || stateDb.LoadState(AccountPacer.ProbeStateName) is not { } state) return notes;
            using var doc = JsonDocument.Parse(state.Value);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return notes;
            foreach (var login in doc.RootElement.EnumerateObject())
            {
                if (login.Value.ValueKind != JsonValueKind.Object) continue;
                string? Text(string name) => login.Value.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var failures = login.Value.TryGetProperty("failuresInRow", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
                if (Text("skipped") is { Length: > 0 } skipped) notes[login.Name] = $"the meter probe isn't asking: {skipped}";
                else if (failures > 0) notes[login.Name] = $"the meter probe failed {failures} time{(failures == 1 ? "" : "s")} in a row" +
                    (Text("lastResult") is { Length: > 0 } last ? $": {last}" : "");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Data.Common.DbException or InvalidOperationException) { }
        return notes;
    }

    // ---- The main table ---------------------------------------------------------------------------------------------------

    /// <summary>The main table's columns, in order.</summary>
    public IReadOnlyList<string> Headers =>
        ["Member", "5-hour used", "5h resets", "Week used", "Week left", "Week resets", "Week resets in", "Last updated"];

    /// <summary>The member's name as the table shows it: "Erin (~/.claude login)" for the global login's, "(not a member)" for a login nobody entered.</summary>
    public static string MemberText(UsageReportRow r) =>
        r.Name + (r.IsGlobalLogin ? " (~/.claude login)" : "") + (r.IsMember ? "" : " (not a member)");

    /// <summary>Each row's cells in <see cref="Headers"/>' order (times local).</summary>
    public IReadOnlyList<IReadOnlyList<string>> Cells() => [.. Rows.Select(r => (IReadOnlyList<string>)
    [
        MemberText(r),
        r.FiveHourUnknown ? "unknown" : Pct(r.FiveHourUsed),
        When(r.FiveHourResetUtc, GeneratedUtc),
        Pct(r.WeekUsed),
        Whole(r.WeekLeft),
        When(r.WeekResetUtc, GeneratedUtc),
        Countdown(r.WeekResetUtc, GeneratedUtc),
        Age(r.ReadUtc, GeneratedUtc),
    ])];

    /// <summary>A reset in local time: "2:20 PM today", "Fri 10/30, 9:00 AM"; "—" when not known.</summary>
    public static string When(DateTime? utc, DateTime nowUtc)
    {
        if (utc is not { } t) return "—";
        var local = t.ToLocalTime();
        return local.Date == nowUtc.ToLocalTime().Date
            ? local.ToString("h:mm tt", CultureInfo.InvariantCulture) + " today"
            : local.ToString("ddd M/d, h:mm tt", CultureInfo.InvariantCulture);
    }

    /// <summary>How long until a reset: "1d 2h 14m", "19h 22m", "14m", "now"; "—" when not known.</summary>
    public static string Countdown(DateTime? utc, DateTime nowUtc)
    {
        if (utc is not { } t) return "—";
        var left = t - nowUtc;
        if (left <= TimeSpan.Zero) return "now";
        var minutes = (long)Math.Ceiling(left.TotalMinutes);
        long d = minutes / (24 * 60), h = minutes / 60 % 24, m = minutes % 60;
        return d > 0 ? $"{d}d {h}h {m}m" : h > 0 ? $"{h}h {m}m" : $"{m}m";
    }

    /// <summary>"82 pts", "1 pt", "at target", "over by 4"; "—" when the week isn't known.</summary>
    public static string ToTarget(double? left)
    {
        if (left is not { } l) return "—";
        var points = (int)Math.Round(l, MidpointRounding.AwayFromZero);
        return points > 0 ? $"{points} pt{(points == 1 ? "" : "s")}" : points == 0 ? "at target" : $"over by {-points}";
    }

    /// <summary>How old a reading is: "just now", "3m ago", "2hr 44m ago", "1d 3hr ago"; "never".</summary>
    public static string Age(DateTime? utc, DateTime nowUtc)
    {
        if (utc is not { } t) return "never";
        var d = nowUtc - t;
        if (d < TimeSpan.FromMinutes(1)) return "just now";
        if (d < TimeSpan.FromHours(1)) return $"{(int)d.TotalMinutes}m ago";
        if (d < TimeSpan.FromDays(1)) return d.Minutes == 0 ? $"{(int)d.TotalHours}hr ago" : $"{(int)d.TotalHours}hr {d.Minutes}m ago";
        return d.Hours == 0 ? $"{(int)d.TotalDays}d ago" : $"{(int)d.TotalDays}d {d.Hours}hr ago";
    }

    /// <summary>A percent left, whole: "9%".</summary>
    private static string Whole(double? p) => p is { } v ? Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%" : "—";

    /// <summary>A percent used, with a decimal from 90 on (as the dashboard shows the last stretch): "48%", "91.0%".</summary>
    private static string Pct(double? p) => p is { } v ? v.ToString(v >= Formatting.FiveHourTenthsFrom && v < 100 ? "0.0" : "0", CultureInfo.InvariantCulture) + "%" : "—";

    /// <summary>Where the main table's members are at and where work should go: one line each, only those that apply.</summary>
    public IReadOnlyList<string> Notes()
    {
        var notes = new List<string>();
        var known = Rows.Where(r => r.WeekUsed is not null).ToList();
        var atTarget = known.Where(r => r.WeekUsed >= WeeklyTargetPercent).ToList();
        if (atTarget.Count > 0)
            notes.Add($"At or past the {WeeklyTargetPercent}% weekly target, so no new work until their week resets: " +
                string.Join(", ", atTarget.Select(r => $"{r.Name} ({When(r.WeekResetUtc, GeneratedUtc)}, in {Countdown(r.WeekResetUtc, GeneratedUtc)})")) + ".");
        var paced = known.Where(r => r.WeekUsed >= PacingLinePercent && r.WeekUsed < WeeklyTargetPercent).OrderByDescending(r => r.WeekUsed).ToList();
        if (paced.Count > 0)
            notes.Add((PacingOn
                    ? $"Past the {PacingLinePercent}% line, weekly-aware pacing is limiting them: "
                    : $"Past the {PacingLinePercent}% line (weekly-aware pacing is off, so nothing slows them before the target): ") +
                string.Join(", ", paced.Select(r => $"{r.Name} ({ToTarget(r.LeftToTarget)} left)")) + ".");
        var room = known.Where(r => r.WeekUsed < PacingLinePercent && !(r.FiveHourUsed >= 90))
            .OrderByDescending(r => r.LeftToTarget).ToList();
        if (room.Count > 0)
            notes.Add("Plenty of room, where most of the work should go: " +
                string.Join(", ", room.Select(r => $"{r.Name} ({ToTarget(r.LeftToTarget)} to the target)")) + ".");
        var stale = Rows.Where(r => r.Stale).ToList();
        if (stale.Count > 0)
            notes.Add("Stale: " + string.Join("; ", stale.Select(r =>
                $"{r.Name} ({(r.ReadUtc is null ? "never read" : "read " + Age(r.ReadUtc, GeneratedUtc))}): {r.StaleReason ?? "why isn't known"}")) + ".");
        return notes;
    }

    // ---- The detail table --------------------------------------------------------------------------------------------------

    /// <summary>The second table's columns: where each reading came from, what the orchestrator's CLIs spent on each login, and the member's own use.</summary>
    public IReadOnlyList<string> DetailHeaders =>
        ["Member", "Email", "Source", "CLIs today", "CLIs this week", .. Since is { } s ? [$"CLIs since {s:yyyy-MM-dd}"] : Array.Empty<string>(), "Outside use"];

    public IReadOnlyList<IReadOnlyList<string>> DetailCells() => [.. Rows.Select(r => (IReadOnlyList<string>)
    [
        MemberText(r), r.Email, r.Source ?? "no reading",
        Formatting.Tokens(r.CliToday), Formatting.Tokens(r.CliWeek), .. r.CliSince is { } s ? [Formatting.Tokens(s)] : Array.Empty<string>(),
        r.OutsideUse ?? NotMeasured,
    ])];

    /// <summary>The API key's line under the detail table.</summary>
    public string ApiKeyLine =>
        $"API key (billed off the subscription): {Formatting.Tokens(ApiKeyToday)} today, {Formatting.Tokens(ApiKeyWeek)} in the last 7 days" +
        (ApiKeySince is { } s ? $", {Formatting.Tokens(s)} since {Since:yyyy-MM-dd}" : "") + ".";

    private const string SpendNote = "CLI spend is uncached tokens (input + cache writes + output) the orchestrator's CLIs spent on that login.";

    public string Title => $"Usage report, {GeneratedUtc.ToLocalTime().ToString("ddd M/d/yyyy h:mm tt", CultureInfo.InvariantCulture)}";

    // ---- Formats -----------------------------------------------------------------------------------------------------------

    /// <summary>The report as plain text for a terminal: the main table in aligned columns and its notes, and with
    /// <paramref name="detail"/> the second table and the API key's line.</summary>
    public string PlainText(bool detail = false)
    {
        var sb = new StringBuilder();
        if (Rows.Count == 0) return "No members yet (orchestrator members add) and no Claude login on this machine.\n";
        Aligned(sb, Headers, Cells());
        foreach (var note in Notes()) sb.Append('\n').Append(note);
        sb.Append('\n');
        if (detail)
        {
            sb.Append('\n');
            Aligned(sb, DetailHeaders, DetailCells());
            sb.Append('\n').Append(ApiKeyLine).Append('\n').Append(SpendNote).Append('\n');
        }
        return sb.ToString();

        static void Aligned(StringBuilder sb, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> cells)
        {
            var widths = headers.Select((h, i) => cells.Select(c => c[i].Length).Prepend(h.Length).Max()).ToList();
            void Line(IEnumerable<string> row) => sb.Append(string.Join("  ", row.Select((c, i) => c.PadRight(widths[i]))).TrimEnd()).Append('\n');
            Line(headers);
            Line(widths.Select(w => new string('-', w)));
            foreach (var row in cells) Line(row);
        }
    }

    /// <summary>One row per member, raw values (percents as numbers, times ISO 8601 UTC, tokens as counts), then the API key's.</summary>
    public string Csv()
    {
        var sb = new StringBuilder("member,email,is_member,global_login,five_hour_used,five_hour_unknown,five_hour_resets_utc,week_used,week_left,left_to_target,week_resets_utc,read_utc,accurate,stale_reason,source,cli_today,cli_week,cli_since,outside_use\n");
        foreach (var r in Rows)
            sb.AppendJoin(',', Q(r.Name), Q(r.Email), B(r.IsMember), B(r.IsGlobalLogin), N(r.FiveHourUsed), B(r.FiveHourUnknown), T(r.FiveHourResetUtc),
                N(r.WeekUsed), N(r.WeekLeft), N(r.LeftToTarget), T(r.WeekResetUtc), T(r.ReadUtc), r.ReadUtc is null ? "no reading" : r.Stale ? "stale" : "yes",
                Q(r.StaleReason), Q(r.Source), I(r.CliToday), I(r.CliWeek), r.CliSince is { } s ? I(s) : "", Q(r.OutsideUse ?? NotMeasured)).Append('\n');
        sb.AppendJoin(',', "API key", "", "false", "false", "", "false", "", "", "", "", "", "", "", "", "", I(ApiKeyToday), I(ApiKeyWeek),
            ApiKeySince is { } since ? I(since) : "", "").Append('\n');
        return sb.ToString();

        static string Q(string? s) => s is null ? "" : s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        static string N(double? v) => v?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
        static string T(DateTime? t) => t?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "";
        static string B(bool b) => b ? "true" : "false";
        static string I(long n) => n.ToString(CultureInfo.InvariantCulture);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The report as JSON: the rows' raw values (an unmeasured outside use is null), the notes and the API key's spend.</summary>
    public string Json() => JsonSerializer.Serialize(new
    {
        generatedUtc = GeneratedUtc,
        since = Since?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        weeklyTargetPercent = WeeklyTargetPercent,
        pacingLinePercent = PacingLinePercent,
        pacingOn = PacingOn,
        staleAfterMinutes = StaleAfter.TotalMinutes,
        members = Rows,
        notes = Notes(),
        apiKey = new { today = ApiKeyToday, week = ApiKeyWeek, since = ApiKeySince },
    }, JsonOptions);

    /// <summary>The report as a Markdown page to share: the main table (members in bold) and its notes, then the detail table
    /// and the API key's line.</summary>
    public string Markdown()
    {
        var sb = new StringBuilder($"# {Title}\n\n");
        if (Rows.Count == 0) return sb.Append("No members yet (orchestrator members add) and no Claude login on this machine.\n").ToString();
        Table(sb, Headers, Cells(), bold: true);
        sb.Append('\n');
        foreach (var note in Notes()) sb.Append("- ").Append(Md(note)).Append('\n');
        sb.Append("\n## Spend\n\n");
        Table(sb, DetailHeaders, DetailCells(), bold: true);
        sb.Append('\n').Append(Md(ApiKeyLine)).Append(' ').Append(SpendNote).Append('\n');
        return sb.ToString();

        static void Table(StringBuilder sb, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> cells, bool bold)
        {
            sb.Append("| ").AppendJoin(" | ", headers.Select(Md)).Append(" |\n");
            sb.Append('|').AppendJoin("|", headers.Select(_ => " --- ")).Append("|\n");
            foreach (var row in cells)
                sb.Append("| ").AppendJoin(" | ", row.Select((c, i) => i == 0 && bold ? $"**{Md(c)}**" : Md(c))).Append(" |\n");
        }
        static string Md(string s) => s.Replace("|", "\\|").Replace("*", "\\*").Replace("\n", " ");
    }

    /// <summary>The same report as a self-contained HTML page (no scripts, no external files), light or dark as the reader's system is.</summary>
    public string Html()
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append($"<title>{E(Title)}</title>\n");
        sb.Append("""
            <style>
            :root { --bg: #ffffff; --fg: #1b1b1f; --muted: #5d5d68; --line: #d9d9e0; --head: #f3f3f6; --stale: #9a5b00; }
            @media (prefers-color-scheme: dark) { :root { --bg: #16161a; --fg: #ececf1; --muted: #a3a3ad; --line: #34343c; --head: #202026; --stale: #f0b35a; } }
            body { background: var(--bg); color: var(--fg); font: 14px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif; margin: 24px 16px; }
            h1 { font-size: 20px; margin: 0 0 12px; }
            h2 { font-size: 16px; margin: 24px 0 8px; }
            p, li { color: var(--muted); max-width: 70em; }
            .wrap { overflow-x: auto; }
            table { border-collapse: collapse; font-variant-numeric: tabular-nums; }
            th, td { border-bottom: 1px solid var(--line); padding: 6px 10px; text-align: left; white-space: nowrap; }
            th { background: var(--head); font-weight: 600; }
            td:first-child { font-weight: 600; }
            td.stale { color: var(--stale); }
            </style>
            </head>
            <body>

            """);
        sb.Append($"<h1>{E(Title)}</h1>\n");
        if (Rows.Count == 0) return sb.Append("<p>No members yet (orchestrator members add) and no Claude login on this machine.</p>\n</body>\n</html>\n").ToString();
        Table(sb, Headers, Cells(), Rows, staleColumn: Headers.Count - 1);
        sb.Append("<ul>\n");
        foreach (var note in Notes()) sb.Append($"<li>{E(note)}</li>\n");
        sb.Append("</ul>\n<h2>Spend</h2>\n");
        Table(sb, DetailHeaders, DetailCells(), Rows, staleColumn: -1);
        sb.Append($"<p>{E(ApiKeyLine)} {E(SpendNote)}</p>\n</body>\n</html>\n");
        return sb.ToString();

        static void Table(StringBuilder sb, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> cells, IReadOnlyList<UsageReportRow> rows, int staleColumn)
        {
            sb.Append("<div class=\"wrap\"><table>\n<thead><tr>");
            foreach (var h in headers) sb.Append($"<th scope=\"col\">{E(h)}</th>");
            sb.Append("</tr></thead>\n<tbody>\n");
            for (var i = 0; i < cells.Count; i++)
            {
                sb.Append("<tr>");
                for (var c = 0; c < cells[i].Count; c++)
                    sb.Append(c == staleColumn && rows[i].Stale ? $"<td class=\"stale\">{E(cells[i][c])}</td>" : $"<td>{E(cells[i][c])}</td>");
                sb.Append("</tr>\n");
            }
            sb.Append("</tbody>\n</table></div>\n");
        }
    }

    /// <summary>Writes the report to <paramref name="path"/>: HTML for .html/.htm, else Markdown. Its folder is made when missing.</summary>
    public void WriteFile(string path)
    {
        var ext = Path.GetExtension(path);
        var html = ext.Equals(".html", StringComparison.OrdinalIgnoreCase) || ext.Equals(".htm", StringComparison.OrdinalIgnoreCase);
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        File.WriteAllText(path, html ? Html() : Markdown());
    }

    /// <summary>Where the dashboard's Report button writes its pages: <c>reports</c> in the orchestrator's store folder.</summary>
    public static string DefaultFolder(string storeDirectory) => Path.Combine(storeDirectory, "reports");

    /// <summary>A file name for the report taken at <paramref name="nowUtc"/>: "usage-2026-10-23-1405.html".</summary>
    public static string FileName(DateTime nowUtc, string extension) =>
        $"usage-{nowUtc.ToLocalTime().ToString("yyyy-MM-dd-HHmm", CultureInfo.InvariantCulture)}{extension}";
}
