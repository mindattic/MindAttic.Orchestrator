using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// One ticket on the Roadmap (M.ORCH-T2382): its level (epic, feature, story, task; levels may skip), who has it, and how far
/// along it is. <see cref="DoneLeaves"/> of <see cref="TotalLeaves"/> are the leaf tasks under it (itself when it has none): a
/// closed one counts 1, one someone is working on the working credit, an open one 0; wont-do ones are left out of both.
/// <see cref="Description"/> is the ticket's markdown description, null when it has none (M.ORCH-T2394).
/// </summary>
public sealed record RoadmapNode(string Id, string Repo, string Name, string Summary, string? Description, string Level, string Status, string? Assignee,
    string? WorkingLogin, long Priority, IReadOnlyList<RoadmapNode> Children, double DoneLeaves, int TotalLeaves)
{
    /// <summary>Done leaves over all leaves, 0 to 1; a node with no counted leaf is done when it's closed.</summary>
    public double Fraction => TotalLeaves == 0 ? (Status == TicketRules.Closed ? 1 : 0) : DoneLeaves / TotalLeaves;

    /// <summary>Whoever is working on it now, else whoever it's assigned to.</summary>
    public string? Who => WorkingLogin ?? Assignee;

    /// <summary>Open leaf tasks under it (itself when it's an open leaf).</summary>
    public int OpenTasks => Children.Count == 0 ? (Status == TicketRules.Open ? 1 : 0) : Children.Sum(c => c.OpenTasks);
}

/// <summary>One repo on the Roadmap: its active milestone and that milestone's acceptance checks, and its top-level tickets.</summary>
public sealed record RoadmapRepo(string Repo, string? MilestoneId, string? Milestone, long MilestoneOrder, int ChecksPassed,
    int ChecksTotal, IReadOnlyList<RoadmapNode> Epics)
{
    public double DoneLeaves => Epics.Sum(e => e.DoneLeaves);
    public int TotalLeaves => Epics.Sum(e => e.TotalLeaves);
    public double Fraction => TotalLeaves == 0 ? 0 : DoneLeaves / TotalLeaves;
    public int OpenTasks => Epics.Sum(e => e.OpenTasks);
}

/// <summary>
/// The whole Roadmap (M.ORCH-T2382): every repo with open work, each open ticket under its <c>part-of</c> parents, with every
/// descendant and ancestor (closed or not) so the percents are right.
/// </summary>
public sealed record RoadmapTree(IReadOnlyList<RoadmapRepo> Repos)
{
    public static readonly RoadmapTree Empty = new([]);

    public double Fraction
    {
        get
        {
            var total = Repos.Sum(r => r.TotalLeaves);
            return total == 0 ? 0 : Repos.Sum(r => r.DoneLeaves) / total;
        }
    }

    /// <summary>Repos with an active milestone.</summary>
    public int ActiveMilestones => Repos.Count(r => r.MilestoneId is not null);

    /// <summary>
    /// Builds the tree from rows already read. <paramref name="parents"/> is ticket → its <c>part-of</c> targets, milestone
    /// targets already dropped. <paramref name="keptClosed"/> are closed tickets shown as though open (recently closed epics).
    /// <paramref name="descriptionOf"/> reads a ticket's description, once per ticket in the tree.
    /// </summary>
    public static RoadmapTree Build(IReadOnlyList<(string Repo, Ticket Ticket)> tickets,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parents, IReadOnlyDictionary<string, string> levels,
        IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, long> priorities,
        IReadOnlyDictionary<string, TicketWork> work, IReadOnlySet<string> keptClosed,
        IReadOnlyDictionary<string, (string Id, string Name, long Order, int Passed, int Total)> milestones, double workingCredit,
        Func<string, string?>? descriptionOf = null)
    {
        var byId = new Dictionary<string, (string Repo, Ticket Ticket)>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tickets)
            if (t.Ticket.Status != TicketRules.WontDo) byId.TryAdd(t.Ticket.Id, t);
        var children = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (child, ps) in parents)
        {
            if (!byId.ContainsKey(child)) continue;
            foreach (var p in ps.Where(byId.ContainsKey))
            {
                if (!children.TryGetValue(p, out var list)) children[p] = list = [];
                list.Add(child);
            }
        }

        // Open tickets and kept closed ones, then their ancestors, then everything under those.
        var seeds = byId.Values.Where(t => t.Ticket.Status == TicketRules.Open || keptClosed.Contains(t.Ticket.Id))
            .Select(t => t.Ticket.Id).ToList();
        var up = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>(seeds);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!up.Add(id)) continue;
            if (parents.TryGetValue(id, out var ps))
                foreach (var p in ps.Where(byId.ContainsKey)) stack.Push(p);
        }
        var roots = up.Where(id => !parents.TryGetValue(id, out var ps) || !ps.Any(byId.ContainsKey)).ToList();

        RoadmapNode Node(string id, HashSet<string> path)
        {
            var (repo, ticket) = byId[id];
            path.Add(id);
            var kids = children.TryGetValue(id, out var list)
                ? [.. list.Where(c => !path.Contains(c)).Distinct(StringComparer.OrdinalIgnoreCase).Select(c => Node(c, path))
                    .OrderByDescending(n => n.Priority).ThenBy(n => TicketRules.ParseNumber(n.Id) ?? int.MaxValue)]
                : new List<RoadmapNode>();
            path.Remove(id);
            work.TryGetValue(id, out var w);
            var login = string.IsNullOrWhiteSpace(w?.Login) ? null : w!.Login;
            double done;
            int total;
            if (kids.Count == 0 || kids.Sum(k => k.TotalLeaves) == 0)
            {
                total = 1;
                done = ticket.Status == TicketRules.Closed ? 1 : login is not null ? workingCredit : 0;
            }
            else
            {
                total = kids.Sum(k => k.TotalLeaves);
                done = kids.Sum(k => k.DoneLeaves);
            }
            var summary = ticket.Summary.Split('\n', 2)[0].Trim();
            return new RoadmapNode(id, repo, names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name) ? name : summary,
                summary, descriptionOf?.Invoke(id), levels.TryGetValue(id, out var level) ? level : "task", ticket.Status, TicketsDb.NormalizeEmail(w?.Assignee),
                login, priorities.TryGetValue(id, out var pr) ? pr : 0, kids, done, total);
        }

        var byRepo = roots.Select(id => Node(id, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .ToLookup(n => n.Repo, StringComparer.OrdinalIgnoreCase);
        var repoNames = byRepo.Select(g => g.Key).Union(milestones.Keys, StringComparer.OrdinalIgnoreCase);
        return new([.. repoNames.Select(r =>
            {
                var m = milestones.TryGetValue(r, out var found) ? found : default((string Id, string Name, long Order, int Passed, int Total)?);
                return new RoadmapRepo(r, m?.Id, m?.Name, m?.Order ?? long.MaxValue, m?.Passed ?? 0, m?.Total ?? 0,
                    [.. byRepo[r].OrderByDescending(n => n.Priority).ThenBy(n => TicketRules.ParseNumber(n.Id) ?? int.MaxValue)]);
            })
            .OrderBy(r => r.MilestoneOrder).ThenBy(r => r.Repo, StringComparer.OrdinalIgnoreCase)]);
    }
}
