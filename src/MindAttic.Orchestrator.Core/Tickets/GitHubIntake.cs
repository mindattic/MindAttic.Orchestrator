using System.Text.Json;
using System.Text.RegularExpressions;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>Something on GitHub that should be a ticket: its marker (GH#12, CI#987), type, summary and link.</summary>
public sealed record IntakeItem(string Marker, string Type, string Summary, string Url);

/// <summary>
/// Files GitHub's signals as tickets so they land in the queue: open issues labelled
/// <c>orchestrator</c>, and CI runs that failed on main/master in the last day. Each is filed once
/// (its marker goes in the summary) and nothing is written back to GitHub. Reads through the
/// <c>gh</c> CLI, so it uses whatever account <c>gh auth</c> has.
/// </summary>
public static partial class GitHubIntake
{
    public const string Label = "orchestrator";

    [GeneratedRegex(@"github\.com[:/](?<slug>[^/\s]+/[^/\s]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex Remote();

    public static string? Slug(string remote) => Remote().Match(remote.Trim()) is { Success: true } m ? m.Groups["slug"].Value : null;

    /// <summary>How long one <c>gh</c> call may run inside the daemon's tick before it's killed.</summary>
    internal static readonly TimeSpan GhTimeout = TimeSpan.FromMinutes(1);

    public static (int Code, string Output) Gh(params string[] args) => Gh(ProcessRunner.Default, GhTimeout, args);

    /// <summary><see cref="Gh(string[])"/> through <paramref name="runner"/>: a call past <paramref name="timeout"/> is killed and reads as a failure.</summary>
    internal static (int Code, string Output) Gh(IProcessRunner runner, TimeSpan timeout, params string[] args)
    {
        try
        {
            // It runs inside the daemon's tick: a network stall mustn't stop pacing.
            var r = runner.Run("gh", args, timeout);
            if (r.TimedOut)
            {
                OrchestratorLog.For("GitHubIntake").Warning("gh {Args} took over {Seconds:0}s; stopped it", string.Join(' ', args), timeout.TotalSeconds);
                return (-1, $"gh took over {timeout.TotalSeconds:0}s; stopped it");
            }
            return (r.ExitCode, r.ExitCode == 0 ? r.Output : r.Error);
        }
        catch (System.ComponentModel.Win32Exception) { return (-1, "the GitHub CLI (gh) isn't installed"); }
    }

    public static IReadOnlyList<IntakeItem> Find(string slug, Func<string[], (int Code, string Output)> gh, DateTime nowUtc)
    {
        var items = new List<IntakeItem>();
        // gh returns 30 unless told otherwise: issue 31 on was never filed (M.ORCH-T548).
        var (code, json) = gh(["issue", "list", "--repo", slug, "--label", Label, "--state", "open", "--limit", "1000", "--json", "number,title,labels,url"]);
        if (code == 0)
            foreach (var issue in Parse(json))
            {
                var labels = issue.TryGetProperty("labels", out var l) && l.ValueKind == JsonValueKind.Array
                    ? l.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "").ToList() : [];
                // Any case, as GitHub's own labels go ("Feature", "Enhancement"): only "bug" was, so a feature filed as a fix
                // was worked without your approval (M.ORCH-T443).
                bool Has(params string[] names) => labels.Any(x => names.Any(n => x.Equals(n, StringComparison.OrdinalIgnoreCase)));
                var type = Has("bug") ? "fix"
                    : Has("enhancement", "feature") ? "feature"
                    : Has("documentation", "docs") ? "docs"
                    : Has("chore") ? "chore" : "fix";
                var number = issue.GetProperty("number").GetInt32();
                items.Add(new IntakeItem($"GH#{number}", type, $"GH#{number}: {Str(issue, "title")}", Str(issue, "url")));
            }

        // Asked per main line: one --limit over every branch let feature-branch failures hide one on main (M.ORCH-T548).
        var seen = new HashSet<long>();
        foreach (var mainline in new[] { "main", "master" })
        {
            (code, json) = gh(["run", "list", "--repo", slug, "--branch", mainline, "--status", "failure", "--limit", "100", "--json", "databaseId,name,headBranch,createdAt,url"]);
            if (code != 0) continue;
            foreach (var run in Parse(json))
            {
                var branch = Str(run, "headBranch");
                if (branch is not ("main" or "master")) continue;
                if (!DateTime.TryParse(Str(run, "createdAt"), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var at) || nowUtc - at > TimeSpan.FromDays(1)) continue;
                var id = run.GetProperty("databaseId").GetInt64();
                if (!seen.Add(id)) continue;
                items.Add(new IntakeItem($"CI#{id}", "fix", $"CI#{id}: {Str(run, "name")} failed on {branch}", Str(run, "url")));
            }
        }
        return items;
    }

    /// <summary>Adds the items no ticket mentions yet (by marker), returning what was filed; the caller saves the file.</summary>
    public static IReadOnlyList<Ticket> FileNew(StoryFile stories, IEnumerable<IntakeItem> items)
    {
        var filed = new List<Ticket>();
        foreach (var item in items)
        {
            var marker = new Regex($@"{Regex.Escape(item.Marker)}\b");
            if (stories.Tickets.Any(t => marker.IsMatch(t.Summary))) continue;
            filed.Add(stories.Add(item.Type, TicketRules.Open, item.Summary.Replace('|', '/'), item.Url));
        }
        return filed;
    }

    /// <summary>Adds the items no ticket in <paramref name="repo"/> mentions yet (by marker) to the ticket database; with
    /// <paramref name="dryRun"/>, only says which it would add (ids unassigned), as the USER_STORIES.md path does.</summary>
    public static IReadOnlyList<Ticket> FileNew(MindAttic.Tickets.Data.TicketsDb db, string repo, IEnumerable<IntakeItem> items, bool dryRun = false)
    {
        var existing = db.Tickets(repo).Select(t => t.Ticket).ToList();
        var filed = new List<Ticket>();
        foreach (var item in items)
        {
            var marker = new Regex($@"{Regex.Escape(item.Marker)}\b");
            if (existing.Concat(filed).Any(t => marker.IsMatch(t.Summary))) continue;
            filed.Add(dryRun ? new Ticket("(new)", DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), item.Type, TicketRules.Open, item.Summary, item.Url)
                : db.Add(repo, item.Type, TicketRules.Open, item.Summary, item.Url));
        }
        return filed;
    }

    private static IEnumerable<JsonElement> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? [.. doc.RootElement.EnumerateArray().Select(e => e.Clone())] : [];
        }
        catch (JsonException) { return []; }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
