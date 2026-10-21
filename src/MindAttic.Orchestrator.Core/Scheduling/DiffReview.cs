using System.Diagnostics;
using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// One Opus review of a Sonnet ticket's diff before it lands (M.ORCH-T563): when the diff changes more than
/// <see cref="OrchestratorSettings.ReviewDiffLines"/> lines, or touches a file in <see cref="OrchestratorSettings.RiskyFiles"/>
/// (the gates, pacing, git landing). The reviewer gets the diff and the ticket's summary only, with no tools, so it never
/// re-reads the repo; it's a headless <c>claude -p</c> on <see cref="ModelChoice.OpusId"/> whose prompt names the ticket, so its
/// cost is the ticket's. <c>orchestrator review</c> runs it from the ticket's worktree, as the Sonnet subagent's prompt says to.
/// </summary>
public static class DiffReview
{
    /// <summary>One file of <c>git diff --numstat</c>: lines added and removed (0 and 0 for a binary file).</summary>
    public sealed record FileChange(string Path, int Added, int Removed);

    /// <summary>Whether the diff gets its review, the lines it changes, the risky files it touches, and why, in a few words.</summary>
    public sealed record Verdict(bool Review, int Lines, IReadOnlyList<string> Risky, string Why);

    /// <summary>The most diff text the reviewer is sent; past it the rest is cut, and it's told so.</summary>
    public const int MaxDiffChars = 400_000;

    /// <summary>How long the review may run.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    /// <summary><c>git diff --numstat</c>'s lines as file changes.</summary>
    public static IReadOnlyList<FileChange> ParseNumstat(string text) =>
        [.. text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\t', 3))
            .Where(p => p.Length == 3 && p[2].Length > 0)
            .Select(p => new FileChange(p[2], Count(p[0]), Count(p[1])))];

