using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>What the milestone steward does about one thing it found.</summary>
public enum StewardActionKind
{
    /// <summary>File a ticket for an acceptance check nothing open covers, and put it forward for the milestone.</summary>
    Propose,
    /// <summary>Reopen a closed ticket of the milestone that a later, still open ticket says came back.</summary>
    Reopen,
    /// <summary>Only tell the owner: nothing is changed.</summary>
    Report,
}

/// <summary>
/// One action of the steward's plan. <see cref="Text"/> says it in a sentence. A proposal carries the ticket to file
/// (<see cref="Repo"/>, <see cref="Type"/>, <see cref="Summary"/>, <see cref="Description"/>) and the <see cref="Marker"/> label
/// it's found again by; a reopen names the closed <see cref="TicketId"/> and the later ticket that says it came back
/// (<see cref="Because"/>); a report names the ticket it's about, when there is one.
/// </summary>
public sealed record StewardAction(StewardActionKind Kind, string MilestoneId, string Text)
{
    /// <summary>The acceptance check's number, when the action is about one.</summary>
    public long? Check { get; init; }
    /// <summary>The repo the proposed ticket is filed in.</summary>
    public string? Repo { get; init; }
    /// <summary>The proposed ticket's type: <c>fix</c> for a build that fails, else <c>feature</c> or <c>chore</c>.</summary>
    public string? Type { get; init; }
    /// <summary>The proposed ticket's one-line summary.</summary>
    public string? Summary { get; init; }
    /// <summary>The proposed ticket's description: the milestone, the check and its evidence.</summary>
    public string? Description { get; init; }
    /// <summary>The label that marks the proposed ticket as the one for this check.</summary>
    public string? Marker { get; init; }
    /// <summary>The ticket a reopen or a report is about.</summary>
    public string? TicketId { get; init; }
    /// <summary>The later ticket that says <see cref="TicketId"/> came back.</summary>
    public string? Because { get; init; }
}

/// <summary>
/// The milestone steward's plan (M.ORCH-T1686, roadmap Phase 9), worked out from what the ticket database says and nothing
/// else: no agent is started and nothing is read from a repo. For each active or accepting milestone:
/// <list type="bullet">
/// <item>a <c>build</c> or <c>exists</c> check recorded as failed (or as unknown with evidence recorded for it) that no open
/// ticket covers gets one proposed ticket, in the first repo the check names (else the milestone's first). A ticket covers a
/// check when it carries the check's <see cref="Marker"/> label, which the steward puts on every ticket it files. A build that
/// fails is a bug (<c>fix</c>); anything else is a <c>feature</c> or <c>chore</c> that waits for the owner. A check not yet
/// evaluated, a passed one, and every <c>harness</c> and <c>signoff</c> check get nothing;</item>
/// <item>a check whose own ticket was closed or declined while it still fails is reported, never proposed again;</item>
/// <item>of the closed tickets on the milestone that <see cref="TicketHygiene"/> doubts, one that a later, still open ticket
/// says came back is reopened; one whose later ticket is closed too, and one that only cites no Ref, are reported.</item>
/// </list>
/// Planning twice from the same database gives the same plan, and once a proposal is filed its marker covers the check, so
/// applying a plan and planning again proposes nothing new.
/// </summary>
public static class MilestoneSteward
{
    /// <summary>The label on every ticket the steward files.</summary>
    public const string Label = "steward";

    /// <summary>Who the steward's proposals and notes are by.</summary>
    public const string By = "milestone steward";

    /// <summary>What <see cref="TicketHygiene"/> says of a closed ticket a later one points back to, after that ticket's ID.</summary>
    public const string CameBack = " says it came back";

    private const int SummaryLength = 200;

    /// <summary>A milestone as the steward sees it: its checks and the tickets on it (directly or under an epic).</summary>
    public sealed record Scope(Milestone Milestone, IReadOnlyList<MilestoneCheck> Checks, IReadOnlyList<string> Tickets);

