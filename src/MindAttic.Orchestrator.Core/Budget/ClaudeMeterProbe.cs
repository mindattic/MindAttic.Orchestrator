using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// Reads Claude's meter itself when no statusline is reporting it while tokens are being spent (M.ORCH-T203).
/// <c>claude-usage.json</c> is written by the statusline, and a statusline carries only its own session's last reply:
/// a CLI waiting on foreground subagents spends millions of tokens through them while its statusline keeps stamping the
/// percent from before they started, which the statusline (rightly) won't write over a higher one. The Oct 17 window
/// went blind that way for 2h46m (every token in the gaps was a subagent's). So once the last report is
/// <see cref="StaleAfter"/> old and the transcripts show tokens spent since it, the lease holder asks Claude's own usage
/// endpoint (what <c>/usage</c> shows) and writes the answer to <c>claude-usage.json</c> in the statusline's format,
/// where the samples, the budget and the pace hooks already read it.
/// </summary>
/// <remarks>Uses Claude Code's saved sign-in read-only: an expired token is skipped (Claude Code refreshes it), never
/// refreshed here, since refreshing rotates the token Claude Code holds.</remarks>
public sealed class ClaudeMeterProbe
{
    /// <summary>A report this old, with tokens spent since, is refreshed by asking Claude directly. Well inside the
    /// pace ledger's <see cref="PaceLedger.MinGap"/>, so a window no longer goes blind while anything is spending.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(4);

    /// <summary>How long to wait after a failed ask (no sign-in, an expired token Claude Code is about to refresh, rejected,
    /// offline) before trying again. Kept well under <see cref="PaceLedger.MinGap"/> (M.ORCH-T647): at 15 minutes a few failed
    /// asks in a row left the window unseen for an hour while tokens were spent. An ask is one small request, and only
    /// happens while tokens are being spent.</summary>
    public static readonly TimeSpan Backoff = TimeSpan.FromMinutes(2);

    /// <summary>The wait after an API-key login, which has no meter to read at all (so no point asking again soon).</summary>
    public static readonly TimeSpan NoMeterBackoff = TimeSpan.FromMinutes(15);

    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string DefaultCredentialsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    /// <summary>Where the reading is written; the statusline's file by default.</summary>
    public string UsagePath { get; init; } = ReportedUsage.DefaultPath;

    /// <summary>Where the readings go (M.ORCH-T528): the <see cref="ReportedUsage.StateName"/> row of this database, so the
    /// statusline's <see cref="UsagePath"/> is read and never replaced. The default database for the real file; null (a
    /// test's own file) writes the file as before.</summary>
    public Settings.OrchestratorDb? Db { get; init; }
    private Settings.OrchestratorDb? Store => Db ?? (UsagePath == ReportedUsage.DefaultPath ? ReportedUsage.DefaultDb : null);

    public string CredentialsPath { get; init; } = DefaultCredentialsPath;

    /// <summary>The account signed in now (<see cref="ClaudeAccount.Current"/>), which each reading is tagged with: after a
    /// /login, only a reading tied to the new account is taken for it. Null tags nothing.</summary>
    public Func<string?>? AccountKey { get; init; }

    /// <summary>Gets Claude's usage JSON; null when it couldn't. Tests swap it.</summary>
    internal Func<string, CancellationToken, Task<string?>> Fetch { get; init; } = FetchAsync;

    private readonly object gate = new();
    private DateTime nextUtc;
    /// <summary>Bumped by <see cref="Reset"/>: an ask started before it that then fails doesn't put the backoff back (M.ORCH-T342).</summary>
    private int generation;

    /// <summary>The ask in flight (or the last one), for tests to await.</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>What the last ask did, for the log and tests.</summary>
    public string? LastResult { get; private set; }

    /// <summary>The last reading written (as written, with its time and account); null before one. Every open CLI's statusline
    /// rewrites <see cref="UsagePath"/> each minute, an idle one with its last reply from a window that already reset, so the
    /// written reading could be gone before the next refresh read it (M.ORCH-T479): the refresh takes it from here too
    /// (<see cref="Fresher"/>).</summary>
    public ReportedUsage? LastReading { get; private set; }

