using System.ComponentModel;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator org</c> (M.ORCH-T686): the organization, behind the global /org and /organization commands. <c>usage</c> is
/// <c>orchestrator report</c> (<see cref="UsageReportCommand"/>), <c>members</c> is <c>orchestrator members</c>, <c>login &lt;who&gt;</c>
/// is <c>orchestrator login --account &lt;email&gt;</c> and <c>start &lt;repo&gt; &lt;who&gt;</c> is <c>orchestrator start &lt;repo&gt;
/// --account &lt;email&gt;</c>, with &lt;who&gt; any way of naming a member (<see cref="MemberLookup"/>). Bare, or <c>help</c>, lists them.
/// </summary>
internal sealed class OrgHelpCommand : Command
{
    internal static readonly (string Use, string What)[] Subcommands =
    [
        ("usage [--detail] [--since <date>] [--csv|--json] [--md|--html <file>]", "every member's 5-hour and weekly use, resets, points left to the weekly target, reading age and whether it's accurate"),
        ("login <who>", "sign that member's own login in (opens the browser: run it in your own terminal)"),
        ("members", "list the members; members add|set|remove change them"),
        ("start <repo> <who>", "start a CLI in <repo> on that member's login"),
        ("help", "this list"),
    ];

    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLine("[bold]orchestrator org[/] [grey](/org or /organization in a Claude CLI)[/]");
        foreach (var (use, what) in Subcommands)
            AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(use)}[/]  {Markup.Escape(what)}");
        AnsiConsole.MarkupLine("[grey]<who> is a member's email, the part before the @, name, first name, login alias or initials (RD, E), any case.[/]");
        return 0;
    }

    /// <summary>The people <c>&lt;who&gt;</c> is matched against; tests give their own (never this machine's).</summary>
    internal static Func<IReadOnlyList<OrgPerson>>? PeopleOverride { get; set; }

    /// <summary>The one member <paramref name="who"/> names, or null with why said in red: nobody, or several (each listed).</summary>
    internal static OrgPerson? Resolve(string who)
    {
        var people = PeopleOverride?.Invoke() ?? MemberLookup.People(MembersCommand.Db(), LoginCommand.DirsOverride?.Invoke() ?? new AccountDirs());
        var found = MemberLookup.Match(people, who);
        if (found.Count == 1) return found[0];
        AnsiConsole.MarkupLine(found.Count > 1
            ? $"[red]\"{Markup.Escape(who.Trim())}\" could be more than one member: name one of them.[/]"
            : $"[red]No member is \"{Markup.Escape(who.Trim())}\": name one by email, the part before the @, name, first name, login alias or initials.[/]");
        foreach (var p in found.Count > 1 ? found : people)
            AnsiConsole.MarkupLine($"  {Markup.Escape(p.Label)}");
        return null;
    }
}

/// <summary><c>orchestrator org login &lt;who&gt; [--sso]</c>: <c>orchestrator login --account &lt;their email&gt;</c>.</summary>
internal sealed class OrgLoginCommand : Command<OrgLoginCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<WHO>")]
        [Description("The member: email, the part before the @, name (Ryan DeBraal, quoted or not), first name, login alias or initials.")]
        public string[] Who { get; init; } = [];

        [CommandOption("--sso")]
        [Description("Force the SSO login flow.")]
        public bool Sso { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is { } who
            ? new LoginCommand().Execute(context, new LoginCommand.Settings { OwnLogin = who.Email, Sso = settings.Sso })
            : 1;
}

/// <summary><c>orchestrator org start &lt;repo&gt; &lt;who&gt; [--prompt]</c>: <c>orchestrator start &lt;repo&gt; --account &lt;their email&gt;</c>.</summary>
internal sealed class OrgStartCommand : Command<OrgStartCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REPO>")]
        public string Repo { get; init; } = "";

        [CommandArgument(1, "<WHO>")]
        [Description("The member whose login the CLI runs on: email, the part before the @, name (quoted or not), first name, login alias or initials.")]
        public string[] Who { get; init; } = [];

        [CommandOption("--prompt <TEXT>")]
        public string? Prompt { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        OrgHelpCommand.Resolve(string.Join(' ', settings.Who)) is { } who
            ? new StartCommand().Execute(context, new StartCommand.Settings { Repo = settings.Repo, Account = who.Email, Prompt = settings.Prompt })
            : 1;
}
