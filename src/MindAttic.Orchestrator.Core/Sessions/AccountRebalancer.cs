using System.Globalization;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One CLI the rebalancer moved, or tried to: what to log, and whether the Launcher tab took the request (toasted).</summary>
public sealed record Rebalanced(bool Moved, string Line);

/// <summary>A move the daemon made, as the store keeps it for the dashboard's toast.</summary>
public sealed class RebalanceMove
{
    public DateTime AtUtc { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>
/// Rebalance when an account runs low (M.ORCH-T608). Each tick, a Claude CLI whose login is low (its 5-hour window spent to the
/// pace target, its pace at or past a limit, or its week at or past the weekly reserve's stop line, M.ORCH-T609) is moved to a
/// signed-in per-CLI login that has no CLI on it and more headroom (<see cref="LoginPicker.Choose"/>), through the Launcher's
/// restart-resume at the CLI's next idle point (<see cref="AccountMove"/>), so every account keeps at most one CLI.
/// Against ping-pong: a CLI moved (or refused) isn't moved again within <see cref="CoolDown"/>; a login is never a target while
/// it's low itself or needs to gain less than <see cref="MinGain"/> points of headroom; and a login a CLI is on its way to stays
/// taken until that CLI shows up on it (its restart can wait out a long turn) or the cool-down passes.
/// The <see cref="OrchestratorSettings.RebalanceLogins"/> setting turns it off.
/// <para>Emergency failover (M.ORCH-T664): when no signed-in login has room for a low CLI, that one CLI is moved onto the Vault's
/// API key instead (<see cref="AccountMove.FailoverAsync"/>: a restart-resume billing the key for it alone, the global mode left
/// as it is), at once when a limit error ended its turn, and moved back to its login once that login resets, so the key (no
/// rate ceiling, only cost) bills only the gap. <see cref="OrchestratorSettings.FailoverToApiKey"/> turns it off.</para>
/// </summary>
public sealed class AccountRebalancer(AccountMove mover, LoginPicker? picker = null)
{
    /// <summary>How long a CLI moved (or one its tab refused) is left where it is before it can be moved again.</summary>
    public static readonly TimeSpan CoolDown = TimeSpan.FromMinutes(30);

    /// <summary>The fewest points of headroom a move must gain: a target barely better than the source isn't worth a restart.</summary>
    public const double MinGain = 10;

    private readonly LoginPicker picker = picker ?? new LoginPicker(mover.Dirs);

    /// <summary>Session → when it was last moved or tried.</summary>
    private readonly Dictionary<string, DateTime> tried = new(StringComparer.Ordinal);

    /// <summary>Target email → the session on its way there, and since when.</summary>
    private readonly Dictionary<string, (string Session, DateTime AtUtc)> pending = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where the CLIs failed over to the API key are kept (<see cref="FailoversName"/>), so the billing switch leaves them
    /// on the key and a daemon restart still moves them back; null keeps them in memory only.</summary>
    public JsonStore? Store { get; init; }

    /// <summary>Whether the Vault holds a key to fail over to (seam).</summary>
    internal Func<bool> HasApiKey { get; init; } = () => mover.Billing.HasKey;

    /// <summary>Approves or rejects the Vault key in a login's Claude Code config (seam): null when done, else why not.</summary>
    internal Func<string, bool, string?> SetKeyChoice { get; init; } = (configPath, approve) => mover.Billing.SetKeyChoice(configPath, approve);

    /// <summary>Claude's meter for a login (null: the global one) (seam).</summary>
    internal Func<AccountDir?, DateTime, ReportedUsage?> Usage { get; init; } = (a, now) => ReportedUsage.Load(now, a?.UsagePath);

    /// <summary>
    /// Why a login is low, or null when it isn't: its pace stopped or at/past a limit, its 5-hour window at or past the pace
    /// target by Claude's meter, or its week at or past <paramref name="weeklyStopPercent"/>.
    /// </summary>
    public static string? Low(Pace? pace, ReportedUsage? usage, DateTime nowUtc, int paceTargetPercent, int weeklyStopPercent)
    {
        if (ReportedUsage.Usable(usage?.Week, nowUtc) is { } week && week.Percent >= weeklyStopPercent)
            return $"its week is at {Pct(week.Percent)}% (stops at {weeklyStopPercent}%)";
        if (ReportedUsage.Usable(usage?.FiveHour, nowUtc) is { } five && five.Percent >= Math.Clamp(paceTargetPercent, 1, 100))
            return $"its 5-hour window is at {Pct(five.Percent)}% (pace target {Math.Clamp(paceTargetPercent, 1, 100)}%)";
        if (pace is { } p && (p.Stop || p.Mode == PaceMode.Over))
            return $"its {p.Governing ?? "5-hour"} pace is at the limit";
        return null;
    }

    private static string Pct(double v) => v.ToString("0", CultureInfo.InvariantCulture);

    /// <summary>The store entry the daemon keeps its recent moves in, for the dashboard to toast (it runs in another process).</summary>
    public const string MovesName = "rebalance-moves";

    /// <summary>How many recent moves the store keeps.</summary>
    public const int KeepMoves = 20;

    /// <summary>Adds a move the daemon made to the store's recent moves. Best effort: a store it can't write only loses the toast.</summary>
    public static void Record(JsonStore store, string text, DateTime nowUtc)
    {
        try
        {
            var moves = store.Load<List<RebalanceMove>>(MovesName);
            moves.Add(new RebalanceMove { AtUtc = nowUtc, Text = text });
            store.Save(MovesName, moves.OrderBy(m => m.AtUtc).TakeLast(KeepMoves).ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }

    /// <summary>The recorded moves made after <paramref name="sinceUtc"/>, oldest first; none when the store can't be read.</summary>
    public static IReadOnlyList<RebalanceMove> Since(JsonStore store, DateTime sinceUtc)
    {
        try { return [.. store.Load<List<RebalanceMove>>(MovesName).Where(m => m.AtUtc > sinceUtc).OrderBy(m => m.AtUtc)]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return []; }
    }

    /// <summary>The store entry the CLIs failed over to the API key are kept in.</summary>
    public const string FailoversName = "api-failovers";

    /// <summary>How long a failed-over CLI can go unseen (ended, or restarting) before it's forgotten.</summary>
    public static readonly TimeSpan GoneFor = TimeSpan.FromMinutes(10);

    /// <summary>How long before a move back the tab couldn't take is asked again.</summary>
    public static readonly TimeSpan BackRetry = TimeSpan.FromMinutes(5);

    private List<ApiFailover>? failovers;
    private readonly Dictionary<string, DateTime> backTried = new(StringComparer.Ordinal);

    /// <summary>The CLIs on the API key by failover now.</summary>
    public IReadOnlyList<ApiFailover> Failovers => Book();

    /// <summary>The failovers <paramref name="store"/> keeps; none when it can't be read.</summary>
    public static IReadOnlyList<ApiFailover> LoadFailovers(JsonStore store)
    {
        try { return store.Load<List<ApiFailover>>(FailoversName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return []; }
    }

    private List<ApiFailover> Book() => failovers ??= Store is { } s ? [.. LoadFailovers(s)] : [];

    private void SaveBook()
    {
        if (Store is not { } s) return;
        try { s.Save(FailoversName, Book()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }

    /// <summary>When the window that made a login low resets: the limit hit's, else the week's or the 5-hour window's by the
    /// meter, else the pace's; null when none says.</summary>
    public static DateTime? Until(LimitHit? hit, Pace? pace, ReportedUsage? usage, DateTime nowUtc, int paceTargetPercent, int weeklyStopPercent)
    {
        if (hit is { } h) return h.ResetUtc;
        if (ReportedUsage.Usable(usage?.Week, nowUtc) is { } week && week.Percent >= weeklyStopPercent) return week.ResetUtc;
        if (ReportedUsage.Usable(usage?.FiveHour, nowUtc) is { } five && five.Percent >= Math.Clamp(paceTargetPercent, 1, 100)) return five.ResetUtc;
        return pace?.ResetUtc;
    }

    /// <summary>Each session's newest limit hit still in force (its reset ahead).</summary>
    internal static Dictionary<string, LimitHit> InForce(IReadOnlyList<TranscriptSession>? sessions, DateTime nowUtc)
    {
        var hits = new Dictionary<string, LimitHit>(StringComparer.Ordinal);
        foreach (var s in sessions ?? [])
            foreach (var h in s.Hits.Where(h => h.ResetUtc > nowUtc && h.AtUtc <= nowUtc))
                if (!hits.TryGetValue(s.SessionId, out var seen) || h.AtUtc > seen.AtUtc) hits[s.SessionId] = h;
        return hits;
    }

    private static string LimitWords(LimitHit hit) => hit.Kind == LimitKind.Weekly ? "weekly" : "5-hour";

    /// <summary>
    /// Moves each movable CLI on a low login to a free login with more headroom, one CLI to a login; with none to move to, fails
    /// it over to the API key (M.ORCH-T664), and moves each failed-over CLI back once its login resets. <paramref name="globalPace"/>
    /// is the global login's pace, <paramref name="accounts"/> each per-CLI login's (<see cref="Snapshot.Accounts"/>), and
    /// <paramref name="sessions"/> the transcripts, whose limit hits (a limit error that ended a turn) fail a CLI over at once.
    /// </summary>
    public IReadOnlyList<Rebalanced> Tick(Pace? globalPace, IReadOnlyList<AccountPace> accounts, IReadOnlyList<HostSession> hosts,
        OrchestratorSettings settings, DateTime nowUtc, IReadOnlyList<TranscriptSession>? sessions = null)
    {
        if (!settings.RebalanceLogins && !settings.FailoverToApiKey && Book().Count == 0) return [];
        foreach (var old in tried.Where(t => nowUtc - t.Value >= CoolDown).Select(t => t.Key).ToList()) tried.Remove(old);

        IReadOnlyList<AccountDir> signedIn;
        try { signedIn = mover.Dirs.SignedIn(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }

        var stop = settings.WeeklyStopPercent;
        var target = settings.PaceTargetPercent;
        var own = mover.OnOwnLogin();
        string? LoginOf(CliBilling cli) => own.TryGetValue(cli.SessionId, out var dir) ? mover.Dirs.Of(dir)?.Email : null;
        var live = mover.Live(hosts, nowUtc);

        // A login a CLI is on its way to stays taken until the CLI is there, or the cool-down passes.
        foreach (var (email, (session, at)) in pending.ToList())
            if (nowUtc - at >= CoolDown || live.Any(c => c.SessionId == session && string.Equals(LoginOf(c), email, StringComparison.OrdinalIgnoreCase)))
                pending.Remove(email);

        var paces = accounts.ToDictionary(a => a.Email, a => a.Pace, StringComparer.OrdinalIgnoreCase);
        var usage = new Dictionary<string, ReportedUsage?>(StringComparer.OrdinalIgnoreCase);
        ReportedUsage? UsageOf(AccountDir a) => usage.TryGetValue(a.Email, out var u) ? u : usage[a.Email] = Usage(a, nowUtc);
        var globalUsage = new Lazy<ReportedUsage?>(() => Usage(null, nowUtc));
        AccountDir? DirOf(string? email) => email is null ? null : signedIn.FirstOrDefault(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));

        var results = new List<Rebalanced>();
        var onKey = MoveBack(live, DirOf, UsageOf, globalUsage, globalPace, paces, target, stop, nowUtc, results);

        var busy = new HashSet<string>(picker.Busy(nowUtc), StringComparer.OrdinalIgnoreCase);
        foreach (var cli in live) if (LoginOf(cli) is { } e) busy.Add(e);
        busy.UnionWith(pending.Keys);
        // A login whose CLI failed over is still that CLI's: it goes back there at the reset.
        foreach (var f in Book()) if (f.FromEmail is { } e) busy.Add(e);
        // Never a target while low itself: its CLIs would only be moved off again.
        foreach (var a in signedIn)
            if (Low(paces.GetValueOrDefault(a.Email), UsageOf(a), nowUtc, target, stop) is not null) busy.Add(a.Email);

        var hits = InForce(sessions, nowUtc);
        foreach (var cli in live.OrderBy(c => c.Pid))
        {
            if (cli.Host is null || cli.Bills == BillingModes.Api || onKey.Contains(cli.SessionId)) continue;
            LimitHit? hit = hits.TryGetValue(cli.SessionId, out var h) ? h : null;
            // A limit error since the last move or try can't wait out the cool-down: its turn is over and it can't go on.
            if (tried.TryGetValue(cli.SessionId, out var triedAt) && !(hit is { } th && th.AtUtc > triedAt)) continue;
            var from = LoginOf(cli);
            var fromDir = DirOf(from);
            if (from is not null && fromDir is null) continue; // a folder nobody's signed in to: nothing to measure
            var fromUsage = fromDir is null ? globalUsage.Value : UsageOf(fromDir);
            var fromPace = fromDir is null ? globalPace : paces.GetValueOrDefault(fromDir.Email);
            var why = hit is { } lh ? $"its {LimitWords(lh)} limit was hit" : Low(fromPace, fromUsage, nowUtc, target, stop);
            if (why is null) continue;

            // A pace at its limit (or a limit hit) has no room left, whatever the meter last said (it may not have been read lately).
            var fromRoom = hit is not null || fromPace is { } fp && (fp.Stop || fp.Mode == PaceMode.Over) ? 0 : LoginPicker.Headroom(fromUsage, nowUtc, stop);
            var to = LoginPicker.Choose(signedIn, busy, UsageOf, nowUtc, stop);
            var toRoom = to is null ? 0 : LoginPicker.Headroom(UsageOf(to), nowUtc, stop);
            var fromLabel = from ?? AccountMove.GlobalLabel.ToLowerInvariant();

            if (to is not null && toRoom - fromRoom >= MinGain)
            {
                // A login with room is always preferred to the API key.
                if (!settings.RebalanceLogins) continue;
                tried[cli.SessionId] = nowUtc;
                AccountMoveResult result;
                try { result = mover.MoveAsync(cli, to.Email, nowUtc).GetAwaiter().GetResult(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { result = new(false, ex.Message); }
                if (result.Ok)
                {
                    busy.Add(to.Email);
                    pending[to.Email] = (cli.SessionId, nowUtc);
                    LoginPicker.Claim(to, nowUtc);
                    results.Add(new(true, $"rebalance: moving the CLI in {cli.Where} (pid {cli.Pid}) from {fromLabel} to {to.Email}: {why}, " +
                                          $"and {to.Email} has no CLI and {Pct(toRoom)}% headroom; it restarts with claude --resume at its next idle point."));
                }
                else
                    results.Add(new(false, $"rebalance: {fromLabel} is low ({why}), but the CLI in {cli.Where} (pid {cli.Pid}) couldn't be moved to {to.Email}: {result.Message}"));
                continue;
            }

            // No signed-in login has room: the emergency failover onto the API key, for this CLI alone.
            if (!settings.FailoverToApiKey) continue;
            tried[cli.SessionId] = nowUtc;
            results.Add(Failover(cli, from, fromDir, why, Until(hit, fromPace, fromUsage, nowUtc, target, stop), now: hit is not null, nowUtc));
        }
        return results;
    }

    /// <summary>Fails <paramref name="cli"/> over to the API key: the key approved in its login's config, then its tab asked to
    /// restart it billing the key under the same login.</summary>
    private Rebalanced Failover(CliBilling cli, string? from, AccountDir? fromDir, string why, DateTime? untilUtc, bool now, DateTime nowUtc)
    {
        var configDir = fromDir?.Directory ?? "";
        var name = mover.NameFor(configDir, from ?? AccountMove.GlobalLabel);
        var reason = why.StartsWith("its ", StringComparison.Ordinal) ? $"{name}'s {why[4..]}" : $"{name}: {why}";
        if (!HasApiKey())
            return new(false, $"rebalance: the CLI in {cli.Where} (pid {cli.Pid}) can't fail over to the API key ({reason}, and no login has room): " +
                              $"there's no {BillingSwitch.VaultProvider} key in the MindAttic Vault keyring.");
        var configPath = fromDir?.ConfigPath ?? mover.Billing.ConfigPath;
        if (SetKeyChoice(configPath, true) is { } error)
            return new(false, $"rebalance: the CLI in {cli.Where} (pid {cli.Pid}) can't fail over to the API key ({reason}): {error}");
        bool sent;
        try { sent = mover.FailoverAsync(cli, configDir, BillingModes.Api, now, nowUtc).GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { sent = false; }
        if (!sent)
        {
            if (fromDir is not null) SetKeyChoice(configPath, false);
            return new(false, $"rebalance: the CLI in {cli.Where} (pid {cli.Pid}) couldn't fail over to the API key ({reason}): its Launcher tab has no control pipe to ask.");
        }
        Book().Add(new ApiFailover(cli.SessionId, cli.Pid, cli.Host?.ProcessId, cli.Cwd, from, configDir, name, why, nowUtc) { UntilUtc = untilUtc, SeenUtc = nowUtc });
        SaveBook();
        var back = untilUtc is { } u ? $" at {u.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}" : "";
        return new(true, $"rebalance: {cli.Where} CLI moved to API key: {reason}. No login has room, so it resumes its session on the key " +
                         $"{(now ? "at once (a limit error ended its turn)" : "at its next idle point")}, the billing mode unchanged, and moves back to {name} once it resets{back}.");
    }

    /// <summary>Moves each failed-over CLI whose login has reset back onto it, forgets the ones gone, and returns the sessions
    /// still on the key.</summary>
    private HashSet<string> MoveBack(IReadOnlyList<CliBilling> live, Func<string?, AccountDir?> dirOf, Func<AccountDir, ReportedUsage?> usageOf,
        Lazy<ReportedUsage?> globalUsage, Pace? globalPace, IReadOnlyDictionary<string, Pace> paces, int target, int stop, DateTime nowUtc,
        List<Rebalanced> results)
    {
        var book = Book();
        var changed = false;
        var onKey = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in book.ToList())
        {
            // The same session, else the CLI its Launcher tab runs now (a resume may start a new session id).
            var cli = live.FirstOrDefault(c => c.SessionId == f.SessionId) ??
                      (f.HostPid is { } hp ? live.FirstOrDefault(c => c.Host?.ProcessId == hp) : null);
            if (cli is null)
            {
                if (nowUtc - f.SeenUtc >= GoneFor) { book.Remove(f); changed = true; }
                continue;
            }
            var seen = f with { SessionId = cli.SessionId, Pid = cli.Pid, SeenUtc = nowUtc };
            book[book.IndexOf(f)] = seen;
            changed = true;
            onKey.Add(cli.SessionId);

            var home = dirOf(seen.FromEmail);
            if (seen.FromEmail is not null && home is null) continue; // signed out meanwhile: it stays on the key
            if (seen.UntilUtc is { } until && nowUtc < until) continue;
            var homeUsage = home is null ? globalUsage.Value : usageOf(home);
            var homePace = home is null ? globalPace : paces.GetValueOrDefault(home.Email);
            if (Low(homePace, homeUsage, nowUtc, target, stop) is not null) continue;
            if (backTried.TryGetValue(seen.SessionId, out var at) && nowUtc - at < BackRetry) continue;

            backTried[seen.SessionId] = nowUtc;
            bool sent;
            try { sent = mover.FailoverAsync(cli, seen.ConfigDir, BillingModes.Team, now: false, nowUtc).GetAwaiter().GetResult(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { sent = false; }
            if (!sent)
            {
                results.Add(new(false, $"rebalance: {seen.Name} has reset, but the Launcher tab running the CLI in {cli.Where} (pid {cli.Pid}) couldn't be asked " +
                                       "to move it back off the API key; it keeps billing the key until it can."));
                continue;
            }
            book.Remove(seen);
            onKey.Remove(cli.SessionId);
            backTried.Remove(seen.SessionId);
            tried[cli.SessionId] = nowUtc;
            if (home is not null) SetKeyChoice(home.ConfigPath, false);
            var gap = nowUtc - seen.SinceUtc;
            var gapText = gap.TotalHours >= 1 ? $"{(int)gap.TotalHours}h {gap.Minutes}m" : $"{Math.Max(1, (int)gap.TotalMinutes)}m";
            results.Add(new(true, $"rebalance: {cli.Where} CLI moved back to {seen.Name}: it has reset, so the API key billed only the gap ({gapText}); " +
                                  "it restarts with claude --resume at its next idle point."));
        }
        if (changed) SaveBook();
        return onKey;
    }
}

/// <summary>A CLI the rebalancer failed over to the API key (M.ORCH-T664): its session and tab, the login it came off
/// (<see cref="FromEmail"/> null: the global one, <see cref="ConfigDir"/> "") and its name, why, since when, and when that
/// login's window resets (null: not known; it goes back as soon as the login isn't low).</summary>
public sealed record ApiFailover(string SessionId, int Pid, int? HostPid, string? Cwd, string? FromEmail, string ConfigDir, string Name, string Why, DateTime SinceUtc)
{
    public DateTime? UntilUtc { get; init; }

    /// <summary>When its CLI was last seen running.</summary>
    public DateTime SeenUtc { get; init; }
}
