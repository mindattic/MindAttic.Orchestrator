using System.Data.Common;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One login's lock (M.ORCH-T2108, part of the epic's design): set by hand (<see cref="LoginLocks.ReasonManual"/>) or
/// by that login reaching its own weekly cutoff (<see cref="LoginLocks.ReasonWeeklyCutoff"/>, M.ORCH-T2112). Never expires by
/// time, not even at <see cref="WeekResetUtc"/>: only an explicit Unlock clears it.</summary>
public sealed class LoginLock
{
    /// <summary>The locked login's normalized email (<see cref="AccountDirs.Normalize"/>): the key this lock is found by.</summary>
    public string Email { get; set; } = "";
    /// <summary>The login's display name when it was locked, for the log and messages.</summary>
    public string Name { get; set; } = "";
    /// <summary>Why it's locked: <see cref="LoginLocks.ReasonManual"/> or <see cref="LoginLocks.ReasonWeeklyCutoff"/>.</summary>
    public string Reason { get; set; } = LoginLocks.ReasonManual;
    /// <summary>When it was locked (UTC).</summary>
    public DateTime SinceUtc { get; set; }
    /// <summary>Who locked it ("you" for a manual lock; a description for an auto one).</summary>
    public string By { get; set; } = "";
    /// <summary>The weekly reset it was set in, for a <see cref="LoginLocks.ReasonWeeklyCutoff"/> lock; null for a manual one.</summary>
    public DateTime? WeekResetUtc { get; set; }
}

/// <summary>One line of the locks' log: locked or unlocked.</summary>
public sealed class LoginLockEvent
{
    /// <summary>When it happened (UTC).</summary>
    public DateTime AtUtc { get; set; }
    /// <summary>The line itself, e.g. "Erin (ohleo199@mindattic.com) unlocked by you."</summary>
    public string Text { get; set; } = "";
}

/// <summary>Records that <see cref="Email"/> was explicitly unlocked within the weekly window ending
/// <see cref="WeekResetUtc"/>, whatever its lock's reason, so the same weekly cutoff never re-locks it before that reset comes
/// (the epic's decision, 2026-11-23: an owner Unlock holds for the rest of that week).</summary>
public sealed class UnlockedWeek
{
    /// <summary>The unlocked login's normalized email.</summary>
    public string Email { get; set; } = "";
    /// <summary>The reset ending the weekly window it was unlocked in; matched within <see cref="LoginLocks.SameWeekTolerance"/>.</summary>
    public DateTime WeekResetUtc { get; set; }
}

/// <summary>What the database keeps (<see cref="LoginLocks.StateName"/>): the locks in force, their log, and which logins were
/// unlocked out of which weekly window.</summary>
public sealed class LoginLockBook
{
    /// <summary>The locks in force, at most one per login.</summary>
    public List<LoginLock> Locks { get; set; } = [];
    /// <summary>Every lock and unlock, oldest first, the last <see cref="LoginLocks.KeepLog"/> kept.</summary>
    public List<LoginLockEvent> Log { get; set; } = [];
    /// <summary>Per login, the weekly window it was last unlocked in, which its weekly cutoff doesn't re-lock.</summary>
    public List<UnlockedWeek> UnlockedInWeek { get; set; } = [];
}

/// <summary>
/// Lock/unlock a login (M.ORCH-T2108): a locked login (any real login, including the global one, never the API key,
/// <see cref="ApiKeyPerson"/>) can't open a CLI, take dispatched or moved work, or keep running CLIs, until it's unlocked.
/// Kept in orchestrator.db, keyed by normalized email, logged. Set by hand (<c>org lock</c>, right-click Lock) or by
/// <see cref="AutoLock"/> at the login's own weekly cutoff, whatever an <c>org allow</c> says; cleared only by the owner's
/// <see cref="Unlock"/>, which then holds for the rest of that weekly window. Enforcement reads <see cref="Load"/> or
/// <see cref="LiveLocked"/> (M.ORCH-T2114, T2117, T2118, T2123, T2124).
/// </summary>
public static class LoginLocks
{
    /// <summary>The <c>state</c> row they're kept in.</summary>
    public const string StateName = "login-locks";

    /// <summary>A lock the owner set by hand.</summary>
    public const string ReasonManual = "manual";

