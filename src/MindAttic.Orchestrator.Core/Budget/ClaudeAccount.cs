using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One stretch of time the machine was signed in to <paramref name="Account"/>; <paramref name="To"/> is null while it still is.</summary>
public sealed record AccountSpan(string Account, DateTime From, DateTime? To);

/// <summary>The latest windows Claude reported for an account that are known to be that account's: the resets of its
/// 5-hour and weekly windows.</summary>
public sealed record ConfirmedWindows(DateTime? FiveHour, DateTime? Week);

/// <summary>
/// The account signed in now (the last one while signed out), every span of the last <see cref="ClaudeAccount.HistoryKept"/>
/// and the windows confirmed for each account.
/// </summary>
public sealed record SignInState(string Account, IReadOnlyList<AccountSpan> History, IReadOnlyDictionary<string, ConfirmedWindows> Confirmed)
{
    /// <summary>When the current span began: the last move to this account.</summary>
    public DateTime Since => History.Last(s => s.Account == Account).From;

    public bool IsApiKey => Account == ClaudeAccount.ApiKey;

    /// <summary>Whether another account was signed in within the history: until it ages out, a reading has to be tied to this one.</summary>
    public bool Switched => History.Any(s => s.Account != Account);

    /// <summary>Whether <paramref name="utc"/> falls in one of this account's spans.</summary>
    public bool Contains(DateTime utc) => History.Any(s => s.Account == Account && utc >= s.From && (s.To is not { } to || utc < to));

    /// <summary>The account signed in at <paramref name="utc"/>; null before the first span kept.</summary>
    public string? AccountAt(DateTime utc) => History.LastOrDefault(s => utc >= s.From && (s.To is not { } to || utc < to))?.Account;

    /// <summary>Whether <paramref name="utc"/> falls in another account's span: before the first account seen, it's no one else's.</summary>
    public bool Others(DateTime utc) => History.Any(s => s.Account != Account && utc >= s.From && (s.To is not { } to || utc < to));

    /// <summary>Whether a 5-hour window confirmed for this account is still running.</summary>
    public bool HasWindow(DateTime nowUtc) => Confirmed.GetValueOrDefault(Account)?.FiveHour > nowUtc;

    /// <summary>
    /// Whether a <c>claude-usage.json</c> reading is this account's. Every open CLI's statusline restamps its own last reply,
    /// so after a /login the others (and this one until it next replies) keep writing the old account's percent as new:
    /// the old account's 99% latched again under the new one. Once there has been a switch, a reading counts only when the
    /// meter probe tagged it with this account, or its windows are ones already confirmed for it (its 5-hour reset, and the
    /// week's when both have one; or a week no other account has, for the next 5-hour window).
    /// </summary>
    public bool Owns(ReportedUsage? reading)
    {
        if (reading is null) return false;
        if (reading.Account is { } tag) return tag == Account;
        if (!Switched) return true;
        if (Confirmed.GetValueOrDefault(Account) is not { } mine) return false;
        if (Same(reading.FiveHour?.ResetUtc, mine.FiveHour) && (reading.Week is null || mine.Week is null || Same(reading.Week.ResetUtc, mine.Week)))
            return true;
        return Same(reading.Week?.ResetUtc, mine.Week) && !Confirmed.Any(c => c.Key != Account && Same(reading.Week!.ResetUtc, c.Value.Week));
    }

    internal static bool Same(DateTime? a, DateTime? b) => a is { } x && b is { } y && Math.Abs((x - y).TotalSeconds) < 90;
}

/// <summary>
/// Which Claude account this machine is signed in to, and when it was. Every limit the orchestrator watches is one
/// account's: after a <c>/login</c> to another, the old account's weekly 99% (in <c>claude-usage.json</c>, the stop latch,
/// the samples and the limit hits in the transcripts) kept every prompt refused until the old account's reset, though the
/// new one had room. Only what falls in the current account's spans is counted, so going back to an account picks up its
/// usage and its latched stop where they were. Signing out isn't a switch: the last account stays. The hooks keep the same
/// record (<see cref="FileName"/>), so whichever sees a switch first dates it for both.
/// </summary>
public sealed class ClaudeAccount
{
    public const string FileName = "account.json";

    /// <summary>How long a closed span is kept: past a week, the old account's windows have all reset.</summary>
    public static readonly TimeSpan HistoryKept = TimeSpan.FromDays(8);

    /// <summary>Claude Code's own config, where the signed-in account is kept (<c>oauthAccount</c>, or an API key).</summary>
    public string ConfigPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    public string StatePath { get; init; } = Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", FileName);

    private DateTime configStamp;
    private bool configRead;
    private string? signedIn;
    private SignInState? last;

    /// <summary>The current account and its history, recording a switch the first time it's seen (the first account seen
    /// counts as one, so an install made while already switched starts clean); null until any account has been signed in.</summary>
    public SignInState? Read(DateTime nowUtc)
    {
        try
        {
            // ~/.claude.json is rewritten often and can be large: read only when it changed.
            var stamp = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : default;
            if (!configRead || stamp != configStamp)
            {
                signedIn = Current(ConfigPath);
                (configStamp, configRead) = (stamp, true);
            }
            var record = Load();
            var spans = Spans(record);
            var current = spans.Count > 0 ? spans[^1].Account : null;
            var changed = false;
            // A record from before the organization was part of the key is the same sign-in: keep its dates.
            if (signedIn is { } s && current is not null && !current.Contains('/') && s.StartsWith(current + "/", StringComparison.Ordinal))
            {
                spans = [.. spans.Select(x => x.Account == current ? x with { Account = s } : x)];
                (current, changed) = (s, true);
            }
            if (signedIn is { } now && now != current)
            {
                if (spans.Count > 0) spans[^1] = spans[^1] with { To = nowUtc };
                spans = [.. spans, new AccountSpan(now, nowUtc, null)];
                (current, changed) = (now, true);
            }
            // A closed span ages out on every read, not only at the next switch: else Switched stays true for good after one
            // /login, and once the current account's confirmed windows have reset no untagged reading is its again.
            var kept = spans.Where(x => x.To is not { } to || nowUtc - to <= HistoryKept).ToList();
            if (kept.Count > 0 && kept.Count != spans.Count) (spans, changed) = (kept, true);
            if (changed) Save(record, spans);
            return last = current is null ? null : new SignInState(current, spans, Confirmations(record));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return last;
        }
    }

