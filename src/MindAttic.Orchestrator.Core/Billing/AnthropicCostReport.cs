using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator.Billing;

/// <summary>
/// Anthropic's own bill for the API key (M.ORCH-T953, after M.ORCH-T906): the organization's cost report from the Usage and Cost
/// Admin API (<c>GET /v1/organizations/cost_report</c>, daily buckets, each result's <c>amount</c> a decimal string in cents),
/// read with an Admin API key from the Vault (<see cref="OrchestratorKeys.AdminKey"/>). Anthropic exposes no prepaid-balance
/// endpoint, so the balance is still the starting credit you enter (<see cref="OrchestratorSettings.ApiCreditUsd"/>), less what
/// Anthropic says was spent since. The report is org-wide (it can't be filtered by key), which is what a prepaid credit is spent
/// by. Reading it never throws and never logs or shows the key.
/// </summary>
public static class AnthropicCostReport
{
    public const string Url = "https://api.anthropic.com/v1/organizations/cost_report";

    /// <summary>The <c>anthropic-version</c> header sent.</summary>
    public const string ApiVersion = "2026-10-21";

    /// <summary>The last report read, beside the settings: what the daemon's spend ledger folds in (<see cref="ApiSpendLedger.Bill"/>).</summary>
    public const string FileName = "api-cost.json";

    /// <summary>Daily buckets per page: the endpoint's most.</summary>
    public const int PageDays = 31;

    /// <summary>The most pages read in one go (the ledger keeps 120 days: 4 pages).</summary>
    public const int MaxPages = 8;

    /// <summary>One day of the bill, in dollars.</summary>
    public sealed record Day(DateTime StartUtc, DateTime EndUtc, double Usd);

    /// <summary>One page of the report.</summary>
    public sealed record Page(IReadOnlyList<Day> Days, bool HasMore, string? NextPage);

    /// <summary>A whole read: the days from the window's start to now, and when it was read.</summary>
    public sealed record Billed(DateTime ReadAtUtc, IReadOnlyList<Day> Days)
    {
        public double TotalUsd => Days.Sum(d => d.Usd);
    }

