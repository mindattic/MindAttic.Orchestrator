using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator.Scheduling;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator branches [--ready] [--json]</c>: the remote host's work branches and their builds (M.ORCH-T1501). A
/// branch built clean at its head is ready for the user's local test run; <c>--ready --json</c> is the queue as data, for the
/// local fetch job to come.</summary>
internal sealed class BranchesCommand : Command<BranchesCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--ready")]
        [Description("Only the branches built clean at their head: the queue for a local test run.")]
        public bool Ready { get; init; }

        [CommandOption("--json")]
        [Description("Print it as JSON.")]
        public bool Json { get; init; }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var state = BranchBuilds.Load(store);
        if (settings.Ready)
        {
            var ready = BranchBuilds.ReadyToTest(state);
            if (settings.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(ready, Indented));
                return 0;
            }
            if (ready.Count == 0) AnsiConsole.MarkupLine("[grey]No work branch is built and waiting for a local test run.[/]");
            foreach (var r in ready)
                AnsiConsole.MarkupLine($"{Markup.Escape(r.Repo)} [aqua]{Markup.Escape(r.Branch)}[/] at {Markup.Escape(Short(r.Head))}" +
                    (r.BuiltUtc is { } at ? $", built {at.ToLocalTime():yyyy-MM-dd HH:mm}" : ""));
            return 0;
        }
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(state, Indented));
            return 0;
        }
        if (!store.TryLoadSettings(out var s) || !s.RemoteBranchBuilds)
            AnsiConsole.MarkupLine("[grey]Work-branch builds are off (the Vault setting \"Build work branches (remote host)\"): work lands on main as always.[/]");
        if (state.Count == 0 || state.All(r => r.Value.Count == 0))
        {
            AnsiConsole.MarkupLine("[grey]No work branches (origin/work/*) seen yet.[/]");
            return 0;
        }
        foreach (var (repo, branches) in state.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
            foreach (var (branch, b) in branches.OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                var what = b.Building is not null ? "[yellow]building[/]"
                    : !string.Equals(b.Built, b.Head, StringComparison.OrdinalIgnoreCase) ? "[grey]waiting to build[/]"
                    : b.Ok ? "[green]built, ready to test[/]" : "[red]fails to build[/]";
                AnsiConsole.MarkupLine($"{Markup.Escape(repo)} [aqua]{Markup.Escape(branch)}[/] at {Markup.Escape(Short(b.Head))}: {what}" +
                    (b.Note is { Length: > 0 } note && string.Equals(b.Built, b.Head, StringComparison.OrdinalIgnoreCase) ? $" ({Markup.Escape(note)})" : ""));
            }
        return 0;
    }

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
