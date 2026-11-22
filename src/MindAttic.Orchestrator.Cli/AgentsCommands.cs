using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator agents new|list|pause|resume|retire</c> (M.ORCH-T1744, roadmap Phase 1): the <c>agents</c> table
/// (M.ORCH-T1742) by hand, on <see cref="AgentBook"/>. <c>new &lt;role&gt;</c> writes an idle agent's row under a stable id
/// (M.ORCH-T1743); the rest move it between idle, paused and terminated (a retired row is kept).
/// </summary>
internal static class AgentsCli
{
    /// <summary>The database the agents live in; tests point it at their own.</summary>
    internal static Func<OrchestratorDb> Db { get; set; } = () => new OrchestratorDb(OrchestratorDb.LivePath);

    /// <summary>The workspace the role files are found in; tests stand in their own.</summary>
    internal static Func<string?> WorkspaceRoot { get; set; } = () =>
    {
        try { return OrchestratorOptions.Load(Ctx.Store()).WorkspaceRoot; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    };

    public static AgentBook Book() => new(Db());

    /// <summary>Runs <paramref name="work"/>, turning a store that can't be read or written into a red line and exit code 1.</summary>
    public static int Guard(Func<int> work)
    {
        try { return work(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or System.Data.Common.DbException)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }

    public static string When(DateTime utc) => utc.ToLocalTime().ToString("MMM d h:mm tt", CultureInfo.CurrentCulture);

    public static string Color(string state) => state switch
    {
        AgentBook.Running => "green",
        AgentBook.Idle => "default",
        AgentBook.Paused => "yellow",
        _ => "grey",
    };

    /// <summary>Moves <paramref name="id"/> to <paramref name="state"/> and says so.</summary>
    public static int Move(string id, string state, string done) => Guard(() =>
    {
        var (agent, problem) = Book().SetState(id, state, DateTime.UtcNow);
        if (agent is null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(problem ?? "It couldn't be changed.")}[/]");
            return 1;
        }
        AnsiConsole.MarkupLine($"[bold]{agent.Id}[/] ({Markup.Escape(agent.Role)}, {Markup.Escape(agent.Owner)}) {done}.");
        return 0;
    });

    public sealed class IdSettings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The agent's id (agt-...).")]
        public string Id { get; init; } = "";
    }
}

internal sealed class AgentsNewCommand : Command<AgentsNewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ROLE>")]
        [Description("The role it plays: a role file's name under mindattic-agent-standard\\shared\\roles (planner, implementer, reviewer, ...).")]
        public string Role { get; init; } = "";

        [CommandOption("--owner <EMAIL>")]
        [Description("The login it runs on, by account email. Default: this CLI's own login (CLAUDE_CONFIG_DIR).")]
        public string? Owner { get; init; }

        [CommandOption("--runtime <NAME>")]
        [Description("What runs it: claude (the default), codex, ...")]
        public string? Runtime { get; init; }

        [CommandOption("--unit <ID>")]
        [Description("The unit (ticket) it starts on; none by default.")]
        public string? Unit { get; init; }

        [CommandOption("--id <ID>")]
        [Description("Use this id (one a started CLI already carries as MINDATTIC_AGENT_ID) instead of making a new one.")]
        public string? Id { get; init; }

        [CommandOption("--force")]
        [Description("Take a role that isn't in the catalog.")]
        public bool Force { get; init; }

        [CommandOption("--json")]
        [Description("Print the new agent as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => AgentsCli.Guard(() =>
    {
        var role = settings.Role.Trim().ToLowerInvariant();
        if (role.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Name a role: orchestrator agents new reviewer.[/]");
            return 1;
        }
        var book = AgentsCli.Book();
        var roles = book.Roles(AgentsCli.WorkspaceRoot());
        if (!settings.Force && roles.Count > 0 && !roles.Contains(role))
        {
            AnsiConsole.MarkupLine($"[red]There's no role \"{Markup.Escape(role)}\". The roles: {Markup.Escape(string.Join(", ", roles.Order(StringComparer.Ordinal)))} (--force takes it anyway).[/]");
            return 1;
        }
        if (roles.Count == 0)
            AnsiConsole.MarkupLine("[yellow]Note: no role catalog was found (no roles rows, no role files), so the role wasn't checked.[/]");

        var owner = settings.Owner is { } o ? AccountDirs.Normalize(o) : new AccountDirs().Current()?.Email;
        if (owner is null)
        {
            AnsiConsole.MarkupLine(settings.Owner is null
                ? "[red]This CLI runs on the global login, so say whose login the agent runs on: --owner <email>.[/]"
                : $"[red]\"{Markup.Escape(settings.Owner)}\" isn't an account email.[/]");
            return 1;
        }

        var agent = book.New(role, owner, DateTime.UtcNow, settings.Runtime, settings.Unit, settings.Id);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(agent, BranchesCommand.Indented));
            return 0;
        }
        AnsiConsole.MarkupLine($"Agent [bold]{agent.Id}[/]: {Markup.Escape(agent.Role)} on {Markup.Escape(agent.Owner)} ({Markup.Escape(agent.Runtime)}), idle" +
                               (agent.Unit is { } u ? $", unit {Markup.Escape(u)}" : "") + ".");
        return 0;
    });
}

internal sealed class AgentsListCommand : Command<AgentsListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--all")]
        [Description("The retired agents too.")]
        public bool All { get; init; }

        [CommandOption("--role <ROLE>")]
        [Description("Only the agents playing this role.")]
        public string? Role { get; init; }

        [CommandOption("--owner <EMAIL>")]
        [Description("Only the agents on this login.")]
        public string? Owner { get; init; }

        [CommandOption("--json")]
        [Description("Print them as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) => AgentsCli.Guard(() =>
    {
        var agents = AgentsCli.Book().All()
            .Where(a => settings.All || !a.Terminated)
            .Where(a => settings.Role is not { } r || a.Role.Equals(r.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(a => settings.Owner is not { } o || a.Owner.Equals(o.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(agents, BranchesCommand.Indented));
            return 0;
        }
        if (agents.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No agents. Make one: orchestrator agents new <role>[/]");
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Id", "Role", "Owner", "Runtime", "State", "Unit", "Heartbeat", "Since");
        foreach (var a in agents)
            table.AddRow(
                a.Id,
                Markup.Escape(a.Role),
                Markup.Escape(a.Owner),
                Markup.Escape(a.Runtime),
                $"[{AgentsCli.Color(a.State)}]{Markup.Escape(a.State)}[/]",
                Markup.Escape(a.Unit ?? ""),
                a.HeartbeatUtc is { } h ? Markup.Escape(AgentsCli.When(h)) : "",
                Markup.Escape(AgentsCli.When(a.StateUtc)));
        AnsiConsole.Write(table);
        return 0;
    });
}

internal sealed class AgentsPauseCommand : Command<AgentsCli.IdSettings>
{
    public override int Execute(CommandContext context, AgentsCli.IdSettings settings) =>
        AgentsCli.Move(settings.Id, AgentBook.Paused, "is paused");
}

internal sealed class AgentsResumeCommand : Command<AgentsCli.IdSettings>
{
    public override int Execute(CommandContext context, AgentsCli.IdSettings settings) =>
        AgentsCli.Move(settings.Id, AgentBook.Idle, "is resumed (idle)");
}

internal sealed class AgentsRetireCommand : Command<AgentsCli.IdSettings>
{
    public override int Execute(CommandContext context, AgentsCli.IdSettings settings) =>
        AgentsCli.Move(settings.Id, AgentBook.Terminated, "is retired");
}
