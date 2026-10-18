using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Settings;

/// <summary>What you tell the orchestrator. Everything has a working default.</summary>
public sealed record OrchestratorSettings
{
    /// <summary>The folder holding every repo. Null uses MindAttic.Workflow's default; the first run
    /// asks for one when that doesn't exist on this machine.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>Uncached tokens (input + cache write + output) allowed per 5-hour usage block.
    /// Null measures blocks against your own peak block in the last 7 days instead.</summary>
    public long? BlockBudget { get; init; }

    /// <summary>Uncached tokens allowed per week. Null means no weekly line.</summary>
    public long? WeeklyBudget { get; init; }

    /// <summary>When the week resets, as "Monday 09:00" local time (see <see cref="Budget.WeeklyReset"/>).
    /// Null means the week is a rolling 7 days.</summary>
    public string? WeeklyReset { get; init; }

    /// <summary>Percent of a budget at which the dashboard warns. Without pacing, the scheduler also stops proposing here.</summary>
    public int WarnPercent { get; init; } = 80;

    /// <summary>On: with a limit to pace against (set or learned), the scheduler caps sessions so
    /// spend lands on <see cref="PaceTargetPercent"/> at the reset instead of holding at the warn line.</summary>
    public bool Pacing { get; init; } = true;

    /// <summary>How much of each limit pacing aims to use by the reset; the rest is margin for usage
    /// the orchestrator can't see (claude.ai, other machines).</summary>
    public int PaceTargetPercent { get; init; } = 98;

    /// <summary>On: while the forecast says the burn won't last until the reset, running Claude Code agents
    /// wait before each tool call (the pace-gate hook), so they slow down instead of hitting the wall.</summary>
    public bool ThrottleAgents { get; init; } = true;

    /// <summary>The slash commands exempt from orchestration pacing (M.ORCH-T198): a turn whose prompt starts with one skips
    /// every Orchestrator gate (no pace delay, no subagent hold, no stop refusal) until it ends, and a Skill call naming one
    /// is let through anywhere. The pace gate reads this live from settings.json; its spend is still counted.</summary>
    public List<string> ExemptCommands { get; init; } = [.. DefaultExemptCommands];

    public static IReadOnlyList<string> DefaultExemptCommands { get; } = ["/throttle", "/unthrottle", "/deploy", "/run"];