    /// <summary>A lock the login's own weekly cutoff set.</summary>
    public const string ReasonWeeklyCutoff = "weekly-cutoff";

    /// <summary>How many log lines are kept.</summary>
    public const int KeepLog = 200;

    /// <summary>What <paramref name="store"/> keeps; empty when nothing is, or it can't be read now.</summary>
    public static LoginLockBook LoadBook(JsonStore? store)
    {
        if (store is null) return new();
        try { return store.Load<LoginLockBook>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or DbException) { return new(); }
    }

    /// <summary>The locks in force in <paramref name="store"/>; none when it can't be read now (<see cref="LoadBook"/>).</summary>
    public static IReadOnlyList<LoginLock> Load(JsonStore? store) => LoadBook(store).Locks;

    /// <summary><paramref name="email"/>'s lock, or null when it isn't locked.</summary>
    public static LoginLock? For(IEnumerable<LoginLock>? locks, string? email) =>
        email is not { Length: > 0 } ? null
        : locks?.FirstOrDefault(l => string.Equals(l.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when <paramref name="email"/> is locked.</summary>
    public static bool IsLocked(IEnumerable<LoginLock>? locks, string? email) => For(locks, email) is not null;

    /// <summary>
    /// Why an explicit open (<c>orchestrator start --account</c>, <c>org start</c>, <c>machine start --login</c>, and a ticket
    /// dispatched to its assignee's login, M.ORCH-T2124) refuses <paramref name="email"/>'s login: "<paramref name="who"/> is
    /// locked (manual since Tue 5:00 PM): orchestrator org unlock <paramref name="who"/>" (no trailing period, so a caller can add
    /// its own). Null while it isn't locked.
    /// </summary>
    public static string? Refusal(IEnumerable<LoginLock>? locks, string? email, string who) =>
        For(locks, email) is { } locked ? $"{who} is locked ({locked.Reason} since {WeeklyAllowances.When(locked.SinceUtc)}): orchestrator org unlock {who}" : null;

    /// <summary>Every locked login's email.</summary>
    public static IReadOnlyList<string> LockedEmails(IEnumerable<LoginLock>? locks) => [.. (locks ?? []).Select(l => l.Email)];

    private static readonly object liveGate = new();
    private static (DateTime At, IReadOnlySet<string> Emails) liveLocked = (DateTime.MinValue, new HashSet<string>());

    /// <summary>The live database's locked logins' emails, read at most every few seconds; none when it can't be read.</summary>
    public static IReadOnlySet<string> LiveLocked()
    {
        var now = DateTime.UtcNow;
        lock (liveGate)
        {
            if (now - liveLocked.At < TimeSpan.FromSeconds(10)) return liveLocked.Emails;
            IReadOnlySet<string> emails;
            try { emails = LockedEmails(Load(new JsonStore())).ToHashSet(StringComparer.Ordinal); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or DbException) { emails = liveLocked.Emails; }
            liveLocked = (now, emails);
            return emails;
        }
    }

    /// <summary>Clears <see cref="LiveLocked"/>'s cache, so its next call rereads the database: after a local Lock/Unlock or a
    /// pushed <see cref="StateName"/> change, rather than waiting out the cache.</summary>
    public static void InvalidateLiveCache()
    {
        lock (liveGate) liveLocked = (DateTime.MinValue, liveLocked.Emails);
    }

    /// <summary>Two readings of one weekly reset (the statusline's and the meter probe's) differ by seconds; resets of
    /// different weeks are days apart.</summary>
    public static readonly TimeSpan SameWeekTolerance = TimeSpan.FromHours(1);

    /// <summary>True when <paramref name="email"/> was already unlocked in the weekly window ending
    /// <paramref name="weekResetUtc"/> (within <see cref="SameWeekTolerance"/>), so that same window's cutoff must not re-lock it.</summary>
    public static bool UnlockedThisWeek(IEnumerable<UnlockedWeek>? unlocked, string? email, DateTime weekResetUtc) =>
        email is { Length: > 0 } &&
        (unlocked ?? []).Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) && (u.WeekResetUtc - weekResetUtc).Duration() < SameWeekTolerance);

