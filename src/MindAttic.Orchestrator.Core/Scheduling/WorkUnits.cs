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
}
