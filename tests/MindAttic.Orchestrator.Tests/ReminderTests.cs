using System.ComponentModel;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Serilog.Events;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Reminders (M.ORCH-T477): <c>orchestrator remind</c> saves them, the daemon texts each one due through Psst.</summary>
[NonParallelizable] // the CLI tests set Ctx.StoreDirectory and AnsiConsole.Console, which are process-wide
public sealed class ReminderTests
{
    private readonly TempDirs temp = new();
    private string dir = "";
    private readonly List<(LogEventLevel Level, string Line)> said = [];
    private readonly List<(string Title, string Message, bool Urgent)> alerts = [];

    // A Monday morning.
    private static readonly DateTime Now = new(2026, 10, 19, 10, 0, 0);

    private const string PingOk = """
        MindAttic.Psst — config check
          Email-to-SMS:  configured (me@example.com)
          Recipient #:   +15555550101
          Recipient @ explicit:  <unset>
        """;

    [SetUp]
    public void SetUp()
    {
        dir = temp.New();
        said.Clear();
        alerts.Clear();
    }

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private JsonStore Store => new(dir);

    private sealed class FakePsst(Func<IReadOnlyList<string>, ProcessResult> run) : IProcessRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout)
        {
            Calls.Add(args);
            return run(args);
        }
    }

    private static ProcessResult Ok(string output = "") => new(0, output, "", false);

    private static FakePsst Psst(Func<IReadOnlyList<string>, ProcessResult>? sms = null, string ping = PingOk) =>
        new(args => args[0] == "ping" ? Ok(ping) : (sms ?? (_ => Ok("✓ sent")))(args));

    private ReminderSender Sender(IProcessRunner runner) =>
        new(Store, new PsstClient(runner), (level, line) => said.Add((level, line)), (title, message, urgent) => alerts.Add((title, message, urgent)));

    // ---- --at ----

    [TestCase("9am", "2026-10-20 09:00")]            // already past today: tomorrow's
    [TestCase("5pm", "2026-10-19 17:00")]
    [TestCase("5:30 PM", "2026-10-19 17:30")]
    [TestCase("21:00", "2026-10-19 21:00")]
    [TestCase("noon", "2026-10-19 12:00")]
    [TestCase("tomorrow", "2026-10-20 09:00")]
    [TestCase("tomorrow 8am", "2026-10-20 08:00")]
    [TestCase("tomorrow at 3:15pm", "2026-10-20 15:15")]
    [TestCase("tonight", "2026-10-19 20:00")]
    [TestCase("today 3pm", "2026-10-19 15:00")]
    [TestCase("fri 5pm", "2026-10-23 17:00")]
    [TestCase("Friday", "2026-10-23 09:00")]
    [TestCase("mon 9am", "2026-10-26 09:00")]        // said Monday after 9: next Monday
    [TestCase("mon 11am", "2026-10-19 11:00")]
    [TestCase("in 2h", "2026-10-19 12:00")]
    [TestCase("30m", "2026-10-19 10:30")]
    [TestCase("in 1h30m", "2026-10-19 11:30")]
    [TestCase("2 days", "2026-10-21 10:00")]
    [TestCase("now", "2026-10-19 10:00")]
    [TestCase("2026-10-25 14:00", "2026-10-25 14:00")]
    [TestCase("2026-10-25", "2026-10-25 09:00")]
    public void At_reads_clock_times_days_spans_and_dates(string text, string expected)
    {
        Assert.That(ReminderTime.TryParse(text, Now, out var local), Is.True, text);
        Assert.That(local, Is.EqualTo(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [TestCase("")]
    [TestCase("today")]
    [TestCase("whenever")]
    [TestCase("tomorrow at lunch")]
    [TestCase("in 0m")]
    public void At_refuses_what_it_cant_read(string text) => Assert.That(ReminderTime.TryParse(text, Now, out _), Is.False);

    // ---- Psst ----

    [Test]
    public void Ping_gives_the_owners_number_and_whether_psst_can_send()
    {
        var ok = PsstClient.Parse(PingOk);
        Assert.That((ok.Configured, ok.OwnerNumber, ok.Problem), Is.EqualTo((true, "+15555550101", (string?)null)));

        var off = PsstClient.Parse("  Email-to-SMS:  not configured\n  Recipient #:   <unset>\n");
        Assert.That(off.Configured, Is.False);
        Assert.That(off.OwnerNumber, Is.Null);
        Assert.That(off.Problem, Does.Contain("isn't configured"));
    }

    [Test]
    public void A_missing_psst_exe_reads_as_not_installed()
    {
        var status = new PsstClient(new FakePsst(_ => throw new Win32Exception(2))).Probe();
        Assert.That(status.Installed, Is.False);
        Assert.That(status.Problem, Does.Contain("isn't installed"));
    }

    [Test]
    public void To_takes_a_us_number_or_a_contact_in_psst_s_book()
    {
        var contacts = Path.Combine(dir, "contacts.json");
        File.WriteAllText(contacts, """{ "contacts": [ { "name": "Jordan", "phone": "+15555550123" } ] }""");
        Assert.That(PsstClient.Addressable("jordan", contacts), Is.True);
        Assert.That(PsstClient.Addressable("(555) 555-0101", contacts), Is.True);
        Assert.That(PsstClient.Addressable("+1 555 555 0101", contacts), Is.True);
        Assert.That(PsstClient.Addressable("jordna", contacts), Is.False);
        Assert.That(PsstClient.Addressable("555-0101", contacts), Is.False);
        Assert.That(PsstClient.Addressable("jordan", Path.Combine(dir, "missing.json")), Is.False);
    }

    // ---- the daemon ----

    [Test]
    public void A_due_reminder_is_texted_to_the_owner_and_marked_sent_and_a_later_one_waits()
    {
        var utc = DateTime.UtcNow;
        var due = ReminderBook.Add(Store, "call the dentist", utc.AddMinutes(-1), null, utc.AddHours(-1));
        var later = ReminderBook.Add(Store, "stand-up", utc.AddHours(1), "jordan", utc);
        var psst = Psst();

        Sender(psst).Tick(utc);

        Assert.That(psst.Calls, Has.Some.EqualTo(new[] { "sms", "+15555550101", "Reminder: call the dentist" }));
        Assert.That(psst.Calls.Count(c => c[0] == "sms"), Is.EqualTo(1));
        Assert.That(ReminderBook.Pending(Store).Select(r => r.Id), Is.EqualTo(new[] { later.Id }));
        Assert.That(Store.Load<List<Reminder>>(ReminderBook.Name).Single(r => r.Id == due.Id).SentUtc, Is.EqualTo(utc));
        Assert.That(alerts, Is.Empty);
    }

    [Test]
    public void One_missed_while_the_machine_slept_is_sent_at_the_next_tick_and_said_late()
    {
        var utc = DateTime.UtcNow;
        ReminderBook.Add(Store, "take the bins out", utc.AddHours(-8), "jordan", utc.AddDays(-1));
        var psst = Psst();

        Sender(psst).Tick(utc);

        Assert.That(psst.Calls, Has.Some.EqualTo(new[] { "sms", "jordan", "Reminder: take the bins out" }));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Line.Contains("sent late")));
        Assert.That(ReminderBook.Pending(Store), Is.Empty);
    }

    [Test]
    public void A_failed_send_is_counted_alerted_once_and_retried_next_tick()
    {
        var utc = DateTime.UtcNow;
        ReminderBook.Add(Store, "pay rent", utc.AddMinutes(-1), null, utc.AddHours(-1));
        var fail = true;
        var psst = Psst(sms: _ => fail ? new ProcessResult(1, "", "all 8 recipients failed — smtp down", false) : Ok());
        var sender = Sender(psst);

        sender.Tick(utc);
        sender.Tick(utc.AddSeconds(15));
        var pending = ReminderBook.Pending(Store).Single();
        Assert.That(pending.Failures, Is.EqualTo(2));
        Assert.That(pending.LastError, Does.Contain("smtp down"));
        Assert.That(alerts.Count(a => a.Title == "Reminder not sent"), Is.EqualTo(1));

        fail = false;
        sender.Tick(utc.AddSeconds(30));
        Assert.That(ReminderBook.Pending(Store), Is.Empty);
        Assert.That(psst.Calls.Count(c => c[0] == "sms"), Is.EqualTo(3));
    }

    [Test]
    public void Psst_not_installed_or_not_configured_holds_them_and_alerts_once()
    {
        var utc = DateTime.UtcNow;
        ReminderBook.Add(Store, "water the plants", utc.AddMinutes(-1), null, utc.AddHours(-1));
        var missing = Sender(new FakePsst(_ => throw new Win32Exception(2)));
        missing.Tick(utc);
        missing.Tick(utc.AddSeconds(15));
        Assert.That(alerts.Count, Is.EqualTo(1));
        Assert.That(alerts[0].Message, Does.Contain("isn't installed"));
        Assert.That(alerts[0].Urgent, Is.True);
        Assert.That(ReminderBook.Pending(Store), Has.Count.EqualTo(1));

        alerts.Clear();
        var unconfigured = Psst(ping: "  Email-to-SMS:  not configured\n  Recipient #:   <unset>\n");
        Sender(unconfigured).Tick(utc);
        Assert.That(alerts.Single().Message, Does.Contain("isn't configured"));
        Assert.That(unconfigured.Calls.Any(c => c[0] == "sms"), Is.False);
        Assert.That(ReminderBook.Pending(Store), Has.Count.EqualTo(1));
    }

    [Test]
    public void A_reminder_for_the_owner_waits_when_psst_has_no_number_of_its_own()
    {
        var utc = DateTime.UtcNow;
        ReminderBook.Add(Store, "mine", utc.AddMinutes(-1), null, utc.AddHours(-1));
        ReminderBook.Add(Store, "theirs", utc.AddMinutes(-1), "jordan", utc.AddHours(-1));
        var psst = Psst(ping: "  Email-to-SMS:  configured (me@example.com)\n  Recipient #:   <unset>\n");

        Sender(psst).Tick(utc);

        Assert.That(ReminderBook.Pending(Store).Single().Text, Is.EqualTo("mine"));
        Assert.That(alerts.Single().Message, Does.Contain("no number of its own"));
    }

    [Test]
    public void Nothing_due_runs_no_psst_at_all()
    {
        var utc = DateTime.UtcNow;
        ReminderBook.Add(Store, "later", utc.AddHours(1), null, utc);
        var psst = Psst();
        Sender(psst).Tick(utc);
        Assert.That(psst.Calls, Is.Empty);
    }

    [Test]
    public void Cancel_drops_a_pending_reminder_by_id_in_any_case()
    {
        var utc = DateTime.UtcNow;
        var r = ReminderBook.Add(Store, "x", utc.AddHours(1), null, utc);
        Assert.That(ReminderBook.Cancel(Store, "nope"), Is.False);
        Assert.That(ReminderBook.Cancel(Store, r.Id.ToUpperInvariant()), Is.True);
        Assert.That(ReminderBook.Pending(Store), Is.Empty);
    }

    // ---- orchestrator remind ----

    private string RunCli(out int code, params string[] args)
    {
        var previousStore = Ctx.StoreDirectory;
        var previousConsole = AnsiConsole.Console;
        var previousPsst = RemindCommand.Psst;
        var previousContacts = RemindCommand.ContactsPath;
        var output = new StringWriter();
        try
        {
            Ctx.StoreDirectory = dir;
            RemindCommand.Psst = () => new PsstClient(Psst());
            var contacts = Path.Combine(dir, "contacts.json");
            File.WriteAllText(contacts, """{ "contacts": [ { "name": "Jordan", "phone": "+15555550123" } ] }""");
            RemindCommand.ContactsPath = contacts;
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(output),
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No,
            });
            code = CliApp.Create().Run(args);
            return output.ToString();
        }
        finally
        {
            Ctx.StoreDirectory = previousStore;
            AnsiConsole.Console = previousConsole;
            RemindCommand.Psst = previousPsst;
            RemindCommand.ContactsPath = previousContacts;
        }
    }

    [Test]
    public void Remind_saves_one_lists_it_and_cancels_it()
    {
        var said = RunCli(out var code, "remind", "call the dentist", "--at", "in 2h");
        Assert.That(code, Is.EqualTo(0), said);
        var r = ReminderBook.Pending(Store).Single();
        Assert.That(r.Text, Is.EqualTo("call the dentist"));
        Assert.That(r.To, Is.Null);
        Assert.That(r.DueUtc, Is.EqualTo(DateTime.UtcNow.AddHours(2)).Within(TimeSpan.FromMinutes(1)));
        Assert.That(said, Does.Contain(r.Id));

        Assert.That(RunCli(out code, "remind", "--list"), Does.Contain("call the dentist"));
        RunCli(out code, "remind", "--cancel", r.Id);
        Assert.That(code, Is.EqualTo(0));
        Assert.That(ReminderBook.Pending(Store), Is.Empty);
    }

    [Test]
    public void Remind_refuses_no_time_an_unreadable_time_a_past_one_and_an_unknown_contact()
    {
        RunCli(out var code, "remind", "x");
        Assert.That(code, Is.EqualTo(1));
        RunCli(out code, "remind", "x", "--at", "whenever");
        Assert.That(code, Is.EqualTo(1));
        RunCli(out code, "remind", "x", "--at", "2020-01-01 09:00");
        Assert.That(code, Is.EqualTo(1));
        Assert.That(RunCli(out code, "remind", "x", "--at", "5m", "--to", "jordna"), Does.Contain("isn't a Psst contact"));
        Assert.That(code, Is.EqualTo(1));
        Assert.That(ReminderBook.Pending(Store), Is.Empty);

        RunCli(out code, "remind", "x", "--at", "5m", "--to", "jordan");
        Assert.That(code, Is.EqualTo(0));
        Assert.That(ReminderBook.Pending(Store).Single().To, Is.EqualTo("jordan"));
    }

    [Test]
    public void The_dashboard_lists_each_pending_one_with_its_id()
    {
        var utc = DateTime.UtcNow;
        var r = new Reminder { Id = "abc123", Text = "pay rent", DueUtc = utc.AddHours(1), To = "jordan" };
        var lines = MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.ReminderLines([r], utc);
        Assert.That(lines.Single(), Does.StartWith("abc123").And.Contain("→ jordan: pay rent"));
    }
}
