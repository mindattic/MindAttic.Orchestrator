using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T2128: the lock seams driven together, not one at a time - a login crosses its weekly cutoff and is auto-locked
/// (M.ORCH-T2126), <see cref="LoginPicker"/> never picks it (M.ORCH-T2115) and its <see cref="AccountPacer"/> pace comes back
/// stopped (M.ORCH-T2117), it's still locked once its weekly reset passes, and only an explicit <see cref="LoginLocks.Unlock"/>
/// makes it pickable again - without the same weekly window's cutoff immediately re-locking it.
/// </summary>
public sealed class LoginLockEndToEndTests
{
    private const string Locked = "b@example.com";
    private const string Other = "a@example.com";
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = Now.AddDays(4);
    private readonly TempDirs temps = new();
    private string dir = "";
    private JsonStore store = null!;
    private AccountDirs dirs = null!;

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        store = new JsonStore(temps.New());
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"))
        {
            // The real seam (AccountDirs.SignedIn): the locked logins' emails, read from the same book AutoLock/Unlock write.
            Locked = () => LoginLocks.Load(store).Select(l => l.Email).ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    [TearDown]
    public void TearDown()
    {
        LoginLocks.ResetLiveCache();
        temps.DeleteAll();
    }

    private static void SignIn(AccountDir account, double fiveHour, double week)
    {
        File.WriteAllText(account.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "id-" + account.Email, emailAddress = account.Email } }));
        var limits = new
        {
            five_hour = new { used_percentage = fiveHour, resets_at = new DateTimeOffset(Now.AddHours(2)).ToUnixTimeSeconds() },
            seven_day = new { used_percentage = week, resets_at = new DateTimeOffset(Now.AddDays(3)).ToUnixTimeSeconds() },
        };
        File.WriteAllText(account.UsagePath, JsonSerializer.Serialize(new { rate_limits = limits, at = Now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ") }));
    }

    private LoginPicker Picker() => new(dirs, () => []);

    private static ReportedUsage Week(double percent, DateTime resetUtc) => new(null, new ReportedWindow(percent, resetUtc), Now);

    private bool PacerStopsLocked(DateTime nowUtc)
    {
        var b = dirs.For(Locked);
        static TranscriptSession Session(string id, long fresh, DateTime at) =>
            new(id, @"D:\W\A", at, default, null) { Events = [new UsageEvent(at, new TokenUsage(fresh, 0, 0, 0))] };
        var sessions = new[] { Session("own", 1_000, nowUtc.AddMinutes(-2)) };
        var map = new Dictionary<string, string> { ["own"] = b.Directory };
        var pacer = new AccountPacer { Dirs = dirs, Probe = false, ProjectsDir = Path.Combine(dir, "no-projects"), Locks = () => LoginLocks.Load(store) };
        // Both logins' folders share this test's root, so Tick paces both of them (M.ORCH-T2117's own test paces one alone).
        var pace = pacer.Tick(sessions, map, new OrchestratorSettings(), nowUtc, nowUtc.AddHours(-1), write: true).Single(p => p.Email == Locked);
        return pace.Pace.Stop;
    }

    [Test]
    public void A_login_that_crosses_its_weekly_cutoff_is_locked_skipped_and_stopped_until_an_explicit_unlock()
    {
        var other = dirs.For(Other);
        Directory.CreateDirectory(other.Directory);
        SignIn(other, fiveHour: 70, week: 60); // modest headroom: min(30, 35) = 30
        var locked = dirs.For(Locked);
        Directory.CreateDirectory(locked.Directory);
        SignIn(locked, fiveHour: 5, week: 5); // far more headroom: min(95, 90) = 90

        // Not yet crossed: picked on its headroom alone, far ahead of the other login.
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Locked), Is.False);
        Assert.That(Picker().Pick(Now).Account?.Email, Is.EqualTo(Locked), "the most headroom wins before any lock");

        // 1. One simulated tick crosses the weekly cutoff: AutoLock locks it at once (M.ORCH-T2126).
        var autoLocked = LoginLocks.AutoLock(store, Locked, "Bee Dee", Week(92, WeekReset), 90, Now);
        Assert.That(autoLocked, Is.Not.Null);
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Locked), Is.True);

        // 2. LoginPicker never picks the locked login, however much headroom it has: it falls back to the other login, and
        // even asked for the locked one by itself (eligible narrows to it alone) there is none to give.
        Assert.That(Picker().Pick(Now).Account?.Email, Is.EqualTo(Other), "the locked login is skipped despite its far larger headroom");
        Assert.That(Picker().ForNewTab("Claude", autoLogin: true, Now, out _)?.Email, Is.EqualTo(Other));
        Assert.That(Picker().Pick(Now, eligible: e => e == Locked).Account, Is.Null, "the locked login alone: nothing to pick");

        // 3. Its AccountPacer pace comes back Stop (M.ORCH-T2117).
        Assert.That(PacerStopsLocked(Now), Is.True, "locked: paced to a stop");

        // 4. Still locked once its weekly reset passes: AutoLock never unlocks by itself, not even then.
        var pastReset = WeekReset.AddMinutes(1);
        var nextReset = WeekReset.AddDays(7);
        Assert.That(LoginLocks.AutoLock(store, Locked, "Bee Dee", Week(2, nextReset), 90, pastReset), Is.Null, "a fresh, low week doesn't unlock it");
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Locked), Is.True, "only an explicit Unlock clears it");
        Assert.That(Picker().Pick(pastReset, eligible: e => e == Locked).Account, Is.Null, "still skipped after the reset passes");
        Assert.That(PacerStopsLocked(pastReset), Is.True, "still stopped after the reset passes");

        // 5. After an explicit Unlock, it's pickable again...
        var afterUnlock = pastReset.AddMinutes(1);
        Assert.That(LoginLocks.Unlock(store, Locked, "Bee Dee", "you", afterUnlock), Is.Not.Null);
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Locked), Is.False);
        Assert.That(Picker().Pick(afterUnlock, eligible: e => e == Locked).Account?.Email, Is.EqualTo(Locked), "pickable again");
        Assert.That(PacerStopsLocked(afterUnlock), Is.False, "pace is normal again once unlocked");

        // ...and NOT immediately re-locked by the same weekly window's cutoff (the window the lock and the Unlock were for).
        var relocked = LoginLocks.AutoLock(store, Locked, "Bee Dee", Week(95, WeekReset), 90, afterUnlock.AddMinutes(1));
        Assert.That(relocked, Is.Null, "unlocked in this same weekly window: the cutoff doesn't re-lock it before the reset");
        Assert.That(LoginLocks.IsLocked(LoginLocks.Load(store), Locked), Is.False);
        Assert.That(Picker().Pick(afterUnlock.AddMinutes(1), eligible: e => e == Locked).Account?.Email, Is.EqualTo(Locked), "stays pickable");
    }
}
