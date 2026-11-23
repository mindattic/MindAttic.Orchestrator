using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2122: the <c>login-locks</c> book itself (set/unlock/lookup/normalization/no-relock-within-week), modelled
/// on <see cref="UsageCutoffBook"/>/<see cref="WeeklyAllowanceBook"/>. Enforcement, the context menu and auto-lock triggering
/// are separate tickets (M.ORCH-T2114, T2117, T2118, T2123, T2124).</summary>
public sealed class LoginLockTests
{
    private const string Erin = "ohleo199@mindattic.com";
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = Now.AddDays(4);
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    [Test]
    public void Locking_adds_the_login_and_logs_it()
    {
        var store = new JsonStore(temps.New());
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Erin), Is.False);

        var locked = LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonManual, "you", null, Now);
        Assert.That(locked, Is.Not.Null);
        Assert.That((locked!.Email, locked.Reason, locked.SinceUtc, locked.By), Is.EqualTo((Erin, LoginLocks.ReasonManual, Now, "you")));

        var book = LoginLocks.LoadBook(store);
        Assert.That(LoginLocks.IsLocked(book.Locks, Erin), Is.True);
        Assert.That(LoginLocks.LockedEmails(book.Locks), Is.EqualTo(new[] { Erin }));
        Assert.That(book.Log.Single().Text, Does.Contain("Erin").And.Contain("locked by you"));
    }

    [Test]
    public void Relocking_an_already_locked_login_is_a_no_op()
    {
        var store = new JsonStore(temps.New());
        LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonManual, "you", null, Now);

        var again = LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonManual, "you", null, Now.AddMinutes(10));
        Assert.That(again, Is.Null, "already locked: nothing to do");

        var book = LoginLocks.LoadBook(store);
        Assert.That(book.Locks.Single().SinceUtc, Is.EqualTo(Now), "the original lock's time is kept, not overwritten");
        Assert.That(book.Log, Has.Count.EqualTo(1), "the relock isn't logged again");
    }

    [Test]
    public void Unlock_removes_the_lock_logs_it_and_records_the_weekly_window_for_a_weekly_cutoff_lock()
    {
        var store = new JsonStore(temps.New());
        LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonWeeklyCutoff, "its weekly cutoff", WeekReset, Now);
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Erin), Is.True);

        var line = LoginLocks.Unlock(store, Erin, "Erin", "you", Now.AddHours(1));
        Assert.That(line, Does.Contain("Erin").And.Contain("unlocked by you"));

        var book = LoginLocks.LoadBook(store);
        Assert.That(LoginLocks.IsLocked(book.Locks, Erin), Is.False);
        Assert.That(book.Log, Has.Count.EqualTo(2));
        Assert.That(LoginLocks.UnlockedThisWeek(book.UnlockedInWeek, Erin, WeekReset), Is.True);

        Assert.That(LoginLocks.Unlock(store, Erin, "Erin", "you", Now.AddHours(2)), Is.Null, "nothing left to unlock");
    }

    [Test]
    public void A_weekly_cutoff_lock_does_not_refire_for_a_login_unlocked_within_the_same_weekly_window()
    {
        var store = new JsonStore(temps.New());
        LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonWeeklyCutoff, "its weekly cutoff", WeekReset, Now);
        LoginLocks.Unlock(store, Erin, "Erin", "you", Now.AddMinutes(1));

        var relocked = LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonWeeklyCutoff, "its weekly cutoff", WeekReset, Now.AddHours(1));
        Assert.That(relocked, Is.Null, "the same weekly window was already unlocked: it stays unlocked until the reset");
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Erin), Is.False);

        // Past the reset, a new weekly window locks it again.
        var nextWeek = WeekReset.AddDays(7);
        var reLocked = LoginLocks.Lock(store, Erin, "Erin", LoginLocks.ReasonWeeklyCutoff, "its weekly cutoff", nextWeek, Now.AddDays(5));
        Assert.That(reLocked, Is.Not.Null, "a later weekly window's cutoff still locks it");
    }

    [Test]
    public void Email_is_normalized_and_matched_case_insensitively()
    {
        var store = new JsonStore(temps.New());
        LoginLocks.Lock(store, "  OHLEO199@MindAttic.Com  ", "Erin", LoginLocks.ReasonManual, "you", null, Now);

        Assert.That(LoginLocks.Load(store).Single().Email, Is.EqualTo(Erin), "kept under its normalized form");
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), "Ohleo199@MindAttic.com"), Is.True, "looked up case-insensitively");

        var line = LoginLocks.Unlock(store, "OHLEO199@mindattic.com", "Erin", "you", Now.AddMinutes(1));
        Assert.That(line, Is.Not.Null);
        Assert.That(LoginLocks.Load(store), Is.Empty);
    }

    [Test]
    public void The_api_key_is_never_locked()
    {
        var store = new JsonStore(temps.New());
        Assert.That(LoginLocks.Lock(store, ApiKeyPerson.Email, ApiKeyPerson.Name, LoginLocks.ReasonManual, "you", null, Now), Is.Null);
        Assert.That(LoginLocks.Load(store), Is.Empty);
    }

    [Test]
    public void An_unreadable_store_reads_empty()
    {
        Assert.That(LoginLocks.LoadBook(null).Locks, Is.Empty, "no store at all");

        var dir = temps.New();
        var path = Path.Combine(dir, LoginLocks.StateName + ".json");
        File.WriteAllText(path, """{ "Locks": [] }""");
        var store = new JsonStore(dir);
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.That(LoginLocks.Load(store), Is.Empty, "held right now: defaults rather than thrown");
        }
    }
}
