using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>Who a Claude account is, from <c>oauthAccount</c> in <c>~/.claude.json</c> while it's signed in: the only place its
/// email is ever written, so the roster keeps it once seen.</summary>
public sealed record AccountProfile(string Account, string? Email, string? Org, string? Plan)
{
    /// <summary>The signed-in account's profile, keyed as <see cref="ClaudeAccount.Current"/> keys it; null when signed out or
    /// on an API key.</summary>
    internal static AccountProfile? Read(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("oauthAccount", out var o) ||
            o.ValueKind != JsonValueKind.Object || Text(o, "accountUuid") is not { } id)
            return null;
        var account = Text(o, "organizationUuid") is { } org ? $"{id}/{org}" : id;
        return new AccountProfile(account, Text(o, "emailAddress"), Text(o, "organizationName"), PlanOf(Text(o, "organizationType"), Text(o, "userRateLimitTier")));
    }

    /// <summary>"Team · Max 5x", "Max 20x", "Pro": the organization's kind, and the Max tier the account's limits are sized by.</summary>
    internal static string? PlanOf(string? organizationType, string? rateLimitTier)
    {
        var kind = organizationType switch
        {
            "claude_team" => "Team",
            "claude_enterprise" => "Enterprise",
            "claude_max" => "Max",
            "claude_pro" => "Pro",
            null => null,
            _ => organizationType.Replace("claude_", "", StringComparison.Ordinal),
        };
        var tier = rateLimitTier is null ? null
            : rateLimitTier.Contains("max_20x", StringComparison.Ordinal) ? "Max 20x"
            : rateLimitTier.Contains("max_5x", StringComparison.Ordinal) ? "Max 5x"
            : null;
        if (kind == "Max" && tier is not null) return tier;
        return kind is null ? tier : tier is null ? kind : $"{kind} · {tier}";
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;
}

/// <summary>
/// One Claude account's row in the roster (M.ORCH-T490): who it is and its last reading of Claude's meter, with the time of
/// that reading. A window whose reset has passed reads 0% used, and a weekly reset rolls on by whole weeks, so an account
/// not signed in for days still says how much it has left and when it resets.
/// </summary>
public sealed record AccountRow
{
    /// <summary>The account key: <c>accountUuid/organizationUuid</c>, as <see cref="ClaudeAccount"/> keys its spans.</summary>
    public string Account { get; init; } = "";
    public string? Email { get; init; }
    public string? Org { get; init; }
    public string? Plan { get; init; }
    public double? WeekPercent { get; init; }
    public DateTime? WeekResetUtc { get; init; }
    public double? FiveHourPercent { get; init; }
    public DateTime? FiveHourResetUtc { get; init; }
    /// <summary>When the percents were read from Claude's meter; null while only the resets are known.</summary>
    public DateTime? ReadUtc { get; init; }
    /// <summary>When this account was last signed in to (the start of its latest span).</summary>
    public DateTime? SignedInUtc { get; init; }

    /// <summary>The email, or the start of the account id until its email is seen at a sign-in.</summary>
    public string Label => Email ?? (Account.Length > 8 ? Account[..8] + "…" : Account);

    /// <summary>The weekly percent used now: 0 once its reset has passed, null when it was never read.</summary>
    public double? WeekUsedAt(DateTime nowUtc) => WeekResetUtc is { } r && r <= nowUtc ? 0 : WeekPercent;

    public double? WeekLeftAt(DateTime nowUtc) => WeekUsedAt(nowUtc) is { } used ? Math.Max(0, 100 - used) : null;

    /// <summary>The next weekly reset: the known one, rolled on a week at a time once it has passed.</summary>
    public DateTime? WeekResetAt(DateTime nowUtc)
    {
        if (WeekResetUtc is not { } r) return null;
        if (r > nowUtc) return r;
        var weeks = Math.Floor((nowUtc - r).TotalDays / 7) + 1;
        return r.AddDays(7 * weeks);
    }

    /// <summary>The 5-hour percent used now: 0 once its window has reset, null when it was never read.</summary>
    public double? FiveHourUsedAt(DateTime nowUtc) => FiveHourResetUtc is { } r && r <= nowUtc ? 0 : FiveHourPercent;

    /// <summary>When the running 5-hour window resets; null once it has (the next starts at the account's next use).</summary>
    public DateTime? FiveHourResetAt(DateTime nowUtc) => FiveHourResetUtc is { } r && r > nowUtc ? r : null;
}

