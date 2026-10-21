using System.Text.Json;
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

    private sealed class State(AccountDir dir, AccountPacer owner)
    {
        public RateLimitStatusWriter Writer { get; } = new(dir.StatusPath) { Db = null };
        public ContextCycler Marks { get; } = owner.ProjectsDir is { } p ? new(dir.MarksDirectory) { ProjectsDir = p } : new(dir.MarksDirectory);
        private ClaudeMeterProbe? meter;
        public ClaudeMeterProbe? Meter => owner.Probe
            ? meter ??= new ClaudeMeterProbe { UsagePath = dir.UsagePath, CredentialsPath = dir.CredentialsPath, AccountKey = () => Account }
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
        foreach (var (dir, account) in accounts)
        {
            if (!states.TryGetValue(dir.Email, out var state)) states[dir.Email] = state = new State(dir, this);
            state.Account = account;
            var ids = byEmail[dir.Email].ToHashSet(StringComparer.Ordinal);
            var mine = sessions.Where(s => ids.Contains(s.SessionId)).ToList();
            result.Add(Pace(dir, state, mine, settings, nowUtc, startOfTodayUtc, typicalPerSession, write));
        }
        return result;
    }

    private static AccountPace Pace(AccountDir dir, State state, List<TranscriptSession> sessions, OrchestratorSettings settings,
        DateTime now, DateTime startOfTodayUtc, double? typicalPerSession, bool write)
    {
        var events = sessions.SelectMany(s => s.Events).OrderBy(e => e.AtUtc).ToList();
        var reported = ReportedUsage.Load(now, dir.UsagePath);
        if (state.Meter?.LastReading is { } probed) reported = ClaudeMeterProbe.Fresher(reported, probed, now);
        var samples = state.Samples ??= LoadSamples(dir);
        if (ReportedLimit.Record(samples, reported, events, now) && write) SaveSamples(dir, samples);
        var limit = ReportedLimit.Estimate(samples);
        reported = ReportedLimit.Highest(samples, reported, now);
        var budget = BudgetCalculator.Compute(events, settings, now, startOfTodayUtc, reported: reported, reportedLimit: limit);

        var recentFrom = now - BudgetCalculator.RecentWindow;
        var active = sessions.Count(s => s.Events.Count > 0 && s.Events[^1].AtUtc >= recentFrom);
        var subagents = state.Marks.Subagents(out var seenThrough);
        var pace = Pacer.Compute(budget, active, typicalPerSession, settings, now, state.Writer.AgentDelay(now), subagents) with
        {
            SubagentsSeenThroughUtc = seenThrough,
            SubagentsByCli = state.Marks.AliveSubagents(now).Where(x => x.Cli is not null)
                .GroupBy(x => x.Cli!.Value).ToDictionary(g => g.Key, g => g.Count()),
        };
        var utilization = UtilizationSeries.Build(samples, budget, settings.PaceTargetPercent, now, settings.PaceExponent);
        pace = Pacer.UnderTarget(pace, utilization, settings.MaxSubagents);
        if (write)
        {
            state.Writer.Write(budget, pace);
            state.Marks.PruneSubagentMarks();
            // No statusline on this login has reported lately while it spends (a CLI waiting on its subagents): ask Claude.
            state.Meter?.Tick(ClaudeMeterProbe.SeenAt(reported, samples, now), events, now);
        }
        return new AccountPace(dir.Email, dir.Directory, state.Account, budget, pace, utilization, active);
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
