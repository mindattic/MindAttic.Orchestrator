using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2072 (M.ORCH-T1739): a CLI is parked only with /allocate off, no pending ScheduleWakeup, CronCreate or /loop, no
/// pace hold and nothing running, idle long enough.</summary>
public sealed class ParkCheckTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 22, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    private static ParkFacts Facts(CliActivity activity = CliActivity.Idle, bool allocate = false, string? goal = null, LimitStop? stop = null,
        bool resume = false, IReadOnlyList<string>? wakes = null, DateTime? idleSince = null, bool noWakes = false) =>
        new(7, "s1", activity, [], allocate, goal, stop, resume, noWakes ? null : wakes ?? [], idleSince ?? Now.AddHours(-1));

    private static string At(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private static string Use(DateTime at, string id, string name, string input) =>
        "{\"type\":\"assistant\",\"timestamp\":\"" + At(at) + "\",\"message\":{\"role\":\"assistant\",\"content\":[{\"type\":\"tool_use\",\"id\":\"" + id
        + "\",\"name\":\"" + name + "\",\"input\":" + input + "}]}}";

    private static string Result(DateTime at, string id, string text, bool error = false) =>
        "{\"type\":\"user\",\"timestamp\":\"" + At(at) + "\",\"message\":{\"role\":\"user\",\"content\":[{\"type\":\"tool_result\",\"tool_use_id\":\"" + id
        + "\",\"content\":\"" + text + "\"" + (error ? ",\"is_error\":true" : "") + "}]}}";

    private static string Typed(DateTime at, string text) =>
        "{\"type\":\"user\",\"timestamp\":\"" + At(at) + "\",\"message\":{\"role\":\"user\",\"content\":\"" + text + "\"}}";

    [Test]
    public void An_idle_cli_holding_nothing_past_the_idle_time_may_be_parked()
    {
        var v = ParkCheck.Judge(Facts(), Idle, Now);
        Assert.That(v.Safe, Is.True, string.Join("; ", v.Why));
    }

    [Test]
    public void Each_part_of_the_owners_rule_holds_it_back()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ParkCheck.Judge(Facts(allocate: true), Idle, Now).Why.Single(), Does.Contain("/allocate"));
            Assert.That(ParkCheck.Judge(Facts(wakes: ["a ScheduleWakeup is pending"]), Idle, Now).Safe, Is.False);
            Assert.That(ParkCheck.Judge(Facts(stop: new LimitStop("5-hour", Now.AddHours(1), Now.AddMinutes(-5))), Idle, Now).Why.Single(), Does.Contain("pace hold"));
            Assert.That(ParkCheck.Judge(Facts(resume: true), Idle, Now).Why.Single(), Does.Contain("pace hold"));
            Assert.That(ParkCheck.Judge(Facts(activity: CliActivity.Working), Idle, Now).Safe, Is.False);
            Assert.That(ParkCheck.Judge(Facts(activity: CliActivity.Unknown), Idle, Now).Safe, Is.False);
            Assert.That(ParkCheck.Judge(Facts(goal: "ship it"), Idle, Now).Safe, Is.False);
        });
    }

    [Test]
    public void An_unreadable_transcript_a_short_idle_or_parking_off_parks_nothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ParkCheck.Judge(Facts(noWakes: true), Idle, Now).Why.Single(), Does.Contain("transcript"));
            Assert.That(ParkCheck.Judge(Facts(idleSince: Now.AddMinutes(-10)), Idle, Now).Why.Single(), Does.Contain("idle only"));
            Assert.That(ParkCheck.Judge(Facts(), TimeSpan.Zero, Now).Safe, Is.False);
        });
    }

    [Test]
    public void A_schedule_wakeup_counts_until_its_delay_has_passed()
    {
        var lines = new[] { Use(Now.AddMinutes(-10), "t1", "ScheduleWakeup", """{"delaySeconds":1200,"prompt":"go on"}""") };
        Assert.That(ParkCheck.PendingWakes(lines, Now), Has.Count.EqualTo(1));
        Assert.That(ParkCheck.PendingWakes(lines, Now.AddMinutes(30)), Is.Empty);
    }

    [Test]
    public void A_cron_counts_until_deleted_failed_or_expired()
    {
        var made = Now.AddHours(-1);
        var create = Use(made, "c1", "CronCreate", """{"cron":"*/5 * * * *","prompt":"check","recurring":true}""");
        var result = Result(made, "c1", "Scheduled job abc123 (*/5 * * * *)");
        Assert.That(ParkCheck.PendingWakes([create, result], Now), Has.Count.EqualTo(1));
        Assert.That(ParkCheck.PendingWakes([create, result, Use(Now.AddMinutes(-5), "d1", "CronDelete", """{"id":"abc123"}""")], Now), Is.Empty);
        Assert.That(ParkCheck.PendingWakes([create, Result(made, "c1", "failed", error: true)], Now), Is.Empty);
        Assert.That(ParkCheck.PendingWakes([create, result], made + ParkCheck.CronLife + TimeSpan.FromMinutes(1)), Is.Empty);
    }

    [Test]
    public void A_one_shot_cron_ends_once_its_prompt_came_back()
    {
        var made = Now.AddHours(-1);
        var create = Use(made, "c1", "CronCreate", """{"cron":"30 21 22 11 *","prompt":"resume the deploy","recurring":false}""");
        var result = Result(made, "c1", "Scheduled job x9");
        Assert.That(ParkCheck.PendingWakes([create, result], Now), Has.Count.EqualTo(1));
        Assert.That(ParkCheck.PendingWakes([create, result, Typed(Now.AddMinutes(-20), "resume the deploy")], Now), Is.Empty);
    }

    [Test]
    public void A_missing_transcript_is_never_taken_for_nothing_pending()
    {
        var dir = Path.Combine(Path.GetTempPath(), "parkcheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "D--Repo"));
        try
        {
            File.WriteAllLines(Path.Combine(dir, "D--Repo", "s1.jsonl"), [Typed(Now, "hi")]);
            Assert.That(ParkCheck.PendingWakes(dir, ["s1"], Now), Is.Empty);
            Assert.That(ParkCheck.PendingWakes(dir, ["s1", "s2"], Now), Is.Null);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
