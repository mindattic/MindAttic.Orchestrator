using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One CLI's /autopilot as it stood when the handoff began, so the CLI that starts in its place after the switch gets it
/// back: <see cref="Repos"/> on (comma-separated, as its mark), <see cref="Off"/> typed off, neither for the default.</summary>
public sealed record HandoffCli(
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("repo")] string? Repo,
    [property: JsonPropertyName("cwd")] string? Cwd,
    [property: JsonPropertyName("repos")] string Repos,
    [property: JsonPropertyName("off")] bool Off);

/// <summary>The global handoff mark (<c>~/.claude/session-context/handoff.json</c>). The context gate writes it for a typed
/// /handoff, the daemon marks it quiet, and the first CLI to start after the switch marks it resumed.</summary>
public sealed record HandoffMark(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("started")] string Started,
    [property: JsonPropertyName("by")] int? By,
    [property: JsonPropertyName("account")] string? Account,
    [property: JsonPropertyName("quiet_at")] string? QuietAt,
    [property: JsonPropertyName("resumed_at")] string? ResumedAt,
    [property: JsonPropertyName("autopilot")] IReadOnlyList<HandoffCli>? Autopilot)
{
    /// <summary>Winding down or quiet: nothing new starts until the switch.</summary>
    [JsonIgnore] public bool Holds => State is Handoff.Winding or Handoff.Quiet;

    [JsonIgnore] public DateTime StartedUtc => Handoff.Parse(Started) ?? DateTime.MinValue;
    [JsonIgnore] public DateTime? QuietUtc => Handoff.Parse(QuietAt);
    [JsonIgnore] public DateTime? ResumedUtc => Handoff.Parse(ResumedAt);
}

/// <summary>What still keeps a handoff from being quiet: CLIs mid-turn, subagents running (by the CLI that runs them), and
/// tickets claimed by <c>orchestrator next --claim</c> that no subagent has started on yet.</summary>
public sealed record HandoffWork(IReadOnlyList<CliRow> Busy, IReadOnlyList<(string Agent, int? Cli)> Subagents, IReadOnlyList<Assignment> Claims)
{
    public bool Any => Busy.Count > 0 || Subagents.Count > 0 || Claims.Count > 0;
}

/// <summary>
/// /handoff (M.ORCH-T481): winds every CLI down for an account switch and picks up after it. While the mark holds,
/// <c>orchestrator next</c> hands out nothing, keep-going sends no CLI back for work, the pace gate starts no new subagent, and the
/// daemon returns claims no subagent started on; each CLI quicksaves its repo's handoff as its turn ends. Once nothing is working
/// the daemon marks it quiet and says so once. On the new account, each CLI's start loads its handoff and gets its /autopilot back.
/// </summary>
public static class Handoff
{
    public const string FileName = "handoff.json";

    /// <summary>A CLI's mark once it quicksaved its handoff (<c>handoff-saved-&lt;pid&gt;</c>, its repo inside).</summary>
    public const string SavedPrefix = "handoff-saved-";

    /// <summary>An <see cref="HandoffMark.Autopilot"/> entry already given back to a starting CLI (<c>handoff-restored-&lt;index&gt;</c>).</summary>
    public const string RestoredPrefix = "handoff-restored-";

    public const string Winding = "winding";
    public const string Quiet = "quiet";
    public const string Resumed = "resumed";

    /// <summary>Why <c>orchestrator next</c> hands out nothing while the mark holds.</summary>
    public const string HoldReason = "handing off: /handoff is winding every CLI down for an account switch (/handoff off cancels it)";

    /// <summary>The one line said once nothing is working.</summary>
    public const string QuietLine = "All quiet, safe to switch accounts";

    /// <summary>A claim no subagent has shown up on within this is taken as never started: past the pace gate's longest hold of
    /// the Agent call that would start it, and the slot it kept for it.</summary>
    public static readonly TimeSpan ClaimGrace = TimeSpan.FromSeconds(Setup.AgentGate.MaxHoldSeconds + Setup.AgentGate.SlotLifeSeconds);

