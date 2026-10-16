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

    /// <summary>The session's transcript (recorded by the context gate), to tell when a <c>/goal</c> has finished on its own.</summary>
    public string? Transcript { get; init; }
}

/// <summary>
/// Reads what the context gate records about each session (its context fill, turn state, CLI process, /goal) and the
/// marks it keeps (subagents, /autopilot, /crossrepo, /unthrottle, keep-going). It never types into a session.
/// </summary>
public sealed class ContextCycler(string? directory = null)
{
    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "session-context");

    /// <summary>How long a turn has to have been over before cycling, so a quick follow-up isn't typed over.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(10);

    /// <summary>Sessions to leave alone: autopilot's headless agents.</summary>
    public Func<int, bool> IsAgent { get; set; } = _ => false;

    // Nothing here types into a session any more: typing /quicksave, /clear and /quickload into a live chat at 80%
    // cleared a session the user was working in (2026-10-16). Context is left to Claude's own compaction.

    /// <summary>
    /// Whether a <c>/goal</c> finished on its own: once its condition holds, the session's last entry is the stop summary
    /// naming it (an unmet goal keeps the turn going, so more follows). The context gate can't see this at the Stop
    /// itself, because the summary is written after its hook runs (MAO-T109).
    /// </summary>
    internal static bool GoalFinished(string transcript, string goal)
    {
        try
        {
            using var f = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            f.Seek(Math.Max(0, f.Length - 64 * 1024), SeekOrigin.Begin);
            foreach (var line in new StreamReader(f).ReadToEnd().Split('\n').Reverse())
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(line); } catch (JsonException) { continue; }
                using (doc)
                {
                    var r = doc.RootElement;
                    if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("type", out var type) || type.GetString() is not ("assistant" or "user" or "system")) continue;
                    return r.TryGetProperty("subtype", out var sub) && sub.GetString() == "stop_hook_summary" &&
                        r.TryGetProperty("hookInfos", out var hooks) && hooks.ValueKind == JsonValueKind.Array &&
                        hooks.EnumerateArray().Any(h => h.TryGetProperty("promptText", out var p) && p.GetString() == goal);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        return false;
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
            string? goal = null, transcript = null;
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
                if (t.RootElement.TryGetProperty("transcript", out var tp) && tp.ValueKind == JsonValueKind.String) transcript = tp.GetString();
                if (cwd is null && t.RootElement.TryGetProperty("cwd", out var tc) && tc.ValueKind == JsonValueKind.String) cwd = tc.GetString();
            }
            return new SessionContext(sessionId, cwd, percent, idle, turn) { Pid = pid, Goal = goal, Transcript = transcript };
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

    /// <summary>A subagent whose own transcript hasn't been written for this long has stopped, whether or not its
    /// SubagentStop came (MAO-T114). Longer than the quietest a live one gets: a tool call held by the pace gate
    /// (up to 5 minutes) and then running to the Bash tool's 10-minute limit writes nothing in between.</summary>
    public static readonly TimeSpan SubagentIdleLife = TimeSpan.FromMinutes(20);

    /// <summary>Where Claude Code keeps transcripts, to tell when a subagent's own has gone quiet.</summary>
    public string ProjectsDir { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>Subagents running now across every CLI: marks whose CLI is still running and that aren't stale.</summary>
    public int Subagents() => !System.IO.Directory.Exists(SubagentDirectory) ? 0
        : System.IO.Directory.EnumerateFiles(SubagentDirectory).Count(f => SubagentAlive(f, DateTime.UtcNow, idle: true));

    /// <summary>The agent ids (mark file names) of subagents running now across every CLI.</summary>
    public IReadOnlyList<string> AliveSubagentIds(DateTime nowUtc) => !System.IO.Directory.Exists(SubagentDirectory) ? []
        : [.. System.IO.Directory.EnumerateFiles(SubagentDirectory).Where(f => SubagentAlive(f, nowUtc, idle: true)).Select(f => Path.GetFileName(f))];

    /// <param name="idle">Also count one whose transcript has gone quiet (<see cref="SubagentIdleLife"/>) as stopped. The mark
    /// itself is only deleted by the old rules (its CLI closed, or <see cref="SubagentMarkLife"/>), so a quiet one that
    /// turns out to be alive counts again as soon as it writes.</param>
    private bool SubagentAlive(string mark, DateTime nowUtc, bool idle)
    {
        try
        {
            if (nowUtc - File.GetLastWriteTimeUtc(mark) > SubagentMarkLife) return false;
            if (idle && Activity.SubagentTranscripts.Find(ProjectsDir, Path.GetFileName(mark)) is { } transcript &&
                nowUtc - File.GetLastWriteTimeUtc(transcript) > SubagentIdleLife)
                return false;
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
            foreach (var f in System.IO.Directory.EnumerateFiles(SubagentDirectory).Where(f => !SubagentAlive(f, DateTime.UtcNow, idle: false)))
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

    /// <summary>Where the context gate logs each time it kept a CLI going (or let one stop that insisted).</summary>
    public string KeepGoingLog => Path.Combine(Directory, "keep-going.jsonl");

    private long? keepGoingRead;

    /// <summary>What the context gate's keep-going did since the last call, one line each; the first call only
    /// remembers where the log ends, so a restart doesn't repeat old entries.</summary>
    public IReadOnlyList<string> KeepGoingSince()
    {
        try
        {
            if (!File.Exists(KeepGoingLog)) { keepGoingRead ??= 0; return []; }
            using var f = new FileStream(KeepGoingLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (keepGoingRead is not { } from || from > f.Length) { keepGoingRead = f.Length; return []; }
            f.Seek(from, SeekOrigin.Begin);
            var text = new StreamReader(f).ReadToEnd();
            var complete = text.LastIndexOf('\n') + 1; // a line still being written waits for the next call
            keepGoingRead = from + System.Text.Encoding.UTF8.GetByteCount(text[..complete]);
            return [.. text[..complete].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Describe).OfType<string>()];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }

        static string? Describe(string line)
        {
            try
            {
                using var j = JsonDocument.Parse(line);
                var r = j.RootElement;
                string S(string name) => r.TryGetProperty(name, out var v) ? v.ToString() : "?";
                var cli = r.TryGetProperty("provider", out var p) && p.GetString() is { } who && who != Providers.Claude ? $"{who} CLI" : "CLI";
                return S("action") == "let stop"
                    ? $"keep-going: let the {cli} in {S("repo")} stop ({S("why")}, {S("open")} open); it stopped again without doing anything, so it likely needs you"
                    : $"keep-going: told the {cli} in {S("repo")} to carry on ({S("why")}, {S("open")} tickets still open)";
            }
            catch (JsonException) { return null; }
        }
    }

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
