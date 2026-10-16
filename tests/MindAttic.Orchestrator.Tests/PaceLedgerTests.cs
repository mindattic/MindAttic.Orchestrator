using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

public sealed class PaceLedgerTests
{
    private static readonly DateTime End = new(2026, 10, 15, 21, 50, 0, DateTimeKind.Utc);
    private static DateTime Start => End - BudgetCalculator.BlockLength;

    // A report every 5 minutes through the window, climbing to `final` percent at 100k tokens a percent.
    private static List<UsageSample> Steady(double final, DateTime? end = null)
    {
        var e = end ?? End;
        var list = new List<UsageSample>();
        for (var m = 5; m < 300; m += 5)
        {
            var pct = Math.Round(final * m / 295);
            list.Add(new UsageSample(e.AddMinutes(m - 300), pct, e, (long)(pct * 100_000)));
        }
        return list;
    }

    private static LimitObservation Hit(DateTime at, DateTime reset) => new(LimitKind.Session, at, reset, 9_000_000);

    [TestCase(98, WindowGrade.Ok, TestName = "98% with no hit is ok")]
    [TestCase(70, WindowGrade.Under, TestName = "70% is under")]
    [TestCase(100, WindowGrade.Over, TestName = "Claude reporting 100% is over")]
    public void Grades_a_window_by_where_it_ended(double final, WindowGrade grade) =>
        Assert.That(PaceLedger.Build(End, Steady(final), [], []).Grade, Is.EqualTo(grade));

    [Test]
    public void A_limit_hit_is_over_whatever_the_percent()
    {
        var w = PaceLedger.Build(End, Steady(80), [Hit(End.AddMinutes(-40), End)], []);
        Assert.That(w.Grade, Is.EqualTo(WindowGrade.Over));
        Assert.That(w.LimitHitUtc, Is.EqualTo(End.AddMinutes(-40)));
    }

    [Test]
    public void A_long_stretch_without_the_meter_while_tokens_are_spent_is_blind()
    {
        // No reports for the middle two hours, while 40% of the window was spent.
        var samples = Steady(98).Where(s => s.AtUtc < Start.AddHours(1) || s.AtUtc > Start.AddHours(3)).ToList();
        var w = PaceLedger.Build(End, samples, [], []);
        Assert.That(w.Grade, Is.EqualTo(WindowGrade.Blind));
        Assert.That(w.Blind, Is.GreaterThan(TimeSpan.FromHours(1.9)));
    }

    [Test]
    public void A_quiet_stretch_with_nothing_spent_is_not_blind()
    {
        var samples = Steady(98);
        // Nothing reported and nothing spent for an hour: idle, not blind.
        var flat = samples.Select(s => s.AtUtc > Start.AddHours(1) && s.AtUtc < Start.AddHours(2)
            ? null : s).Where(s => s is not null).Select(s => s!).ToList();
        var mid = flat.Last(s => s.AtUtc <= Start.AddHours(1));
        flat = [.. flat.Select(s => s.AtUtc >= Start.AddHours(2) && s.AtUtc < Start.AddHours(2.1) ? s with { WindowFresh = mid.WindowFresh, Percent = mid.Percent } : s)];
        Assert.That(PaceLedger.Build(End, flat, [], []).Gaps, Is.Empty);
    }

    [Test]
    public void Counts_the_pace_gates_holds_and_refusals_inside_the_window()
    {
        List<GateEvent> events =
        [
            new(Start.AddHours(4), GateEvent.Held, 30, "Bash"),
            new(Start.AddHours(4.5), GateEvent.Held, 90, "Edit"),
            new(Start.AddHours(4.9), GateEvent.Refused, 300, "Bash"),
            new(Start.AddHours(4.95), GateEvent.PromptAnswered, 0, null),
            new(End.AddMinutes(1), GateEvent.Held, 60, "Bash"), // the next window's
        ];
        var w = PaceLedger.Build(End, Steady(99), [], events);
        Assert.That((w.Held, w.HeldMinutes, w.Refused, w.PromptsAnswered), Is.EqualTo((2, 2.0, 1, 1)));
    }

    [Test]
    public void Due_closes_each_ended_window_once_including_hits_from_before_the_samples()
    {
        var earlier = End.AddHours(-10);
        List<UsageSample> samples = [.. Steady(98), .. Steady(50, End.AddHours(5))]; // the second is still running
        List<LimitObservation> hits = [Hit(earlier.AddMinutes(-30), earlier), Hit(End.AddMinutes(-5), End.AddMinutes(10))];
        var now = End.AddMinutes(30);

        var due = PaceLedger.Due([], samples, hits, [], now);
        Assert.That(due.Select(d => d.EndUtc), Is.EqualTo(new[] { earlier, End }), "the hit whose reset is 10 minutes off is the same window");
        Assert.That(due[1].Grade, Is.EqualTo(WindowGrade.Over));

        Assert.That(PaceLedger.Due(due, samples, hits, [], now), Is.Empty, "nothing twice");
    }

