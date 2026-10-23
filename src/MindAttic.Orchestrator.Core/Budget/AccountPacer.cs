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

    /// <summary>Each login's outside use (M.ORCH-T682): a login whose member is using Claude elsewhere is paced to the lowered
    /// ceiling and weekly stop kept there (<see cref="OutsideUseWatch.Apply"/>). Null paces every login to the settings.</summary>
    public Func<IReadOnlyList<OutsideUseLogin>>? OutsideHolds { get; init; }

    /// <summary>The logins the user let past their weekly target (M.ORCH-T694, <see cref="WeeklyAllowances"/>): each is paced to
    /// its week's wall, not the weekly target, until its allowance ends. Null holds every login to the target.</summary>
    public Func<IReadOnlyList<WeeklyAllowance>>? Allowances { get; init; }

    private sealed class State(AccountDir dir, AccountPacer owner)
    {
        public RateLimitStatusWriter Writer { get; } = new(dir.StatusPath) { Db = null };
        public ContextCycler Marks { get; } = owner.ProjectsDir is { } p ? new(dir.MarksDirectory) { ProjectsDir = p } : new(dir.MarksDirectory);
        private ClaudeMeterProbe? meter;
        public ClaudeMeterProbe? Meter => owner.Probe
            ? meter ??= new ClaudeMeterProbe { UsagePath = dir.UsagePath, CredentialsPath = dir.CredentialsPath, AccountKey = () => Account, Label = dir.Email }
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
        IReadOnlyList<OutsideUseLogin>? holds = null;
        try { holds = OutsideHolds?.Invoke(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        IReadOnlyList<WeeklyAllowance>? allowed = null;
        try { allowed = Allowances?.Invoke(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        foreach (var (dir, account) in accounts)
        {
            if (!states.TryGetValue(dir.Email, out var state)) states[dir.Email] = state = new State(dir, this);
            state.Account = account;
            var ids = byEmail[dir.Email].ToHashSet(StringComparer.Ordinal);
            var mine = sessions.Where(s => ids.Contains(s.SessionId)).ToList();
            var (held, outsideWhy) = OutsideUseWatch.Apply(holds, dir.Email, settings);
            // Let past its weekly target by the user (M.ORCH-T694): its week stops at the wall until the allowance ends.
            var (own, allowedWhy) = WeeklyAllowances.Apply(allowed, dir.Email, held, nowUtc);
            var why = allowedWhy is null ? outsideWhy : outsideWhy is null ? allowedWhy : $"{outsideWhy} {allowedWhy}";
            result.Add(Pace(dir, state, mine, own, nowUtc, startOfTodayUtc, typicalPerSession, write, why));
        }
        return result;
    }

    private AccountPace Pace(AccountDir dir, State state, List<TranscriptSession> sessions, OrchestratorSettings settings,
        DateTime now, DateTime startOfTodayUtc, double? typicalPerSession, bool write, string? outsideWhy = null)
    {
        var events = sessions.SelectMany(s => s.Events).OrderBy(e => e.AtUtc).ToList();
        var reported = ReportedUsage.Load(now, dir.UsagePath);
        if (state.Meter?.LastReading is { } probed) reported = ClaudeMeterProbe.Fresher(reported, probed, now);
        var samples = state.Samples ??= LoadSamples(dir);
        // The week's percent is kept with each reading, to learn how much of the week a 5-hour point costs (M.ORCH-T666).
        if (ReportedLimit.Record(samples, reported, events, now, withWeek: true) && write) SaveSamples(dir, samples);
        var limit = ReportedLimit.Estimate(samples);
        reported = ReportedLimit.Highest(samples, reported, now);
        var budget = BudgetCalculator.Compute(events, settings, now, startOfTodayUtc, reported: reported, reportedLimit: limit);

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
        // A login with no limit learned yet is Unpaced with SubagentAllowance null, which otherwise falls back (Autopilot.More)
        // to MaxConcurrentSessions minus every busy ticket across the whole workspace, leaving a fresh account no room of its
        // own (M.ORCH-T660). Its room is its own: the session setting, bounded by MaxSubagents, counted against its own marks.
        if (pace is { IsPaced: false, SubagentAllowance: null, Stop: false })
            pace = pace with { SubagentAllowance = Math.Max(1, Math.Min(settings.MaxConcurrentSessions, settings.MaxSubagents)), Subagents = subagents };
        // Held to a lowered ceiling while its member uses Claude elsewhere (M.ORCH-T682): said in its pace note, which its tab shows.
        if (outsideWhy is not null) pace = pace with { Note = $"{pace.Note} {outsideWhy}" };
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
}