    /// <summary>Whether a fresh reading is needed: the last report (null when there is none) is
    /// <see cref="StaleAfter"/> old and tokens were spent after it, the latest of them within <see cref="StaleAfter"/>.</summary>
    public static bool Due(DateTime? reportedAtUtc, IReadOnlyList<UsageEvent> events, DateTime nowUtc)
    {
        if (reportedAtUtc is { } at && nowUtc - at < StaleAfter) return false;
        var since = reportedAtUtc ?? nowUtc - BudgetCalculator.BlockLength;
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.AtUtc > nowUtc) continue;
            if (e.AtUtc <= since || nowUtc - e.AtUtc > StaleAfter) return false;
            if (e.Usage.Fresh > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// When Claude's meter was last seen for the window still running, for <see cref="Due"/> (M.ORCH-T479): the report's time
    /// only when it is a reading of that window no lower than one already recorded for it. Every open CLI's statusline
    /// restamps <c>claude-usage.json</c> each minute, an idle one with its last reply from a window that already reset (or a
    /// lower percent), so the report's own time stayed under <see cref="StaleAfter"/> and the probe never asked: the Oct 19
    /// 06:20-11:20Z window went unseen for 1h55m while 3.5M were spent. Otherwise the latest sample of the running window;
    /// null when there is none.
    /// </summary>
    public static DateTime? SeenAt(ReportedUsage? reported, IEnumerable<UsageSample> samples, DateTime nowUtc)
    {
        var running = samples.Where(s => s.ResetUtc > nowUtc && s.AtUtc <= nowUtc).ToList();
        if (ReportedUsage.Usable(reported?.FiveHour, nowUtc) is { } w &&
            !running.Any(s => s.ResetUtc == w.ResetUtc && s.Percent > w.Percent))
            return reported!.AtUtc;
        return running.Count == 0 ? null : running.Max(s => s.AtUtc);
    }

    /// <summary><paramref name="probed"/> (the probe's last reading) where it says more about the running window than
    /// <paramref name="file"/> (what <c>claude-usage.json</c> holds now): the file has no running 5-hour window, or a lower
    /// percent for the same one. Otherwise the file.</summary>
    public static ReportedUsage? Fresher(ReportedUsage? file, ReportedUsage? probed, DateTime nowUtc)
    {
        if (ReportedUsage.Usable(probed?.FiveHour, nowUtc) is not { } p) return file;
        if (ReportedUsage.Usable(file?.FiveHour, nowUtc) is not { } f) return probed;
        return f.ResetUtc == p.ResetUtc && p.Percent > f.Percent ? probed : file;
    }

    /// <summary>Starts an ask in the background when <see cref="Due"/> and none is in flight or backing off; returns
    /// whether it started. The reading lands in <see cref="UsagePath"/> for the next refresh to pick up.</summary>
    /// <param name="force">Ask whether or not it's due (still one at a time, and not while backing off): the account
    /// just signed in to has no window confirmed yet.</param>
    public bool Tick(DateTime? reportedAtUtc, IReadOnlyList<UsageEvent> events, DateTime nowUtc, bool force = false)
    {
        if (!force && !Due(reportedAtUtc, events, nowUtc)) return false;
        lock (gate)
        {
            if (!Pending.IsCompleted || nowUtc < nextUtc) return false;
            nextUtc = nowUtc + StaleAfter;
            var asked = generation;
            Pending = Task.Run(() => AskAsync(nowUtc, asked));
        }
        return true;
    }

    /// <summary>Drops the wait after the last ask, so the next forced <see cref="Tick"/> asks at once: a switch to another
    /// account is read within seconds, not after the old one's backoff.</summary>
    public void Reset()
    {
        lock (gate)
        {
            nextUtc = default;
            generation++;
        }
    }

    private async Task AskAsync(DateTime nowUtc, int asked)
    {
        var account = Who();
        if (account == ClaudeAccount.ApiKey)
        {
            Fail("an API key has no subscription meter to read", NoMeterBackoff);
            return;
        }
        string? token = null;
        try { token = Token(CredentialsPath, DateTime.UtcNow); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        if (token is null)
        {
            Fail("no usable Claude sign-in to read the meter with");
            return;
        }
        string? body;
        try { body = await Fetch(token, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException) { body = null; }
        if (body is null || Parse(body) is not { } reading)
        {
            Fail("Claude's usage endpoint gave no reading");
            return;
        }
        // The credentials and the config are both rewritten by a /login: a token read just before one is the old account's.
        if (Who() != account)
        {
            LastResult = "the Claude sign-in changed while reading the meter";
            return;
        }
        try
        {
            LastReading = Write(UsagePath, reading, DateTime.UtcNow, account, Store);
            LastResult = $"read Claude's meter: {reading.FiveHour?.Percent:0}% of the 5-hour window";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            // A reader or the statusline had the file (or a write the database) at that moment: try again at the next due tick, not after a backoff.
            LastResult = $"couldn't write {Path.GetFileName(UsagePath)}: {ex.Message}";
        }

        string? Who()
        {
            try { return AccountKey?.Invoke(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        }

        void Fail(string why, TimeSpan? wait = null)
        {
            LastResult = why;
            // The old account's ask failing (its credentials mid-rewrite by the /login) mustn't hold off reading the new one.
            lock (gate)
                if (generation == asked) nextUtc = nowUtc + (wait ?? Backoff);
        }
    }

    /// <summary>Claude Code's saved access token, or null when there's none or it has expired.</summary>
    internal static string? Token(string path, DateTime nowUtc)
    {
        if (!File.Exists(path)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var o) || o.ValueKind != JsonValueKind.Object ||
            !o.TryGetProperty("accessToken", out var t) || t.ValueKind != JsonValueKind.String || t.GetString() is not { Length: > 0 } token)
            return null;
        if (o.TryGetProperty("expiresAt", out var x) && x.ValueKind == JsonValueKind.Number &&
            DateTimeOffset.FromUnixTimeMilliseconds(x.GetInt64()).UtcDateTime <= nowUtc)
            return null;
        return token;
    }

    private static async Task<string?> FetchAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd("MindAttic.Orchestrator");
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false) : null;
    }

    /// <summary>The endpoint's 5-hour and 7-day windows; null when it has no 5-hour one. Resets come back a fraction of a
    /// second off the statusline's (20:29:59.89 for 20:30), so they're rounded to the minute to match its windows.</summary>
    internal static ReportedUsage? Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var five = Window(doc.RootElement, "five_hour");
            return five is null ? null : new ReportedUsage(five, Window(doc.RootElement, "seven_day"), default);
        }
        catch (JsonException) { return null; }

        static ReportedWindow? Window(JsonElement root, string name) =>
            root.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Object &&
            w.TryGetProperty("utilization", out var u) && u.ValueKind == JsonValueKind.Number &&
            w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(r.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var reset)
                ? new ReportedWindow(u.GetDouble(), new DateTime((reset.Ticks + TimeSpan.TicksPerMinute / 2) / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc))
                : null;
    }

