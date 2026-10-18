using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The orchestrator reads Claude's meter itself while tokens are spent with no statusline reporting (M.ORCH-T203).</summary>
public sealed class ClaudeMeterProbeTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 11, 0, 0, DateTimeKind.Utc);
    private const string Body = """{"five_hour":{"utilization":11.0,"resets_at":"2026-10-17T15:29:59.890097+00:00"},"seven_day":{"utilization":79.0,"resets_at":"2026-10-23T14:00:00.39+00:00"}}""";

    private string dir = null!;

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(dir = Path.Combine(Path.GetTempPath(), "meter-probe-" + Guid.NewGuid().ToString("N")));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    private static UsageEvent E(DateTime at, long output) => new(at, new TokenUsage(0, 0, 1_000_000, output));

    private string Credentials(DateTime expires)
    {
        var path = Path.Combine(dir, ".credentials.json");
        File.WriteAllText(path, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{new DateTimeOffset(expires).ToUnixTimeMilliseconds()}}}}}""");
        return path;
    }

    private ClaudeMeterProbe Probe(Func<string, CancellationToken, Task<string?>> fetch, DateTime? expires = null) => new()
    {
        UsagePath = Path.Combine(dir, "claude-usage.json"),
        CredentialsPath = Credentials(expires ?? DateTime.UtcNow.AddHours(1)),
        Fetch = fetch,
    };

    [Test]
    public void Due_only_when_the_report_is_stale_and_tokens_are_being_spent_after_it()
    {
        var spending = new[] { E(Now.AddMinutes(-30), 500), E(Now.AddMinutes(-1), 500) };

        Assert.That(ClaudeMeterProbe.Due(Now.AddMinutes(-45), spending, Now), Is.True, "45 minutes unseen while a subagent spends");
        Assert.That(ClaudeMeterProbe.Due(null, spending, Now), Is.True, "no report at all");
        Assert.That(ClaudeMeterProbe.Due(Now.AddMinutes(-2), spending, Now), Is.False, "a statusline reported just now");
        Assert.That(ClaudeMeterProbe.Due(Now.AddMinutes(-45), [E(Now.AddMinutes(-30), 500)], Now), Is.False, "nothing spending now: idle, nothing to read");
        Assert.That(ClaudeMeterProbe.Due(Now.AddMinutes(-5), [E(Now.AddMinutes(-1), 0)], Now), Is.False, "cache reads only aren't spend");
        Assert.That(ClaudeMeterProbe.Due(Now.AddMinutes(-5), [E(Now.AddMinutes(-6), 500)], Now), Is.False, "spent before the report: it's counted");
    }

    [Test]
    public void Parse_rounds_the_endpoints_reset_to_the_statuslines_minute()
    {
        var r = ClaudeMeterProbe.Parse(Body)!;

        Assert.That(r.FiveHour, Is.EqualTo(new ReportedWindow(11, new DateTime(2026, 10, 17, 15, 30, 0, DateTimeKind.Utc))));
        Assert.That(r.Week, Is.EqualTo(new ReportedWindow(79, new DateTime(2026, 10, 23, 14, 0, 0, DateTimeKind.Utc))));
        Assert.That(ClaudeMeterProbe.Parse("""{"five_hour":null}"""), Is.Null);
        Assert.That(ClaudeMeterProbe.Parse("not json"), Is.Null);
    }

    [Test]
    public async Task A_stale_meter_while_subagents_spend_is_read_and_written_where_the_budget_reads_it()
    {
        string? token = null;
        var probe = Probe((t, _) => { token = t; return Task.FromResult<string?>(Body); });

        Assert.That(probe.Tick(Now.AddMinutes(-45), [E(Now.AddMinutes(-1), 500)], Now), Is.True);
        await probe.Pending;

        Assert.That(token, Is.EqualTo("tok"));
        var read = ReportedUsage.Load(DateTime.UtcNow, probe.UsagePath)!;
        Assert.That(read.FiveHour, Is.EqualTo(new ReportedWindow(11, new DateTime(2026, 10, 17, 15, 30, 0, DateTimeKind.Utc))));
        Assert.That(read.Week!.Percent, Is.EqualTo(79));
        Assert.That(DateTime.UtcNow - read.AtUtc, Is.LessThan(TimeSpan.FromMinutes(1)), "stamped now: a fresh reading");
        Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty);

        Assert.That(probe.Tick(Now.AddMinutes(-45), [E(Now.AddMinutes(1), 500)], Now.AddMinutes(1)), Is.False, "at most one ask per StaleAfter");
        Assert.That(probe.Tick(Now.AddMinutes(-45), [E(Now.AddMinutes(5), 500)], Now + ClaudeMeterProbe.StaleAfter + TimeSpan.FromMinutes(1)), Is.True);
        await probe.Pending;
    }

    [Test]
    public async Task A_higher_percent_already_reported_for_the_window_is_kept()
    {
        var probe = Probe((_, _) => Task.FromResult<string?>(Body));
        File.WriteAllText(probe.UsagePath, $$$"""{"rate_limits":{"five_hour":{"used_percentage":12,"resets_at":{{{new DateTimeOffset(2026, 10, 17, 15, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds()}}}}},"at":"{{{DateTime.UtcNow.AddMinutes(-30):yyyy-MM-ddTHH:mm:ssZ}}}"}""");

        probe.Tick(Now.AddMinutes(-45), [E(Now.AddMinutes(-1), 500)], Now);
        await probe.Pending;

        var read = ReportedUsage.Load(DateTime.UtcNow, probe.UsagePath)!;
        Assert.That(read.FiveHour!.Percent, Is.EqualTo(12));
        Assert.That(DateTime.UtcNow - read.AtUtc, Is.LessThan(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void Another_accounts_higher_percent_for_the_same_reset_isnt_written_as_this_ones()
    {
        var path = Path.Combine(dir, "claude-usage.json");
        var reset = new DateTimeOffset(2026, 10, 17, 15, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        // Another CLI's statusline still restamps the old account's 99% after the /login to B.
        File.WriteAllText(path, $$$"""{"rate_limits":{"five_hour":{"used_percentage":99,"resets_at":{{{reset}}}}},"at":"{{{DateTime.UtcNow.AddMinutes(-1):yyyy-MM-ddTHH:mm:ssZ}}}"}""");

        ClaudeMeterProbe.Write(path, ClaudeMeterProbe.Parse(Body)!, DateTime.UtcNow, "b/max");

        var read = ReportedUsage.Load(DateTime.UtcNow, path)!;
        Assert.That((read.FiveHour!.Percent, read.Account), Is.EqualTo((11d, "b/max")));
    }

    [Test]
    public async Task An_expired_sign_in_is_never_used_or_refreshed_and_the_ask_backs_off()
    {
        var asked = 0;
        var probe = Probe((_, _) => { asked++; return Task.FromResult<string?>(Body); }, expires: DateTime.UtcNow.AddMinutes(-1));

        Assert.That(probe.Tick(null, [E(Now.AddMinutes(-1), 500)], Now), Is.True);
        await probe.Pending;

        Assert.That(asked, Is.Zero);
        Assert.That(File.Exists(probe.UsagePath), Is.False);
        var later = Now + ClaudeMeterProbe.StaleAfter + TimeSpan.FromMinutes(1);
        Assert.That(probe.Tick(null, [E(later, 500)], later), Is.False, "backing off after a failed ask");
        var after = Now + ClaudeMeterProbe.Backoff + TimeSpan.FromMinutes(1);
        Assert.That(probe.Tick(null, [E(after, 500)], after), Is.True);
        await probe.Pending;
    }

    [Test]
    public async Task A_rejected_or_failed_ask_writes_nothing()
    {
        var probe = Probe((_, _) => Task.FromResult<string?>(null));

        probe.Tick(null, [E(Now.AddMinutes(-1), 500)], Now);
        await probe.Pending;

        Assert.That(File.Exists(probe.UsagePath), Is.False);
        Assert.That(probe.LastResult, Does.Contain("no reading"));
    }

    [Test]
    public async Task A_reading_is_tagged_with_the_account_it_was_read_for_and_none_is_taken_across_a_login()
    {
        var account = "a/max";
        var probe = new ClaudeMeterProbe
        {
            UsagePath = Path.Combine(dir, "claude-usage.json"), CredentialsPath = Credentials(DateTime.UtcNow.AddHours(1)),
            AccountKey = () => account, Fetch = (_, _) => Task.FromResult<string?>(Body),
        };
        Assert.That(probe.Tick(DateTime.UtcNow, [], Now, force: true), Is.True, "forced though nothing is stale");
        await probe.Pending;
        Assert.That(ReportedUsage.Load(DateTime.UtcNow, probe.UsagePath)!.Account, Is.EqualTo("a/max"));

        File.Delete(probe.UsagePath);
        probe.Reset();
        var switching = new ClaudeMeterProbe
        {
            UsagePath = probe.UsagePath, CredentialsPath = probe.CredentialsPath,
            AccountKey = () => account, Fetch = (_, _) => { account = "b/max"; return Task.FromResult<string?>(Body); },
        };
        switching.Tick(null, [], Now, force: true);
        await switching.Pending;
        Assert.That((File.Exists(probe.UsagePath), switching.LastResult), Is.EqualTo((false, "the Claude sign-in changed while reading the meter")),
            "the token may have been the old account's");

        account = ClaudeAccount.ApiKey;
        var asked = false;
        var keyed = new ClaudeMeterProbe { UsagePath = probe.UsagePath, CredentialsPath = probe.CredentialsPath, AccountKey = () => account,
            Fetch = (_, _) => { asked = true; return Task.FromResult<string?>(Body); } };
        keyed.Tick(null, [], Now, force: true);
        await keyed.Pending;
        Assert.That(asked, Is.False, "an API key has no subscription meter");
    }
}
