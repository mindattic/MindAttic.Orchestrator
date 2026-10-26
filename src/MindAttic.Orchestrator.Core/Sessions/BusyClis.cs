using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>Where a Claude CLI stands for a redeploy (M.ORCH-T873).</summary>
public enum CliActivity
{
    /// <summary>Its turn has ended, nothing of its is running, and it holds no claimed ticket: done.</summary>
    Idle,

    /// <summary>Mid-turn, a subagent running, a ticket claimed, or holding on a prompt.</summary>
    Working,

    /// <summary>Its state couldn't be read or isn't recorded: never taken for idle.</summary>
    Unknown,

    /// <summary>The CLI asking (the one running the deploy): necessarily busy, and left out.</summary>
    Self,

    /// <summary>Running inside a live TermHost (M.ORCH-T928): it survives a dashboard redeploy, so it never blocks one.</summary>
    Hosted,
}

/// <summary>One open Claude CLI as the busy check sees it: its process, repo, login, and why it counts as working or unknown.</summary>
public sealed record BusyCli(int? Pid, string Repo, string Login, string? SessionId, CliActivity Activity, IReadOnlyList<string> Why);

/// <summary>Every open Claude CLI and what the check couldn't tie to one (an unreadable state file, a subagent with no CLI).</summary>
public sealed record BusyReport(DateTime AtUtc, IReadOnlyList<BusyCli> Clis)
{
    /// <summary>The CLIs a redeploy would cut off mid-work, or can't be sure about.</summary>
    public IReadOnlyList<BusyCli> Blocking => [.. Clis.Where(c => c.Activity is CliActivity.Working or CliActivity.Unknown)];

    /// <summary>Whether nothing but idle CLIs (and the asking one) is open.</summary>
    public bool AllIdle => Blocking.Count == 0;

    /// <summary>The CLIs running in a live TermHost: unaffected by a redeploy (M.ORCH-T928).</summary>
    public IReadOnlyList<BusyCli> Hosted => [.. Clis.Where(c => c.Activity == CliActivity.Hosted)];
}

/// <summary>
/// Whether any Claude CLI on this machine is still working (M.ORCH-T873), on any login, so a redeploy (which restarts the dashboard,
/// and with it every terminal its Terminals panels host) can refuse rather than cut them off. A CLI is working while its turn runs
/// (the context gate's <c>busy</c>), while any of its subagents runs (their marks, in the global login's and every per-CLI login's
/// session-context), while it holds an <c>/allocate</c> claim, or while it holds on a prompt (<c>asking</c>). Anything that can't
/// be read is <see cref="CliActivity.Unknown"/>, never idle. The CLI running the check (<c>MINDATTIC_CLI_PID</c>, else the
/// claude.exe it runs under) is <see cref="CliActivity.Self"/> and doesn't count.
/// </summary>
public static class BusyClis
{
    /// <summary>A Claude process found running: its id, when it started (null when unreadable), the Claude CLI above it, if any, and
    /// the live TermHost it runs under, if any (M.ORCH-T928).</summary>
    public sealed record Proc(int Pid, DateTime? StartedUtc, int? ClaudeParent, int? HostPid = null);

    /// <summary>The TermHost (its pid) a process runs under: the nearest of <paramref name="hostPids"/> among its ancestors; null when
    /// none (an unhosted CLI, which a redeploy does cut off). With <paramref name="startOf"/> (when a pid's process started, null when
    /// it can't be read) each step up is checked (M.ORCH-T1006): a parent that started after its child is another process that took a
    /// dead parent's pid, so the walk ends there, and so does one whose start can't be read. Otherwise an unhosted CLI whose dead
    /// parent's pid a TermHost reused counted as hosted, and an install cut it off.</summary>
    public static int? HostAncestor(int pid, IReadOnlyDictionary<int, (int Parent, string Exe)> tree, IReadOnlySet<int> hostPids,
        Func<int, DateTime?>? startOf = null)
    {
        if (hostPids.Count == 0) return null;
        var child = pid;
        foreach (var (ancestor, _) in ProcessTree.Ancestors(pid, tree))
        {
            if (startOf is not null && (startOf(child) is not { } childStart || startOf(ancestor) is not { } parentStart || parentStart > childStart + StartSlack))
                return null;
            if (hostPids.Contains(ancestor)) return ancestor;
            child = ancestor;
        }
        return null;
    }

    /// <summary>One session's recorded state: null <see cref="Pid"/> and <see cref="Unreadable"/> when it couldn't be parsed.</summary>
    public sealed record State(string SessionId, int? Pid, DateTime WrittenUtc, string? Turn, DateTime? AtUtc, DateTime? AskingSinceUtc,
        string? Cwd, string? ConfigDir, bool Unreadable = false, DateTime? DecidingUtc = null);

