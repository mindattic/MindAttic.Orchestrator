using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A session's context fill (saved by the statusline) and turn state (saved by the context gate hook).</summary>
public sealed record SessionContext(string SessionId, string? Cwd, double Percent, bool Idle, DateTime TurnUtc)
{
    /// <summary>The Claude CLI process the session runs in (recorded by the context gate), for typing into a plain terminal.</summary>
    public int? Pid { get; init; }

    /// <summary>The condition of a <c>/goal</c> running in the session (the context gate records it, <c>/goal clear</c> ends it); null when none.</summary>
    public string? Goal { get; init; }
}

/// <summary>
/// Keeps agents from running out of context: once a Claude session in a Launcher tab is past
/// <see cref="OrchestratorSettings.ContextCyclePercent"/> and its turn has ended (so its subagents are
/// done; the context gate told it to wrap up), types <c>/quicksave</c>, waits for that turn to end,
/// then <c>/clear</c> and <c>/quickload</c>, so it carries on from the handoff with a fresh context.
/// A session in a plain terminal is typed into through its console (<see cref="ConsoleTyper"/>); one with
/// no console (the Claude desktop app) can't be, so you're told instead.
/// </summary>
public sealed class ContextCycler(string? directory = null)
{
    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "session-context");

    /// <summary>How long a turn has to have been over before cycling, so a quick follow-up isn't typed over.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(10);

    /// <summary>The longest /quicksave gets to write its handoff before the cycle gives up (without clearing).</summary>
    public static readonly TimeSpan SaveTimeout = TimeSpan.FromMinutes(10);

    /// <summary>Types a line into a console by process id; an empty line only checks it can. Replaceable for tests.</summary>
    internal Func<int, string, bool> Typer { get; init; } = (pid, text) => text.Length == 0 ? ConsoleTyper.CanType(pid) : ConsoleTyper.Submit(pid, text);

    /// <summary>Sessions to leave alone: autopilot's headless agents, which have no prompt to type /quicksave into.</summary>
    public Func<int, bool> IsAgent { get; set; } = _ => false;

    private readonly Dictionary<string, Task<string>> cycling = new(StringComparer.Ordinal);
    private readonly HashSet<string> told = new(StringComparer.Ordinal);

    /// <summary>Starts a cycle for every session that's due; returns what it did or couldn't do, and finished cycles' outcomes.</summary>
    public IReadOnlyList<string> Tick(IReadOnlyList<HostSession> hosts, OrchestratorSettings settings, DateTime nowUtc)
    {
        var said = new List<string>();
        foreach (var (id, task) in cycling.Where(c => c.Value.IsCompleted).ToList())
        {
            cycling.Remove(id);
            said.Add(task.IsCompletedSuccessfully ? task.Result : $"context cycle for session {id} failed: {task.Exception?.InnerException?.Message}");
        }
        if (!settings.ContextCycle) return said;

        foreach (var s in Load())
        {
            if (cycling.ContainsKey(s.SessionId) || !Due(s, settings.ContextCyclePercent, nowUtc) || (s.Pid is { } agent && IsAgent(agent))) continue;
            // /clear would silently end a /goal; Claude's own compaction carries it on instead.
            if (s.Goal is { } goal)
            {
                if (told.Add(s.SessionId)) said.Add($"session in {s.Cwd} is {s.Percent:0}% full of context but running /goal \"{goal}\", so it isn't cycled; Claude's own compaction carries it on");
                continue;
            }
            Func<string, Task<bool>>? send = null;
            var host = HostFor(s, hosts, out var why);
            if (host is not null) send = text => SessionControl.SubmitAsync(host.PipeName, text);
            else if (s.Pid is { } pid && Typer(pid, "")) send = text => Task.FromResult(Typer(pid, text));
            else why = s.Pid is null ? why : "its console can't be typed into (a desktop-app session)";
            if (send is null)
            {
                if (told.Add(s.SessionId)) said.Add($"session in {s.Cwd} is {s.Percent:0}% full of context but {why}: /quicksave, /clear and /quickload it yourself");
                continue;
            }
            said.Add($"cycling the context of {s.Cwd} ({s.Percent:0}% full{(host is null ? ", in its own terminal" : "")}): /quicksave, /clear, /quickload");
            cycling[s.SessionId] = Task.Run(() => CycleAsync(s, send));
        }
        return said;
    }

    /// <summary>Past the cycle point with its turn over long enough that nothing is still running.</summary>
    public static bool Due(SessionContext s, int percent, DateTime nowUtc) =>
        s.Percent >= percent && s.Idle && nowUtc - s.TurnUtc >= Settle;

    /// <summary>The one Claude Launcher tab rooted where the session runs; null (and why) when there's none or it's ambiguous.</summary>
    internal static HostSession? HostFor(SessionContext s, IEnumerable<HostSession> hosts, out string why)
    {
        why = "it isn't in a Launcher tab the orchestrator can type into";
        if (s.Cwd is null) return null;
        var target = Path.GetFullPath(s.Cwd).TrimEnd('\\', '/');
        var matches = hosts.Where(h => h.Provider == Providers.Claude && h.WorkingDirectory is { } d &&
            string.Equals(Path.GetFullPath(d).TrimEnd('\\', '/'), target, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 1) why = "more than one Claude tab is open there, so it can't tell which is which";
        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<string> CycleAsync(SessionContext s, Func<string, Task<bool>> send)
    {
        var sent = DateTime.UtcNow;
        if (!await send("/quicksave"))
            return $"couldn't reach the tab for {s.Cwd} to cycle its context";
        // /quicksave is a turn of its own; its Stop marks the handoff written.
        var deadline = sent + SaveTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            if (Read(s.SessionId) is { Idle: true } after && after.TurnUtc > sent) break;
        }
        if (DateTime.UtcNow >= deadline)
            return $"{s.Cwd}: /quicksave didn't finish in {SaveTimeout.TotalMinutes:0} minutes, so its context was left as is";
        await send("/clear");
        await Task.Delay(TimeSpan.FromSeconds(5));
        await send("/quickload");
        Forget(s.SessionId); // /clear starts a new session id; this one is done
        return $"{s.Cwd}: context cycled; it picks up from its handoff";
    }

    public IEnumerable<SessionContext> Load()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        // A session that hasn't drawn its statusline yet has only the hook's state file.
        return System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(f => Path.GetFileName(f) is var n && n.EndsWith(".state.json", StringComparison.OrdinalIgnoreCase) ? n[..^".state.json".Length] : n[..^".json".Length])
            .Distinct(StringComparer.Ordinal)
            .Select(Read)
            .OfType<SessionContext>()
            .ToList();
    }

    internal SessionContext? Read(string sessionId)
    {
        try
        {
            var context = Path.Combine(Directory, sessionId + ".json");
            var state = Path.Combine(Directory, sessionId + ".state.json");
            if (!File.Exists(context) && !File.Exists(state)) return null;
            double percent = 0;
            string? cwd = null;
            if (File.Exists(context))
            {
                using var c = JsonDocument.Parse(File.ReadAllText(context));
                percent = c.RootElement.TryGetProperty("used_percentage", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;
                cwd = c.RootElement.TryGetProperty("cwd", out var w) ? w.GetString() : null;
            }
            bool idle = false;
            int? pid = null;
            string? goal = null;
            var turn = DateTime.MinValue;
            if (File.Exists(state))
            {
                using var t = JsonDocument.Parse(File.ReadAllText(state));
                idle = t.RootElement.TryGetProperty("state", out var st) && st.GetString() == "idle";
                if (t.RootElement.TryGetProperty("at", out var at) &&
                    DateTime.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when))
                    turn = when;
                if (t.RootElement.TryGetProperty("pid", out var pp) && pp.ValueKind == JsonValueKind.Number) pid = pp.GetInt32();
                if (t.RootElement.TryGetProperty("goal", out var g) && g.ValueKind == JsonValueKind.String) goal = g.GetString();
                if (cwd is null && t.RootElement.TryGetProperty("cwd", out var tc) && tc.ValueKind == JsonValueKind.String) cwd = tc.GetString();
            }
            return new SessionContext(sessionId, cwd, percent, idle, turn) { Pid = pid, Goal = goal };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private void Forget(string sessionId)
    {
        foreach (var suffix in new[] { ".json", ".state.json" })
            try { File.Delete(Path.Combine(Directory, sessionId + suffix)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Where the context gate marks each running subagent (SubagentStart writes its CLI's process id, SubagentStop removes it).</summary>
    public string SubagentDirectory => Path.Combine(Directory, "subagents");

    /// <summary>A subagent mark older than this is a stop that never came (the CLI was killed mid-run).</summary>
    public static readonly TimeSpan SubagentMarkLife = TimeSpan.FromHours(6);

    /// <summary>Subagents running now across every CLI: marks whose CLI is still running and that aren't stale.</summary>
    public int Subagents() => !System.IO.Directory.Exists(SubagentDirectory) ? 0
        : System.IO.Directory.EnumerateFiles(SubagentDirectory).Count(f => SubagentAlive(f, DateTime.UtcNow));

    /// <summary>The agent ids (mark file names) of subagents running now across every CLI.</summary>
    public IReadOnlyList<string> AliveSubagentIds(DateTime nowUtc) => !System.IO.Directory.Exists(SubagentDirectory) ? []
        : [.. System.IO.Directory.EnumerateFiles(SubagentDirectory).Where(f => SubagentAlive(f, nowUtc)).Select(f => Path.GetFileName(f))];

    private static bool SubagentAlive(string mark, DateTime nowUtc)
    {
        try
        {
            if (nowUtc - File.GetLastWriteTimeUtc(mark) > SubagentMarkLife) return false;
            // No process id (the gate couldn't find its CLI): trust the age alone.
            return !int.TryParse(File.ReadAllText(mark).Trim(), out var pid) || IsClaude(pid);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Drops /unthrottle marks whose CLI has closed, so a reused process id never inherits one, and
    /// subagent marks whose stop never came.</summary>
    public void PruneMarks()
    {
        if (System.IO.Directory.Exists(SubagentDirectory))
            foreach (var f in System.IO.Directory.EnumerateFiles(SubagentDirectory).Where(f => !SubagentAlive(f, DateTime.UtcNow)))
                try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (!System.IO.Directory.Exists(Directory)) return;
        foreach (var prefix in new[] { "unthrottled-", "crossrepo-" })
            foreach (var f in System.IO.Directory.EnumerateFiles(Directory, prefix + "*"))
            {
                var alive = int.TryParse(Path.GetFileName(f)[prefix.Length..], out var pid) && IsClaude(pid);
                if (!alive) try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
    }

    /// <summary>The CLIs running unthrottled right now (by process id).</summary>
    public IReadOnlyList<int> Unthrottled() => !System.IO.Directory.Exists(Directory) ? []
        : [.. System.IO.Directory.EnumerateFiles(Directory, "unthrottled-*")
            .Select(f => int.TryParse(Path.GetFileName(f)["unthrottled-".Length..], out var pid) ? pid : 0).Where(IsClaude)];

    /// <summary>The standing <c>/autopilot</c> order the context gate writes: the file is there while it's on.</summary>
    public const string AutopilotFile = "autopilot.order";

    /// <summary>Null while <c>/autopilot</c> is off; otherwise the repos whose CLIs it works (<c>-repo=a,b</c>), empty for every open one.</summary>
    public IReadOnlyList<string>? AutopilotOrder()
    {
        try
        {
            var path = Path.Combine(Directory, AutopilotFile);
            return File.Exists(path)
                ? [.. File.ReadAllText(path).Split([',', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Whether <paramref name="pid"/> is a running Claude CLI.</summary>
    public static bool IsClaude(int pid)
    {
        if (pid <= 0) return false;
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return ConsoleTyper.IsClaude(p.ProcessName); }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>Drops files for sessions not heard from in a week, so the folder doesn't grow forever.</summary>
    public void Prune(DateTime nowUtc)
    {
        if (!System.IO.Directory.Exists(Directory)) return;
        foreach (var f in System.IO.Directory.EnumerateFiles(Directory).Where(f => Path.GetFileName(f) != AutopilotFile))
            try { if (nowUtc - File.GetLastWriteTimeUtc(f) > TimeSpan.FromDays(7)) File.Delete(f); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
