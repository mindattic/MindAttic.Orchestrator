using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator billing [team|api]</c> (M.ORCH-T436): with no mode, who every CLI bills and each open CLI's own billing;
/// with one, switches every CLI to it, as the dashboard's toggle does.
/// </summary>
internal sealed class BillingCommand : Command<BillingCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[MODE]")]
        [Description("team (the Claude Team login, for everyday tokens) or api (the MindAttic Vault's Claude key, for emergencies only).")]
        public string? Mode { get; init; }
    }

    /// <summary>The billing switch and the Launcher tabs it sees; tests point both elsewhere (never the real ~/.claude.json).</summary>
    internal static Func<(BillingSwitch Switch, IReadOnlyList<HostSession> Hosts)>? Override { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (billing, hosts) = Override?.Invoke() ?? (new BillingSwitch(), new HostSessionScanner(OrchestratorOptions.Load()).Scan());
        var now = DateTime.UtcNow;
        if (settings.Mode is null)
        {
            Write(billing.Status(hosts, now), now);
            return 0;
        }
        if (BillingModes.Parse(settings.Mode) is not { } mode)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(settings.Mode)}\" isn't a billing mode: use team (Claude Team) or api (the Vault's API key).[/]");
            return 1;
        }
        var result = billing.Switch(mode, "cli", hosts, now);
        if (result.Error is { } error)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
            return 1;
        }
        foreach (var line in result.Lines) AnsiConsole.MarkupLine(Markup.Escape(line));
        Write(result.Status, now);
        return 0;
    }

    /// <summary>The billing lines <c>status</c> and this command print: the headline in bold (yellow on the API key, for emergencies
    /// only), a CLI billing the other mode in yellow.</summary>
    internal static void Write(BillingStatus status, DateTime nowUtc)
    {
        var lines = BillingText.Lines(status, nowUtc);
        var color = status.Mode == BillingModes.Api ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[bold {color}]{Markup.Escape(lines[0])}[/]");
        // BillingText.Lines' order: the headline, the problem if any, one line per CLI, then the count of those billing the other mode.
        var i = 1;
        void Say(bool warn) { AnsiConsole.MarkupLine(warn ? $"[yellow]{Markup.Escape(lines[i])}[/]" : Markup.Escape(lines[i])); i++; }
        if (status.Problem is not null) Say(true);
        foreach (var c in status.Clis) Say(c.Restart is not null);
        while (i < lines.Count) Say(true);
    }
}
