using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>One session that worked on a ticket, boiled down: when, what it cost, what was asked and how it ended.</summary>
public sealed record ReplaySession(string File, DateTime StartUtc, DateTime EndUtc, long Fresh, IReadOnlyList<string> Prompts, string LastReply);

/// <summary>
/// Everything about a ticket without digging through Launcher history: the sessions dispatched on it
/// (found by the dispatch prompt near the top of a transcript, or by the ticket's worktree folder) and
/// the commits that mention its ID.
/// </summary>
public static partial class TicketReplay
{
    private const int HeadBytes = 64 * 1024;

    public static IReadOnlyList<ReplaySession> Sessions(string ticketId, string projectsDir)
    {
        if (!Directory.Exists(projectsDir)) return [];
        var marker = $"{TicketAttribution.PromptMarker}{ticketId} ";
        // Claude names a project folder after its path with every non-alphanumeric turned into '-',
        // so worktree ...\M.ORCH-T1 is folder ...-M-ORCH-T1: compare both sides that way.
        var worktreeFolder = "-" + FolderSafe(ticketId);
        var found = new List<ReplaySession>();
        foreach (var file in Directory.EnumerateFiles(projectsDir, "*.jsonl", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(file)) ?? "";
            var inWorktree = folder.Contains("worktrees", StringComparison.OrdinalIgnoreCase) && FolderSafe(folder).EndsWith(worktreeFolder, StringComparison.Ordinal);
            if (!inWorktree && !(Head(file) is var head && head.Contains(marker, StringComparison.Ordinal) && AskedFor(head, ticketId))) continue;
            if (Summarize(file) is { } s) found.Add(s);
        }
        return [.. found.OrderBy(s => s.StartUtc)];
    }