    /// <summary>A resumed mark is kept this long for CLIs still to start on the new account, then dropped.</summary>
    public static readonly TimeSpan ResumedKept = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Stamp(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    internal static DateTime? Parse(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ? at : null;

    /// <summary>The <c>state</c> row of the orchestrator's database the mark lives in (M.ORCH-T528), left out of the dump. The
    /// hooks write it through stdlib sqlite3 (the context gate names it literally); <see cref="FileName"/> is read in the
    /// first time it's seen (kept as .bak) and written again only while the database is held, missing or older, a clear
    /// then leaving <c>{"state": "cleared"}</c> in it.</summary>
    public const string StateName = "handoff";

    /// <summary>The database the mark is kept in; replaceable for tests.</summary>
    internal static Func<OrchestratorDb> Db { get; set; } = () => new OrchestratorDb(OrchestratorDb.StatePath(JsonStore.LiveDirectory));

    /// <summary>The mark in <paramref name="directory"/> (the session-context folder), or null when there's none or it can't be read:
    /// the database's handoff row, or <see cref="FileName"/> while one is there (read in here when the database takes it).</summary>
    public static HandoffMark? Read(string directory)
    {
        var path = Path.Combine(directory, FileName);
        ReadIn(path);
        var text = ReadFile(path);
        if (text is null && !File.Exists(path))
        {
            var db = Db();
            try { text = File.Exists(db.Path) ? db.LoadState(StateName)?.Value : null; }
            catch (Exception ex) when (ex is SqliteException or IOException) { return null; }
        }
        try
        {
            var mark = text is null ? null : JsonSerializer.Deserialize<HandoffMark>(text);
            return mark?.State is Winding or Quiet or Resumed ? mark : null;
        }
        catch (JsonException) { return null; }
    }

    private static string? ReadFile(string path)
    {
        for (var i = 0; i < 20; i++)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (IOException) { Thread.Sleep(50); } // being replaced: a retry, not a missing mark
            catch (UnauthorizedAccessException) { return null; }
        }
        return null;
    }

    /// <summary>Reads <paramref name="path"/> (a mark from before the database, or one a hook kept while it was held) into the
    /// handoff row and keeps it as .bak. It's moved aside first, so a hook writing a newer file meanwhile isn't lost; while the
    /// database can't take it, it's put back (unless a newer one took its place).</summary>
    private static void ReadIn(string path)
    {
        if (!File.Exists(path)) return;
        var claim = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.reading";
        try { File.Move(path, claim); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        try
        {
            var text = File.ReadAllText(claim);
            HandoffMark? mark = null;
            try { mark = JsonSerializer.Deserialize<HandoffMark>(text); } catch (JsonException) { }
            if (mark?.State is Winding or Quiet or Resumed) Db().SaveState(StateName, JsonSerializer.Serialize(mark, Json));
            else Db().DeleteState(StateName); // cleared while the database was held
            File.Move(claim, path + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            try { if (!File.Exists(path)) File.Move(claim, path); else File.Delete(claim); }
            catch (Exception again) when (again is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Whether a handoff holds new work back now (winding down or quiet).</summary>
    public static bool Holding(string directory) => Read(directory)?.Holds == true;

    public static void Write(string directory, HandoffMark mark)
    {
        var path = Path.Combine(directory, FileName);
        var json = JsonSerializer.Serialize(mark, Json);
        try
        {
            Db().SaveState(StateName, json);
            if (File.Exists(path)) File.Move(path, path + ".bak", overwrite: true);
            return;
        }
        catch (SqliteException) { } // held past the busy timeout: the file, which the next read takes in
        Directory.CreateDirectory(directory);
        var tmp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Starts a handoff (unless one already holds), recording every open CLI's /autopilot so the CLIs on the new account get
    /// it back. Returns the mark in force.</summary>
    public static HandoffMark Start(ContextCycler cycler, int? by, string? account, DateTime nowUtc)
    {
        if (Read(cycler.Directory) is { Holds: true } running) return running;
        Clear(cycler.Directory, markToo: false);
        var on = cycler.AutopilotClis();
        var cwds = cycler.Load().Where(s => s.Pid is not null).GroupBy(s => s.Pid!.Value).ToDictionary(g => g.Key, g => g.MaxBy(s => s.TurnUtc)!.Cwd);
        var off = !Directory.Exists(cycler.Directory) ? [] : Directory.EnumerateFiles(cycler.Directory, "noautopilot-*")
            .Select(f => int.TryParse(Path.GetFileName(f)["noautopilot-".Length..], out var p) ? p : 0)
            .Where(p => p > 0 && ContextCycler.IsClaude(p)).ToHashSet();
        var clis = on.Keys.Concat(off).Distinct().Order().Select(pid =>
        {
            var cwd = cwds.GetValueOrDefault(pid);
            return new HandoffCli(pid, RepoOf(cwd), cwd, on.TryGetValue(pid, out var repos) ? string.Join(",", repos) : "", !on.ContainsKey(pid));
        }).ToList();
        var mark = new HandoffMark(Winding, Stamp(nowUtc), by, account, null, null, clis);
        Write(cycler.Directory, mark);
        return mark;
    }

    /// <summary>The repo a CLI's folder is in: the folder under the workspace (or a worktree root) it sits in.</summary>
    private static string? RepoOf(string? cwd)
    {
        if (string.IsNullOrEmpty(cwd)) return null;
        try
        {
            var options = OrchestratorOptions.Load();
            var full = Path.GetFullPath(cwd);
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.WorkspaceRoot)) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full[root.Length..].Split(Path.DirectorySeparatorChar)[0] : Path.GetFileName(full);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { return null; }
    }

    /// <summary>Cancels a handoff (or tidies one that's done): the mark and every CLI's saved and restored marks go.</summary>
    public static void Clear(string directory, bool markToo = true)
    {
        if (markToo)
        {
            var path = Path.Combine(directory, FileName);
            try
            {
                var db = Db();
                if (File.Exists(db.Path)) db.DeleteState(StateName);
                try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            catch (SqliteException)
            {
                // Held past the busy timeout: a cleared file, which every reader takes over the row.
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, """{"state": "cleared"}""");
            }
        }
        if (!Directory.Exists(directory)) return;
        var files = Directory.EnumerateFiles(directory, SavedPrefix + "*").Concat(Directory.EnumerateFiles(directory, RestoredPrefix + "*")).ToList();
        foreach (var f in files)
            try { File.Delete(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The CLIs that quicksaved their handoff, by process id, with the repo each named.</summary>
    public static IReadOnlyDictionary<int, string> Saved(string directory) => !Directory.Exists(directory) ? new Dictionary<int, string>()
        : Directory.EnumerateFiles(directory, SavedPrefix + "*")
            .Select(f => (Pid: int.TryParse(Path.GetFileName(f)[SavedPrefix.Length..], out var p) ? p : 0, File: f))
            .Where(x => x.Pid > 0)
            .ToDictionary(x => x.Pid, x => ReadOrEmpty(x.File).Trim());

    private static string ReadOrEmpty(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; }
    }

    /// <summary>A claim (<c>orchestrator next --claim</c>: a worktree, no background agent) that no live subagent or session is
    /// working (<paramref name="worked"/>) and was made more than <see cref="ClaimGrace"/> ago: its subagent never started.</summary>
    public static bool Unstarted(Assignment a, IReadOnlySet<string> worked, DateTime nowUtc) =>
        a.Log is null && a.Pilot is null && a.Worktree && !worked.Contains(a.TicketId) && nowUtc - a.StartedUtc >= ClaimGrace;

    /// <summary>What's still working: <paramref name="roster"/>'s CLIs mid-turn (one that never had a turn isn't), the subagents
    /// running, and claims no subagent has shown up on yet (unstarted ones are returned to the queue by the daemon).</summary>
    public static HandoffWork Working(IReadOnlyList<CliRow> roster, IReadOnlyList<(string Agent, int? Cli)> subagents,
        IEnumerable<Assignment> assignments, IReadOnlySet<string> worked)
    {
        var busy = roster.Where(c => !c.Idle && c.LastTurnUtc is not null).ToList();
        var claims = assignments.Where(a => a.Log is null && a.Pilot is null && a.Worktree && !worked.Contains(a.TicketId)).ToList();
        return new HandoffWork(busy, subagents, claims);
    }

    /// <summary>Lines for <c>orchestrator handoff status</c>.</summary>
    public static IReadOnlyList<string> Describe(HandoffMark? mark, HandoffWork work, IReadOnlyList<CliRow> roster, IReadOnlyDictionary<int, string> saved, DateTime nowUtc)
    {
        if (mark is null) return ["No handoff: /handoff (or orchestrator handoff on) winds every CLI down for an account switch."];
        var lines = new List<string>();
        lines.Add(mark.State switch
        {
            Winding => $"Handing off since {Formatting.Ago(mark.StartedUtc, nowUtc)}: nothing new starts; what's working finishes, lands and closes, then each CLI quicksaves.",
            Quiet => $"{QuietLine} (quiet since {Formatting.Ago(mark.QuietUtc, nowUtc)}). Each CLI that starts next picks up its handoff and its /autopilot.",
            _ => $"Picked up after the switch {Formatting.Ago(mark.ResumedUtc, nowUtc)}: new work starts again; CLIs still to start get their handoff and /autopilot.",
        });
        if (mark.State == Winding)
        {
            if (!work.Any) lines.Add("Nothing is working: the daemon marks it quiet at its next tick.");
            foreach (var c in work.Busy)
            {
                var subs = work.Subagents.Count(s => s.Cli == c.Pid);
                lines.Add($"  working: the CLI in {c.Repo} (pid {c.Pid}), mid-turn{(subs > 0 ? $" with {subs} subagent{(subs == 1 ? "" : "s")}" : "")}");
            }
            var loose = work.Subagents.Where(s => s.Cli is not { } p || work.Busy.All(c => c.Pid != p)).ToList();
            if (loose.Count > 0) lines.Add($"  working: {loose.Count} subagent{(loose.Count == 1 ? "" : "s")} ({string.Join(", ", loose.Select(s => s.Agent))})");
            foreach (var a in work.Claims)
                lines.Add($"  claimed: {a.TicketId} in {a.Repo}, no subagent on it yet; returned to the queue {(nowUtc - a.StartedUtc >= ClaimGrace ? "at the daemon's next tick" : $"if none starts by {Formatting.ResetTime(a.StartedUtc + ClaimGrace, nowUtc)}")}");
        }
        if (mark.State != Resumed)
            foreach (var c in roster)
                lines.Add($"  {(saved.ContainsKey(c.Pid) ? "saved" : "not saved")}: the CLI in {c.Repo} (pid {c.Pid})");
        lines.Add(mark.State == Resumed ? "" : "/handoff off (or orchestrator handoff off) cancels it.");
        return [.. lines.Where(l => l.Length > 0)];
    }
}
