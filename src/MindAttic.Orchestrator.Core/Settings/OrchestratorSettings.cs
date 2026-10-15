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
    public int PaceTargetPercent { get; init; } = 99;

    /// <summary>On: while the forecast says the burn won't last until the reset, running Claude Code agents
    /// wait before each tool call (the pace-gate hook), so they slow down instead of hitting the wall.</summary>
    public bool ThrottleAgents { get; init; } = true;

    /// <summary>The longest one tool call is held while throttling.</summary>
    public int MaxAgentDelaySeconds { get; init; } = 300;

    /// <summary>On: a Claude session in a Launcher tab past <see cref="ContextCyclePercent"/> of its context
    /// is told to wrap up, then /quicksave, /clear and /quickload are typed into it once its turn ends.</summary>
    public bool ContextCycle { get; init; } = true;

    /// <summary>Context fill at which a session is wrapped up and cycled; high enough to use most of the
    /// window, low enough that the wrap-up turn fits before Claude Code compacts on its own.</summary>
    public int ContextCyclePercent { get; init; } = 80;

    /// <summary>Sessions the scheduler will fill up to (working + waiting both occupy a slot).</summary>
    public int MaxConcurrentSessions { get; init; } = 4;

    /// <summary>Off by default: proposals wait for a click. On: the scheduler opens tabs itself
    /// (prompt pre-filled, never submitted) when a slot is free and the budget allows.</summary>
    public bool AutoDispatch { get; init; }

    /// <summary>The repos auto-dispatch may start tickets in; empty means every repo. Manual starts and
    /// the queue are unaffected.</summary>
    public List<string> DispatchRepos { get; init; } = [];

    /// <summary>Repos whose auto-dispatched tabs submit their prompt by themselves once the agent is up,
    /// so work runs unattended under the pacer's caps. Empty by default; manual starts never submit.</summary>
    public List<string> AutoSubmitRepos { get; init; } = [];

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

    /// <summary>Local-time windows when auto-dispatch pauses, like "22:00-07:00". Manual starts still work.</summary>
    public List<string> QuietHours { get; init; } = [];

    /// <summary>Ticket → the tickets it waits for. A summary saying "blocked by X-T1" counts too.</summary>
    public Dictionary<string, List<string>> BlockedBy { get; init; } = [];

    /// <summary>Sessions one repo may run at once (the repo itself plus worktrees), unless <see cref="RepoMaxSessions"/> says otherwise.</summary>
    public int MaxSessionsPerRepo { get; init; } = 2;

    public Dictionary<string, int> RepoMaxSessions { get; init; } = [];

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
public sealed record Assignment(string TicketId, string Repo, string Directory, string Provider, DateTime StartedUtc, bool Worktree);

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

    public T Load<T>(string name) where T : new()
    {
        var path = PathOf(name);
        if (!File.Exists(path)) return new T();
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T(); }
        catch (JsonException) { return new T(); }
        catch (IOException) { return new T(); }
    }

    public void Save<T>(string name, T value)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var path = PathOf(name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }

    public OrchestratorSettings LoadSettings() => Load<OrchestratorSettings>("settings");
    public void SaveSettings(OrchestratorSettings s) => Save("settings", s);
    public List<Assignment> LoadAssignments() => Load<List<Assignment>>("assignments");
    public void SaveAssignments(List<Assignment> a) => Save("assignments", a);
    public bool HasLimits => File.Exists(PathOf("limits"));
    public List<Budget.LimitObservation> LoadLimits() => Load<List<Budget.LimitObservation>>("limits");
    public void SaveLimits(List<Budget.LimitObservation> o) => Save("limits", o);
    public List<Budget.DayUsage> LoadUsageHistory() => Load<List<Budget.DayUsage>>("usage-history");
    public void SaveUsageHistory(List<Budget.DayUsage> d) => Save("usage-history", d);
    public List<Budget.SessionCost> LoadTicketCosts() => Load<List<Budget.SessionCost>>("ticket-costs");
    public void SaveTicketCosts(List<Budget.SessionCost> c) => Save("ticket-costs", c);

    private string PathOf(string name) => Path.Combine(Directory, name + ".json");
}
