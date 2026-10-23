using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog.Events;

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
/// <remarks>Uses Claude Code's saved sign-in. A token that has only just expired is skipped (Claude Code, if one is running
/// on the login, refreshes it, and refreshing here would rotate the token it holds). One expired for
/// <see cref="RefreshAfterExpired"/> has no CLI keeping it fresh (M.ORCH-T684: the global login, with billing on the API key,
/// went 20 hours unread on a dead token), so it is refreshed here and the new tokens written back to the file.</remarks>
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

    /// <summary>How long a token must have been expired before the probe refreshes it itself (M.ORCH-T684): a CLI on the login
    /// refreshes its token as it expires, so one this stale has none running.</summary>
    public static readonly TimeSpan RefreshAfterExpired = TimeSpan.FromMinutes(10);

    public const string TokenEndpoint = "https://console.anthropic.com/v1/oauth/token";
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

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

    /// <summary>Exchanges a refresh token for a new access token; null when refused. Tests swap it.</summary>
    internal Func<string, CancellationToken, Task<RefreshedToken?>> Renew { get; init; } = RenewAsync;

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
    /// <see cref="StaleAfter"/> old and tokens were spent after it, the latest of them within <see cref="PaceLedger.MinGap"/> (M.ORCH-T651: within StaleAfter, a login whose events come in bursts further apart than that, long turns, went unasked for an hour).</summary>
    /// <param name="samples">The login's recorded readings. Also due while the running window's percent rose within
    /// <see cref="ActiveFor"/> (M.ORCH-T659): the events are the transcripts of the sessions known to be on this login, and a
    /// session that isn't (yet) mapped to it, or whose transcript isn't read yet, spends without showing there, so the
    /// Oct 23 12:20-5:20 AM window went 25 minutes unseen with 1.3M spent while the probe, seeing no events, never asked.</param>
    public static bool Due(DateTime? reportedAtUtc, IReadOnlyList<UsageEvent> events, DateTime nowUtc, IEnumerable<UsageSample>? samples = null)
    {
        if (reportedAtUtc is { } at && nowUtc - at < StaleAfter) return false;
        var since = reportedAtUtc ?? nowUtc - BudgetCalculator.BlockLength;
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.AtUtc > nowUtc) continue;
            if (e.AtUtc <= since || nowUtc - e.AtUtc > PaceLedger.MinGap) break;
            if (e.Usage.Fresh > 0) return true;
        }
        return samples is not null && LastRise(samples, nowUtc) is { } rose && nowUtc - rose <= ActiveFor;
    }

    /// <summary>How long after the running window's percent last rose the login still counts as spending, whatever the
    /// transcripts say (M.ORCH-T659). Bounded, so an idle login is asked a few times at most after its last reading.</summary>
    public static readonly TimeSpan ActiveFor = TimeSpan.FromMinutes(30);

    /// <summary>How often a signed-in login is read even while idle (M.ORCH-T661): a login nothing seemed to spend on was never
    /// asked, so a fresh or just-reset account never got a reading, its limit was never learned and its tile drew no pace while
    /// the others showed real percents. Every login's meter is read at least this often, each reading kept with its time
    /// (the samples), so a rise in it can be set against what that login's CLIs spent over the same stretch (M.ORCH-T682).
    /// Five minutes: at five logins that is one small request a minute in all, twelve an hour per login, and a spending login
    /// is already read every <see cref="StaleAfter"/>.</summary>
    public static readonly TimeSpan IdleEvery = TimeSpan.FromMinutes(5);

    /// <summary>Whether an idle login is due its periodic reading (M.ORCH-T661): its running window was last seen
    /// <see cref="IdleEvery"/> ago or never, and the probe last asked that long ago or never, so a login with no running
    /// window to read (the endpoint gives no reading) is still asked only every <see cref="IdleEvery"/>, not every failed ask's
    /// <see cref="Backoff"/>.</summary>
    public static bool IdleDue(DateTime? reportedAtUtc, DateTime? lastAskUtc, DateTime nowUtc) =>
        (reportedAtUtc is not { } at || nowUtc - at >= IdleEvery) && (lastAskUtc is not { } asked || nowUtc - asked >= IdleEvery);

    /// <summary>When the running window's percent first reached its highest recorded value; null with no reading of a
    /// running window.</summary>
    public static DateTime? LastRise(IEnumerable<UsageSample> samples, DateTime nowUtc)
    {
        var running = samples.Where(s => s.ResetUtc > nowUtc && s.AtUtc <= nowUtc).OrderBy(s => s.AtUtc).ToList();
        if (running.Count == 0) return null;
        var top = running.Max(s => s.Percent);
        return running.First(s => s.Percent >= top).AtUtc;
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
    /// <remarks>Also asks an idle login every <see cref="IdleEvery"/> (<see cref="IdleDue"/>, M.ORCH-T661).</remarks>
    public bool Tick(DateTime? reportedAtUtc, IReadOnlyList<UsageEvent> events, DateTime nowUtc, bool force = false, IEnumerable<UsageSample>? samples = null)
    {
        if (!force && !Due(reportedAtUtc, events, nowUtc, samples) && !IdleDue(reportedAtUtc, LastAskUtc, nowUtc))
        {
            IsDue = false;
            return false;
        }
        IsDue = true;
        lock (gate)
        {
            if (!Pending.IsCompleted || nowUtc < nextUtc)
            {
                Skipped = !Pending.IsCompleted ? "an ask is still in flight" : $"waiting until {nextUtc:HH:mm:ss}Z after the last ask";
                return false;
            }
            Skipped = null;
            nextUtc = nowUtc + StaleAfter;
            var asked = generation;
            LastAskUtc = nowUtc;
            Asks++;
            Pending = Task.Run(() => AskAsync(nowUtc, asked));
        }
        return true;
    }

    /// <summary>Whether the last <see cref="Tick"/> found a reading needed.</summary>
    public bool IsDue { get; private set; }

    /// <summary>Why the last due <see cref="Tick"/> didn't ask (an ask in flight, or waiting out the last one); null when it asked.</summary>
    public string? Skipped { get; private set; }

    /// <summary>When the last ask started, how many have, and how many failed in a row since the last reading.</summary>
    public DateTime? LastAskUtc { get; private set; }
    public int Asks { get; private set; }
    public int FailuresInRow { get; private set; }

    /// <summary>When the next ask may start (<see cref="Tick"/> won't before).</summary>
    public DateTime NextAskUtc => nextUtc;

    /// <summary>Names this probe in the log (the login's email); null for the global login's.</summary>
    public string? Label { get; init; }

    /// <summary>Where each ask and what came of it is logged (M.ORCH-T659: <see cref="LastResult"/> alone was in memory, so two
    /// fixes for a blind window were guesses). The daemon sets it.</summary>
    public static Action<LogEventLevel, string>? Say { get; set; }

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
        try { await AskCoreAsync(nowUtc, asked).ConfigureAwait(false); }
        catch (Exception ex)
        {
            // Whatever it was (a half-written credentials file, a number that isn't one), the ask is over and says why.
            LastResult = $"the ask failed: {ex.GetType().Name}: {ex.Message}";
            lock (gate)
                if (generation == asked) nextUtc = nowUtc + Backoff;
        }
        FailuresInRow = LastResult?.StartsWith("read Claude's meter", StringComparison.Ordinal) == true ? 0 : FailuresInRow + 1;
        try
        {
            Say?.Invoke(FailuresInRow == 0 ? LogEventLevel.Information : LogEventLevel.Warning,
                $"meter probe{(Label is null ? "" : $" [{Label}]")}: ask #{Asks}: {LastResult}{(FailuresInRow > 1 ? $" ({FailuresInRow} in a row)" : "")}");
        }
        catch (Exception) { }
    }

    private async Task AskCoreAsync(DateTime nowUtc, int asked)
    {
        var account = Who();
        if (account == ClaudeAccount.ApiKey)
        {
            Fail("an API key has no subscription meter to read", NoMeterBackoff);
            return;
        }
        string? token = null;
        try
        {
            token = Token(CredentialsPath, DateTime.UtcNow);
            if (token is null) token = await RefreshStaleAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or HttpRequestException or TaskCanceledException) { }
        if (token is null)
        {
            Fail("no usable Claude sign-in to read the meter with");
            return;
        }
        string? body;
        string? why = null;
        try
        {
            // Bounded here too, so an ask that never answers can't hold every later one off.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            body = await Fetch(token, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            body = null;
            why = ex is HttpRequestException { StatusCode: { } code } ? $"HTTP {(int)code}" : ex.GetType().Name;
        }
        if (body is null || Parse(body) is not { } reading)
        {
            Fail($"Claude's usage endpoint gave no reading{(why is null ? "" : $" ({why})")}");
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
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

    /// <summary>Refreshes the saved sign-in when its access token has been expired for <see cref="RefreshAfterExpired"/>, writing
    /// the new tokens back beside the rest of the file; returns the new access token, null when it isn't stale or was refused.</summary>
    private async Task<string?> RefreshStaleAsync()
    {
        var now = DateTime.UtcNow;
        if (!File.Exists(CredentialsPath) || JsonNode.Parse(File.ReadAllText(CredentialsPath)) is not JsonObject root ||
            root["claudeAiOauth"] is not JsonObject oauth ||
            oauth["refreshToken"] is not JsonValue rv || !rv.TryGetValue<string>(out var refresh) || refresh.Length == 0 ||
            oauth["expiresAt"] is not JsonValue xv || !xv.TryGetValue<long>(out var expiresAt) ||
            now - DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).UtcDateTime < RefreshAfterExpired)
            return null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        if (await Renew(refresh, cts.Token).ConfigureAwait(false) is not { } fresh) return null;
        // A CLI may have refreshed it while this asked: its tokens are the live ones, not these.
        if (JsonNode.Parse(File.ReadAllText(CredentialsPath)) is not JsonObject again || again["claudeAiOauth"] is not JsonObject current ||
            current["refreshToken"]?.GetValue<string>() != refresh)
            return Token(CredentialsPath, DateTime.UtcNow);
        current["accessToken"] = fresh.AccessToken;
        if (fresh.RefreshToken is { Length: > 0 } rt) current["refreshToken"] = rt;
        current["expiresAt"] = DateTimeOffset.UtcNow.AddSeconds(fresh.ExpiresInSeconds).ToUnixTimeMilliseconds();
        var tmp = $"{CredentialsPath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, again.ToJsonString());
        try { File.Move(tmp, CredentialsPath, overwrite: true); }
        catch
        {
            try { File.Delete(tmp); } catch (IOException) { }
            throw;
        }
        return fresh.AccessToken;
    }

    /// <summary>A refreshed sign-in from the token endpoint.</summary>
    internal sealed record RefreshedToken(string AccessToken, string? RefreshToken, long ExpiresInSeconds);

    private static async Task<RefreshedToken?> RenewAsync(string refreshToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new StringContent(new JsonObject { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken, ["client_id"] = ClientId }.ToJsonString(),
                System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.UserAgent.ParseAdd("MindAttic.Orchestrator");
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        var root = doc.RootElement;
        if (!root.TryGetProperty("access_token", out var a) || a.ValueKind != JsonValueKind.String || a.GetString() is not { Length: > 0 } access) return null;
        var refreshed = root.TryGetProperty("refresh_token", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        var seconds = root.TryGetProperty("expires_in", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : 3600;
        return new RefreshedToken(access, refreshed, seconds);
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
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"the usage endpoint answered {(int)response.StatusCode}", null, response.StatusCode);
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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
