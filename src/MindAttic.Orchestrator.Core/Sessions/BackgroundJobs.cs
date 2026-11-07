using System.Globalization;
using System.Text.Json;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A CLI's background shells (M.ORCH-T1528): a Bash or PowerShell tool call run with <c>run_in_background</c> keeps running after
/// the CLI's turn ends, so the turn state alone read the CLI idle (its tab and its person's tab the gray "zzz") while its job still
/// ran for hours. The context gate marks each one at its PostToolUse in <c>session-context/background/&lt;task id&gt;</c>
/// (<c>{"cli", "session_id", "at", "what"}</c>), and a mark counts while its CLI is a live Claude process with a shell of its own
/// (bash, sh, powershell, pwsh; never cmd, which MCP servers run under) that started around the mark's time: the shell the job
/// runs in, gone once the job ends. Marks that no longer count are deleted.
/// </summary>
public static class BackgroundJobs
{
    /// <summary>The marks' folder under the shared session-context folder (every login's CLIs mark there, as their turn states do).</summary>
    public const string DirName = "background";

    /// <summary>How long before its mark the job's shell may have started: the hook runs once the tool has started it, and a
    /// pegged machine can take a while to get to it.</summary>
    public static readonly TimeSpan StartedBefore = TimeSpan.FromMinutes(5);

    /// <summary>How long after its mark the job's shell may have started (clocks and rounding): a shell started later is a later call's.</summary>
    public static readonly TimeSpan StartedAfter = TimeSpan.FromSeconds(10);

    /// <summary>A mark older than this never counts and is deleted, even when the process check can't be made.</summary>
    public static readonly TimeSpan MarkLife = TimeSpan.FromHours(48);

    /// <summary>The shells a background Bash or PowerShell call runs in.</summary>
    private static readonly string[] Shells = ["bash.exe", "sh.exe", "powershell.exe", "pwsh.exe"];

    /// <summary>One background shell's mark: its task id (the file name), its CLI's process id, session, when the hook wrote it,
    /// and what it runs (the call's description or command).</summary>
    public sealed record Mark(string Id, int? Cli, string? SessionId, DateTime AtUtc, string? What);

    /// <summary>Where the marks are kept under <paramref name="sessionContext"/>.</summary>
    public static string DirectoryOf(string sessionContext) => Path.Combine(sessionContext, DirName);

    /// <summary>How many background jobs each live CLI (by process id) has running now, read from <paramref name="sessionContext"/>;
    /// marks that no longer count are deleted. Empty when there are none or the folder can't be read.</summary>
    public static IReadOnlyDictionary<int, int> RunningByCli(string sessionContext, DateTime nowUtc)
    {
        var dir = DirectoryOf(sessionContext);
        var marks = Read(dir);
        if (marks.Count == 0) return new Dictionary<int, int>();
        IReadOnlyDictionary<int, (int Parent, string Exe)>? tree;
        try { tree = ProcessTree.Snapshot(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { tree = null; }
        var starts = new Dictionary<int, DateTime?>();
        DateTime? StartOf(int pid)
        {
            if (starts.TryGetValue(pid, out var known)) return known;
            DateTime? at = null;
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid);
                at = p.StartTime.ToUniversalTime();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            return starts[pid] = at;
        }
        var counts = new Dictionary<int, int>();
        foreach (var mark in marks)
        {
            if (Judge(mark, tree is { Count: > 0 } ? tree : null, StartOf, nowUtc))
            {
                if (mark.Cli is { } cli) counts[cli] = counts.GetValueOrDefault(cli) + 1;
                continue;
            }
            try { File.Delete(Path.Combine(dir, mark.Id)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return counts;
    }

    /// <summary>
    /// Whether <paramref name="mark"/>'s job is still running: true while its CLI is a live Claude process in <paramref name="tree"/>
    /// with a shell under it (the shell's nearest Claude ancestor) that started within [mark - <see cref="StartedBefore"/>, mark +
    /// <see cref="StartedAfter"/>]; false once it isn't, or the mark is past <see cref="MarkLife"/> or names no CLI, so it can be
    /// deleted. With no process tree (<paramref name="tree"/> null: it couldn't be read) a mark inside its life counts: never idle
    /// for want of a read.
    /// </summary>
    public static bool Judge(Mark mark, IReadOnlyDictionary<int, (int Parent, string Exe)>? tree, Func<int, DateTime?> startOf, DateTime nowUtc)
    {
        if (nowUtc - mark.AtUtc > MarkLife || mark.Cli is not { } cli) return false;
        if (tree is null) return true;
        if (!tree.TryGetValue(cli, out var proc) || !proc.Exe.StartsWith("claude.exe", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var (pid, (_, exe)) in tree)
        {
            if (!Shells.Contains(exe, StringComparer.OrdinalIgnoreCase) || ProcessTree.ClaudeAncestor(pid, tree) != cli) continue;
            if (startOf(pid) is { } started && started >= mark.AtUtc - StartedBefore && started <= mark.AtUtc + StartedAfter) return true;
        }
        return false;
    }

    /// <summary>Every mark in <paramref name="dir"/>; one that can't be read is left out (a hook may be writing it), not deleted.</summary>
    public static IReadOnlyList<Mark> Read(string dir)
    {
        var marks = new List<Mark>();
        IEnumerable<string> files;
        try { files = Directory.Exists(dir) ? [.. Directory.EnumerateFiles(dir)] : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return marks; }
        foreach (var file in files)
        {
            try
            {
                string text;
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
                if (Parse(Path.GetFileName(file), text, File.GetLastWriteTimeUtc(file)) is { } mark) marks.Add(mark);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return marks;
    }

    /// <summary>A mark's JSON as the context gate writes it; its file's write time stands in for a missing <c>at</c>. Null when it
    /// isn't a JSON object (half-written: read again next time).</summary>
    internal static Mark? Parse(string id, string json, DateTime writtenUtc)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            string? Text(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            int? cli = !r.TryGetProperty("cli", out var c) ? null : c.ValueKind switch
            {
                JsonValueKind.Number when c.TryGetInt32(out var n) && n > 0 => n,
                JsonValueKind.String when int.TryParse(c.GetString(), out var n) && n > 0 => n,
                _ => null,
            };
            var at = Text("at") is { } t && DateTime.TryParse(t, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var w) ? w : writtenUtc;
            return new Mark(id, cli, Text("session_id"), at, Text("what"));
        }
        catch (JsonException) { return null; }
    }
}