    /// <summary>Writes <paramref name="reading"/> as the statusline does, stamped <paramref name="atUtc"/> and tagged with
    /// <paramref name="account"/>. Usage never falls within a window, so a higher percent already in the file for the same
    /// window is kept (with this, newer, time), when it's the same account's: an untagged statusline reading may be another
    /// account's whose window happens to reset at the same minute, and its percent would be written tagged as this one's.</summary>
    internal static ReportedUsage Write(string path, ReportedUsage reading, DateTime atUtc, string? account = null, Settings.OrchestratorDb? db = null)
    {
        var five = reading.FiveHour!;
        if (ReportedUsage.Load(atUtc, path, db) is { FiveHour: { } old } was && was.Account == account && old.ResetUtc == five.ResetUtc && old.Percent > five.Percent)
            five = old;
        var limits = new JsonObject { ["five_hour"] = Node(five) };
        if (reading.Week is { } week) limits["seven_day"] = Node(week);
        var json = new JsonObject
        {
            ["rate_limits"] = limits,
            ["at"] = atUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["source"] = "orchestrator",
        };
        if (account is not null) json["account"] = account;
        if (db is not null)
        {
            db.SaveState(ReportedUsage.StateName, json.ToJsonString()); // the statusline's file is never replaced
            return new ReportedUsage(five, reading.Week, atUtc) { Account = account };
        }
        var tmp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, json.ToJsonString());
        try { File.Move(tmp, path, overwrite: true); }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { }
            throw;
        }
        return new ReportedUsage(five, reading.Week, atUtc) { Account = account };

        static JsonObject Node(ReportedWindow w) => new()
        {
            ["used_percentage"] = w.Percent,
            ["resets_at"] = new DateTimeOffset(w.ResetUtc).ToUnixTimeSeconds(),
        };
    }
}