    [Test]
    public void The_ledger_round_trips_and_skips_a_torn_line()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(dir, PaceLedger.FileName);
        var w = PaceLedger.Build(End, Steady(98).Where(s => s.AtUtc < Start.AddHours(1) || s.AtUtc > Start.AddHours(3)).ToList(), [], []);
        PaceLedger.Append(path, [w]);
        File.AppendAllText(path, "{\"StartUtc\":\"2026-");
        var back = PaceLedger.Load(path);
        Assert.That(back, Has.Count.EqualTo(1));
        Assert.That((back[0].Grade, back[0].Gaps.Count, back[0].EndUtc), Is.EqualTo((WindowGrade.Blind, w.Gaps.Count, End)));
    }

    [Test]
    public void Reads_the_gates_log_and_trims_what_the_ledger_has_counted()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "pace-gate.jsonl");
        File.WriteAllLines(path,
        [
            """{"at": "2026-10-01T00:00:00Z", "decision": "held", "held": 12.5, "tool": "Bash", "percent": 90}""",
            """{"at": "2026-10-15T20:00:00Z", "decision": "refused", "held": 300.0, "tool": "Edit", "percent": 99}""",
            """{"at": "2026-10-15T20:01""",
        ]);
        var events = PaceLedger.LoadEvents(path, new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Is.EqualTo(new GateEvent(new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc), GateEvent.Refused, 300, "Edit")));

        PaceLedger.TrimEvents(path, new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(File.ReadAllLines(path), Has.Length.EqualTo(2), "the old line goes; the torn one isn't dated, so it stays");
    }

    [Test]
    public void Summary_says_how_long_since_the_last_window_went_over()
    {
        var over = PaceLedger.Build(End, Steady(80), [Hit(End.AddHours(-1), End)], []);
        var ok = PaceLedger.Build(End.AddHours(5), Steady(98, End.AddHours(5)), [], []);
        var line = PaceLedger.Summary([over, ok], End.AddHours(1 + 48));
        Assert.That(line, Does.Contain("2 windows").And.Contain("1 over").And.Contain("1 ok").And.Contain("last over: 2d ago"));
        Assert.That(PaceLedger.Summary([ok], End.AddHours(6)), Does.EndWith("last over: never"));
    }

    private sealed class Harness
    {
        public readonly JsonStore Store = new(Directory.CreateTempSubdirectory().FullName);
        public readonly List<(LogEventLevel Level, string Line)> Said = [];
        public readonly List<(string Title, bool Urgent)> Alerts = [];
        public readonly List<string> Filed = [];
        public readonly PaceLedgerWatch Watch;

        public Harness()
        {
            Watch = new PaceLedgerWatch(Store, "unused", (l, s) => Said.Add((l, s)), (t, _, u) => Alerts.Add((t, u)))
            {
                EventsPath = Path.Combine(Store.Directory, "pace-gate.jsonl"),
                FileTicket = s => { Filed.Add(s); return "M.ORCH-T" + (900 + Filed.Count); },
            };
        }
    }

    [Test]
    public void A_new_limit_hit_is_raised_and_ticketed_at_once_but_history_is_only_recorded()
    {
        var h = new Harness();
        var old = End.AddDays(-3);
        h.Store.SaveLimits([Hit(old.AddMinutes(-20), old)]);
        h.Store.Save("usage-samples", Steady(60, End.AddHours(5)));

        h.Watch.Tick(End);
        Assert.That(h.Filed, Is.Empty, "a hit from before the ledger is history");
        Assert.That(PaceLedger.Load(h.Watch.LedgerPath).Single().Grade, Is.EqualTo(WindowGrade.Over), "but it's on the record");

        // A test limit hit appears in the transcripts.
        h.Store.SaveLimits([Hit(old.AddMinutes(-20), old), Hit(End.AddMinutes(30), End.AddHours(5))]);
        h.Watch.Tick(End.AddMinutes(31));
        Assert.That(h.Filed, Has.Count.EqualTo(1));
        Assert.That(h.Filed[0], Does.StartWith("[pace-ledger hit "));
        Assert.That(h.Alerts, Does.Contain(("Ran out of tokens", true)));
        Assert.That(h.Said.Any(s => s.Level == LogEventLevel.Error), Is.True);

        // Its window closing over is the same news: recorded, not ticketed again; a restarted daemon doesn't re-raise it.
        var watch = new PaceLedgerWatch(h.Store, "unused", (_, _) => { }, (t, _, u) => h.Alerts.Add((t, u)))
        {
            EventsPath = h.Watch.EventsPath,
            FileTicket = s => { h.Filed.Add(s); return "M.ORCH-T999"; },
        };
        watch.Tick(End.AddHours(5).AddMinutes(5));
        Assert.That(h.Filed, Has.Count.EqualTo(1));
        Assert.That(PaceLedger.Load(h.Watch.LedgerPath).Last().Grade, Is.EqualTo(WindowGrade.Over));
    }

    [Test]
    public void A_blind_window_closing_live_files_one_ticket()
    {
        var h = new Harness();
        h.Store.Save("usage-samples", Steady(98).Where(s => s.AtUtc < Start.AddHours(1) || s.AtUtc > Start.AddHours(3)).ToList());
        h.Watch.Tick(End.AddMinutes(5));
        Assert.That(h.Filed.Single(), Does.StartWith("[pace-ledger blind]"));
        Assert.That(h.Alerts.Single().Title, Is.EqualTo("Pace was blind"));
    }
}