    /// <summary>Whether a prompt typed into the session (a user line's own text, not a tool's output or a tool call's input)
    /// asks for <paramref name="ticketId"/>: an /autopilot CLI prints and hands out many tickets' prompts, but works none itself.</summary>
    internal static bool AskedFor(string head, string ticketId)
    {
        foreach (var line in head.Split('\n'))
        {
            if (!line.Contains(TicketAttribution.PromptMarker, StringComparison.Ordinal)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var t) || t.GetString() != "user" ||
                    !root.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("content", out var c))
                    continue;
                var texts = c.ValueKind == JsonValueKind.String ? [c.GetString() ?? ""]
                    : c.ValueKind == JsonValueKind.Array ? c.EnumerateArray()
                        .Where(b => b.ValueKind == JsonValueKind.Object && b.TryGetProperty("type", out var bt) && bt.GetString() == "text" && b.TryGetProperty("text", out _))
                        .Select(b => b.GetProperty("text").GetString() ?? "").ToList()
                    : new List<string>();
                if (texts.Any(x => TicketAttribution.FromPrompt(x) is { } asked && asked.Id.Equals(ticketId, StringComparison.OrdinalIgnoreCase))) return true;
            }
            catch (JsonException) { } // the head's last line can be cut short
        }
        return false;
    }

    private static string FolderSafe(string name) => NonAlphanumeric().Replace(name, "-").ToUpperInvariant();

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();

    private static string Head(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[Math.Min(HeadBytes, stream.Length)];
            var read = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
    }

    private static ReplaySession? Summarize(string file)
    {
        DateTime? start = null, end = null;
        long fresh = 0;
        var prompts = new List<string>();
        var last = "";
        var counted = new Dictionary<string, TokenUsage>(StringComparer.Ordinal);
        // Counted as the budget counts it (M.ORCH-T379): a fork's copy of the transcript it was started from isn't its own, and
        // a response is counted at the most any of its lines says (a subagent's grow their output_tokens line by line).
        var forkCall = TranscriptReader.SubagentId(file) is null ? null : TranscriptReader.ForkToolUseId(file);
        var prologue = forkCall is not null;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(line); } catch (JsonException) { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) continue;
                    if (root.TryGetProperty("type", out var lineType) && lineType.ValueKind == JsonValueKind.String && lineType.GetString() == "fork-context-ref")
                    {
                        prologue = true;
                        continue;
                    }
                    if (prologue)
                    {
                        if (!TranscriptReader.EndsForkPrologue(root, forkCall)) continue;
                        prologue = false;
                    }
                    if (TranscriptReader.CopiedFromAnotherSession(root)) continue;
                    if (!root.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object) continue;
                    if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String && DateTime.TryParse(ts.GetString(), null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var at))
                    {
                        start ??= at;
                        end = at;
                    }
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var text = Text(m);
                    if (type == "user" && text.Length > 0 && !text.StartsWith('<')) prompts.Add(Trim(text, 300));
                    if (type == "assistant")
                    {
                        if (text.Length > 0) last = text;
                        if (m.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
                        {
                            var used = new TokenUsage(Num(u, "input_tokens"), Num(u, "cache_creation_input_tokens"), Num(u, "cache_read_input_tokens"), Num(u, "output_tokens"));
                            if (m.TryGetProperty("id", out var id) && id.GetString() is { } key)
                            {
                                var before = counted.TryGetValue(key, out var c) ? c : default;
                                used = TranscriptReader.TokenUsageGrowth(before, used);
                                counted[key] = before + used;
                            }
                            fresh += used.Input + used.CacheCreation + used.Output;
                        }
                    }
                }
            }
        }
        catch (IOException) { return null; }
        return start is null ? null : new ReplaySession(file, start.Value, end!.Value, fresh, prompts, Trim(last, 1500));
    }

    /// <summary>Commits in the repo whose message mentions the ticket, newest first, with what they touched (or the full diff).</summary>
    public static string Commits(string ticketId, string repoPath, bool diff)
    {
        if (!Directory.Exists(repoPath)) return "";
        var wholeWord = $"(^|[^A-Za-z0-9]){Regex.Escape(ticketId)}([^A-Za-z0-9]|$)";
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-C", repoPath, "log", "--all", "--extended-regexp", $"--grep={wholeWord}", "--date=format-local:%Y-%m-%d %H:%M",
                     "--format=%n%h %ad %an%n    %s", diff ? "--patch" : "--stat" })
            psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEndAsync();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return output.Result.Trim();
        }
        catch (System.ComponentModel.Win32Exception) { return ""; }
    }

    /// <summary>The whole replay as plain text, for a window that just shows it. With <paramref name="ticket"/>,
    /// leads with its row (type, status, summary, ref) before the sessions and commits.</summary>
    public static string PlainReport(string ticketId, string repoPath, string projectsDir, MindAttic.Workflow.Tickets.Ticket? ticket = null)
    {
        var sb = new StringBuilder();
        if (ticket is { } t)
        {
            sb.AppendLine($"{t.Id} · {t.Type} · {t.Status} · opened {t.Date}");
            sb.AppendLine(t.Summary);
            if (t.Ref.Trim() is { Length: > 0 } r && r != "—") sb.AppendLine($"Ref: {r}");
            sb.AppendLine();
        }
        var sessions = Sessions(ticketId, projectsDir);
        sb.AppendLine(sessions.Count == 0 ? "No dispatched sessions found (only sessions the orchestrator started are tied to a ticket)."
            : $"{sessions.Count} session(s), {Formatting.Tokens(sessions.Sum(s => s.Fresh))} uncached");
        foreach (var s in sessions)
        {
            sb.AppendLine().AppendLine($"{s.StartUtc.ToLocalTime():ddd MM-dd h:mm tt} - {s.EndUtc.ToLocalTime():h:mm tt} · {Formatting.Tokens(s.Fresh)} · {s.File}");
            foreach (var p in s.Prompts) sb.AppendLine($"  > {p}");
            if (s.LastReply.Length > 0) sb.AppendLine($"  last reply: {s.LastReply}");
        }
        var commits = Commits(ticketId, repoPath, diff: true);
        sb.AppendLine().AppendLine(commits.Length == 0 ? "No commits mention it." : "Commits").AppendLine(commits);
        return sb.ToString();
    }

    private static string Text(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var c)) return "";
        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind != JsonValueKind.Array) return "";
        var sb = new StringBuilder();
        foreach (var b in c.EnumerateArray())
            if (b.ValueKind == JsonValueKind.Object && b.TryGetProperty("type", out var bt) && bt.GetString() == "text" && b.TryGetProperty("text", out var x))
                sb.Append(x.GetString());
        return sb.ToString().Trim();
    }

    private static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}