    /// <summary>Reads one page of the cost report's JSON: each bucket's results summed, cents to dollars. Null when it isn't one.</summary>
    public static Page? ParsePage(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root || root["data"] is not JsonArray data) return null;
            var days = new List<Day>();
            foreach (var node in data)
            {
                if (node is not JsonObject bucket || !Utc(bucket["starting_at"], out var start) || !Utc(bucket["ending_at"], out var end)) return null;
                var cents = 0d;
                if (bucket["results"] is JsonArray results)
                    foreach (var r in results)
                        if (r is JsonObject result && Cents(result["amount"]) is { } c &&
                            (result["currency"] is not JsonValue cur || string.Equals(cur.GetValue<string>(), "USD", StringComparison.OrdinalIgnoreCase)))
                            cents += c;
                days.Add(new Day(start, end, cents / 100d));
            }
            var hasMore = root["has_more"] is JsonValue more && more.TryGetValue<bool>(out var m) && m;
            var next = root["next_page"] is JsonValue page && page.TryGetValue<string>(out var p) && !string.IsNullOrWhiteSpace(p) ? p : null;
            return new Page(days, hasMore && next is not null, next);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return null; }

        static bool Utc(JsonNode? node, out DateTime utc)
        {
            utc = default;
            return node is JsonValue v && v.TryGetValue<string>(out var s) &&
                   DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
        }

        static double? Cents(JsonNode? node) => node is JsonValue v
            ? v.TryGetValue<string>(out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d
            : v.TryGetValue<double>(out var n) && double.IsFinite(n) ? n : null
            : null;
    }

    /// <summary>Where the report is read from: the UTC day the credit was entered on, else as far back as the ledger keeps, never
    /// further back than that.</summary>
    public static DateTime From(DateTime? creditSinceUtc, DateTime nowUtc)
    {
        var floor = nowUtc - ApiSpendLedger.Keep;
        var from = creditSinceUtc is { } s && s > floor ? s : floor;
        return new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>The request's query: from <paramref name="fromUtc"/>, a page of <see cref="PageDays"/>, after <paramref name="page"/>.</summary>
    public static string Query(DateTime fromUtc, string? page) =>
        $"{Url}?starting_at={Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}&limit={PageDays}" +
        (page is null ? "" : $"&page={Uri.EscapeDataString(page)}");

    /// <summary>
    /// Reads the cost report from <paramref name="fromUtc"/> to now, every page (up to <see cref="MaxPages"/>). The bill, or why
    /// not ("HTTP 401 authentication_error", "timed out", ...): never the key, nor the response body.
    /// </summary>
    public static async Task<(Billed? Bill, string? Error)> FetchAsync(HttpClient http, string adminKey, DateTime fromUtc, DateTime nowUtc, CancellationToken cancel)
    {
        var days = new SortedDictionary<DateTime, Day>();
        string? page = null;
        try
        {
            for (var i = 0; i < MaxPages; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, Query(fromUtc, page));
                request.Headers.Add("x-api-key", adminKey);
                request.Headers.Add("anthropic-version", ApiVersion);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("MindAttic.Orchestrator", "1.0"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await http.SendAsync(request, cancel).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return (null, $"HTTP {(int)response.StatusCode}{(ErrorType(body) is { } t ? " " + t : "")}");
                if (ParsePage(body) is not { } parsed) return (null, "the cost report's reply couldn't be read");
                foreach (var d in parsed.Days) days[d.StartUtc] = d;
                if (!parsed.HasMore) return (new Billed(nowUtc, [.. days.Values]), null);
                page = parsed.NextPage;
            }
            return (new Billed(nowUtc, [.. days.Values]), null);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { return (null, "timed out"); }
        catch (HttpRequestException ex) { return (null, ex.StatusCode is { } code ? $"HTTP {(int)code}" : "couldn't reach api.anthropic.com"); }
    }

    /// <summary>The error's type from an Anthropic error body (<c>{"type":"error","error":{"type":"authentication_error",...}}</c>):
    /// only its type, a fixed word, so nothing the body echoes reaches the log.</summary>
    public static string? ErrorType(string? body)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(body) && JsonNode.Parse(body) is JsonObject root && root["error"] is JsonObject e &&
                   e["type"] is JsonValue t && t.TryGetValue<string>(out var type) && type.Length <= 64 && type.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                ? type : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>The last read, from <paramref name="path"/>; null when there's none or it can't be read.</summary>
    public static Billed? Load(string path)
    {
        try
        {
            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root ||
                root["readAtUtc"] is not JsonValue at || !at.TryGetValue<DateTime>(out var readAt) || root["days"] is not JsonArray list) return null;
            var days = new List<Day>();
            foreach (var node in list)
                if (node is JsonObject d && d["start"] is JsonValue s && s.TryGetValue<DateTime>(out var start) &&
                    d["end"] is JsonValue e && e.TryGetValue<DateTime>(out var end) && d["usd"] is JsonValue u && u.TryGetValue<double>(out var usd))
                    days.Add(new Day(DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc), DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc), usd));
            return new Billed(DateTime.SpecifyKind(readAt.ToUniversalTime(), DateTimeKind.Utc), days);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>Writes <paramref name="bill"/> to <paramref name="path"/>; false when it couldn't be.</summary>
    public static bool Save(string path, Billed bill)
    {
        try
        {
            var days = new JsonArray();
            foreach (var d in bill.Days) days.Add(new JsonObject { ["start"] = d.StartUtc, ["end"] = d.EndUtc, ["usd"] = Math.Round(d.Usd, 6) });
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = $"{path}.{Environment.ProcessId}.tmp";
            File.WriteAllText(temp, new JsonObject { ["readAtUtc"] = bill.ReadAtUtc, ["days"] = days }.ToJsonString(), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Drops the last read (no Admin API key any more, or reading turned off): the spend is the estimate again.</summary>
    public static bool Forget(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

/// <summary>
/// The daemon's reads of Anthropic's cost report (M.ORCH-T953): opt-in, only while an Admin API key is in the Vault, at most once
/// every <see cref="OrchestratorSettings.ApiCostReadMinutes"/>, off the tick's thread, one at a time. Each read lands in
/// <see cref="AnthropicCostReport.FileName"/>, which the status refresh folds into the spend ledger the dashboard and the pace
/// gate's credit stop read; with no key (or reading off) the file is removed, so the spend is the estimate again. A read that
/// fails leaves the last one in place (the estimate covers the hours since it) and is said once per new reason, never with the key.
/// </summary>
public sealed class ApiCostWatch(string directory, Action<LogEventLevel, string> say)
{
    public const string LeaseName = "api-cost";

    /// <summary>The Admin API key (seam): the Vault's, read fresh at each read.</summary>
    public Func<string?> AdminKey { get; init; } = () => OrchestratorKeys.ForCurrentUser().AdminKey();

    /// <summary>How a read is sent (seam): tests answer with sample JSON; nothing in a test reaches Anthropic.</summary>
    public HttpMessageHandler? Handler { get; init; }

    public string Path { get; } = System.IO.Path.Combine(directory, AnthropicCostReport.FileName);

    private Task? running;
    private DateTime lastStartUtc = DateTime.MinValue;
    private string? lastSaid;

    /// <summary>The read in flight, for tests to wait on; null when none.</summary>
    internal Task? Running => running;

    /// <summary>Starts a read when one is due; returns at once.</summary>
    public void Tick(OrchestratorSettings settings, DateTime nowUtc, CancellationToken cancel = default)
    {
        if (running is { IsCompleted: false }) return;
        var key = settings.ApiCostReadMinutes > 0 ? AdminKey() : null;
        if (key is null)
        {
            if (AnthropicCostReport.Forget(Path))
                Say(LogEventLevel.Information, settings.ApiCostReadMinutes > 0
                    ? "API key spend: no Admin API key in the Vault any more, so its spend is the list-price estimate again"
                    : "API key spend: reading Anthropic's bill is off, so its spend is the list-price estimate again");
            return;
        }
        if (nowUtc - lastStartUtc < OrchestratorSettings.SecondsSpan(settings.ApiCostReadMinutes * 60)) return;
        lastStartUtc = nowUtc;
        var from = AnthropicCostReport.From(settings.ApiCreditSinceUtc, nowUtc);
        running = Task.Run(() => ReadAsync(key, from, nowUtc, cancel), CancellationToken.None);
    }

    private async Task ReadAsync(string key, DateTime fromUtc, DateTime nowUtc, CancellationToken cancel)
    {
        try
        {
            using var http = Handler is null ? new HttpClient() : new HttpClient(Handler, disposeHandler: false);
            http.Timeout = TimeSpan.FromSeconds(30);
            var (bill, error) = await AnthropicCostReport.FetchAsync(http, key, fromUtc, nowUtc, cancel).ConfigureAwait(false);
            if (bill is null)
            {
                Say(LogEventLevel.Warning, $"API key spend: couldn't read Anthropic's bill ({error}); the list-price estimate covers the hours since the last read");
                return;
            }
            if (!AnthropicCostReport.Save(Path, bill))
            {
                Say(LogEventLevel.Warning, $"API key spend: couldn't write {AnthropicCostReport.FileName}; retrying at the next read");
                return;
            }
            Say(LogEventLevel.Information, "API key spend: reading Anthropic's own bill");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The exception's type only: its message could carry the request.
            Say(LogEventLevel.Warning, $"API key spend: couldn't read Anthropic's bill ({ex.GetType().Name})");
        }
    }

    /// <summary>Says <paramref name="line"/> when it differs from the last thing said, so a steady state is said once.</summary>
    private void Say(LogEventLevel level, string line)
    {
        if (line == lastSaid) return;
        lastSaid = line;
        try { say(level, line); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }
}
