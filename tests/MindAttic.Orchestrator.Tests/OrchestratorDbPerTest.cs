using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: OrchestratorDbPerTest]

/// <summary>Gives every test its own scratch orchestrator.db (M.ORCH-T490): a StatusAggregator built with the default workspace
/// would otherwise write the account roster into the real repo's data/orchestrator.db, and one test's rows would reach the next.
/// The tests run one at a time, so the process-wide variable is safe to set per test.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class OrchestratorDbPerTestAttribute : Attribute, ITestAction
{
    private string? dir;
    private string? before;

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        before = Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar);
        dir = Path.Combine(Path.GetTempPath(), "orchestrator-db-test-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, Path.Combine(dir, "orchestrator.db"));
    }

    public void AfterTest(ITest test)
    {
        Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, before);
        try { if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