    /// <summary>
    /// Locks <paramref name="email"/>'s login, keyed by its normalized form (never the API key's, <see cref="ApiKeyPerson"/>).
    /// Idempotent: already locked, this is a no-op (the existing lock's reason and time are kept) and nothing is logged. A
    /// <see cref="ReasonWeeklyCutoff"/> lock is also a no-op when <paramref name="weekResetUtc"/>'s window was already
    /// unlocked (<see cref="UnlockedThisWeek"/>). Returns the lock as stored, or null when nothing was (or needed to be) done.
    /// </summary>
    public static LoginLock? Lock(JsonStore store, string email, string name, string reason, string by, DateTime? weekResetUtc, DateTime nowUtc)
    {
        if (ApiKeyPerson.Is(email)) return null;
        var normalized = AccountDirs.Normalize(email);
        if (normalized is null) return null;
        LoginLock? result = null;
        store.Update<LoginLockBook>(StateName, book =>
        {
            if (For(book.Locks, normalized) is not null) return;
            if (reason == ReasonWeeklyCutoff && weekResetUtc is { } reset && UnlockedThisWeek(book.UnlockedInWeek, normalized, reset)) return;
            result = new LoginLock { Email = normalized, Name = name, Reason = reason, SinceUtc = nowUtc, By = by, WeekResetUtc = weekResetUtc };
            book.Locks.Add(result);
            Say(book, nowUtc, $"{name} ({normalized}) locked by {by}" + (reason == ReasonWeeklyCutoff ? " at its weekly cutoff." : "."));
        });
        return result;
    }

    /// <summary>
    /// Locks <paramref name="email"/>'s login with <see cref="ReasonWeeklyCutoff"/> when <paramref name="usage"/>'s week is at or
    /// past <paramref name="weeklyCutoff"/>, keyed to that week's reset; a no-op when nothing's usable yet, it's already
    /// locked, or that same weekly window was already unlocked (<see cref="Lock"/>). Never unlocks anything, not even at the
    /// reset: only an explicit Unlock does. Resolved (epic M.ORCH-T2108, 2026-11-23): fires regardless of any active
    /// <c>org allow</c> for <paramref name="email"/> — reaching 100% of the week must stay impossible no matter what.
    /// </summary>
    public static LoginLock? AutoLock(JsonStore store, string email, string name, ReportedUsage? usage, int weeklyCutoff, DateTime nowUtc) =>
        ReportedUsage.Usable(usage?.Week, nowUtc) is { } week && week.Percent >= weeklyCutoff
            ? Lock(store, email, name, ReasonWeeklyCutoff, "its weekly cutoff", week.ResetUtc, nowUtc)
            : null;

    /// <summary>Unlocks <paramref name="email"/>'s login; logged. Whatever the lock's reason, the weekly window it's unlocked in
    /// (<paramref name="weekResetUtc"/>, the login's current week reset when the caller knows it, else the one the lock was set
    /// in) is recorded in <see cref="LoginLockBook.UnlockedInWeek"/>, so the owner's Unlock holds for the rest of that week: the
    /// login's weekly cutoff doesn't re-lock it before the reset. The line said, or null when it wasn't locked.</summary>
    public static string? Unlock(JsonStore store, string email, string name, string by, DateTime nowUtc, DateTime? weekResetUtc = null)
    {
        var normalized = AccountDirs.Normalize(email) ?? email;
        string? line = null;
        store.Update<LoginLockBook>(StateName, book =>
        {
            if (For(book.Locks, normalized) is not { } existing) return;
            book.Locks.RemoveAll(l => string.Equals(l.Email, normalized, StringComparison.OrdinalIgnoreCase));
            if ((weekResetUtc ?? existing.WeekResetUtc) is { } reset)
            {
                book.UnlockedInWeek.RemoveAll(u => string.Equals(u.Email, normalized, StringComparison.OrdinalIgnoreCase));
                book.UnlockedInWeek.Add(new UnlockedWeek { Email = normalized, WeekResetUtc = reset });
            }
            line = $"{name} ({normalized}) unlocked by {by}.";
            Say(book, nowUtc, line);
        });
        return line;
    }

    private static void Say(LoginLockBook book, DateTime nowUtc, string line)
    {
        book.Log.Add(new LoginLockEvent { AtUtc = nowUtc, Text = line });
        if (book.Log.Count > KeepLog) book.Log.RemoveRange(0, book.Log.Count - KeepLog);
        try { OrchestratorLog.For("Org").Information("{Lock:l}", line); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
    }
}