    /// <summary>Normalizes typed command names ("deploy, /Run") to "/deploy", "/run"; blanks and repeats dropped.</summary>
    public static List<string> ParseCommands(string text) =>
        [.. text.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => "/" + c.TrimStart('/').ToLowerInvariant()).Where(c => c.Length > 1).Distinct()];

    /// <summary>The longest one tool call is held while throttling.</summary>
    public int MaxAgentDelaySeconds { get; init; } = 300;

    /// <summary>The most subagents that may run at once across every CLI, however much room the pace has; under it,
    /// the allowance is worked out live from the pace (<see cref="Budget.Pace.SubagentAllowance"/>).</summary>
    public int MaxSubagents { get; init; } = 16;

    /// <summary>Context fill at which a session is wrapped up and cycled; high enough to use most of the
    /// window, low enough that the wrap-up turn fits before Claude Code compacts on its own.</summary>
    public int ContextCyclePercent { get; init; } = 80;

    /// <summary>Sessions the scheduler will fill up to (working + waiting both occupy a slot).</summary>
    public int MaxConcurrentSessions { get; init; } = 4;

    /// <summary>The master switch for <c>/autopilot</c>: on, every CLI where <c>/autopilot</c> was typed is fed its
    /// repos' next story whenever it's idle and the pace allows; off pauses all of them (their marks stay).</summary>
    public bool Autopilot { get; init; } = true;

    /// <summary>Hours without a transcript write or open tab where a ticket was dispatched before it
    /// goes back in the queue as stale. 0 keeps assignments until the ticket closes or is released.</summary>
    public int StaleAfterHours { get; init; } = 12;

    public string DefaultProvider { get; init; } = "Claude";

    /// <summary>Worktrees for concurrent tickets live here, outside the workspace root so they never
    /// show up as repos of their own.</summary>
    public string WorktreeRoot { get; init; } = @"D:\Projects\MindAttic.worktrees";

    /// <summary>Percent of the 5-hour limit one repo may spend in a block before it gets no new sessions
    /// (repo name → percent), so one greedy repo can't eat the block. Missing means no cap.</summary>
    public Dictionary<string, int> RepoBudgetPercent { get; init; } = [];

    /// <summary>For a provider other than Claude: its own reported 5-hour used percent at which dispatching to it holds. 0 means never.</summary>
    public int ProviderCapPercent { get; init; } = 90;

    /// <summary>Uncached tokens set aside for a ticket (ticket → tokens): it's proposed first and may start
    /// while pacing coasts, and everything else paces against what's left after it.</summary>
    public Dictionary<string, long> Reservations { get; init; } = [];

    /// <summary>Local-time windows when autopilot pauses, like "22:00-07:00". Manual starts still work.</summary>
    public List<string> QuietHours { get; init; } = [];

    /// <summary>Ticket → the tickets it waits for. A summary saying "blocked by X-T1" counts too.</summary>
    public Dictionary<string, List<string>> BlockedBy { get; init; } = [];

    /// <summary>Sessions one repo may run at once (the repo itself plus worktrees), unless <see cref="RepoMaxSessions"/> says otherwise.</summary>
    public int MaxSessionsPerRepo { get; init; } = 2;

    public Dictionary<string, int> RepoMaxSessions { get; init; } = [];

    /// <summary><paramref name="repo"/>'s entry in a per-repo map, its name matched ignoring case as everywhere else repos are
    /// named (typed as "prose", saved as such, it still applies to Prose); <paramref name="otherwise"/> when it has none.</summary>
    public static int ForRepo(IReadOnlyDictionary<string, int> map, string repo, int otherwise) =>
        map.TryGetValue(repo, out var exact) ? exact
            : map.FirstOrDefault(kv => kv.Key.Equals(repo, StringComparison.OrdinalIgnoreCase)) is { Key: not null } kv ? kv.Value
            : otherwise;

    /// <summary>Keep development going without you: a CLI whose turn ends asking whether to go on while its repo still has
    /// open tickets is told to carry on (the context gate's Stop hook). False lets it stop and ask.</summary>
    public bool KeepGoing { get; init; } = true;

    /// <summary>Minutes since a repo's own CLI last ended a turn that it still counts as worked by hand, so autopilot
    /// leaves the repo alone through the pauses between your prompts. 0 counts only a CLI that's mid-turn.</summary>
    public int AutopilotHandMinutes { get; init; } = 15;

    /// <summary>Repos <c>/autopilot</c> never starts agents in, even when named or their CLI is open.</summary>
    public List<string> AutopilotSkip { get; init; } = [];

    /// <summary>Tickets you approved for work (<c>orchestrator approve</c>). Fixes need no approval; any other open
    /// ticket waits outside the queue until it's here, so no agent works a feature you didn't ask for.</summary>
    public List<string> Approved { get; init; } = [];

    /// <summary>Whether <paramref name="ticket"/> may be worked: a fix, or one you approved.</summary>
    public bool MayWork(MindAttic.Workflow.Tickets.Ticket ticket) =>
        ticket.Type.Equals("fix", StringComparison.OrdinalIgnoreCase) || Approved.Contains(ticket.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Open tickets older than this many days with nobody on them are listed for grooming. 0 means never.</summary>
    public int GroomAfterDays { get; init; } = 30;

    /// <summary>Where alerts go off the desktop: a URL that takes a plain-text POST, like an ntfy.sh topic
    /// (https://ntfy.sh/your-topic) for phone pushes. Null sends nothing.</summary>
    public string? NotifyUrl { get; init; }

    /// <summary>Hours between audits the daemon runs by itself (it alerts on anything new). 0 means only on demand.</summary>
    public int AuditEveryHours { get; init; } = 24;

    /// <summary>Off by default, since it writes tickets into every repo's log: hourly, file GitHub issues
    /// labelled "orchestrator" and CI runs that failed on main as tickets (see <see cref="Tickets.GitHubIntake"/>).</summary>
    public bool GitHubIntake { get; init; }

    /// <summary>Manual priority per ticket ID; higher runs sooner. Missing means 0.</summary>
    public Dictionary<string, int> Priorities { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>A ticket the orchestrator has handed to a session.</summary>
public sealed record Assignment(string TicketId, string Repo, string Directory, string Provider, DateTime StartedUtc, bool Worktree)
{
    /// <summary>The background agent <c>/autopilot</c> started on it (its process id); null for a tab.</summary>
    public int? Pilot { get; init; }

    /// <summary>When <see cref="Pilot"/>'s process started, so a later process given the same id isn't taken for it.</summary>
    public DateTime? PilotStartedUtc { get; init; }

    /// <summary>Where that background agent's output goes.</summary>
    public string? Log { get; init; }
}

/// <summary>Small JSON files under <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>, written atomically.</summary>
public sealed class JsonStore(string? directory = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Orchestrator");

    /// <summary>How long a file another process has open (mid-save, mid-read) is retried before giving up.</summary>
    private static readonly TimeSpan Busy = TimeSpan.FromSeconds(1);

    /// <summary>The file's contents, or defaults when it's missing, unreadable JSON, access is denied, or locked past <see cref="Busy"/>.
    /// A briefly locked file is retried: callers load, change and save, so defaults here would be saved over the real file.</summary>
    public T Load<T>(string name) where T : new()
    {
        TryLoad<T>(name, out var value);
        return value;
    }

    /// <summary>Like <see cref="Load{T}"/>, but says whether the file was read: false (with defaults) when it exists and
    /// couldn't be, so a caller holding an earlier copy keeps that rather than taking an empty one for the truth.</summary>
    public bool TryLoad<T>(string name, out T value) where T : new()
    {
        var path = PathOf(name);
        value = new T();
        if (!File.Exists(path)) return true;
        try
        {
            value = JsonSerializer.Deserialize<T>(Retry(() => File.ReadAllText(path)), Options) ?? new T();
            return true;
        }
        catch (FileNotFoundException) { return true; } // removed since the check: missing, as above
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; } // denied, or still locked past Busy
    }

    public void Save<T>(string name, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathOf(name);
        // Each writer has its own temp file, so two saving at once can't move each other's half-written JSON into place.
        var temp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        try { Retry(() => { File.Move(temp, path, overwrite: true); return true; }); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static TResult Retry<TResult>(Func<TResult> io)
    {
        var until = DateTime.UtcNow + Busy;
        while (true)
        {
            try { return io(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && ex is not FileNotFoundException && DateTime.UtcNow < until)
            {
                Thread.Sleep(50);
            }
        }
    }

    public OrchestratorSettings LoadSettings() => Load<OrchestratorSettings>("settings");
    public void SaveSettings(OrchestratorSettings s) => Save("settings", s);
    public List<Assignment> LoadAssignments() => Load<List<Assignment>>("assignments");
    public void SaveAssignments(List<Assignment> a) => Save("assignments", a);

    /// <summary>Loads, changes and saves the assignments under a lock every process takes (the daemon's autopilot, a
    /// dashboard's Dispatch, the CLI), so one's save never drops what another added in between.</summary>
    public void UpdateAssignments(Action<List<Assignment>> change) => Update("assignments", change);

    public void Update<T>(string name, Action<T> change) where T : new()
    {
        System.IO.Directory.CreateDirectory(Directory);
        using var held = Lock(name);
        // Not Load's defaults: a file that can't be read right now would be saved over with an empty one (every
        // assignment lost, its agents untracked). Throwing leaves the file as it is for the next try.
        var path = PathOf(name);
        T value;
        try { value = File.Exists(path) ? JsonSerializer.Deserialize<T>(Retry(() => File.ReadAllText(path)), Options) ?? new T() : new T(); }
        catch (JsonException ex) { throw new InvalidDataException($"{path} isn't readable JSON; left as it is rather than saved over", ex); }
        change(value);
        Save(name, value);
    }

    /// <summary>An exclusive lock file beside <paramref name="name"/>; null past a few seconds (go ahead unlocked rather than
    /// stall a refresh behind a stuck holder).</summary>
    private FileStream? Lock(string name)
    {
        var path = PathOf(name) + ".lock";
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow >= until) return null;
                Thread.Sleep(25);
            }
        }
    }
    public bool HasLimits => File.Exists(PathOf("limits"));
    public List<Budget.LimitObservation> LoadLimits() => Load<List<Budget.LimitObservation>>("limits");
    public void SaveLimits(List<Budget.LimitObservation> o) => Save("limits", o);
    public List<Budget.DayUsage> LoadUsageHistory() => Load<List<Budget.DayUsage>>("usage-history");
    public void SaveUsageHistory(List<Budget.DayUsage> d) => Save("usage-history", d);
    public List<Budget.SessionCost> LoadTicketCosts() => Load<List<Budget.SessionCost>>("ticket-costs");
    public void SaveTicketCosts(List<Budget.SessionCost> c) => Save("ticket-costs", c);

    private string PathOf(string name) => Path.Combine(Directory, name + ".json");
}
