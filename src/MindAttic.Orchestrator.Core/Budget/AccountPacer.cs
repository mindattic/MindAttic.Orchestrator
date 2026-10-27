using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One per-CLI Claude login's own budget and pace (M.ORCH-T559), as <see cref="Snapshot.Accounts"/> lists them.</summary>
/// <param name="Email">The account (its folder's name).</param>
/// <param name="Account">Its key as <see cref="ClaudeAccount.Current"/> keys it (the ledger's account); null when unreadable.</param>
/// <param name="Sessions">CLIs and subagents that spent on it in the last <see cref="BudgetCalculator.RecentWindow"/>.</param>
public sealed record AccountPace(string Email, string Directory, string? Account, BudgetStatus Budget, Pace Pace, Utilization? Utilization, int Sessions);

/// <summary>
/// Paces each per-CLI Claude login on its own (M.ORCH-T559): its 5-hour and weekly windows are its token pool's, not the global
/// login's, so each gets its own budget (from Claude's meter in its folder and the transcripts of the sessions on it), its own
/// pace and subagent allowance (from its own subagents' marks), written to its folder's <c>rl-status.json</c> for the hooks of
/// the CLIs on it, and its own week of readings, which size its limit and grade its windows in the pace ledger. The global
/// login's pace leaves those sessions out (<see cref="ContextCycler.OnOwnLogin"/>).
/// </summary>
public sealed class AccountPacer
{
    public AccountDirs Dirs { get; init; } = new();

    /// <summary>Where transcripts are, to tell a subagent's mark from one whose transcript went quiet.</summary>
    public string? ProjectsDir { get; init; }

    /// <summary>Asks Claude's meter for an account none of whose CLIs has drawn a statusline lately; off in tests.</summary>
    public bool Probe { get; init; } = true;

    /// <summary>The logins the user let past their weekly target (M.ORCH-T694, <see cref="WeeklyAllowances"/>): each is paced to
    /// its week's wall, not the weekly target, until its allowance ends. Null holds every login to the target.</summary>
    public Func<IReadOnlyList<WeeklyAllowance>>? Allowances { get; init; }

    /// <summary>The members' own usage cutoffs (M.ORCH-T707, <see cref="UsageCutoffs"/>); null: the defaults for every login.</summary>
    public Func<IReadOnlyList<UsageCutoff>>? Cutoffs { get; init; }

    /// <summary>Whether a live Claude CLI runs on the login (by email) right now (M.ORCH-T1119); null counts every login as having one.</summary>
    public Func<string, bool>? CliOnLogin { get; init; }

    /// <summary>Whether the login's tabs bill its long-lived token (<see cref="LoginTokens.TabEnv"/>: kept, unexpired and unsealable here),
    /// so none of its CLIs holds the folder's own sign-in (M.ORCH-T1119).</summary>
    public Func<string, bool> TabsUseToken { get; init; } = email =>
        LoginTokens.Default.For(email) is { } t && t.Valid(DateTime.UtcNow) && LoginTokens.Reveal(t) is not null;

