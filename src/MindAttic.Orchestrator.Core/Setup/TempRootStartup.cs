using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Setup;

/// <summary>The daemon's and dashboard's start-up half of <see cref="TempRoot.Enforce"/> (M.ORCH-T2219): reads
/// <see cref="OrchestratorSettings.TempRoot"/> and logs the warning. Never throws, so it can't stop a start.</summary>
public static class TempRootStartup
{
    /// <summary>Moves this process's TEMP/TMP under the configured temp root when they aren't; logs under <paramref name="component"/>.</summary>
    public static void Enforce(string component)
    {
        try
        {
            var root = new JsonStore().TryLoadSettings(out var s) ? s.TempRoot : new OrchestratorSettings().TempRoot;
            if (TempRoot.Enforce(root) is { } warning) OrchestratorLog.For(component).Warning("{Warning:l}", warning);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For(component).Warning(ex, "Checking the temp root failed; TEMP left as it is");
        }
    }
}
