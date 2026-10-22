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

    /// <summary>
    /// Moves each movable CLI on a low login to a free login with more headroom, one CLI to a login. <paramref name="globalPace"/>
    /// is the global login's pace, <paramref name="accounts"/> each per-CLI login's (<see cref="Snapshot.Accounts"/>).
    /// </summary>
    public IReadOnlyList<Rebalanced> Tick(Pace? globalPace, IReadOnlyList<AccountPace> accounts, IReadOnlyList<HostSession> hosts,
        OrchestratorSettings settings, DateTime nowUtc)
    {
        if (!settings.RebalanceLogins) return [];
        foreach (var old in tried.Where(t => nowUtc - t.Value >= CoolDown).Select(t => t.Key).ToList()) tried.Remove(old);

        IReadOnlyList<AccountDir> signedIn;
        try { signedIn = mover.Dirs.SignedIn(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
        if (signedIn.Count == 0) return [];

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

        var busy = new HashSet<string>(picker.Busy(nowUtc), StringComparer.OrdinalIgnoreCase);
        foreach (var cli in live) if (LoginOf(cli) is { } e) busy.Add(e);
        busy.UnionWith(pending.Keys);
        // Never a target while low itself: its CLIs would only be moved off again.
        foreach (var a in signedIn)
            if (Low(paces.GetValueOrDefault(a.Email), UsageOf(a), nowUtc, target, stop) is not null) busy.Add(a.Email);

        var results = new List<Rebalanced>();
        foreach (var cli in live.OrderBy(c => c.Pid))
        {
            if (cli.Host is null || cli.Bills == BillingModes.Api || tried.ContainsKey(cli.SessionId)) continue;
            var from = LoginOf(cli);
            var fromDir = from is null ? null : signedIn.FirstOrDefault(a => string.Equals(a.Email, from, StringComparison.OrdinalIgnoreCase));
            if (from is not null && fromDir is null) continue; // a folder nobody's signed in to: nothing to measure
            var fromUsage = fromDir is null ? globalUsage.Value : UsageOf(fromDir);
            var fromPace = fromDir is null ? globalPace : paces.GetValueOrDefault(fromDir.Email);
            if (Low(fromPace, fromUsage, nowUtc, target, stop) is not { } why) continue;

            // A pace at its limit has no room left, whatever the meter last said (it may not have been read lately).
            var fromRoom = fromPace is { } fp && (fp.Stop || fp.Mode == PaceMode.Over) ? 0 : LoginPicker.Headroom(fromUsage, nowUtc, stop);
            var to = LoginPicker.Choose(signedIn, busy, UsageOf, nowUtc, stop);
            if (to is null) continue;
            var toRoom = LoginPicker.Headroom(UsageOf(to), nowUtc, stop);
            if (toRoom - fromRoom < MinGain) continue;

            var fromLabel = from ?? AccountMove.GlobalLabel.ToLowerInvariant();
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
        }
        return results;
    }
}
