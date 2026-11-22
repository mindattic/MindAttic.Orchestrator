using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator mail send|list</c> (M.ORCH-T1745, roadmap Phase 1): the agent mailbox (<see cref="Mailbox"/>) by hand. A message
/// is left for an agent by its stable id; the daemon hands it to that agent's CLI on its next prompt and marks it read.
/// </summary>
internal static class MailCli
{
    public static Mailbox Box() => new(AgentsCli.Db());
}

internal sealed class MailSendCommand : Command<MailSendCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<AGENT>")]
        [Description("The agent it's for, by its id (agt-...; orchestrator agents lists them).")]
        public string To { get; init; } = "";

        [CommandArgument(1, "<BODY>")]
        [Description("What it says.")]
        public string Body { get; init; } = "";

        [CommandOption("--from <WHO>")]
        [Description("Who it's from. Default: this CLI's agent (MINDATTIC_AGENT_ID), else its login, else \"user\".")]
        public string? From { get; init; }

        [CommandOption("--unit <ID>")]
        [Description("The unit (ticket) it's about.")]
        public string? Unit { get; init; }

        [CommandOption("--force")]
        [Description("Send to an id the agents table doesn't have.")]
        public bool Force { get; init; }

        [CommandOption("--json")]
        [Description("Print the message as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => AgentsCli.Guard(() =>
    {
        if (AgentIds.Normalize(settings.To) is not { } to)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(settings.To)}\" isn't an agent id (agt-...).[/]");
            return 1;
        }
        if (!settings.Force && AgentsCli.Book().Find(to) is null)
        {
            AnsiConsole.MarkupLine($"[red]There's no agent {Markup.Escape(to)} (orchestrator agents lists them; --force sends anyway).[/]");
            return 1;
        }
        var from = settings.From is { } f && !string.IsNullOrWhiteSpace(f) ? f.Trim()
            : AgentIds.Current() ?? new AccountDirs().Current()?.Email ?? "user";
        var message = MailCli.Box().Send(to, from, settings.Body, DateTime.UtcNow, settings.Unit);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(message, BranchesCommand.Indented));
            return 0;
        }
        AnsiConsole.MarkupLine($"Sent [bold]{message.Id}[/] to {Markup.Escape(message.To)} from {Markup.Escape(message.From)}" +
                               (message.Unit is { } u ? $" about {Markup.Escape(u)}" : "") + "; its CLI gets it on its next prompt.");
        return 0;
    });
}

internal sealed class MailListCommand : Command<MailListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[AGENT]")]
        [Description("Only this agent's mail. Default: this CLI's own agent (MINDATTIC_AGENT_ID), else everyone's.")]
        public string? To { get; init; }

        [CommandOption("--all")]
        [Description("The read messages too.")]
        public bool All { get; init; }

        [CommandOption("--everyone")]
        [Description("Every agent's mail, even in a CLI that runs as an agent.")]
        public bool Everyone { get; init; }

        [CommandOption("--limit <N>")]
        [Description("At most this many, newest first (default 50).")]
        public int Limit { get; init; } = 50;

        [CommandOption("--json")]
        [Description("Print them as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => AgentsCli.Guard(() =>
    {
        var to = settings.To is { } t && !string.IsNullOrWhiteSpace(t) ? t : settings.Everyone ? null : AgentIds.Current();
        var messages = MailCli.Box().List(to, settings.All, settings.Limit);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(messages, BranchesCommand.Indented));
            return 0;
        }
        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine($"[grey]No {(settings.All ? "" : "unread ")}mail{(to is null ? "" : $" for {Markup.Escape(to)}")}.[/]");
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Id", "To", "From", "Unit", "Sent", "Read", "Body");
        foreach (var m in messages)
            table.AddRow(
                m.Id,
                Markup.Escape(m.To),
                Markup.Escape(m.From),
                Markup.Escape(m.Unit ?? ""),
                Markup.Escape(AgentsCli.When(m.SentUtc)),
                m.ReadUtc is { } r ? Markup.Escape(AgentsCli.When(r)) : "[yellow]unread[/]",
                Markup.Escape(m.Body.Length > 80 ? m.Body[..77] + "..." : m.Body));
        AnsiConsole.Write(table);
        return 0;
    });
}
