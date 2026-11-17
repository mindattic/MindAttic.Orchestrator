using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator inbox</c> (M.ORCH-T1689): everything waiting on the owner right now, each with the command that
/// answers it. Read-only; <c>--json</c> writes the items for a script.</summary>
internal sealed class InboxCommand : Command<InboxCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write the items as JSON: kind, id, title, repo, sinceUtc, action, ticketId.")]
        public bool Json { get; init; }
    }

    /// <summary>The host's consent requests on this member's CLIs (seam); null asks the host when this PC is a member's client.</summary>
    internal static Func<IReadOnlyList<RemoteConsent>>? ConsentsOverride { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var snapshot = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), Ctx.Lease(store)).Refresh();
        var s = store.LoadSettings();
        var plan = Scheduler.Plan(snapshot, s, store.LoadAssignments());
        var now = DateTime.UtcNow;
        var items = OwnerInbox.Build(OwnerInbox.Read(store, snapshot, plan, s, now, ConsentsOverride?.Invoke() ?? Consents()));
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(items.Select(i => new { i.Kind, i.Id, i.Title, i.Repo, i.SinceUtc, i.Action, i.TicketId }), BranchesCommand.Indented));
            return 0;
        }
        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing is waiting on you.[/]");
            return 0;
        }
        AnsiConsole.MarkupLine($"[bold]{items.Count}[/] waiting on you");
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Kind", "Item", "Repo", "Since", "What", "Do");
        foreach (var i in items)
            table.AddRow(Markup.Escape(OwnerInbox.KindLabel(i.Kind)), Markup.Escape(i.Id), Markup.Escape(i.Repo ?? ""),
                i.SinceUtc is { } since ? Markup.Escape(Format.Ago(since, now)) : "", Markup.Escape(i.Title), $"[aqua]{Markup.Escape(i.Action)}[/]");
        AnsiConsole.Write(table);
        return 0;
    }

    /// <summary>On a member's PC, who is asking to type into their CLIs on the host; none anywhere else, or when the host can't
    /// be asked within a few seconds. Never throws.</summary>
    private static IReadOnlyList<RemoteConsent> Consents()
    {
        try
        {
            if (!MindAttic.Orchestrator.Setup.MachineTopology.Current.IsMemberClient) return [];
            using var client = MindAttic.Orchestrator.Setup.MachineSetup.HostClient();
            if (client is null) return [];
            using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            return client.ConsentsAsync(bounded.Token).GetAwaiter().GetResult() ?? [];
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or UnauthorizedAccessException or InvalidOperationException
                                       or JsonException or System.Net.Http.HttpRequestException)
        {
            return [];
        }
    }
}
