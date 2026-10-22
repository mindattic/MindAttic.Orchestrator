using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator move &lt;repo|pid&gt; --account &lt;email&gt;|--global</c> (M.ORCH-T607): moves a running Claude CLI to another
/// signed-in login, as the dashboard's "Move to" menu does: its Launcher tab restarts it with <c>claude --resume</c> under that
/// login's folder at its next idle point, so it keeps its session and spends the other account's tokens from then on.
/// </summary>
internal sealed class MoveCommand : Command<MoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO|PID>")]
        [Description("The CLI to move: its claude.exe process id (orchestrator clis lists them), its session id, or the repo (folder) it runs in.")]
        public string Target { get; init; } = "";

        [CommandOption("--account <EMAIL>")]
        [Description("Move it to this signed-in per-CLI login.")]
        public string? Account { get; init; }

        [CommandOption("--global")]
        [Description("Move it to the global login (~/.claude).")]
        public bool Global { get; init; }
    }

    /// <summary>The move and the Launcher tabs it sees; tests point both elsewhere (never the real logins or tabs).</summary>
    internal static Func<(AccountMove Move, IReadOnlyList<HostSession> Hosts)>? Override { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        if ((settings.Account is null) != settings.Global)
        {
            AnsiConsole.MarkupLine("[red]Give exactly one of --account <email> (a signed-in per-CLI login) or --global (the global login).[/]");
            return 1;
        }
        var (move, hosts) = Override?.Invoke() ?? (AccountMove.Default(), new HostSessionScanner(OrchestratorOptions.Load()).Scan());
        var now = DateTime.UtcNow;
        var found = AccountMove.Find(move.Live(hosts, now), settings.Target);
        if (found.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]No live Claude CLI is {Markup.Escape(settings.Target)}: orchestrator clis lists them.[/]");
            return 1;
        }
        if (found.Count > 1)
        {
            AnsiConsole.MarkupLine($"[red]{found.Count} Claude CLIs run in {Markup.Escape(settings.Target)}: name one by its pid ({string.Join(", ", found.Select(c => c.Pid))}).[/]");
            return 1;
        }
        var result = move.MoveAsync(found[0], settings.Global ? null : settings.Account, now).GetAwaiter().GetResult();
        AnsiConsole.MarkupLine(result.Ok ? Markup.Escape(result.Message) : $"[red]{Markup.Escape(result.Message)}[/]");
        return result.Ok ? 0 : 1;
    }
}
