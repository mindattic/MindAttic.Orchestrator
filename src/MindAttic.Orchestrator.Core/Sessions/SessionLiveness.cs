using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>Whether a session's Claude CLI runs: true, false, or null when it can't be told; and why, in words.</summary>
public sealed record SessionVerdict(bool? Running, string Why);

/// <summary>
/// What decides whether a session's %TEMP%\claude folder may go (M.ORCH-T1467): never its age. A folder goes only when its
/// session has <i>definitely</i> ended, nothing still running uses it and nothing that's kept points at it:
/// <list type="bullet">
/// <item><see cref="Running"/>: the CLI process the context gate recorded for the session, read from every login's
/// session-context (the global one and each <c>.claude-accounts</c> login's), however old the record; a Claude CLI started for
/// the session (<c>--resume &lt;id&gt;</c>) counts as it. With no record it's unknown, unless no Claude CLI runs on the machine
/// at all, which ends every session.</item>
/// <item><see cref="UsedBy"/>: any running process whose command line names the folder (an orphaned background build, a
/// Monitor's <c>tail -F</c> left behind when the CLI exited); unknown, so kept, when command lines can't be read.</item>
/// <item><see cref="ReferencedBy"/>: a path into the folder in an unconsumed quicksave handoff (the registry's root and each
/// repo's <c>.agent\agent-session.json</c>) or in a running session's transcript.</item>
/// </list>
/// A file held open in the folder keeps it too (<see cref="SessionTempPruner"/> checks that itself).
/// </summary>
public sealed class SessionLiveness(
    Func<string, SessionVerdict> running,
    Func<string, string?>? referencedBy = null,
    Func<string, string?>? usedBy = null)
{
    public SessionVerdict Running(string sessionId) => running(sessionId);

    /// <summary>What still points into the session's folder (a handoff, a running session's transcript); null when nothing.</summary>
    public string? ReferencedBy(string sessionId) => referencedBy?.Invoke(sessionId);

    /// <summary>The running processes that name <paramref name="folder"/> on their command line, or why that can't be told;
    /// null when none does.</summary>
    public string? UsedBy(string folder) => usedBy?.Invoke(folder);

    /// <summary>From a plain running check (tests, callers that know only that): null stays unknown.</summary>
    public static SessionLiveness From(Func<string, bool?> running) =>
        new(id => running(id) switch
        {
            true => new SessionVerdict(true, "its CLI runs"),
            false => new SessionVerdict(false, "its CLI has exited"),
            null => new SessionVerdict(null, "no CLI is recorded for it"),
        });

    /// <summary>A path into a session's temp folder, as transcripts, handoffs and command lines write it (<c>\</c>, <c>\\</c> or
    /// <c>/</c>): the project hash, then the session id.</summary>
    internal static readonly Regex TempPath = new(
        @"[0-9a-f]{16}(?:\\{1,2}|/)([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The session ids <paramref name="text"/> points into.</summary>
    internal static IEnumerable<string> SessionIdsIn(string text) =>
        TempPath.Matches(text).Select(m => m.Groups[1].Value.ToLowerInvariant());

    /// <summary>What a session's state file says: the CLI's process id and its transcript.</summary>
    internal sealed record State(int? Pid, string? Transcript);

    /// <summary>One running process: id, image name and command line.</summary>
    internal sealed record Proc(int Pid, string Name, string? CommandLine);

    /// <summary>Liveness as the machine has it now. <paramref name="workspaceRoot"/> is where repos' <c>.agent</c> handoffs are.</summary>
    public static SessionLiveness Current(string? workspaceRoot)
    {
        var states = ReadStates(MarksDirectories());
        var procs = Processes();
        var claudePids = new HashSet<int>();
        foreach (var p in System.Diagnostics.Process.GetProcesses())
            using (p)
                try { if (Providers.IsClaudeProcess(p.ProcessName)) claudePids.Add(p.Id); }
                catch (InvalidOperationException) { } // exited while listed
        return Decide(states, claudePids, procs, () => Referenced(states, claudePids, workspaceRoot));
    }

    /// <summary>The rules, over what was read (pure, for tests). <paramref name="procs"/> is null when command lines couldn't be
    /// read; <paramref name="references"/> is read once, on first need (only a session that has ended asks).</summary>
    internal static SessionLiveness Decide(IReadOnlyDictionary<string, List<State>> states, IReadOnlySet<int> claudePids,
        IReadOnlyList<Proc>? procs, Func<IReadOnlyDictionary<string, string>> references)
    {
        IReadOnlyDictionary<string, string>? refs = null;
        return new SessionLiveness(
            id =>
            {
                if (procs?.FirstOrDefault(p => claudePids.Contains(p.Pid) && p.CommandLine?.Contains(id, StringComparison.OrdinalIgnoreCase) == true) is { } resumed)
                    return new SessionVerdict(true, $"a Claude CLI was started for it (pid {resumed.Pid})");
                if (states.TryGetValue(id, out var recorded) && recorded.Where(s => s.Pid is not null).Select(s => s.Pid!.Value).ToList() is { Count: > 0 } pids)
                    return pids.FirstOrDefault(claudePids.Contains) is var live && live != 0
                        ? new SessionVerdict(true, $"its CLI (pid {live}) runs")
                        : new SessionVerdict(false, $"its CLI (pid {string.Join(", ", pids.Distinct())}) has exited");
                return claudePids.Count == 0
                    ? new SessionVerdict(false, "no CLI is recorded for it, and no Claude CLI runs on this machine")
                    : new SessionVerdict(null, $"no CLI is recorded for it and {claudePids.Count} Claude CLI(s) run, so it can't be ruled out");
            },
            id => (refs ??= references()).TryGetValue(id.ToLowerInvariant(), out var where) ? where : null,
            folder =>
            {
                if (procs is null) return "running processes' command lines couldn't be read";
                var tail = TailOf(folder);
                var users = procs.Where(p => p.CommandLine is { } c && c.Replace('\\', '/').Contains(tail, StringComparison.OrdinalIgnoreCase)).ToList();
                return users.Count == 0 ? null
                    : $"{string.Join(", ", users.Take(3).Select(u => $"{u.Name} (pid {u.Pid})"))}{(users.Count > 3 ? $" and {users.Count - 3} more" : "")} still name it on their command line";
            });
    }

    /// <summary><c>/&lt;project hash&gt;/&lt;session id&gt;</c>: how any spelling of the folder's path ends.</summary>
    internal static string TailOf(string folder)
    {
        var full = Path.TrimEndingDirectorySeparator(folder);
        return "/" + Path.GetFileName(Path.GetDirectoryName(full)) + "/" + Path.GetFileName(full);
    }

    /// <summary>Every login's session-context: the global login's and each own login's.</summary>
    internal static IReadOnlyList<string> MarksDirectories()
    {
        var dirs = new List<string> { new ContextCycler().Directory };
        try { dirs.AddRange(new AccountDirs().All().Select(a => a.MarksDirectory)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return [.. dirs.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Every recorded session's state, from each of <paramref name="dirs"/>, whatever its age.</summary>
    internal static Dictionary<string, List<State>> ReadStates(IEnumerable<string> dirs)
    {
        var states = new Dictionary<string, List<State>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs)
        {
            IEnumerable<string> files;
            try { files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.state.json") : []; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var file in files)
            {
                var id = Path.GetFileName(file)[..^".state.json".Length];
                // Caught mid-write it's still a recorded session; with no pid read it can't be judged by pid, so it's unknown.
                var state = new State(null, null);
                try
                {
                    using var doc = JsonDocument.Parse(ReadShared(file));
                    var r = doc.RootElement;
                    state = new State(
                        r.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pid) ? pid : null,
                        r.TryGetProperty("transcript", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
                if (!states.TryGetValue(id, out var list)) states[id] = list = [];
                list.Add(state);
            }
        }
        // A session with no pid read anywhere (caught mid-write, or no pid recorded) can't be judged by pid.
        foreach (var (id, list) in states.ToList())
            if (list.All(s => s.Pid is null)) states.Remove(id);
        return states;
    }

    /// <summary>Every running process with its command line; null when they can't be read (then nothing is known not to use a folder).</summary>
    internal static IReadOnlyList<Proc>? Processes()
    {
        try
        {
            var list = new List<Proc>();
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, Name, CommandLine FROM Win32_Process");
            using var found = searcher.Get();
            foreach (var o in found)
                using (o)
                    if (o["ProcessId"] is uint pid)
                        list.Add(new Proc((int)pid, o["Name"] as string ?? "?", o["CommandLine"] as string));
            return list.Count > 0 ? list : null;
        }
        catch (ManagementException) { return null; }
        catch (COMException) { return null; }
        catch (PlatformNotSupportedException) { return null; }
    }

    /// <summary>Session id → what points into its folder: unconsumed handoffs, then running sessions' transcripts.</summary>
    internal static IReadOnlyDictionary<string, string> Referenced(IReadOnlyDictionary<string, List<State>> states,
        IReadOnlySet<int> claudePids, string? workspaceRoot)
    {
        var refs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void From(string file, string what)
        {
            try
            {
                foreach (var id in SessionIdsIn(ReadShared(file))) refs.TryAdd(id, what);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        foreach (var file in HandoffFiles(workspaceRoot)) From(file, $"the handoff {file}");
        foreach (var (id, list) in states)
            foreach (var s in list)
                if (s.Pid is { } pid && claudePids.Contains(pid) && s.Transcript is { Length: > 0 } transcript)
                    foreach (var found in TranscriptRefs.Scan(transcript)) refs.TryAdd(found, $"running session {id}'s transcript");
        return refs;
    }

    /// <summary>The unconsumed quicksave handoffs: the registry's root (a loaded one moves to <c>consumed</c>) and each repo's
    /// <c>.agent\agent-session.json</c>.</summary>
    internal static IEnumerable<string> HandoffFiles(string? workspaceRoot)
    {
        var registry = Environment.GetEnvironmentVariable("MINDATTIC_HANDOFF_REGISTRY") is { Length: > 0 } r ? r
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "handoffs");
        var files = new List<string>();
        try { if (Directory.Exists(registry)) files.AddRange(Directory.GetFiles(registry, "*.json")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        if (workspaceRoot is { Length: > 0 } && Directory.Exists(workspaceRoot))
            try
            {
                foreach (var repo in Directory.GetDirectories(workspaceRoot).Prepend(workspaceRoot))
                    if (Path.Combine(repo, ".agent", "agent-session.json") is var f && File.Exists(f)) files.Add(f);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return files;
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The temp folders a transcript points into, read only from where the last look stopped: a running session's
    /// transcript is tens of MB and only grows, so each look reads just what's new.</summary>
    internal static class TranscriptRefs
    {
        private static readonly Dictionary<string, (long Read, HashSet<string> Ids)> Seen = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new();

        public static IReadOnlyCollection<string> Scan(string transcript)
        {
            lock (Gate)
            {
                try
                {
                    using var stream = new FileStream(transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (!Seen.TryGetValue(transcript, out var seen) || stream.Length < seen.Read) seen = (0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    // Back up a little so a path cut by the last read's end is still matched whole.
                    var start = Math.Max(0, seen.Read - 256);
                    stream.Seek(start, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream);
                    var buffer = new char[1 << 20];
                    var carry = "";
                    int n;
                    while ((n = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        var text = carry + new string(buffer, 0, n);
                        seen.Ids.UnionWith(SessionIdsIn(text));
                        carry = text.Length > 256 ? text[^256..] : text;
                    }
                    Seen[transcript] = (stream.Position, seen.Ids);
                    return seen.Ids;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    return Seen.TryGetValue(transcript, out var last) ? last.Ids : [];
                }
            }
        }
    }
}
