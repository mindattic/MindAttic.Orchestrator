using System.Net;
using System.Net.Http;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Anthropic's own bill for the API key (M.ORCH-T953): the Usage and Cost Admin API's cost report, read with the Vault's
/// Admin API key, in place of the list-price estimate up to its last read. Sample JSON from the docs; nothing reaches Anthropic.</summary>
public sealed class AnthropicCostReportTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 18, 20, 0, DateTimeKind.Utc);
    private string dir = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "anthropic-cost-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>The cost report's documented reply (GET /v1/organizations/cost_report), two days, amounts in cents.</summary>
    private const string DocsSample = """
        {
          "data": [
            {
              "ending_at": "2026-11-10T00:00:00Z",
              "results": [
                {
                  "amount": "123.78912",
                  "context_window": "0-200k",
                  "cost_type": "tokens",
                  "currency": "USD",
                  "description": "Claude Opus 5.5 Usage - Input Tokens",
                  "inference_geo": "global",
                  "model": "claude-opus-5-5",
                  "service_tier": "standard",
                  "speed": "standard",
                  "token_type": "cache_creation.ephemeral_1h_input_tokens",
                  "workspace_id": "wrkspc_01JwQvzr7rXLA5AGx3HKfFUJ"
                },
                { "amount": "76.21088", "currency": "USD", "cost_type": null, "description": null, "model": null, "workspace_id": null }
              ],
              "starting_at": "2026-11-09T00:00:00Z"
            },
            { "ending_at": "2026-11-11T00:00:00Z", "results": [], "starting_at": "2026-11-10T00:00:00Z" }
          ],
          "has_more": true,
          "next_page": "page_MjAyNS0wNS0xNFQwMDowMDowMFo="
        }
        """;

    [Test]
    public void A_page_of_the_cost_report_is_read_as_dollars_per_day()
    {
        var page = AnthropicCostReport.ParsePage(DocsSample)!;
        Assert.That(page.Days, Has.Count.EqualTo(2));
        Assert.That(page.Days[0].StartUtc, Is.EqualTo(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(page.Days[0].StartUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(page.Days[0].EndUtc, Is.EqualTo(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(page.Days[0].Usd, Is.EqualTo(2.0).Within(1e-9), "\"123.78912\" + \"76.21088\" cents is $2.00");
        Assert.That(page.Days[1].Usd, Is.EqualTo(0), "a day with no costs has no results");
        Assert.That(page.HasMore, Is.True);
        Assert.That(page.NextPage, Is.EqualTo("page_MjAyNS0wNS0xNFQwMDowMDowMFo="));

        Assert.That(AnthropicCostReport.ParsePage("""{"data":[],"has_more":false,"next_page":null}""")!.HasMore, Is.False);
        Assert.That(AnthropicCostReport.ParsePage("{ torn"), Is.Null);
        Assert.That(AnthropicCostReport.ParsePage("""{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}"""), Is.Null);
        Assert.That(AnthropicCostReport.ParsePage(""), Is.Null);
    }

    [Test]
    public void Only_an_error_bodys_type_is_ever_said()
    {
        Assert.That(AnthropicCostReport.ErrorType("""{"type":"error","error":{"type":"permission_error","message":"sk-ant-admin01-secret is not allowed"}}"""),
            Is.EqualTo("permission_error"));
        Assert.That(AnthropicCostReport.ErrorType("""{"error":{"type":"sk-ant-admin01 secret!"}}"""), Is.Null, "nothing that isn't a plain word");
        Assert.That(AnthropicCostReport.ErrorType("<html>"), Is.Null);
    }

    [Test]
    public void It_is_read_from_the_day_the_credit_was_entered_never_further_back_than_the_ledger_keeps()
    {
        Assert.That(AnthropicCostReport.From(Now.AddDays(-3).AddHours(-5), Now), Is.EqualTo(Now.Date.AddDays(-3)));
        Assert.That(AnthropicCostReport.From(null, Now), Is.EqualTo((Now - ApiSpendLedger.Keep).Date));
        Assert.That(AnthropicCostReport.From(Now.AddYears(-1), Now), Is.EqualTo((Now - ApiSpendLedger.Keep).Date));
        Assert.That(AnthropicCostReport.Query(Now.Date, null), Does.StartWith(AnthropicCostReport.Url + "?starting_at=2026-11-11T00%3A00%3A00Z&limit=31"));
        Assert.That(AnthropicCostReport.Query(Now.Date, "page_x="), Does.EndWith("&page=page_x%3D"));
    }

    [Test]
    public void The_bill_replaces_the_estimate_up_to_its_read_and_the_estimate_counts_after()
    {
        var path = Path.Combine(dir, ApiSpendLedger.FileName);
        var ledger = new ApiSpendLedger(path);
        var today = Now.Date;
        TranscriptSession Session(params (DateTime At, long Output)[] events) =>
            new("s1", dir, Now, default, "claude-sonnet-5-5") { Events = [.. events.Select(e => new UsageEvent(e.At, new TokenUsage(0, 0, 0, e.Output)))] };
        // The estimate: $1 at 10:00 and $3 at 14:00 today (Sonnet output at $10/M), and $1 at 18:00, the read's hour.
        ledger.Observe([Session((today.AddHours(10), 100_000), (today.AddHours(14), 300_000), (today.AddHours(18).AddMinutes(10), 100_000))], Now);
        Assert.That(ledger.Spend(null, today, Now, today).SpentUsd, Is.EqualTo(5.0).Within(1e-9), "the estimate alone");

        // Anthropic says today cost $8 up to the read at 18:20.
        var bill = new AnthropicCostReport.Billed(Now, [new(today, today.AddDays(1), 8)]);
        Assert.That(ledger.Bill(bill), Is.True);
        Assert.That(ledger.Bill(bill), Is.False, "the same read changes nothing");
        var spend = ledger.Spend(10, today, Now, today);
        Assert.That(spend.SpentUsd, Is.EqualTo(8.0 + 1.0).Within(1e-9),
            "the bill, plus the estimate from the read's hour on (that hour may be counted twice in part, never too little)");
        Assert.That(spend.BilledAtUtc, Is.EqualTo(Now));
        Assert.That(spend.Line, Does.Contain("Anthropic's own figures"));

        // The bill is spread in the transcripts' shape, so a credit entered at noon counts only the afternoon's part of the day.
        var noon = today.AddHours(12);
        Assert.That(ledger.Spend(10, noon, Now, today).SpentUsd, Is.EqualTo(8.0 * 4 / 5 + 1.0).Within(1e-9),
            "14:00 and 18:00 carried 4/5 of the day's estimate: $6.40 of the $8 bill, plus the estimate from 18:00");

        Assert.That(ledger.Save(), Is.True);
        var again = new ApiSpendLedger(path);
        Assert.That(again.Spend(10, today, Now, today).SpentUsd, Is.EqualTo(9.0).Within(1e-9), "kept across restarts");
        Assert.That(again.BilledAtUtc, Is.EqualTo(Now));

        Assert.That(again.Bill(null), Is.True, "no Admin API key any more");
        Assert.That(again.Spend(10, today, Now, today).SpentUsd, Is.EqualTo(5.0).Within(1e-9), "the estimate again");
        Assert.That(again.Spend(10, today, Now, today).BilledAtUtc, Is.Null);
        Assert.That(again.Spend(10, today, Now, today).Line, Does.Not.Contain("Anthropic's own figures"));
    }

    [Test]
    public void A_billed_day_the_transcripts_show_nothing_for_is_spread_evenly()
    {
        var ledger = new ApiSpendLedger(Path.Combine(dir, ApiSpendLedger.FileName));
        var yesterday = Now.Date.AddDays(-1);
        ledger.Bill(new AnthropicCostReport.Billed(Now, [new(yesterday, yesterday.AddDays(1), 24)]));
        Assert.That(ledger.Spend(null, yesterday.AddHours(12), Now, Now.Date).SpentUsd, Is.EqualTo(12.0).Within(1e-9), "half of the day: half of its bill");
        Assert.That(ledger.Spend(null, null, Now, Now.Date).TodayUsd, Is.EqualTo(0), "nothing billed today");
    }

    [Test]
    public void The_watch_reads_only_with_an_admin_key_never_says_it_and_drops_the_bill_without_one()
    {
        var said = new List<(LogEventLevel Level, string Line)>();
        var replies = new Queue<HttpResponseMessage>();
        var handler = new Answer(replies);
        string? key = null;
        var watch = new ApiCostWatch(dir, (l, s) => said.Add((l, s))) { AdminKey = () => key, Handler = handler };
        var settings = new OrchestratorSettings { ApiCostReadMinutes = 15, ApiCreditSinceUtc = Now.AddDays(-1) };

        watch.Tick(settings, Now);
        Assert.That(watch.Running, Is.Null, "no Admin API key: nothing is read");
        Assert.That(handler.Sent, Is.Empty);

        key = "sk-ant-admin01-test-only";
        replies.Enqueue(Json(DocsSample.Replace("\"has_more\": true", "\"has_more\": false")));
        watch.Tick(settings, Now);
        watch.Running!.Wait(TimeSpan.FromSeconds(10));
        var request = handler.Sent.Single();
        Assert.That(request.Headers.GetValues("x-api-key").Single(), Is.EqualTo(key));
        Assert.That(request.Headers.GetValues("anthropic-version").Single(), Is.EqualTo(AnthropicCostReport.ApiVersion));
        Assert.That(request.RequestUri!.ToString(), Does.StartWith(AnthropicCostReport.Url + "?starting_at=2026-11-10T00%3A00%3A00Z"));
        var bill = AnthropicCostReport.Load(watch.Path)!;
        Assert.That(bill.TotalUsd, Is.EqualTo(2.0).Within(1e-9));
        Assert.That(bill.ReadAtUtc, Is.EqualTo(Now));

        watch.Tick(settings, Now.AddMinutes(5));
        Assert.That(handler.Sent, Has.Count.EqualTo(1), "not again before ApiCostReadMinutes");

        replies.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            { Content = new StringContent($$$"""{"type":"error","error":{"type":"authentication_error","message":"bad key {{{key}}}"}}""") });
        watch.Tick(settings, Now.AddMinutes(16));
        watch.Running!.Wait(TimeSpan.FromSeconds(10));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Level == LogEventLevel.Warning && x.Line.Contains("HTTP 401 authentication_error")));
        Assert.That(AnthropicCostReport.Load(watch.Path), Is.Not.Null, "a failed read leaves the last one");

        key = null;
        watch.Tick(settings, Now.AddMinutes(40));
        Assert.That(File.Exists(watch.Path), Is.False, "no key any more: the bill is dropped, the spend is the estimate again");
        Assert.That(said.Select(x => x.Line), Has.None.Contains("sk-ant-admin01"), "the key is never said");
    }

    [Test]
    public void Reading_off_drops_the_bill_even_with_a_key()
    {
        AnthropicCostReport.Save(Path.Combine(dir, AnthropicCostReport.FileName), new AnthropicCostReport.Billed(Now, [new(Now.Date, Now.Date.AddDays(1), 1)]));
        var handler = new Answer(new Queue<HttpResponseMessage>());
        var watch = new ApiCostWatch(dir, (_, _) => { }) { AdminKey = () => "sk-ant-admin01-test-only", Handler = handler };
        watch.Tick(new OrchestratorSettings { ApiCostReadMinutes = 0 }, Now);
        Assert.That(handler.Sent, Is.Empty);
        Assert.That(File.Exists(watch.Path), Is.False);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    /// <summary>Answers each request with the next queued reply, keeping what was sent.</summary>
    private sealed class Answer(Queue<HttpResponseMessage> replies) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Sent) Sent.Add(request);
            return Task.FromResult(replies.Count > 0 ? replies.Dequeue() : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