    /// <summary>The label that marks a ticket as the one filed for check <paramref name="check"/> of <paramref name="milestoneId"/>.</summary>
    public static string Marker(string milestoneId, long check) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"milestone-check:{milestoneId.Trim().ToLowerInvariant()}/{check}");

    /// <summary>
    /// The plan for <paramref name="milestones"/> (those not active or accepting are passed over): <paramref name="statuses"/>
    /// is every ticket's status and <paramref name="labels"/> every labelled ticket's labels, both by ticket ID;
    /// <paramref name="findings"/> is what <see cref="TicketHygiene.Check(IEnumerable{ValueTuple{string, Ticket}})"/> reports.
    /// </summary>
    public static IReadOnlyList<StewardAction> Plan(IEnumerable<Scope> milestones, IReadOnlyDictionary<string, string> statuses,
        IReadOnlyDictionary<string, IReadOnlyList<string>> labels, IReadOnlyList<HygieneFinding> findings)
    {
        var plan = new List<StewardAction>();
        var reopened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scope in milestones.Where(s => s.Milestone.IsLocked))
        {
            var m = scope.Milestone;
            foreach (var check in scope.Checks.OrderBy(c => c.Number))
                if (Unmet(check) && ForCheck(m, check, statuses, labels) is { } action) plan.Add(action);

            var closed = scope.Tickets.Where(t => statuses.GetValueOrDefault(t) == TicketRules.Closed).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var f in findings.Where(f => closed.Contains(f.TicketId)))
                if (ForFinding(m, f, statuses, reopened) is { } action) plan.Add(action);
        }
        return plan;
    }

    /// <summary>Whether <paramref name="check"/> is one the steward acts on: a build or exists check that failed, or that is
    /// unknown with evidence recorded for it. One never evaluated isn't, and neither is any harness or sign-off check.</summary>
    public static bool Unmet(MilestoneCheck check) =>
        check.Kind is MilestoneCheckKinds.Build or MilestoneCheckKinds.Exists
        && (check.Result == MilestoneCheckResults.Fail
            || (check.Result == MilestoneCheckResults.Unknown && !string.IsNullOrWhiteSpace(check.Evidence)));

    /// <summary>The type of the ticket proposed for <paramref name="check"/>: a build that fails is a bug; a missing package is
    /// packaging (a chore), any other missing artifact a feature; a check with no result is a chore.</summary>
    public static string TypeFor(MilestoneCheck check) => check switch
    {
        { Kind: MilestoneCheckKinds.Build, Result: MilestoneCheckResults.Fail } => "fix",
        { Kind: MilestoneCheckKinds.Exists, Result: MilestoneCheckResults.Fail } when !MilestoneAcceptance.ParseTarget(check.Target ?? "").IsPackage => "feature",
        _ => "chore",
    };

    private static StewardAction? ForCheck(Milestone m, MilestoneCheck check, IReadOnlyDictionary<string, string> statuses,
        IReadOnlyDictionary<string, IReadOnlyList<string>> labels)
    {
        var marker = Marker(m.Id, check.Number);
        var said = check.Result == MilestoneCheckResults.Fail ? "fails" : "has no result";
        var filed = labels.Where(l => l.Value.Contains(marker, StringComparer.OrdinalIgnoreCase) && statuses.ContainsKey(l.Key))
            .Select(l => l.Key).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        if (filed.Any(id => statuses[id] == TicketRules.Open)) return null;
        if (filed.Count > 0)
        {
            var last = filed[^1];
            var how = statuses[last] == TicketRules.WontDo ? "declined" : "closed";
            return new StewardAction(StewardActionKind.Report, m.Id,
                $"{last} was filed for check #{check.Number} of {m.Id} and is {how}, but the check still {said}: {Evidence(check)} It is not proposed again.")
            { Check = check.Number, TicketId = last, Marker = marker };
        }
        if ((check.Repos.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)) ?? m.Repos.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))) is not { } repo)
            return new StewardAction(StewardActionKind.Report, m.Id,
                $"Check #{check.Number} of {m.Id} {said}, but neither the check nor the milestone names a repo to file a ticket in: {Evidence(check)}")
            { Check = check.Number, Marker = marker };

        var type = TypeFor(check);
        var summary = Clip($"{m.Id} check #{check.Number} ({check.Kind}) {said}: {OneLine(check.Description)}", SummaryLength);
        return new StewardAction(StewardActionKind.Propose, m.Id,
            $"Propose a {type} ticket in {repo} for check #{check.Number} of {m.Id} ({check.Kind}), which {said}: {Evidence(check)}")
        {
            Check = check.Number, Repo = repo, Type = type, Summary = summary, Marker = marker,
            Description = Describe(m, check, repo, type),
        };
    }

    private static StewardAction? ForFinding(Milestone m, HygieneFinding f, IReadOnlyDictionary<string, string> statuses, HashSet<string> reopened)
    {
        var at = f.Why.IndexOf(CameBack, StringComparison.Ordinal);
        if (at <= 0)
            return new StewardAction(StewardActionKind.Report, m.Id, $"{f.TicketId} on {m.Id} is closed without evidence ({f.Why}): \"{OneLine(f.Summary)}\"")
            { TicketId = f.TicketId };

        var later = f.Why[..at].Trim();
        if (statuses.GetValueOrDefault(later) != TicketRules.Open)
            return new StewardAction(StewardActionKind.Report, m.Id,
                $"{f.TicketId} on {m.Id} is closed and {later} says it came back, but {later} isn't open any more, so it is not reopened: \"{OneLine(f.Summary)}\"")
            { TicketId = f.TicketId, Because = later };
        if (!reopened.Add(f.TicketId)) return null;
        return new StewardAction(StewardActionKind.Reopen, m.Id,
            $"Reopen {f.TicketId} on {m.Id}: {later}, which is open, says it came back. \"{OneLine(f.Summary)}\"")
        { TicketId = f.TicketId, Because = later };
    }

    private static string Describe(Milestone m, MilestoneCheck check, string repo, string type)
    {
        var others = check.Repos.Where(r => !r.Equals(repo, StringComparison.OrdinalIgnoreCase)).ToList();
        var lines = new List<string>
        {
            $"Filed by the milestone steward for acceptance check #{check.Number} of {m.Id} ({m.Name}).",
            "",
            $"- Check: {check.Kind}: {OneLine(check.Description)}",
            $"- Result: {check.Result}",
        };
        if (!string.IsNullOrWhiteSpace(check.Target)) lines.Add($"- Target: {check.Target.Trim()}");
        if (others.Count > 0) lines.Add($"- The check also names {string.Join(", ", others)}; this ticket is filed in {repo}, the first repo it names.");
        lines.Add($"- Evidence: {Evidence(check)}");
        lines.Add("");
        lines.Add(type == "fix"
            ? $"A build that fails on the main line is a bug, so this is an open fix. It waits for the owner's approval to join {m.Id}."
            : $"This is a proposal: it is not worked until the owner approves it, and it waits for the owner's approval to join {m.Id}.");
        return string.Join('\n', lines);
    }

    private static string Evidence(MilestoneCheck check) =>
        string.IsNullOrWhiteSpace(check.Evidence) ? "No evidence is recorded." : Clip(OneLine(check.Evidence), 600);

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Clip(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "...";
}