    /// <summary>When the current account's span began; null until any account has been signed in.</summary>
    public DateTime? SwitchedAtUtc(DateTime nowUtc) => Read(nowUtc)?.Since;

    /// <summary>Records <paramref name="reading"/>'s running windows as <paramref name="account"/>'s, so its statusline
    /// readings are known for its own once another account has been signed in.</summary>
    public void Confirm(string account, ReportedUsage reading, DateTime nowUtc)
    {
        if (ReportedUsage.Usable(reading.FiveHour, nowUtc) is not { } five) return;
        var week = ReportedUsage.Usable(reading.Week, nowUtc);
        try
        {
            var record = Load();
            var was = Confirmations(record).GetValueOrDefault(account);
            if (SignInState.Same(was?.FiveHour, five.ResetUtc) && (week is null || SignInState.Same(was?.Week, week.ResetUtc))) return;
            var confirmed = record["confirmed"] as JsonObject ?? (JsonObject)(record["confirmed"] = new JsonObject());
            confirmed[account] = new JsonObject
            {
                ["five_hour"] = Epoch(five.ResetUtc),
                ["seven_day"] = week is not null ? Epoch(week.ResetUtc) : was?.Week is { } w ? Epoch(w) : null,
            };
            Write(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException) { }
    }

    /// <summary>What an API-key sign-in reads as: no Claude subscription's windows are its.</summary>
    public const string ApiKey = "api-key";

    /// <summary>
    /// The signed-in account from Claude Code's config, or null when signed out. A Claude login is its
    /// <c>oauthAccount.accountUuid</c> and <c>organizationUuid</c> (one person's Max plan and their Team seat, or a Console
    /// login, share the account id but not the limits); an API key (<c>primaryApiKey</c>, or an approved
    /// <c>ANTHROPIC_API_KEY</c> with no Claude login) is <see cref="ApiKey"/>. It used to read as signed out, so moving
    /// to an API key wasn't a switch and the Claude account's stop stayed latched. The pace hooks key the same way.
    /// </summary>
    internal static string? Current(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (root.TryGetProperty("oauthAccount", out var o) && o.ValueKind == JsonValueKind.Object && Text(o, "accountUuid") is { } id)
            return Text(o, "organizationUuid") is { } org ? $"{id}/{org}" : id;
        if (Text(root, "primaryApiKey") is not null) return ApiKey;
        return root.TryGetProperty("customApiKeyResponses", out var r) && r.ValueKind == JsonValueKind.Object &&
               r.TryGetProperty("approved", out var approved) && approved.ValueKind == JsonValueKind.Array && approved.GetArrayLength() > 0
            ? ApiKey : null;
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private JsonObject Load() =>
        File.Exists(StatePath) && JsonNode.Parse(File.ReadAllText(StatePath)) is JsonObject o ? o : new JsonObject();

    /// <summary>The record's spans; a record from before they were kept (<c>{account, since}</c>) is one open span from its date.</summary>
    private static List<AccountSpan> Spans(JsonObject record)
    {
        if (record["spans"] is JsonArray spans)
            return [.. spans.OfType<JsonObject>()
                .Where(s => s["account"] is JsonValue a && a.TryGetValue<string>(out _) && s["from"] is JsonValue)
                .Select(s => new AccountSpan(s["account"]!.GetValue<string>(), At(s["from"]!),
                    s["to"] is JsonValue t && t.GetValueKind() == JsonValueKind.Number ? At(t) : null))];
        return record["account"] is JsonValue was && was.TryGetValue<string>(out var account) && record["since"] is JsonValue since
            ? [new AccountSpan(account, At(since), null)] : [];
    }

    private static Dictionary<string, ConfirmedWindows> Confirmations(JsonObject record) =>
        (record["confirmed"] as JsonObject ?? new JsonObject()).Where(c => c.Value is JsonObject).ToDictionary(c => c.Key, c => new ConfirmedWindows(
            c.Value!["five_hour"] is JsonValue f && f.GetValueKind() == JsonValueKind.Number ? At(f) : null,
            c.Value["seven_day"] is JsonValue w && w.GetValueKind() == JsonValueKind.Number ? At(w) : null));

    private static DateTime At(JsonNode seconds) => DateTime.UnixEpoch.AddSeconds(seconds.GetValue<double>());

    private static double Epoch(DateTime utc) => (utc - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>Keeps <c>account</c> and <c>since</c> (the current span) beside the spans, for hooks written before them.</summary>
    private void Save(JsonObject record, List<AccountSpan> spans)
    {
        record["account"] = spans[^1].Account;
        record["since"] = Epoch(spans[^1].From);
        record["spans"] = new JsonArray([.. spans.Select(s => (JsonNode)new JsonObject
        {
            ["account"] = s.Account, ["from"] = Epoch(s.From), ["to"] = s.To is { } to ? Epoch(to) : null,
        })]);
        Write(record);
    }

    private void Write(JsonObject record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var tmp = $"{StatePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, record.ToJsonString());
        File.Move(tmp, StatePath, overwrite: true);
    }
}