    /// <summary>Whether the meter probe may refresh <paramref name="email"/>'s folder sign-in (M.ORCH-T1119).</summary>
    private bool MayRefreshLogin(string email)
    {
        try { return MayRefresh(TabsUseToken(email), CliOnLogin?.Invoke(email) ?? true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Refreshing a folder's sign-in rotates its refresh token, signing out a CLI still holding the old one (M.ORCH-T684),
    /// so it's refreshed only when no CLI holds it: none runs on the login, or every tab on it bills the long-lived token instead.</summary>
    internal static bool MayRefresh(bool tabsUseToken, bool cliOnLogin) => tabsUseToken || !cliOnLogin;

    private sealed class State(AccountDir dir, AccountPacer owner)
    {
        public RateLimitStatusWriter Writer { get; } = new(dir.StatusPath) { Db = null };
        public ContextCycler Marks { get; } = owner.ProjectsDir is { } p ? new(dir.MarksDirectory) { ProjectsDir = p } : new(dir.MarksDirectory);
        private ClaudeMeterProbe? meter;
        public ClaudeMeterProbe? Meter => owner.Probe
            ? meter ??= new ClaudeMeterProbe
            {
                UsagePath = dir.UsagePath, CredentialsPath = dir.CredentialsPath, AccountKey = () => Account, Label = dir.Email,
                // Refreshed here too (M.ORCH-T1119): before, only the global login's probe was, so a login whose CLIs bill its
                // long-lived token, which never refreshes the folder's sign-in, went blind once that sign-in's 8-hour token expired.
                MayRefresh = () => owner.MayRefreshLogin(dir.Email),
            }
            : null;
        public List<UsageSample>? Samples;
        /// <summary>Who's signed in to the folder, as of this tick.</summary>
        public string? Account;
    }

    private readonly Dictionary<string, State> states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every signed-in account's budget and pace as of <paramref name="nowUtc"/>. With <paramref name="write"/> (the process
    /// keeping the global status, under its lease) each is written for its CLIs' hooks, its readings saved and its meter read.
    /// </summary>
    /// <param name="sessions">Every Claude session the transcripts hold.</param>
    /// <param name="onOwnLogin">Session → the config folder it runs under (<see cref="ContextCycler.OnOwnLogin"/>).</param>
    public IReadOnlyList<AccountPace> Tick(IReadOnlyList<TranscriptSession> sessions, IReadOnlyDictionary<string, string> onOwnLogin,
        OrchestratorSettings settings, DateTime nowUtc, DateTime startOfTodayUtc, double? typicalPerSession, bool write)
    {
        // Each folder's sign-in read once a tick: an account's .claude.json can run to megabytes.
        var accounts = Dirs.All().Select(d => (Dir: d, Account: d.Account)).Where(a => a.Account is { } k && k != ClaudeAccount.ApiKey).ToList();
        foreach (var gone in states.Keys.Where(k => accounts.All(a => a.Dir.Email != k)).ToList()) states.Remove(gone);
        if (accounts.Count == 0) return [];
        var byEmail = onOwnLogin
            .Select(o => (Session: o.Key, Dir: Dirs.Of(o.Value)))
            .Where(o => o.Dir is not null)
            .ToLookup(o => o.Dir!.Email, o => o.Session, StringComparer.OrdinalIgnoreCase);
        var result = new List<AccountPace>(accounts.Count);
        IReadOnlyList<WeeklyAllowance>? allowed = null;
        try { allowed = Allowances?.Invoke(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        IReadOnlyList<UsageCutoff>? cutoffs = null;
        try { cutoffs = Cutoffs?.Invoke(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        foreach (var (dir, account) in accounts)
        {
            if (!states.TryGetValue(dir.Email, out var state)) states[dir.Email] = state = new State(dir, this);
            state.Account = account;
            var ids = byEmail[dir.Email].ToHashSet(StringComparer.Ordinal);
            var mine = sessions.Where(s => ids.Contains(s.SessionId)).ToList();
            // Let past its weekly target by the user (M.ORCH-T694): its week stops at the wall until the allowance ends.
            var (own, allowedWhy) = WeeklyAllowances.Apply(allowed, dir.Email, UsageCutoffs.Apply(cutoffs, dir.Email, settings), nowUtc);
            result.Add(Pace(dir, state, mine, own, nowUtc, startOfTodayUtc, typicalPerSession, write, allowedWhy));
        }
        return result;
    }

    private AccountPace Pace(AccountDir dir, State state, List<TranscriptSession> sessions, OrchestratorSettings settings,
        DateTime now, DateTime startOfTodayUtc, double? typicalPerSession, bool write, string? why = null)
    {
        var events = sessions.SelectMany(s => s.Events).OrderBy(e => e.AtUtc).ToList();
        var reported = ReportedUsage.Load(now, dir.UsagePath);
        if (state.Meter?.LastReading is { } probed) reported = ClaudeMeterProbe.Fresher(reported, probed, now);
        var samples = state.Samples ??= LoadSamples(dir);
        // The week's percent is kept with each reading, to learn how much of the week a 5-hour point costs (M.ORCH-T666).
        if (ReportedLimit.Record(samples, reported, events, now, withWeek: true) && write) SaveSamples(dir, samples);
        var limit = ReportedLimit.Estimate(samples);
        reported = ReportedLimit.Highest(samples, reported, now);
        // This account's own limit hits (M.ORCH-T774), read from its own sessions' transcripts: before this, a wall Claude
        // actually reported to one of this account's CLIs never reached BudgetCalculator (no LimitCalibration was ever
        // passed here), so the only thing stopping it was the percent-based target, and the pace ledger could only ever
        // say "Claude reported 100%", never "hit the 5-hour limit", for a per-CLI login. The file is small (pruned to
        // LimitCalibration.Keep), so it's read fresh every tick rather than cached on State, which would otherwise hold a
        // stale, wrongly-tagged merge across a live /login switch within this process's life. Tagged with the account, so a
        // folder re-signed to a different one (its own .claude.json changed under the same folder) starts fresh rather than
        // being held to the old account's wall, which for a weekly one could otherwise block it for days.
        var before = LoadLimits(dir, state.Account);
        var observed = LimitCalibration.Observe(sessions.SelectMany(s => s.Hits), events, now - LimitCalibration.Keep);
        var limits = LimitCalibration.Merge(before, observed, now);
        if (write && !limits.SequenceEqual(before)) SaveLimits(dir, state.Account, limits);
        var calibration = new LimitCalibration(limits);
        // The stop counts what's been spent since Claude's percent was first seen, not since a statusline last restamped it (M.ORCH-T1110).
        var budget = BudgetCalculator.Compute(events, settings, now, startOfTodayUtc, learned: calibration, reported: reported, reportedLimit: limit,
            reportedSeenUtc: ReportedLimit.FirstSeen(samples, reported));

        var recentFrom = now - BudgetCalculator.RecentWindow;
        var active = sessions.Count(s => s.Events.Count > 0 && s.Events[^1].AtUtc >= recentFrom);
        var subagents = state.Marks.Subagents(out var seenThrough);
        // Weekly-aware pacing (M.ORCH-T666): from its line the week caps this login's window; off, the cap is null and nothing changes.
        // Held only as far as the week, spent as it's being spent, needs (M.ORCH-T669), against this login's own weekly reset.
        var week = WeeklyPacing.Cap(budget, WeeklyRatio.Learn(samples), settings, now, WeeklyBurn.PerHour(samples, budget.WeekResetUtc, now));
        var pace = Pacer.Compute(budget, active, typicalPerSession, settings, now, state.Writer.AgentDelay(now), subagents, week) with
        {
            SubagentsSeenThroughUtc = seenThrough,
            SubagentsByCli = state.Marks.AliveSubagents(now).Where(x => x.Cli is not null)
                .GroupBy(x => x.Cli!.Value).ToDictionary(g => g.Key, g => g.Count()),
        };
        // A window the week caps is on course for its ceiling, not the 5-hour target: under it, more subagents start only as far as the cap allows.
        var utilization = UtilizationSeries.Build(samples, budget, week is { Capped: true, CeilingPercent: { } ceiling } ? ceiling : settings.PaceTargetPercent,
            now, settings.PaceExponent);
        pace = Pacer.UnderTarget(pace, utilization, settings.MaxSubagents);
        // Split across many CLIs whose live trend together crossed the pace line: new subagents start slower until it bends back (M.ORCH-T785).
        pace = Pacer.CoolOff(pace, utilization, Math.Max(active, pace.SubagentsByCli.Count));
        // A login with no limit learned yet is Unpaced with SubagentAllowance null, which otherwise falls back (Allocate.More)
        // to MaxConcurrentSessions minus every busy ticket across the whole workspace, leaving a fresh account no room of its
        // own (M.ORCH-T660). Its room is its own: the session setting, bounded by MaxSubagents, counted against its own marks.
        if (pace is { IsPaced: false, SubagentAllowance: null, Stop: false })
            pace = pace with { SubagentAllowance = Math.Max(1, Math.Min(settings.MaxConcurrentSessions, settings.MaxSubagents)), Subagents = subagents };
        // The room under the stop limits a burst of new subagents (M.ORCH-T959): they spend past the stop while it waits for a tool call.
        pace = Pacer.CapByStopHeadroom(pace, ReportedUsage.Usable(reported?.FiveHour, now)?.Percent, Pacer.StopPercent(settings, week));
        // Let past its weekly target by the user (M.ORCH-T694): said in its pace note, which its tab shows.
        if (why is not null) pace = pace with { Note = $"{pace.Note} {why}" };
        if (write)
        {
            state.Writer.Write(budget, pace);
            state.Marks.PruneSubagentMarks();
            // No statusline on this login has reported lately while it spends (a CLI waiting on its subagents): ask Claude.
            state.Meter?.Tick(ClaudeMeterProbe.SeenAt(reported, samples, now), events, now, samples: samples);
            if (state.Meter is { } probe) Observe(dir, state, probe, sessions.Count, events, now);
            if (settings.WeeklyAwarePacing) Plan(dir.Email, budget, week, now);
        }
        return new AccountPace(dir.Email, dir.Directory, state.Account, budget, pace, utilization, active);
    }

    /// <summary>The <c>state</c> row (this machine's, left out of the dump) holding each login's meter probe: when it last asked
    /// and what came of it, so a window that went unseen can be traced afterwards (M.ORCH-T659).</summary>
    public const string ProbeStateName = "meter-probe";

    private readonly Dictionary<string, JsonObject> probeRows = new(StringComparer.OrdinalIgnoreCase);
    private string? probeRowsWritten;
    private readonly Dictionary<string, DateTime> notAskedSince = new(StringComparer.OrdinalIgnoreCase);

    private void Observe(AccountDir dir, State state, ClaudeMeterProbe probe, int sessions, List<UsageEvent> events, DateTime now) =>
        Observe(dir.Email, probe, sessions, events, now);

    /// <summary>Records the global login's probe in the same row as the own logins' (M.ORCH-T687), under <paramref name="key"/>
    /// (its email): the usage report found no row for it and said the meter "wasn't asked" while it asked every few minutes.</summary>
    public void ObserveGlobal(string key, ClaudeMeterProbe probe, int sessions, IReadOnlyList<UsageEvent> events, DateTime now) =>
        Observe(key, probe, sessions, [.. events], now);

    private void Observe(string email, ClaudeMeterProbe probe, int sessions, List<UsageEvent> events, DateTime now)
    {
        // Due but not asked for a while says why, once, in the log: that's the gap the next blind window would be.
        if (probe.IsDue && probe.Skipped is { } why)
        {
            if (!notAskedSince.TryGetValue(email, out var since)) notAskedSince[email] = since = now;
            if (now - since >= TimeSpan.FromMinutes(10))
            {
                ClaudeMeterProbe.Say?.Invoke(Serilog.Events.LogEventLevel.Warning, $"meter probe [{email}]: due for {(now - since).TotalMinutes:0} min but not asking: {why}");
                notAskedSince[email] = now;
            }
        }
        else notAskedSince.Remove(email);
        probeRows[email] = new JsonObject
        {
            ["asks"] = probe.Asks,
            ["lastAskUtc"] = probe.LastAskUtc?.ToString("O"),
            ["lastResult"] = probe.LastResult,
            ["failuresInRow"] = probe.FailuresInRow,
            ["needsSignIn"] = probe.NeedsSignIn,
            ["due"] = probe.IsDue,
            ["skipped"] = probe.Skipped,
            ["sessions"] = sessions,
            ["lastEventUtc"] = events.Count == 0 ? null : events[^1].AtUtc.ToString("O"),
        };
        var json = new JsonObject(probeRows.Select(r => KeyValuePair.Create(r.Key, (JsonNode?)r.Value.DeepClone()))).ToJsonString();
        if (json == probeRowsWritten) return;
        try
        {
            ReportedUsage.DefaultDb.SaveState(ProbeStateName, json);
            probeRowsWritten = json;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
    }

    private List<PlannedWindow>? planRows;
    private string? planWritten;

    /// <summary>Keeps the planned-against-actual log of each login's windows (M.ORCH-T669, <see cref="PacePlan"/>), and says in the
    /// log how each window ended against its plan as it closes.</summary>
    private void Plan(string email, BudgetStatus budget, WeeklyCap? week, DateTime now)
    {
        try
        {
            planRows ??= PacePlan.Load(ReportedUsage.DefaultDb);
            var closed = new List<string>();
            if (!PacePlan.Observe(planRows, email, budget, week, now, closed)) return;
            foreach (var line in closed) ClaudeMeterProbe.Say?.Invoke(Serilog.Events.LogEventLevel.Information, line);
            var json = PacePlan.Serialize(planRows);
            if (json == planWritten) return;
            ReportedUsage.DefaultDb.SaveState(PacePlan.StateName, json);
            planWritten = json;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>The account's week of Claude's readings (<see cref="AccountDir.SamplesPath"/>); empty when there are none yet.</summary>
    public static List<UsageSample> LoadSamples(AccountDir dir)
    {
        try { return File.Exists(dir.SamplesPath) ? JsonSerializer.Deserialize<List<UsageSample>>(File.ReadAllText(dir.SamplesPath), Json) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }

    private static void SaveSamples(AccountDir dir, List<UsageSample> samples)
    {
        try
        {
            var temp = dir.SamplesPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(samples, Json));
            File.Move(temp, dir.SamplesPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>This account's own limit hits (<see cref="AccountDir.LimitsPath"/>, M.ORCH-T774), tagged with the account
    /// they were kept for; empty when there are none yet, or (<paramref name="account"/> given) when the folder's signed-in
    /// account has changed since they were saved, so a previous account's wall (a weekly one could otherwise block a new
    /// sign-in for days) is never read as this one's.</summary>
    public static List<LimitObservation> LoadLimits(AccountDir dir, string? account = null)
    {
        try
        {
            if (!File.Exists(dir.LimitsPath)) return [];
            var stored = JsonSerializer.Deserialize<StoredLimits>(File.ReadAllText(dir.LimitsPath), Json);
            return stored is null || (account is not null && stored.Account != account) ? [] : stored.Observations;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }

    private static void SaveLimits(AccountDir dir, string? account, List<LimitObservation> limits)
    {
        try
        {
            var temp = dir.LimitsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new StoredLimits(account ?? "", limits), Json));
            File.Move(temp, dir.LimitsPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>What <see cref="AccountDir.LimitsPath"/> holds: the account it was kept for, and that account's own observed
    /// hits (M.ORCH-T774 review: a bare array couldn't say whose hits they were, so a re-signed folder would keep reading the
    /// previous account's).</summary>
    private sealed record StoredLimits(string Account, List<LimitObservation> Observations);
}
