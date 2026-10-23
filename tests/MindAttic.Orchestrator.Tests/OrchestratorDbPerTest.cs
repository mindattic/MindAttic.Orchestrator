using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: OrchestratorDbPerTest]

/// <summary>Gives every test its own scratch orchestrator.db (M.ORCH-T490): a StatusAggregator built with the default workspace
/// would otherwise write the account roster into the real repo's data/orchestrator.db, and one test's rows would reach the next.
/// The tests run one at a time, so the process-wide variable is safe to set per test. SQL Server is off for every test too
/// (M.ORCH-T658): a test that clears the variable to look at the live path must never reach the machine's real database.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class OrchestratorDbPerTestAttribute : Attribute, ITestAction
{
    private string? dir;
    private string? before;
    private string? serverBefore;
    private string? ticketsServerBefore;

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        before = Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar);
        serverBefore = Environment.GetEnvironmentVariable(OrchestratorServer.EnvVar);
        Environment.SetEnvironmentVariable(OrchestratorServer.EnvVar, "off");
        ticketsServerBefore = Environment.GetEnvironmentVariable(MindAttic.Tickets.Data.TicketsServer.EnvVar);
        Environment.SetEnvironmentVariable(MindAttic.Tickets.Data.TicketsServer.EnvVar, "off"); // nor the live ticket database (M.TIX-T131)
        dir = Path.Combine(Path.GetTempPath(), "orchestrator-db-test-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, Path.Combine(dir, "orchestrator.db"));
    }

    public void AfterTest(ITest test)
    {
        Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, before);
        Environment.SetEnvironmentVariable(OrchestratorServer.EnvVar, serverBefore);
        Environment.SetEnvironmentVariable(MindAttic.Tickets.Data.TicketsServer.EnvVar, ticketsServerBefore);
        try { if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
