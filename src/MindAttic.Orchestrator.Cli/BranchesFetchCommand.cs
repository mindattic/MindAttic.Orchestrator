using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator branches fetch [--show] [--json]</c> (M.ORCH-T1634): on the owner's PC (a client), pulls the host's ready-to-test
/// queue through its gateway and keeps it here (<see cref="BranchQueue"/>), for the local test run to start from. The client's
/// MindAttic.Scheduler job runs it every <see cref="OrchestratorSettings.BranchQueueFetchMinutes"/>; each run also brings that
/// job in line with the Vault (re-registered when the interval changed, cancelled when the feature is off or this isn't a client).
/// It runs no test. <c>--show</c> prints what was last fetched without asking the host.
/// </summary>
internal sealed class BranchesFetchCommand : AsyncCommand<BranchesFetchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--show")]
        [Description("Only print the queue as last fetched; the host isn't asked.")]
        public bool Show { get; init; }

        [CommandOption("--json")]
        [Description("Print it as JSON.")]
        public bool Json { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var s = store.LoadSettings();
        if (!settings.Show)
        {
            var job = BranchQueue.EnsureJob(store, s, OrchestratorOptions.Load().WorkspaceRoot);
            if (job is not null && !settings.Json) AnsiConsole.MarkupLine($"[grey]{Markup.Escape(job)}[/]");
            var topology = MachineTopology.Current;
            if (!topology.FetchesBranchQueue(s.RemoteBranchBuilds))
            {
                if (!settings.Json)
                    AnsiConsole.MarkupLine(topology.IsClient
                        ? "[grey]Work-branch builds are off here (the Vault setting \"Build work branches (remote host)\"): nothing is fetched.[/]"
                        : "[grey]Only a client fetches its host's queue; on this machine orchestrator branches --ready lists it.[/]");
                else Console.WriteLine(JsonSerializer.Serialize(BranchQueue.Load(store), BranchesCommand.Indented));
                return 0;
            }
            if (GatewayRemotes.Find(s.Gateway, MachineRole.HostRemoteName) is not { } remote)
            {
                AnsiConsole.MarkupLine("[red]This machine is a client but isn't connected to a host (orchestrator machine connect).[/]");
                return 1;
            }
            using var host = new GatewayClient(remote);
            var fetched = await BranchQueue.FetchAsync(host.RunBranchesAsync, store, DateTime.UtcNow, CancellationToken.None);
            Print(fetched, settings.Json);
            return fetched.Problem is null ? 0 : 1;
        }
        Print(BranchQueue.Load(store), settings.Json);
        return 0;
    }

    private static void Print(BranchQueue.State state, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(state, BranchesCommand.Indented));
            return;
        }
        if (state.Problem is { } problem) AnsiConsole.MarkupLine($"[yellow]The latest fetch didn't come back whole: {Markup.Escape(problem)}[/]");
        if (state.LastGoodUtc is null)
        {
            AnsiConsole.MarkupLine("[grey]The host's queue hasn't been fetched yet.[/]");
            return;
        }
        AnsiConsole.MarkupLine($"[grey]The host's queue as of {state.LastGoodUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}:[/]");
        if (state.Ready.Count == 0) AnsiConsole.MarkupLine("[grey]No work branch is built and waiting for a local test run.[/]");
        foreach (var r in state.Ready)
            AnsiConsole.MarkupLine($"{Markup.Escape(r.Repo)} [aqua]{Markup.Escape(r.Branch)}[/] at {Markup.Escape(BranchesCommand.Short(r.Head))}" +
                (r.BuiltUtc is { } at ? $", built {at.ToLocalTime():yyyy-MM-dd HH:mm}" : ""));
    }
}