    private static int Count(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;

    /// <summary>Whether <paramref name="path"/> (as git prints it, a rename's either side) is one of <paramref name="patterns"/>.</summary>
    public static bool IsRisky(string path, IEnumerable<string> patterns)
    {
        // A rename prints as "old => new" or "dir/{old => new}/file": either side counts.
        var sides = path.Contains(" => ") ? Sides(path) : [path];
        return patterns.Where(p => !string.IsNullOrWhiteSpace(p)).Any(p => sides.Any(s =>
            System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(p.Trim().Replace('\\', '/'), s.Replace('\\', '/'), ignoreCase: true)));
    }

    private static string[] Sides(string path)
    {
        var open = path.IndexOf('{');
        var close = path.IndexOf('}');
        if (open >= 0 && close > open)
        {
            var (head, tail) = (path[..open], path[(close + 1)..]);
            var parts = path[(open + 1)..close].Split(" => ");
            return [.. parts.Select(p => (head + p + tail).Replace("//", "/"))];
        }
        return path.Split(" => ");
    }

    /// <summary>Whether a ticket worked on <paramref name="model"/> whose diff is <paramref name="changes"/> gets its review.</summary>
    public static Verdict Judge(IReadOnlyList<FileChange> changes, OrchestratorSettings settings, string model)
    {
        var lines = changes.Sum(c => c.Added + c.Removed);
        if (model != ModelChoice.Sonnet) return new(false, lines, [], $"an {Capital(model)} ticket: only Sonnet tickets are reviewed");
        var risky = changes.Where(c => IsRisky(c.Path, settings.RiskyFiles)).Select(c => c.Path).ToList();
        var limit = settings.ReviewDiffLines;
        var big = limit >= 0 && lines > limit;
        if (risky.Count > 0)
            return new(true, lines, risky, $"it touches risky files ({string.Join(", ", risky.Take(5))}{(risky.Count > 5 ? ", ..." : "")})" +
                (big ? $" and changes {lines} lines, over the {limit}-line limit" : ""));
        if (big) return new(true, lines, risky, $"it changes {lines} lines, over the {limit}-line limit");
        return new(false, lines, risky, limit >= 0
            ? $"it changes {lines} lines, within the {limit}-line limit, and no risky files"
            : "no risky files (no line limit set)");
    }

    private static string Capital(string model) => model.Length == 0 ? model : char.ToUpperInvariant(model[0]) + model[1..];

    /// <summary>What the reviewer is told: the ticket, and the diff alone. It opens as every dispatch does, so the session is the
    /// ticket's (<see cref="Activity.TicketAttribution"/>) and its cost is charged to it.</summary>
    public static string Prompt(string ticketId, string type, string summary, string diff)
    {
        var cut = diff.Length > MaxDiffChars;
        return $"Work on ticket {ticketId} ({type}): review, once, the diff a Sonnet subagent made for it, before it lands. " +
            $"The ticket: {summary}\n\n" +
            "You have only this diff and no tools: don't ask for files or try to read the repo; judge what's here. Report only what must " +
            "change before it lands: bugs, wrong or missing behaviour the ticket asks for, broken edge cases, and anything risky in the " +
            "gates, pacing or git landing. For each, give the file, the line or hunk, what's wrong and the fix, most important first. " +
            "Skip style and taste. If nothing must change, answer with the single word LGTM." +
            (cut ? $"\n\nThe diff is long; only its first {MaxDiffChars:N0} characters are below, so say what you couldn't see." : "") +
            "\n\n```diff\n" + (cut ? diff[..MaxDiffChars] : diff) + "\n```\n";
    }

    /// <summary>The headless Opus run: <paramref name="prompt"/> on stdin, no tools, no MCP servers, from
    /// <paramref name="directory"/>. Returns whether it ran, and its answer (or why it couldn't).</summary>
    public static (bool Ok, string Output) RunClaude(string prompt, string directory)
    {
        var promptPath = Path.Combine(Path.GetTempPath(), $"orchestrator-review-{Environment.ProcessId}-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(promptPath, prompt);
            // cmd's own quoting, as BackgroundAgent runs claude: /s strips the outer pair and keeps the inner ones as written.
            var psi = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{CommandLine(promptPath)}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directory,
            };
            // Not the /allocate CLI's process: its hooks resolve the review's own claude.exe, so a gate never takes it for that CLI.
            psi.Environment.Remove("MINDATTIC_CLI_PID");
            using var p = Process.Start(psi);
            if (p is null) return (false, "couldn't start claude");
            var output = p.StandardOutput.ReadToEndAsync();
            var error = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(Timeout))
            {
                try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException) { }
                return (false, $"the review ran past {Timeout.TotalMinutes:0} minutes and was stopped");
            }
            Task.WaitAll([output, error], TimeSpan.FromSeconds(10));
            var said = output.IsCompletedSuccessfully ? output.Result.Trim() : "";
            return p.ExitCode == 0 && said.Length > 0 ? (true, said)
                : (false, $"claude exited {p.ExitCode}: {(said.Length > 0 ? said : error.IsCompletedSuccessfully ? error.Result.Trim() : "")}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (false, ex.Message);
        }
        finally
        {
            try { File.Delete(promptPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>What <c>orchestrator review</c> says, and whether the diff had its review now.</summary>
    public sealed record Outcome(string Text, bool Reviewed, Verdict? Verdict = null);

    /// <summary>The words a review that finds nothing answers with.</summary>
    public const string Clean = "LGTM";

    /// <summary>
    /// <c>orchestrator review</c>, run from ticket <paramref name="ticketId"/>'s worktree (<paramref name="directory"/>): judges the
    /// commits on it past <c>origin/&lt;main line&gt;</c> and, for a Sonnet ticket whose diff needs it, runs its one Opus review and
    /// records that on the ticket's claim, so it isn't run twice. The model is the one the claim recorded, else the one
    /// <see cref="ModelChoice"/> picks for <paramref name="ticket"/>. <paramref name="force"/> reviews any diff, however small and
    /// whatever the model, and even a second time; <paramref name="dryRun"/> only judges.
    /// </summary>
    public static Outcome Review(JsonStore store, OrchestratorSettings settings, string directory, string ticketId, MindAttic.Tickets.Log.Ticket? ticket,
        string? repo, bool force, bool dryRun, DateTime nowUtc,
        Func<string, string[], (int Code, string Output)>? git = null, Func<string, (bool Ok, string Output)>? claude = null,
        Func<string, string>? mainlineOf = null)
    {
        git ??= (dir, args) => Dispatcher.Git(dir, args);
        mainlineOf ??= Dispatcher.Mainline;
        var (topCode, topText) = git(directory, ["rev-parse", "--show-toplevel"]);
        if (topCode != 0) return new($"{directory} isn't in a git checkout: run it from {ticketId}'s worktree.", false);
        var top = Path.GetFullPath(topText.Trim());
        claude ??= prompt => RunClaude(prompt, top);

        var claimed = store.LoadAssignments().FirstOrDefault(a => a.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase));
        if (claimed?.ReviewedUtc is { } at && !force)
            return new($"{ticketId} already had its one Opus review ({at.ToLocalTime():MMM d HH:mm}): land it.", false);
        var model = ModelChoice.Parse(claimed?.Model) is { } kept and not ModelChoice.Auto ? kept
            : ticket is not null ? ModelChoice.For(ticket, repo ?? claimed?.Repo ?? "", settings).Model
            : ModelChoice.Sonnet;

        var against = $"origin/{mainlineOf(top)}";
        var (statCode, stat) = git(top, ["diff", "--numstat", $"{against}...HEAD"]);
        if (statCode != 0) return new($"Couldn't diff {ticketId}'s commits against {against}: {stat.Trim()}", false);
        var verdict = Judge(ParseNumstat(stat), settings, force ? ModelChoice.Sonnet : model);
        if (!verdict.Review) return new($"No Opus review needed for {ticketId}: {verdict.Why}. Land it.", false, verdict);
        if (dryRun) return new($"{ticketId}'s diff gets its one Opus review: {verdict.Why} (not run: --dry-run).", false, verdict);

        var (diffCode, diff) = git(top, ["diff", $"{against}...HEAD"]);
        if (diffCode != 0) return new($"Couldn't diff {ticketId}'s commits against {against}: {diff.Trim()}", false, verdict);
        var (ok, said) = claude(Prompt(ticketId, ticket?.Type ?? "fix", ticket?.Summary ?? "(its summary isn't known here)", diff));
        if (!ok) return new($"{ticketId}'s Opus review couldn't run ({said}). Land it without one, and say so when you report.", false, verdict);
        store.UpdateAssignments(all =>
        {
            var i = all.FindIndex(a => a.TicketId.Equals(ticketId, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) all[i] = all[i] with { ReviewedUtc = nowUtc };
        });
        var clean = said.Trim().TrimEnd('.').Equals(Clean, StringComparison.OrdinalIgnoreCase);
        return new($"Opus review of {ticketId}'s diff ({verdict.Why}):\n{said.Trim()}\n\n" + (clean
            ? "Nothing to change: land it."
            : "Fix what's really wrong above, rerun the tests and commit, then land it. Don't run the review again."), true, verdict);
    }

    /// <summary>The command <see cref="RunClaude"/> runs under <c>cmd /c</c>.</summary>
    internal static string CommandLine(string promptPath) =>
        $"claude --model {ModelChoice.OpusId} --tools \"\" --strict-mcp-config -p < \"{promptPath}\"";
}