    /// <summary>A running subagent mark: its agent id and the CLI it runs in (null when the mark names none or can't be read).</summary>
    public sealed record Subagent(string Agent, int? Cli, string Login);

    /// <summary>A state file written this long before its process started is a previous process's that had the same id.</summary>
    internal static readonly TimeSpan StartSlack = TimeSpan.FromSeconds(5);

    /// <summary>The CLI the asking process belongs to: <c>MINDATTIC_CLI_PID</c>, else the claude.exe it runs under.</summary>
    public static int? CallingCli() =>
        int.TryParse(Environment.GetEnvironmentVariable("MINDATTIC_CLI_PID"), out var pid) && pid > 0 ? pid : ProcessTree.ClaudeAncestor(Environment.ProcessId);

    /// <summary>Every Claude CLI on this machine now, as it stands. <paramref name="self"/> defaults to <see cref="CallingCli"/>.</summary>
    public static BusyReport Scan(int? self = null, ContextCycler? cycler = null, AccountDirs? accounts = null, JsonStore? store = null)
    {
        var now = DateTime.UtcNow;
        cycler ??= new ContextCycler();
        accounts ??= new AccountDirs();
        var problems = new List<string>();
        var procs = Processes(problems);
        var states = ReadStates(cycler, problems);

        var subagents = new List<Subagent>();
        var marks = new List<(string Login, string Directory)> { ("global login", cycler.Directory) };
        try { marks.AddRange(accounts.All().Select(a => (a.Email, a.MarksDirectory))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { problems.Add($"the per-CLI logins under {accounts.Root} couldn't be listed: {ex.Message}"); }
        foreach (var (login, dir) in marks)
            try
            {
                var c = new ContextCycler(dir) { ProjectsDir = cycler.ProjectsDir };
                subagents.AddRange(c.AliveSubagents(now).Select(a => new Subagent(a.Agent, a.Cli, login)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { problems.Add($"{login}'s subagent marks ({dir}) couldn't be read: {ex.Message}"); }

        IReadOnlyList<Assignment> claims = [];
        try { claims = [.. (store ?? new JsonStore()).LoadAssignments().Where(a => a.IsClaim)]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or System.Data.Common.DbException)
        {
            problems.Add($"the /allocate claims couldn't be read: {ex.Message}");
        }

        return Judge(procs, states, subagents, claims, self ?? CallingCli(), accounts, problems, now);
    }

    /// <summary>The judgement itself, over what <see cref="Scan"/> read (a seam for tests).</summary>
    public static BusyReport Judge(IReadOnlyList<Proc> procs, IReadOnlyList<State> states, IReadOnlyList<Subagent> subagents,
        IReadOnlyList<Assignment> claims, int? self, AccountDirs accounts, IReadOnlyList<string> problems, DateTime nowUtc)
    {
        var clis = new List<BusyCli>();
        var live = procs.Select(p => p.Pid).ToHashSet();
        // A claude.exe started by another (a child it runs) isn't a CLI of its own: it's part of the one above it.
        foreach (var p in procs.Where(p => p.ClaudeParent is not { } parent || !live.Contains(parent)).OrderBy(p => p.Pid))
        {
            var state = states.Where(s => !s.Unreadable && s.Pid == p.Pid && (p.StartedUtc is not { } started || s.WrittenUtc >= started - StartSlack))
                .MaxBy(s => s.WrittenUtc);
            var repo = RepoOf(state?.Cwd);
            var login = LoginOf(state?.ConfigDir, accounts);
            if (p.Pid == self)
            {
                clis.Add(new BusyCli(p.Pid, repo, login, state?.SessionId, CliActivity.Self, ["the CLI running this check"]));
                continue;
            }
            // In a live TermHost it outlives the dashboard: hosted, unaffected by deploy, never blocking.
            if (p.HostPid is { } host)
            {
                clis.Add(new BusyCli(p.Pid, repo, login, state?.SessionId, CliActivity.Hosted, [$"hosted in TermHost pid {host}, unaffected by deploy"]));
                continue;
            }
            var why = new List<string>();
            var unknown = false;
            // Its Stop hook marks the turn busy while it decides whether to send it on (M.ORCH-T1036); a mark older than the hook's
            // timeout is a hook that was cut off, and the turn ended with it.
            if (state is { Turn: "busy" } && !ContextCycler.IsBusyTurn(state.Turn, state.DecidingUtc, nowUtc)) state = state with { Turn = "idle" };
            if (state is null)
            {
                unknown = true;
                why.Add("no session state recorded for it (no prompt typed yet, or the context gate hook isn't installed in it)");
            }
            else if (state.Turn == "busy") why.Add("mid-turn" + Since(state.AtUtc, nowUtc));
            else if (state.Turn != "idle")
            {
                unknown = true;
                why.Add($"its turn state reads \"{state.Turn ?? "(none)"}\"");
            }
            if (state?.AskingSinceUtc is { } asking) why.Add("holding on a prompt for you" + Since(asking, nowUtc));
            var mine = subagents.Where(s => s.Cli == p.Pid).ToList();
            if (mine.Count > 0) why.Add($"{mine.Count} subagent(s) running");
            var held = claims.Where(a => a.Cli == p.Pid || (state is not null && a.CliSession is not null && a.CliSession == state.SessionId))
                .Select(a => a.TicketId).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (held.Count > 0) why.Add("/allocate claims in flight: " + string.Join(", ", held));
            var working = state?.Turn == "busy" || state?.AskingSinceUtc is not null || mine.Count > 0 || held.Count > 0;
            var activity = working ? CliActivity.Working : unknown ? CliActivity.Unknown : CliActivity.Idle;
            clis.Add(new BusyCli(p.Pid, repo, login, state?.SessionId, activity, why));
        }

        // What can't be tied to an open CLI is unknown, not idle: it may be an unhosted CLI's, one the process scan didn't identify.
        // It is dropped only when it is provably a hosted CLI's (or the asking one's), which the redeploy doesn't touch: an unreadable
        // state whose session is one of theirs (M.ORCH-T1042: dropped whenever every CLI the scan found was hosted, an unhosted CLI
        // the scan missed let a deploy pass; M.ORCH-T1006). A subagent mark naming no CLI can't be tied to one, so it always counts.
        var hostedSessions = clis.Where(c => c.Activity is (CliActivity.Self or CliActivity.Hosted) && c.SessionId is { Length: > 0 })
            .Select(c => c.SessionId!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in subagents.Where(s => s.Cli is null))
            clis.Add(new BusyCli(null, "?", s.Login, null, CliActivity.Unknown, [$"subagent {s.Agent} is running, and its mark names no CLI"]));
        var oldest = procs.Where(p => p.StartedUtc is not null).Select(p => p.StartedUtc!.Value).DefaultIfEmpty(DateTime.MinValue).Min();
        foreach (var s in states.Where(s => s.Unreadable && procs.Count > 0 && s.WrittenUtc >= oldest - StartSlack && !hostedSessions.Contains(s.SessionId)))
            clis.Add(new BusyCli(null, "?", "?", s.SessionId, CliActivity.Unknown, [$"session {s.SessionId}'s state couldn't be read"]));
        foreach (var problem in problems)
            clis.Add(new BusyCli(null, "?", "?", null, CliActivity.Unknown, [problem]));
        return new BusyReport(nowUtc, clis);
    }

    /// <summary>One line per CLI that blocks a redeploy: repo, login, pid and what it's doing.</summary>
    public static IReadOnlyList<string> Describe(BusyReport report) => [.. report.Blocking.Select(c =>
        $"{(c.Activity == CliActivity.Unknown ? "UNKNOWN" : "working")}: {c.Repo} ({c.Login}{(c.Pid is { } pid ? $", pid {pid}" : "")}): {string.Join("; ", c.Why)}")];

    private static string Since(DateTime? atUtc, DateTime nowUtc) =>
        atUtc is { } at && at > DateTime.MinValue && nowUtc >= at ? $" (since {Formatting.Ago(at, nowUtc)})" : "";

    private static string RepoOf(string? cwd)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return "?";
        try { return Path.GetFileName(Path.TrimEndingDirectorySeparator(cwd)) is { Length: > 0 } name ? name : cwd; }
        catch (ArgumentException) { return cwd; }
    }

    private static string LoginOf(string? configDir, AccountDirs accounts) =>
        string.IsNullOrWhiteSpace(configDir) ? "global login" : accounts.Of(configDir)?.Email ?? configDir;

    /// <summary>Every Claude CLI process (not the Claude Desktop app), with when it started and the CLI above it.</summary>
    private static List<Proc> Processes(List<string> problems, Func<IReadOnlyList<Term.TermSessionRecord>>? hosts = null)
    {
        var found = new List<Proc>();
        IReadOnlyDictionary<int, (int Parent, string Exe)> tree;
        var hostPids = new HashSet<int>();
        try { hostPids = [.. (hosts ?? Term.TermSessions.Enumerate).Invoke().Select(t => t.HostPid)]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { problems.Add($"the TermHost sessions couldn't be read: {ex.Message}"); }
        try { tree = ProcessTree.Snapshot(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { tree = new Dictionary<int, (int, string)>(); problems.Add($"the process tree couldn't be read: {ex.Message}"); }
        // When each pid's process started, read once a scan: the host walk checks each step up with it (M.ORCH-T1006).
        var starts = new Dictionary<int, DateTime?>();
        DateTime? StartOf(int pid)
        {
            if (starts.TryGetValue(pid, out var known)) return known;
            DateTime? at = null;
            try
            {
                using var proc = Process.GetProcessById(pid);
                at = proc.StartTime.ToUniversalTime();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            return starts[pid] = at;
        }
        Process[] all;
        try { all = Process.GetProcesses(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            problems.Add($"the running processes couldn't be listed: {ex.Message}");
            return found;
        }
        foreach (var p in all)
            using (p)
            {
                string name;
                try { name = p.ProcessName; }
                catch (InvalidOperationException) { continue; } // exited meanwhile
                if (!Providers.IsClaudeProcess(name)) continue;
                string? path = null;
                try { path = p.MainModule?.FileName; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                if (!Scheduling.GlobalPause.IsCli(path)) continue;
                DateTime? started = null;
                try { started = p.StartTime.ToUniversalTime(); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                if (started is not null) starts[p.Id] = started;
                found.Add(new Proc(p.Id, started, ProcessTree.ClaudeAncestor(p.Id, tree), HostAncestor(p.Id, tree, hostPids, StartOf)));
            }
        return found;
    }

    /// <summary>Each session's state: its <c>.state.json</c>, or its <c>session_states</c> row when that's newer or the file won't
    /// read. A file read twice (a hook may be replacing it) and still unreadable, with no row, is <see cref="State.Unreadable"/>.</summary>
    private static List<State> ReadStates(ContextCycler cycler, List<string> problems)
    {
        var states = new List<State>();
        if (!Directory.Exists(cycler.Directory)) return states;
        IReadOnlyDictionary<string, (string Value, DateTime WrittenUtc)> rows;
        try { rows = SessionStates.Rows(); }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { rows = new Dictionary<string, (string, DateTime)>(); }
        IEnumerable<string> files;
        try { files = [.. Directory.EnumerateFiles(cycler.Directory, "*" + SessionStates.Suffix)]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add($"the session states in {cycler.Directory} couldn't be listed: {ex.Message}");
            return states;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var session = Path.GetFileName(file)[..^SessionStates.Suffix.Length];
            seen.Add(session);
            var hasRow = rows.TryGetValue(session, out var row);
            DateTime written;
            try { written = File.GetLastWriteTimeUtc(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { written = DateTime.MinValue; }
            State? state = null;
            if (!hasRow || written >= row.WrittenUtc)
                for (var attempt = 0; attempt < 2 && state is null; attempt++)
                {
                    try { state = Parse(session, ReadShared(file), written); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
                    {
                        if (attempt == 0) Thread.Sleep(100);
                    }
                }
            if (state is null && hasRow)
                try { state = Parse(session, row.Value, row.WrittenUtc); }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }
            states.Add(state ?? new State(session, null, written, null, null, null, null, null, Unreadable: true));
        }
        // A row whose file is gone (deleted by hand, or not exported while the folder was held) still counts.
        foreach (var (session, row) in rows.Where(r => !seen.Contains(r.Key)))
            try { states.Add(Parse(session, row.Value, row.WrittenUtc)); }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }
        return states;
    }

    internal static State Parse(string session, string json, DateTime writtenUtc)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.ValueKind != JsonValueKind.Object) throw new JsonException("not an object");
        string? Text(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        DateTime? When(string name) => Text(name) is { } t &&
            DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var w) ? w : null;
        int? pid = r.TryGetProperty("pid", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) && n > 0 ? n : null;
        return new State(session, pid, writtenUtc, Text("state"), When("at"), ContextCycler.IsAsking(r) ? When("asking") : null, Text("cwd"), Text("config_dir"),
            DecidingUtc: When("deciding"));
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Polls <see cref="Scan"/> until every CLI is idle or <paramref name="timeout"/> passes; the last report either way.</summary>
    public static BusyReport WaitForIdle(TimeSpan poll, TimeSpan? timeout, Action<BusyReport>? progress, CancellationToken cancel, int? self = null)
    {
        var until = timeout is { } t ? DateTime.UtcNow + t : DateTime.MaxValue;
        while (true)
        {
            var report = Scan(self);
            if (report.AllIdle || DateTime.UtcNow >= until || cancel.IsCancellationRequested) return report;
            progress?.Invoke(report);
            if (cancel.WaitHandle.WaitOne(poll)) return report;
        }
    }
}
