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
    /// <summary>Gets or sets the email.</summary>
    public string Email { get; set; } = "";
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the reason: <see cref="LoginLocks.ReasonManual"/> or <see cref="LoginLocks.ReasonWeeklyCutoff"/>.</summary>
    public string Reason { get; set; } = LoginLocks.ReasonManual;
    /// <summary>Gets or sets when it was locked.</summary>
    public DateTime SinceUtc { get; set; }
    /// <summary>Who locked it ("you" for a manual lock; a description for an auto one).</summary>
    public string By { get; set; } = "";
    /// <summary>The weekly reset it was set in, for a <see cref="LoginLocks.ReasonWeeklyCutoff"/> lock; null for a manual one.</summary>
    public DateTime? WeekResetUtc { get; set; }
}

/// <summary>One line of the locks' log: locked or unlocked.</summary>
public sealed class LoginLockEvent
{
    /// <summary>Gets or sets the at utc.</summary>
    public DateTime AtUtc { get; set; }
    /// <summary>Gets or sets the text.</summary>
    public string Text { get; set; } = "";
}

/// <summary>Records that <see cref="Email"/>'s auto-lock was explicitly unlocked within the weekly window ending
/// <see cref="WeekResetUtc"/>, so the same weekly cutoff never re-locks it before that reset comes (the epic's decision,
/// 2026-11-23).</summary>
public sealed class UnlockedWeek
{
    /// <summary>Gets or sets the email.</summary>
    public string Email { get; set; } = "";
    /// <summary>Gets or sets the week reset utc.</summary>
    public DateTime WeekResetUtc { get; set; }
}

/// <summary>What the database keeps (<see cref="LoginLocks.StateName"/>): the locks in force, their log, and which logins were
/// unlocked out of which weekly window.</summary>
public sealed class LoginLockBook
{
    /// <summary>Gets or sets the locks.</summary>
    public List<LoginLock> Locks { get; set; } = [];
    /// <summary>Gets or sets the log.</summary>
    public List<LoginLockEvent> Log { get; set; } = [];
    /// <summary>Gets or sets the unlocked in week.</summary>
    public List<UnlockedWeek> UnlockedInWeek { get; set; } = [];
}

/// <summary>
/// Lock/unlock a login (M.ORCH-T2108): a locked login (any real login, including the global one, never the API key,
/// <see cref="ApiKeyPerson"/>) can't open a CLI, take dispatched or moved work, or keep running CLIs, until it's unlocked.
/// Kept in orchestrator.db, keyed by normalized email, logged. This book is only the state; enforcement, the context menu and
/// auto-lock itself are separate (M.ORCH-T2114, T2117, T2118, T2123, T2124).
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return new(); }
    }

    /// <summary>Loads.</summary>
    public static IReadOnlyList<LoginLock> Load(JsonStore? store) => LoadBook(store).Locks;

    /// <summary><paramref name="email"/>'s lock, or null when it isn't locked.</summary>
    public static LoginLock? For(IEnumerable<LoginLock>? locks, string? email) =>
        email is not { Length: > 0 } ? null
        : locks?.FirstOrDefault(l => string.Equals(l.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when <paramref name="email"/> is locked.</summary>
    public static bool IsLocked(IEnumerable<LoginLock>? locks, string? email) => For(locks, email) is not null;

    /// <summary>Every locked login's email.</summary>
    public static IReadOnlyList<string> LockedEmails(IEnumerable<LoginLock>? locks) => [.. (locks ?? []).Select(l => l.Email)];

    private static readonly object liveGate = new();
    private static (DateTime At, IReadOnlySet<string> Emails) liveLocked = (DateTime.MinValue, new HashSet<string>());

    /// <summary>The live database's locked logins' emails, read at most every few seconds, the same pattern as
    /// <see cref="MemberRoster.LiveDisabled"/>; none when it can't be read.</summary>
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

    /// <summary>Clears <see cref="LiveLocked"/>'s cache (tests): the next call rereads immediately.</summary>
    internal static void ResetLiveCache() => liveLocked = (DateTime.MinValue, new HashSet<string>());

    /// <summary>True when <paramref name="email"/>'s auto-lock for the weekly window ending <paramref name="weekResetUtc"/> was
    /// already unlocked, so that same window's cutoff must not re-lock it.</summary>
    public static bool UnlockedThisWeek(IEnumerable<UnlockedWeek>? unlocked, string? email, DateTime weekResetUtc) =>
        email is { Length: > 0 } &&
        (unlocked ?? []).Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase) && u.WeekResetUtc == weekResetUtc);

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

    /// <summary>Unlocks <paramref name="email"/>'s login; logged. A <see cref="ReasonWeeklyCutoff"/> lock's weekly reset is
    /// recorded in <see cref="LoginLockBook.UnlockedInWeek"/>, so the same window's cutoff doesn't re-lock it. The line said,
    /// or null when it wasn't locked.</summary>
    public static string? Unlock(JsonStore store, string email, string name, string by, DateTime nowUtc)
    {
        var normalized = AccountDirs.Normalize(email) ?? email;
        string? line = null;
        store.Update<LoginLockBook>(StateName, book =>
        {
            if (For(book.Locks, normalized) is not { } existing) return;
            book.Locks.RemoveAll(l => string.Equals(l.Email, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing.Reason == ReasonWeeklyCutoff && existing.WeekResetUtc is { } reset)
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
