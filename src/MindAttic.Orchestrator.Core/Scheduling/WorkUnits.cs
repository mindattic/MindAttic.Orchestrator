using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// One task, one unit of work (M.ORCH-T1905, docs/WORK_UNITS.md): its size in uncached tokens is the
/// <see cref="OrchestratorSettings.UnitTokenBudget"/> setting.
/// </summary>
public static class WorkUnits
{
    /// <summary>The size of one task in uncached tokens, clamped to the setting's bounds.</summary>
    public static long Budget(OrchestratorSettings s) =>
        Math.Clamp(s?.UnitTokenBudget ?? OrchestratorSettings.DefaultUnitTokenBudget,
            OrchestratorSettings.MinUnitTokenBudget, OrchestratorSettings.MaxUnitTokenBudget);

    /// <summary>A token count for a prompt or hook, like "80k".</summary>
    public static string Size(long tokens) => Formatting.Tokens(tokens);

    /// <summary>Task costs needed before <see cref="UnitReport.Suggested"/> is given (M.ORCH-T1908).</summary>
    public const int MinSuggestBasis = 10;

    /// <summary>
    /// What work units actually cost against <paramref name="budget"/> (M.ORCH-T1908): tickets with a known cost, split into
    /// tasks (<paramref name="isTask"/>) and whole-ticket runs, with a suggested budget from the p75 of task costs within the
    /// setting's bounds. Only a suggestion: the owner sets the number.
    /// </summary>
    public static UnitReport Report(TicketCosts costs, Func<string, bool> isTask, long budget) =>
        Report(costs.Tickets, isTask, budget);

    /// <summary>The report over <paramref name="tickets"/> (the costed tickets of a period).</summary>
    public static UnitReport Report(IEnumerable<TicketCost> tickets, Func<string, bool> isTask, long budget)
    {
        var known = tickets.Where(t => t.Fresh > 0).ToList();
        var tasks = CostStats.Of([.. known.Where(t => isTask(t.TicketId)).Select(t => t.Fresh)], budget);
        var whole = CostStats.Of([.. known.Where(t => !isTask(t.TicketId)).Select(t => t.Fresh)], budget);
        long? suggested = tasks.Count >= MinSuggestBasis
            ? Math.Clamp(tasks.P75, OrchestratorSettings.MinUnitTokenBudget, OrchestratorSettings.MaxUnitTokenBudget)
            : null;
        return new UnitReport(tasks, whole, budget, suggested);
    }

    /// <summary>The value at fraction <paramref name="q"/> of <paramref name="sorted"/> (nearest rank); 0 when empty.</summary>
    internal static long Percentile(IReadOnlyList<long> sorted, double q) =>
        sorted.Count == 0 ? 0 : sorted[Math.Clamp((int)Math.Ceiling(q * sorted.Count) - 1, 0, sorted.Count - 1)];
}

/// <summary>Costs of one kind of run: how many, median, p75, and how many went over 1x and 2x the budget (M.ORCH-T1908).</summary>
public sealed record CostStats(int Count, long Median, long P75, int Over, int Over2x)
{
    /// <summary>The stats of <paramref name="fresh"/> against <paramref name="budget"/>.</summary>
    public static CostStats Of(IReadOnlyList<long> fresh, long budget)
    {
        var sorted = fresh.Order().ToList();
        return new(sorted.Count, WorkUnits.Percentile(sorted, 0.5), WorkUnits.Percentile(sorted, 0.75),
            sorted.Count(f => f > budget), sorted.Count(f => f > 2 * budget));
    }
}

/// <summary>What tasks and whole-ticket runs cost against the budget, and the budget suggested from the tasks' p75
/// (null until <see cref="WorkUnits.MinSuggestBasis"/> task costs exist) (M.ORCH-T1908).</summary>
public sealed record UnitReport(CostStats Tasks, CostStats Whole, long Budget, long? Suggested)
{
    /// <summary>One line: "tasks: N, median 52k, p75 71k, over budget 3 (2x: 0); whole tickets: ...; budget 80k, suggested 75k".</summary>
    public override string ToString() =>
        $"tasks: {Part(Tasks)}; whole tickets: {Part(Whole)}; budget {WorkUnits.Size(Budget)}, suggested " +
        (Suggested is { } s ? WorkUnits.Size(s) : $"none (needs {WorkUnits.MinSuggestBasis} task costs)");

    private static string Part(CostStats s) => s.Count == 0 ? "0"
        : $"{s.Count}, median {WorkUnits.Size(s.Median)}, p75 {WorkUnits.Size(s.P75)}, over budget {s.Over} (2x: {s.Over2x})";
}
