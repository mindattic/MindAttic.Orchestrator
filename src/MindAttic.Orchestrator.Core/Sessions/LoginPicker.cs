using System.Globalization;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>What <see cref="LoginPicker"/> chose: a per-CLI login, or null for the global login, and why.</summary>
public sealed record LoginPick(AccountDir? Account, string Reason)
{
    /// <summary>The <c>CLAUDE_CONFIG_DIR</c> a CLI starts with: the account's folder, or "" for the global login.</summary>
    public string ConfigDir => Account?.Directory ?? "";
}

/// <summary>
/// The 'Auto' login (M.ORCH-T605): one CLI per account, so many CLIs spend many accounts' tokens instead of draining one.
/// A new Claude CLI goes on a signed-in per-CLI login (<see cref="AccountDirs"/>) that no live CLI runs on yet, the one with
/// the most headroom first (the smaller of what's left of its 5-hour window and its week, by Claude's own meter for it);
/// when every signed-in account already has a CLI, or has nothing left, or none is signed in, it falls back to the global
/// login. A login just picked is held for <see cref="ClaimFor"/>, so tabs started back to back don't all land on the same
/// account before their CLIs' hooks have recorded which login they run on.
/// </summary>
public sealed class LoginPicker(AccountDirs? dirs = null, Func<IEnumerable<string?>>? liveConfigDirs = null)
{
    /// <summary>How long a pick holds its account for the CLI being started on it, until that CLI's hooks say so themselves.</summary>
    public static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(3);

    /// <summary>The file in an account's folder that says when it was last picked for a new CLI (UTC, round-trip format).</summary>
    public const string ClaimName = "cli-picked-at.txt";

    public AccountDirs Dirs { get; } = dirs ?? new AccountDirs();

    private readonly Func<IEnumerable<string?>> live = liveConfigDirs ?? LiveFromContextGate;

    /// <summary>The <c>CLAUDE_CONFIG_DIR</c> of every live Claude CLI on a login of its own, as the context gate recorded it
    /// (a session whose CLI process is still running).</summary>
    public static IEnumerable<string?> LiveFromContextGate()
    {
        var cycler = new ContextCycler();
        return cycler.OnOwnLogin().Where(kv => cycler.SessionRunning(kv.Key) == true).Select(kv => (string?)kv.Value).ToList();
    }

    /// <summary>The accounts (by email) a live CLI runs on, or that were picked for a CLI within <see cref="ClaimFor"/>.</summary>
    public IReadOnlySet<string> Busy(DateTime nowUtc)
    {
        var busy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string?> dirsInUse;
        try { dirsInUse = live().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { dirsInUse = []; }
        foreach (var dir in dirsInUse)
            if (Dirs.Of(dir) is { } account) busy.Add(account.Email);
        foreach (var account in Dirs.All())
            if (ClaimedAt(account) is { } at && nowUtc - at < ClaimFor && at <= nowUtc + TimeSpan.FromMinutes(1)) busy.Add(account.Email);
        return busy;
    }

    /// <summary>
    /// Picks the login a new Claude CLI should run on now; with <paramref name="claim"/>, holds it (<see cref="ClaimFor"/>)
    /// for the CLI about to start. Never throws for an unreadable folder: anything it can't read falls back to the global login.
    /// </summary>
    public LoginPick Pick(DateTime nowUtc, bool claim = false)
    {
        IReadOnlyList<AccountDir> signedIn;
        try { signedIn = Dirs.SignedIn(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(null, $"couldn't read the per-CLI logins ({ex.Message}): global login"); }
        if (signedIn.Count == 0) return new(null, "no per-CLI login is signed in: global login");
        var busy = Busy(nowUtc);
        var chosen = Choose(signedIn, busy, a => ReportedUsage.Load(nowUtc, a.UsagePath), nowUtc);
        if (chosen is null)
            return new(null, signedIn.All(a => busy.Contains(a.Email))
                ? $"every per-CLI login ({signedIn.Count}) already has a CLI: global login"
                : "no free per-CLI login has headroom left: global login");
        if (claim) Claim(chosen, nowUtc);
        var room = Headroom(ReportedUsage.Load(nowUtc, chosen.UsagePath), nowUtc);
        return new(chosen, $"{chosen.Email}: no CLI on it yet, {room.ToString("0", CultureInfo.InvariantCulture)}% headroom");
    }

    /// <summary>
    /// The rule itself: among <paramref name="signedIn"/> accounts not in <paramref name="busy"/> and with headroom left, the
    /// one with the most (<see cref="Headroom"/>), then the most of its week left, then by email. Null when there's none,
    /// which means the global login.
    /// </summary>
    public static AccountDir? Choose(IEnumerable<AccountDir> signedIn, IReadOnlySet<string> busy, Func<AccountDir, ReportedUsage?> usage, DateTime nowUtc) =>
        signedIn.Where(a => !busy.Contains(a.Email))
            .Select(a => (Account: a, Usage: usage(a)))
            .Select(x => (x.Account, Room: Headroom(x.Usage, nowUtc), Week: 100 - (WeekUsed(x.Usage, nowUtc) ?? 0)))
            .Where(x => x.Room > 0)
            .OrderByDescending(x => x.Room)
            .ThenByDescending(x => x.Week)
            .ThenBy(x => x.Account.Email, StringComparer.Ordinal)
            .Select(x => x.Account)
            .FirstOrDefault();

    /// <summary>The percent an account has left before its tighter limit: the smaller of its 5-hour and its weekly room, by
    /// Claude's meter for it. A window not read, or already reset, counts as untouched (100% left).</summary>
    public static double Headroom(ReportedUsage? usage, DateTime nowUtc)
    {
        var five = ReportedUsage.Usable(usage?.FiveHour, nowUtc)?.Percent ?? 0;
        var week = WeekUsed(usage, nowUtc) ?? 0;
        return Math.Max(0, 100 - Math.Max(five, week));
    }

    private static double? WeekUsed(ReportedUsage? usage, DateTime nowUtc) => ReportedUsage.Usable(usage?.Week, nowUtc)?.Percent;

    /// <summary>Marks <paramref name="account"/> as picked at <paramref name="nowUtc"/>. Best effort: a folder it can't write to
    /// only loses the hold.</summary>
    public static void Claim(AccountDir account, DateTime nowUtc)
    {
        try { File.WriteAllText(Path.Combine(account.Directory, ClaimName), nowUtc.ToString("O", CultureInfo.InvariantCulture)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>When <paramref name="account"/> was last picked; null when never, or unreadable.</summary>
    public static DateTime? ClaimedAt(AccountDir account)
    {
        try
        {
            var path = Path.Combine(account.Directory, ClaimName);
            return File.Exists(path) && DateTime.TryParse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var at) ? at.ToUniversalTime() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// The login a Claude tab starts on when nothing was chosen for it: the Auto pick (claimed for it) when
    /// <paramref name="autoLogin"/> is on, prepared as a Claude home; null (the global login) when it's off, the provider isn't
    /// Claude, or nothing is free. <paramref name="note"/> says what was picked, for the caller to show.
    /// </summary>
    public AccountDir? ForNewTab(string provider, bool autoLogin, DateTime nowUtc, out string? note)
    {
        note = null;
        if (!autoLogin || provider != "Claude") return null;
        var pick = Pick(nowUtc, claim: true);
        note = pick.Reason;
        if (pick.Account is not { } account) return null;
        try { Dirs.Prepare(account); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            note = $"couldn't prepare {account.Email}'s login ({ex.Message}): global login";
            return null;
        }
        return account;
    }
}
