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

    /// <summary>The window this session's last turn stopped for (the context gate's <c>limit_stop</c>, at its stop), and when that window resets; null when it isn't waiting on one.</summary>
    public LimitStop? LimitStop { get; init; }
}

/// <summary>A turn that ended held by a window: which one (<c>5-hour</c> or <c>weekly</c>), when it resets, and when it stopped.</summary>
public sealed record LimitStop(string Window, DateTime UntilUtc, DateTime StoppedUtc);

/// <summary>
/// Reads what the context gate records about each session (its context fill, turn state, CLI process, /goal) and the
/// marks it keeps (subagents, /allocate, /crossrepo, /pacing off, keep-going). It never types into a session.
/// </summary>
public sealed class ContextCycler(string? directory = null)
{
    public string Directory { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "session-context");

    public IEnumerable<SessionContext> Load()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        var rows = SessionStates.Rows();
        // A session that hasn't drawn its statusline yet has only the hook's state file.
        return System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(f => Path.GetFileName(f) is var n && n.EndsWith(".state.json", StringComparison.OrdinalIgnoreCase) ? n[..^".state.json".Length] : n[..^".json".Length])
            .Distinct(StringComparer.Ordinal)
            .Select(id => Read(id, rows))
            .OfType<SessionContext>()
            .ToList();
    }

    /// <summary>The session a CLI is in now: its newest. A CLI that ran /clear keeps the state files of the sessions before,
    /// all with its process id, and the first of those by file name is any of them.</summary>
    public string? LatestSessionOf(int pid) => Load().Where(s => s.Pid == pid).MaxBy(s => s.TurnUtc)?.SessionId;

    /// <summary>Each CLI's login, by process id: the config folder its newest session runs under (<see cref="OnOwnLogin"/>),
    /// null on the global login (M.ORCH-T674).</summary>
    public Func<int, string?> LoginOfCli()
    {
        var own = OnOwnLogin();
        var newest = Load().Where(s => s.Pid is not null).GroupBy(s => s.Pid!.Value)
            .ToDictionary(g => g.Key, g => g.MaxBy(s => s.TurnUtc)!.SessionId);
        return pid => newest.TryGetValue(pid, out var session) && own.TryGetValue(session, out var dir)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)) : null;
    }

    internal SessionContext? Read(string sessionId, IReadOnlyDictionary<string, (string Value, DateTime WrittenUtc)>? rows = null)
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
                // The statusline rewrites this every redraw; a read that catches it mid-write mustn't drop the session's
                // state file (its pid and busy state) with it, so it fails on its own and leaves 0% and no cwd.
                try
                {
                    using var c = JsonDocument.Parse(ReadShared(context));
                    percent = c.RootElement.TryGetProperty("used_percentage", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;
                    cwd = c.RootElement.TryGetProperty("cwd", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() : null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
                {
                    percent = 0;
                    cwd = null;
                }
            }
            bool idle = false;
            int? pid = null;
            string? goal = null;
            LimitStop? limitStop = null;
            var turn = DateTime.MinValue;
            // The session_states row or the exported file, whichever was written later, as the hooks read it; the file read
            // shared (a hook may be writing it), and a read that fails falls back on the row rather than dropping the session,
            // which kept the wakers from seeing that this CLI's current session is busy (M.ORCH-T539). With neither readable
            // the session stays, busy.
            JsonDocument? t = null;
            var hasRow = (rows ?? SessionStates.Rows()).TryGetValue(sessionId, out var row);
            try
            {
                if (File.Exists(state) && (!hasRow || File.GetLastWriteTimeUtc(state) >= row.WrittenUtc)) t = JsonDocument.Parse(ReadShared(state));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            if (t is null && hasRow)
                try { t = JsonDocument.Parse(row.Value); }
                catch (JsonException) { }
            using var stateDoc = t;
            if (t is not null)
            {
                idle = t.RootElement.TryGetProperty("state", out var st) && st.GetString() == "idle";
                if (t.RootElement.TryGetProperty("at", out var at) &&
                    DateTime.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when))
                    turn = when;
                if (t.RootElement.TryGetProperty("pid", out var pp) && pp.ValueKind == JsonValueKind.Number) pid = pp.GetInt32();
                if (t.RootElement.TryGetProperty("goal", out var g) && g.ValueKind == JsonValueKind.String) goal = g.GetString();
                if (cwd is null && t.RootElement.TryGetProperty("cwd", out var tc) && tc.ValueKind == JsonValueKind.String) cwd = tc.GetString();
                if (t.RootElement.TryGetProperty("limit_stop", out var ls) && ls.ValueKind == JsonValueKind.Object &&
                    ls.TryGetProperty("until", out var u) && u.ValueKind == JsonValueKind.Number)
                {
                    var stopped = ls.TryGetProperty("at", out var sa) && sa.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(sa.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var s) ? s : turn;
                    limitStop = new LimitStop(ls.TryGetProperty("window", out var lw) && lw.ValueKind == JsonValueKind.String ? lw.GetString()! : "5-hour",
                        DateTime.UnixEpoch.AddSeconds(u.GetDouble()), stopped);
                }
            }
            return new SessionContext(sessionId, cwd, percent, idle, turn) { Pid = pid, Goal = goal, LimitStop = limitStop };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Reads a file another process may hold open for writing.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Where the context gate marks each running subagent (SubagentStart writes its CLI's process id, SubagentStop removes it).</summary>
    public string SubagentDirectory => Path.Combine(Directory, "subagents");

    /// <summary>A subagent mark older than this is a stop that never came (the CLI was killed mid-run).</summary>
    public static readonly TimeSpan SubagentMarkLife = TimeSpan.FromHours(6);

    /// <summary>A subagent whose own transcript hasn't been written for this long has stopped, whether or not its
    /// SubagentStop came (M.ORCH-T114). Longer than the quietest a live one gets: a tool call held by the pace gate
    /// (up to 5 minutes) and then running to the Bash tool's 10-minute limit writes nothing in between.</summary>
    public static readonly TimeSpan SubagentIdleLife = TimeSpan.FromMinutes(20);

    /// <summary>Where Claude Code keeps transcripts, to tell when a subagent's own has gone quiet.</summary>
    public string ProjectsDir { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

    /// <summary>Subagents running now across every CLI: marks whose CLI is still running and that aren't stale.</summary>
    public int Subagents() => Subagents(out _);

    /// <param name="seenThrough">The last-write time of the newest mark this count saw (counted or not); null when it saw
    /// none. The gates add only marks newer than it to the count, so none the count already saw is counted twice
    /// (M.ORCH-T320: they compared against rl-status.json's timestamp, cut to whole seconds and up to a minute old).</param>
    public int Subagents(out DateTime? seenThrough)
    {
        seenThrough = null;
        if (!System.IO.Directory.Exists(SubagentDirectory)) return 0;
        var now = DateTime.UtcNow;
        var count = 0;
        foreach (var f in System.IO.Directory.EnumerateFiles(SubagentDirectory))
        {
            // A mark deleted since it was listed reads as 1601: it's gone, so it's nothing to see.
            if (File.GetLastWriteTimeUtc(f) is var at && at > DateTime.UnixEpoch && (seenThrough is null || at > seenThrough))
                seenThrough = at;
            if (SubagentAlive(f, now, idle: true)) count++;
        }
        return count;
    }

    /// <summary>The agent ids (mark file names) of subagents running now across every CLI.</summary>
    public IReadOnlyList<string> AliveSubagentIds(DateTime nowUtc) => !System.IO.Directory.Exists(SubagentDirectory) ? []
        : [.. System.IO.Directory.EnumerateFiles(SubagentDirectory).Where(f => SubagentAlive(f, nowUtc, idle: true)).Select(f => Path.GetFileName(f))];

    /// <summary>The subagents running now, each with the CLI that runs it (its mark's process id; null when none was recorded).</summary>
    public IReadOnlyList<(string Agent, int? Cli)> AliveSubagents(DateTime nowUtc) => [.. AliveSubagentIds(nowUtc).Select(id =>
        (id, int.TryParse(ReadOrEmpty(Path.Combine(SubagentDirectory, id)).Trim(), out var pid) && pid > 0 ? pid : (int?)null))];

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

    /// <summary>Drops subagent marks whose stop never came (their CLI closed, or past <see cref="SubagentMarkLife"/>). Alone, for
    /// a Claude login's own folder (M.ORCH-T559), which keeps only its subagents' marks.</summary>
    public void PruneSubagentMarks()
    {
        if (System.IO.Directory.Exists(SubagentDirectory))
            foreach (var f in System.IO.Directory.EnumerateFiles(SubagentDirectory).Where(f => !SubagentAlive(f, DateTime.UtcNow, idle: false)))
                try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Drops /pacing off marks whose CLI has closed, so a reused process id never inherits one, and
    /// subagent marks whose stop never came.</summary>
    public void PruneMarks()
    {
        PruneSubagentMarks();
        // The /allocate and /crossrepo marks a hook kept as files while the database was held (or from before it) are read in
        // first (M.ORCH-T528), then a closed CLI's rows go with its files.
        CliMarks.ReadIn(Directory);
        CliMarks.Prune(IsClaude);
        SessionStates.ReadIn(Directory);
        if (!System.IO.Directory.Exists(Directory)) return;
        foreach (var prefix in new[] { "unthrottled-", "crossrepo-", "autopilot-", "noautopilot-" })
            foreach (var f in System.IO.Directory.EnumerateFiles(Directory, prefix + "*"))
            {
                // A read-in mark's .bak and a removal's .cleared go when their CLI does.
                var rest = Path.GetFileName(f)[prefix.Length..];
                foreach (var suffix in new[] { ".bak", CliMarks.ClearedSuffix })
                    if (rest.EndsWith(suffix, StringComparison.Ordinal)) rest = rest[..^suffix.Length];
                var alive = int.TryParse(rest, out var pid) && IsClaude(pid);
                if (!alive) try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
    }

    /// <summary>The CLIs /allocate is on in (by process id), and the repos each works through its own subagents ("*": all):
    /// their <c>cli_marks</c> rows, or <c>autopilot-&lt;pid&gt;</c> files not read in yet (<see cref="CliMarks"/>).</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> AutopilotClis() => CliMarks.Read(Directory, CliMarks.Autopilot)
        .Where(x => IsClaude(x.Key))
        .ToDictionary(x => x.Key, x => (IReadOnlyList<string>)[.. x.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>When /allocate turned on in each CLI that has it on (M.ORCH-T558: it warms up from then), by process id.</summary>
    public IReadOnlyDictionary<int, DateTime> AutopilotSince() => CliMarks.Since(Directory, CliMarks.Autopilot);

    private static string ReadOrEmpty(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>Whether the CLI <paramref name="pid"/> has a /pacing off mark, the same check the pace gate makes before skipping its holds.</summary>
    public bool IsUnthrottled(int pid) => File.Exists(Path.Combine(Directory, "unthrottled-" + pid));

    /// <summary>Where the context gate logs each time it kept a CLI going (or let one stop that insisted).</summary>
    public string KeepGoingLog => Path.Combine(Directory, "keep-going.jsonl");

    private long? keepGoingRead;

    /// <summary>What the context gate's keep-going did since the last call, one line each; the first call only
    /// remembers where the log ends, so a restart doesn't repeat old entries.</summary>
    public IReadOnlyList<string> KeepGoingSince()
    {
        try
        {
            // Gone (pruned after a quiet week): whatever log the hook starts next is all new, not read from the old one's end (M.ORCH-T380).
            if (!File.Exists(KeepGoingLog)) { keepGoingRead = 0; return []; }
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

    /// <summary>The standing <c>/allocate</c> order the context gate writes: the file is there while it's on.</summary>
    public const string AutopilotFile = "autopilot.order";

    /// <summary>Null while <c>/allocate</c> is off; otherwise the repos whose CLIs it works (<c>-repo=a,b</c>), empty for every open one.</summary>
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

    /// <summary>
    /// The sessions whose Claude CLI is running right now, each with whether its turn is running (true) or it's waiting
    /// for you (false): the newest session of every live CLI process, from the context gate's <c>.state.json</c> files
    /// written in the last day. A CLI that /clear-ed has several sessions; only its latest is the one on screen.
    /// </summary>
    public IReadOnlyDictionary<string, bool> LiveSessions(DateTime nowUtc)
    {
        if (!System.IO.Directory.Exists(Directory)) return new Dictionary<string, bool>();
        var newest = new Dictionary<int, (string Session, DateTime At, bool Busy)>();
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.state.json"))
        {
            try
            {
                var written = File.GetLastWriteTimeUtc(file);
                if (nowUtc - written > TimeSpan.FromDays(1)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (!root.TryGetProperty("pid", out var p) || p.ValueKind != JsonValueKind.Number || !p.TryGetInt32(out var pid)) continue;
                var busy = root.TryGetProperty("state", out var st) && st.GetString() == "busy";
                var session = Path.GetFileName(file)[..^".state.json".Length];
                if (!newest.TryGetValue(pid, out var seen) || written > seen.At) newest[pid] = (session, written, busy);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        }
        return newest.Where(kv => IsClaude(kv.Key)).ToDictionary(kv => kv.Value.Session, kv => kv.Value.Busy, StringComparer.Ordinal);
    }

    /// <summary>Whether <paramref name="sessionId"/>'s Claude CLI is still running, by the process id the context gate
    /// recorded for it (any session of a CLI that /clear-ed counts while the CLI does); null when none was recorded.</summary>
    public bool? SessionRunning(string sessionId)
    {
        try
        {
            var state = Path.Combine(Directory, sessionId + ".state.json");
            if (!File.Exists(state)) return null;
            using var doc = JsonDocument.Parse(ReadShared(state));
            return doc.RootElement.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pid)
                ? IsClaude(pid)
                : null;
        }
        // Caught mid-write: not known this time, and a folder whose session isn't known is only judged by how long it's been quiet.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>What the context gate records for a session billed to the subscription (its <c>auth</c>); anything else is not.</summary>
    public const string SubscriptionAuth = "subscription";

    private readonly Dictionary<string, (DateTime Stamp, string? Auth, string? ConfigDir)> auths = new(StringComparer.Ordinal);

    /// <summary>
    /// The sessions the context gate found billed elsewhere than the Claude subscription (an API key, a gateway, a cloud
    /// provider: see <c>session_auth</c> in the hooks), with how. Hooks see each CLI's own environment and the daemon
    /// doesn't, so this is how their tokens are left out of the subscription's windows. A session with no record counts.
    /// </summary>
    public IReadOnlyDictionary<string, string> OffSubscription()
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (session, known) in ReadAuths())
            if (known.Auth is { } auth && auth != SubscriptionAuth) found[session] = auth;
        return found;
    }

    /// <summary>
    /// The sessions running on a Claude login of their own (M.ORCH-T559: <c>CLAUDE_CONFIG_DIR</c> naming an
    /// <see cref="AccountDirs"/> folder, as the context gate records it), with that folder. Their tokens are their account's,
    /// so they're left out of the global login's budget and pace and counted in their own account's.
    /// </summary>
    public IReadOnlyDictionary<string, string> OnOwnLogin()
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (session, known) in ReadAuths())
            if (known.ConfigDir is { Length: > 0 } dir) found[session] = dir;
        return found;
    }

    private Dictionary<string, (DateTime Stamp, string? Auth, string? ConfigDir)> ReadAuths()
    {
        if (!System.IO.Directory.Exists(Directory)) return auths;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.state.json"))
        {
            var session = Path.GetFileName(file)[..^".state.json".Length];
            seen.Add(session);
            try
            {
                var stamp = File.GetLastWriteTimeUtc(file);
                if (!auths.TryGetValue(session, out var known) || known.Stamp != stamp)
                {
                    using var doc = JsonDocument.Parse(ReadShared(file));
                    var root = doc.RootElement;
                    known = (stamp, root.TryGetProperty("auth", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null,
                        root.TryGetProperty("config_dir", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null);
                    auths[session] = known;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                // Kept as last read: a state file being replaced isn't a session that changed how it's billed.
            }
        }
        foreach (var gone in auths.Keys.Where(k => !seen.Contains(k)).ToList()) auths.Remove(gone);
        return auths;
    }

    /// <summary>Whether <paramref name="pid"/> is a running Claude CLI.</summary>
    public static bool IsClaude(int pid)
    {
        if (pid <= 0) return false;
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return Providers.IsClaudeProcess(p.ProcessName); }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>Drops files for sessions not heard from in a week, so the folder doesn't grow forever.</summary>
    public void Prune(DateTime nowUtc)
    {
        SessionStates.Prune(nowUtc - TimeSpan.FromDays(7)); // their rows go with their files (M.ORCH-T528)
        if (!System.IO.Directory.Exists(Directory)) return;
        // A CLI's own /allocate, /allocate off, /pacing off and /crossrepo marks are written once and live as long as it does,
        // however long that is; PruneMarks drops them when the CLI closes. A week-old noautopilot mark dropped here let the default
        // switch /allocate back on in a CLI where it was typed off (M.ORCH-T484). The billing mode holds until it's switched,
        // however long (M.ORCH-T436), and when a key was set aside is needed as long as a CLI started on it before then runs,
        // however long (M.ORCH-T457).
        foreach (var f in System.IO.Directory.EnumerateFiles(Directory).Where(f => Path.GetFileName(f) is var n && n != AutopilotFile &&
                     n != Billing.BillingModeFile.FileName && n != Setup.AgentGate.ApiKeySetAsideName &&
                     !n.StartsWith("unthrottled-", StringComparison.Ordinal) && !n.StartsWith("crossrepo-", StringComparison.Ordinal) &&
                     !n.StartsWith("autopilot-", StringComparison.Ordinal) && !n.StartsWith("noautopilot-", StringComparison.Ordinal)))
            try { if (nowUtc - File.GetLastWriteTimeUtc(f) > TimeSpan.FromDays(7)) File.Delete(f); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
