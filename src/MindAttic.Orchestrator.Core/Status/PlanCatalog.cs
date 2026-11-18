using System.Security;
using System.Text;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// One saved plan (M.ORCH-T1826): a markdown file under a <c>docs\plans</c> folder, holding a half-formed idea or a feature
/// that isn't ready for an implementation ticket yet.
/// </summary>
/// <param name="Title">Its first <c># </c> heading; the file's name without its extension when it has none.</param>
/// <param name="Repo">The repo it was saved in; the workspace folder's own name for one saved at the workspace root.</param>
/// <param name="Summary">Its first paragraph that is neither a heading nor the status, cut to the settings' length; empty when it has none.</param>
/// <param name="Status">What follows <c>Status:</c> on the line that starts with it; null when the file has no such line.</param>
/// <param name="Path">The file's full path.</param>
/// <param name="LastWriteTimeUtc">When the file was last written.</param>
public sealed record PlanEntry(string Title, string Repo, string Summary, string? Status, string Path, DateTime LastWriteTimeUtc)
{
    /// <summary>What names it across refreshes: its path and when it was written, so an edited plan counts as changed.</summary>
    public string Key => $"{Path}|{LastWriteTimeUtc.Ticks}";
}

/// <summary>
/// The saved plans of a workspace (M.ORCH-T1826): every markdown file in the plans folder
/// (<see cref="OrchestratorSettings.PlansFolder"/>, <c>docs\plans</c>) of the workspace root and of each repo the snapshot
/// already knows, newest first. Only the head of each file is read (<see cref="OrchestratorSettings.PlanHeadKb"/>), so a very
/// large file costs no more than a small one. A missing folder lists nothing, and a file that can't be read is still listed
/// under its file name. Never throws.
/// </summary>
public static class PlanCatalog
{
    /// <summary>The repos <paramref name="snapshot"/> knows that have a checkout: the same filter the daemon's own per-repo
    /// jobs use (a name in brackets is a bucket of sessions, not a repo).</summary>
    public static IReadOnlyList<(string Name, string Path)> KnownRepos(Snapshot snapshot) =>
        [.. snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('('))
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, g.First().Path))];

    /// <summary>Every plan saved at <paramref name="workspaceRoot"/> or in a repo <paramref name="snapshot"/> knows, newest first.</summary>
    public static IReadOnlyList<PlanEntry> Read(string workspaceRoot, Snapshot snapshot, OrchestratorSettings settings) =>
        Read(workspaceRoot, KnownRepos(snapshot), settings);

    /// <summary>Every plan saved at <paramref name="workspaceRoot"/> or in one of <paramref name="repos"/>, newest first.</summary>
    public static IReadOnlyList<PlanEntry> Read(string workspaceRoot, IEnumerable<(string Name, string Path)> repos, OrchestratorSettings settings)
    {
        var folder = FolderOf(settings);
        var headBytes = HeadBytesOf(settings);
        var summaryChars = SummaryCharsOf(settings);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plans = new List<PlanEntry>();
        IEnumerable<(string Name, string Path)> roots = string.IsNullOrWhiteSpace(workspaceRoot) ? repos : [(WorkspaceName(workspaceRoot), workspaceRoot), .. repos];
        foreach (var (name, path) in roots)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            string dir;
            try { dir = Path.GetFullPath(Path.Combine(path, folder)); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException) { continue; }
            // The workspace root listed again as a repo, or two names for one checkout: its plans are listed once.
            if (!seen.Add(dir)) continue;
            foreach (var file in Files(dir))
                if (TryRead(file, name, headBytes, summaryChars) is { } plan) plans.Add(plan);
        }
        return [.. plans.OrderByDescending(p => p.LastWriteTimeUtc).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The plans folder under each root: the setting, without leading or trailing separators; the default when it is blank.</summary>
    internal static string FolderOf(OrchestratorSettings settings)
    {
        var folder = (settings.PlansFolder ?? "").Trim().Trim('\\', '/');
        return folder.Length == 0 ? OrchestratorSettings.DefaultPlansFolder : folder;
    }

    /// <summary>How many bytes of each file are read, from <see cref="OrchestratorSettings.PlanHeadKb"/>, within sane bounds.</summary>
    internal static int HeadBytesOf(OrchestratorSettings settings) =>
        Math.Clamp(settings.PlanHeadKb, OrchestratorSettings.MinPlanHeadKb, OrchestratorSettings.MaxPlanHeadKb) * 1024;

    /// <summary>How long a summary (and a status) may be, from <see cref="OrchestratorSettings.PlanSummaryMaxChars"/>, within sane bounds.</summary>
    internal static int SummaryCharsOf(OrchestratorSettings settings) =>
        Math.Clamp(settings.PlanSummaryMaxChars, OrchestratorSettings.MinPlanSummaryMaxChars, OrchestratorSettings.MaxPlanSummaryMaxChars);

    /// <summary>The workspace folder's own name ("MindAttic" for <c>D:\Projects\MindAttic</c>); the path itself for a drive root.</summary>
    internal static string WorkspaceName(string workspaceRoot)
    {
        try
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(workspaceRoot.Trim()));
            return name.Length == 0 ? workspaceRoot.Trim() : name;
        }
        catch (ArgumentException) { return workspaceRoot; }
    }

    /// <summary>The markdown files directly in <paramref name="dir"/>; none when it is missing or can't be listed.</summary>
    private static IReadOnlyList<string> Files(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return [];
            return [.. Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
                .Where(f => string.Equals(Path.GetExtension(f), ".md", StringComparison.OrdinalIgnoreCase))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException) { return []; }
    }

    /// <summary>The plan in <paramref name="file"/>; one named after the file alone when its text can't be read, and null when
    /// even the file itself is gone or out of reach.</summary>
    private static PlanEntry? TryRead(string file, string repo, int headBytes, int summaryChars)
    {
        var fallback = Path.GetFileNameWithoutExtension(file);
        DateTime written;
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists) return null;
            written = info.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException) { return null; }
        try
        {
            var (title, summary, status) = Parse(ReadHead(file, headBytes), fallback, summaryChars);
            return new PlanEntry(title, repo, summary, status, file, written);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return new PlanEntry(fallback, repo, "", null, file, written);
        }
    }

    /// <summary>The first <paramref name="headBytes"/> bytes of <paramref name="file"/> as text, read while another program may
    /// have it open; a file longer than that ends at its last whole line.</summary>
    internal static string ReadHead(string file, int headBytes)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[(int)Math.Min(headBytes, Math.Max(0, stream.Length))];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n <= 0) break;
            read += n;
        }
        var text = Encoding.UTF8.GetString(buffer, 0, read).TrimStart('\uFEFF');
        if (stream.Length <= read) return text;
        // Cut mid-file: the last line (and maybe its last character) is partial, so it's dropped.
        var lastBreak = text.LastIndexOf('\n');
        return lastBreak < 0 ? text : text[..lastBreak];
    }

    /// <summary>
    /// A plan's title, summary and status from its text. The title is the first <c># </c> heading, else
    /// <paramref name="fallbackTitle"/>. The status is what follows <c>Status:</c> on the first line that starts with it
    /// (and the rest of that paragraph, since such a line is often wrapped); null when there is none. The summary is the
    /// first other paragraph that is not a heading, a rule or a code block. A leading front matter block
    /// (<c>---</c> … <c>---</c>) is read for its status only. Summary and status are one line each, cut to
    /// <paramref name="summaryChars"/> at a word.
    /// </summary>
    internal static (string Title, string Summary, string? Status) Parse(string text, string fallbackTitle, int summaryChars)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string? title = null, summary = null, status = null;
        var i = 0;
        while (i < lines.Length && lines[i].Trim().Length == 0) i++;
        if (i < lines.Length && lines[i].Trim() == "---")
        {
            var end = Array.FindIndex(lines, i + 1, l => l.Trim() == "---");
            if (end > 0)
            {
                for (var f = i + 1; f < end && status is null; f++) status = StatusOf(lines[f].Trim());
                i = end + 1;
            }
        }
        var fenced = false;
        for (; i < lines.Length && (title is null || summary is null || status is null); i++)
        {
            var line = lines[i].Trim();
            if (IsFence(line)) { fenced = !fenced; continue; }
            if (fenced || line.Length == 0) continue;
            if (line[0] == '#')
            {
                if (title is null && line.StartsWith("# ", StringComparison.Ordinal))
                {
                    var heading = line[2..].Trim().TrimEnd('#').Trim();
                    if (heading.Length > 0) title = heading;
                }
                continue;
            }
            if (IsRule(line)) continue;
            // The paragraph this line starts: up to a blank line, a heading, a code fence or a status line.
            var paragraph = new StringBuilder(StatusOf(line) ?? line);
            var isStatus = StatusOf(line) is not null;
            while (i + 1 < lines.Length)
            {
                var next = lines[i + 1].Trim();
                if (next.Length == 0 || next[0] == '#' || IsFence(next) || StatusOf(next) is not null) break;
                paragraph.Append(' ').Append(next);
                i++;
            }
            if (isStatus) status ??= Clip(paragraph.ToString(), summaryChars);
            else summary ??= Clip(paragraph.ToString(), summaryChars);
        }
        return (title ?? fallbackTitle, summary ?? "", string.IsNullOrEmpty(status) ? null : status);
    }

    /// <summary>What follows <c>Status:</c> when <paramref name="line"/> starts with it, in any case and with or without
    /// markdown emphasis around the word (<c>**Status:** draft</c>); null for any other line.</summary>
    internal static string? StatusOf(string line)
    {
        var rest = line.TrimStart('*', '_');
        if (!rest.StartsWith("Status", StringComparison.OrdinalIgnoreCase)) return null;
        rest = rest["Status".Length..].TrimStart('*', '_');
        if (rest.Length == 0 || rest[0] != ':') return null;
        return rest[1..].TrimStart('*', '_', ' ', '\t').TrimEnd();
    }

    private static bool IsFence(string line) => line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal);

    /// <summary>A horizontal rule: three or more of the same of <c>- * _</c>, spaces allowed between.</summary>
    private static bool IsRule(string line)
    {
        var marks = line.Replace(" ", "");
        return marks.Length >= 3 && marks[0] is '-' or '*' or '_' && marks.All(c => c == marks[0]);
    }

    /// <summary><paramref name="text"/> on one line, at most <paramref name="max"/> characters: cut at a word and ended with
    /// an ellipsis when it is longer.</summary>
    internal static string Clip(string text, int max)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (max <= 0 || line.Length <= max) return line;
        var cut = line[..max];
        // Cut inside a word: back to the space before it, unless that would throw away more than half the line.
        var space = cut.LastIndexOf(' ');
        if (line[max] != ' ' && space >= max / 2) cut = cut[..space];
        return cut.TrimEnd(' ', ',', ';', ':') + "…";
    }
}