/// <summary>
/// Every Claude account signed in to on this machine, with its latest 5-hour and weekly used percent and resets
/// (M.ORCH-T490), in the <c>accounts</c> table of the repo's <c>data/orchestrator.db</c> (apart from the per-account token
/// ledger), with <c>data/orchestrator.sql</c> rewritten beside it on each change. Each sign-in adds or refreshes the account's row from Claude's meter; while it
/// stays signed in, the row is refreshed on every <see cref="Step"/>-point move in its weekly percent (taking the 5-hour
/// percent and both resets with it) or a new window, and the caller reads the meter again then. Accounts not signed in keep
/// their last reading.
/// </summary>
public static class AccountRoster
{
    /// <summary>How far the weekly percent moves before the row is refreshed.</summary>
    public const double Step = 1;

    /// <summary>Every row, the signed-in account (<paramref name="current"/>) first, then by label.</summary>
    public static IReadOnlyList<AccountRow> Load(OrchestratorDb db, string? current = null)
    {
        using var connection = db.Open();
        return [.. Rows(connection).OrderBy(r => r.Account != current).ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Whether <paramref name="reading"/>'s week has moved from <paramref name="row"/>'s by <see cref="Step"/> or more
    /// (or is another week, or the row has none yet).</summary>
    public static bool Moved(AccountRow? row, ReportedUsage? reading, DateTime nowUtc)
    {
        if (reading?.Week is not { } w) return false;
        if (row?.WeekUsedAt(nowUtc) is not { } used) return true;
        return Math.Abs(w.Percent - used) >= Step || !SignInState.Same(w.ResetUtc, row.WeekResetAt(nowUtc));
    }

    /// <summary>
    /// Brings the roster up to date for <paramref name="signIn"/>: a row for every account it knows (its spans and confirmed
    /// windows, so accounts signed in before the roster existed show up with their resets), the signed-in account's
    /// <paramref name="profile"/>, and <paramref name="reading"/> (Claude's meter, already known to be the signed-in account's)
    /// when its row has no reading since the account was signed in to, the week has moved a point, or a window is new. Written only when something changed, in one transaction.
    /// Returns whether the week rose a point since this sign-in's last reading, so the caller reads the meter again.
    /// </summary>
    public static bool Observe(OrchestratorDb db, SignInState signIn, AccountProfile? profile, ReportedUsage? reading, DateTime nowUtc)
    {
        using var connection = db.Open();
        using var tx = connection.BeginTransaction();
        var rows = Rows(connection, tx);
        var changed = new List<AccountRow>();
        var known = signIn.History.Select(s => s.Account).Concat(signIn.Confirmed.Keys).Where(a => a != ClaudeAccount.ApiKey).Distinct().ToList();
        foreach (var account in known.Where(a => rows.All(r => r.Account != a)))
        {
            var confirmed = signIn.Confirmed.GetValueOrDefault(account);
            var added = new AccountRow
            {
                Account = account,
                WeekResetUtc = confirmed?.Week,
                FiveHourResetUtc = confirmed?.FiveHour,
                SignedInUtc = signIn.History.LastOrDefault(s => s.Account == account)?.From,
            };
            rows.Add(added);
            changed.Add(added);
        }
        var readAgain = false;
        if (!signIn.IsApiKey)
        {
            var row = rows.Single(r => r.Account == signIn.Account);
            var was = row;
            if (profile is not null && profile.Account == signIn.Account)
                row = row with { Email = profile.Email ?? row.Email, Org = profile.Org ?? row.Org, Plan = profile.Plan ?? row.Plan };
            if (row.SignedInUtc != signIn.Since) row = row with { SignedInUtc = signIn.Since };
            var moved = Moved(row, reading, nowUtc);
            if (reading is not null && (reading.Week is not null || reading.FiveHour is not null) &&
                (row.ReadUtc is not { } read || reading.AtUtc >= read) &&
                (row.ReadUtc is null || row.ReadUtc < signIn.Since || moved || NewWindow(row.FiveHourResetUtc, reading.FiveHour) || NewWindow(row.WeekResetUtc, reading.Week)))
            {
                var updated = row with
                {
                    WeekPercent = reading.Week?.Percent ?? row.WeekPercent,
                    WeekResetUtc = reading.Week?.ResetUtc ?? row.WeekResetUtc,
                    FiveHourPercent = reading.FiveHour?.Percent ?? row.FiveHourPercent,
                    FiveHourResetUtc = reading.FiveHour?.ResetUtc ?? row.FiveHourResetUtc,
                    ReadUtc = reading.AtUtc,
                };
                // A point's rise on a week this sign-in already read: Claude's meter is read again for it. A first reading, or
                // one of a new week, is the meter's own, just taken.
                readAgain = moved && row.ReadUtc is { } prior && prior >= signIn.Since && row.WeekPercent is not null &&
                    reading.Week is { } w && SignInState.Same(w.ResetUtc, row.WeekResetUtc) && reading.AtUtc > prior;
                row = updated;
            }
            if (row != was) changed.Add(row);
        }
        foreach (var row in changed.GroupBy(r => r.Account).Select(g => g.Last())) Upsert(connection, tx, row);
        string? dump = changed.Count > 0 ? OrchestratorDb.Dump(connection, tx) : null;
        tx.Commit();
        if (dump is not null) db.WriteDump(dump);
        return readAgain;

        static bool NewWindow(DateTime? known, ReportedWindow? w) => w is not null && !SignInState.Same(known, w.ResetUtc);
    }

    private static List<AccountRow> Rows(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at FROM accounts;";
        using var r = command.ExecuteReader();
        var rows = new List<AccountRow>();
        while (r.Read())
            rows.Add(new AccountRow
            {
                Account = r.GetString(0),
                Email = Text(r, 1),
                Org = Text(r, 2),
                Plan = Text(r, 3),
                FiveHourPercent = Number(r, 4),
                FiveHourResetUtc = At(Number(r, 5)),
                WeekPercent = Number(r, 6),
                WeekResetUtc = At(Number(r, 7)),
                ReadUtc = At(Number(r, 8)),
                SignedInUtc = At(Number(r, 9)),
            });
        return rows;

        static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        static double? Number(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetDouble(i);
    }

    private static void Upsert(SqliteConnection connection, SqliteTransaction tx, AccountRow row)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO accounts (id, email, org, plan, five_hour_percent, five_hour_reset, week_percent, week_reset, read_at, signed_in_at)
            VALUES ($id, $email, $org, $plan, $fp, $fr, $wp, $wr, $read, $signed)
            ON CONFLICT (id) DO UPDATE SET email = excluded.email, org = excluded.org, plan = excluded.plan,
                five_hour_percent = excluded.five_hour_percent, five_hour_reset = excluded.five_hour_reset,
                week_percent = excluded.week_percent, week_reset = excluded.week_reset,
                read_at = excluded.read_at, signed_in_at = excluded.signed_in_at;
            """;
        Add(command, "$id", row.Account);
        Add(command, "$email", row.Email);
        Add(command, "$org", row.Org);
        Add(command, "$plan", row.Plan);
        Add(command, "$fp", row.FiveHourPercent);
        Add(command, "$fr", Epoch(row.FiveHourResetUtc));
        Add(command, "$wp", row.WeekPercent);
        Add(command, "$wr", Epoch(row.WeekResetUtc));
        Add(command, "$read", Epoch(row.ReadUtc));
        Add(command, "$signed", Epoch(row.SignedInUtc));
        command.ExecuteNonQuery();
    }

    private static void Add(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static double? Epoch(DateTime? utc) => utc is { } t ? (t - DateTime.UnixEpoch).TotalSeconds : null;

    /// <summary>Back from Unix seconds, to the millisecond: a double's seconds don't round-trip every tick.</summary>
    private static DateTime? At(double? seconds) => seconds is { } s ? DateTime.UnixEpoch.AddMilliseconds(Math.Round(s * 1000)) : null;

    /// <summary>"75%" or "—" when never read.</summary>
    public static string Percent(double? p) => p is { } v ? v.ToString("0", CultureInfo.InvariantCulture) + "%" : "—";

    /// <summary>One line per row, for the dashboard: "pdftransferjr@mindattic.com (signed in): week 75% used, 25% left, resets
    /// Sun 4:00 PM · 5h 12%, resets 9:30 PM · read 3m ago".</summary>
    public static string Line(AccountRow row, string? current, DateTime nowUtc)
    {
        var week = row.WeekUsedAt(nowUtc) is null ? "week —" : $"week {Percent(row.WeekUsedAt(nowUtc))} used, {Percent(row.WeekLeftAt(nowUtc))} left";
        if (row.WeekResetAt(nowUtc) is { } wr) week += $", resets {Formatting.ResetTime(wr, nowUtc)}";
        var five = $"5h {Percent(row.FiveHourUsedAt(nowUtc))}";
        if (row.FiveHourResetAt(nowUtc) is { } fr) five += $", resets {Formatting.ResetTime(fr, nowUtc)}";
        var read = row.ReadUtc is { } r ? $"read {Formatting.Ago(r, nowUtc)}" : "not read yet";
        var who = row.Label + (row.Account == current ? " (signed in)" : "") + (row.Plan is { } plan ? $" [{plan}]" : "");
        return $"{who}: {week} · {five} · {read}";
    }
}
