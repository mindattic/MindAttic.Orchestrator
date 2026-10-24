using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator orgs</c> (M.ORCH-T655): the organizations the dashboard switches between above the person tabs, each with
/// its people and their logins (each login on its own LLM provider). With no org added there's one, the default, holding
/// every login; a login no org names stays in the default one.
/// </summary>
internal sealed class OrgsListCommand : Command<OrgsListCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        var orgs = Ctx.Store().Db.LoadOrgs();
        var table = new Table().Border(TableBorder.Rounded).AddColumn("Org").AddColumn("Id").AddColumn("Person").AddColumn("Login").AddColumn("Provider");
        foreach (var org in orgs.Orgs)
        {
            var people = orgs.PeopleOf(org.Id);
            var first = true;
            foreach (var (person, logins) in people)
                foreach (var login in logins)
                {
                    table.AddRow(first ? Markup.Escape(org.Name) : "", first ? Markup.Escape(org.Id) : "", Markup.Escape(person),
                        Markup.Escape(login.Login.Length == 0 ? "(global login)" : login.Login), Markup.Escape(login.Provider));
                    first = false;
                }
            if (first) table.AddRow(Markup.Escape(org.Name), Markup.Escape(org.Id), org.IsDefault ? "[grey]every login no other org names[/]" : "[grey]nobody yet[/]", "", "");
        }
        AnsiConsole.Write(table);
        if (!orgs.HasOrgs)
            AnsiConsole.MarkupLine("One org: the dashboard shows no org switcher until you add a second ([blue]orchestrator orgs add \"Sales\"[/]).");
        return 0;
    }
}

internal sealed class OrgsAddCommand : Command<OrgsAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("The org's name, e.g. \"Accounting\".")]
        public string Name { get; init; } = "";

        [CommandOption("--id <ID>")]
        [Description("Its id (default: the name in lower case, words joined by '-').")]
        public string? Id { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = Ctx.Store().Db;
        var id = string.IsNullOrWhiteSpace(settings.Id) ? Organization.IdFor(settings.Name) : settings.Id.Trim();
        if (id.Length == 0 || settings.Name.Trim().Length == 0)
        {
            AnsiConsole.MarkupLine("[red]An org needs a name.[/]");
            return 1;
        }
        var orgs = db.LoadOrgs();
        if (orgs.Find(id) is { } there)
        {
            AnsiConsole.MarkupLine($"[red]There's already an org {Markup.Escape(id)} ({Markup.Escape(there.Name)}); rename it with orchestrator orgs rename.[/]");
            return 1;
        }
        db.SaveOrg(new Organization(id, settings.Name.Trim(), orgs.Orgs.Count));
        AnsiConsole.MarkupLine($"Added {Markup.Escape(settings.Name.Trim())} ({Markup.Escape(id)}). Add people with [blue]orchestrator orgs member {Markup.Escape(id)} \"<person>\" <email>[/].");
        return 0;
    }
}

internal sealed class OrgsRenameCommand : Command<OrgsRenameCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The org's id (default for the default org).")]
        public string Id { get; init; } = "";

        [CommandArgument(1, "<NAME>")]
        [Description("Its new name.")]
        public string Name { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = Ctx.Store().Db;
        if (db.LoadOrgs().Find(settings.Id.Trim()) is not { } org || settings.Name.Trim().Length == 0)
        {
            AnsiConsole.MarkupLine($"[red]No org {Markup.Escape(settings.Id)} (orchestrator orgs lists them), or no name given.[/]");
            return 1;
        }
        db.SaveOrg(org with { Name = settings.Name.Trim() });
        AnsiConsole.MarkupLine($"{Markup.Escape(org.Id)} is now called {Markup.Escape(settings.Name.Trim())}.");
        return 0;
    }
}

internal sealed class OrgsRemoveCommand : Command<OrgsRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("The org's id.")]
        public string Id { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Id.Trim() == Organization.DefaultId)
        {
            AnsiConsole.MarkupLine("[red]The default org can't be removed; rename it instead.[/]");
            return 1;
        }
        if (!Ctx.Store().Db.RemoveOrg(settings.Id.Trim()))
        {
            AnsiConsole.MarkupLine($"[red]No org {Markup.Escape(settings.Id)}.[/]");
            return 1;
        }
        AnsiConsole.MarkupLine($"Removed {Markup.Escape(settings.Id.Trim())}: its logins are the default org's again.");
        return 0;
    }
}

internal sealed class OrgsMemberCommand : Command<OrgsMemberCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ORG>")]
        [Description("The org's id.")]
        public string Org { get; init; } = "";

        [CommandArgument(1, "<PERSON>")]
        [Description("The person the login is theirs, e.g. \"Jordan\".")]
        public string Person { get; init; } = "";

        [CommandArgument(2, "<LOGIN>")]
        [Description("The login: for Claude its email, or global for the global ~/.claude login.")]
        public string Login { get; init; } = "";

        [CommandOption("--provider <NAME>")]
        [Description("The login's LLM provider (default Claude; Codex, Gemini or any other).")]
        [DefaultValue(Providers.Claude)]
        public string Provider { get; init; } = Providers.Claude;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = Ctx.Store().Db;
        if (db.LoadOrgs().Find(settings.Org.Trim()) is not { } org)
        {
            AnsiConsole.MarkupLine($"[red]No org {Markup.Escape(settings.Org)} (orchestrator orgs lists them).[/]");
            return 1;
        }
        var provider = OrgMember.NormalizeProvider(settings.Provider);
        if (provider == Providers.Claude && settings.Login.Trim() is var l && l.Length > 0 && !l.Equals("global", StringComparison.OrdinalIgnoreCase) && AccountDirs.Normalize(l) is null)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(l)}\" isn't an email address: a Claude login is its account's email, or global.[/]");
            return 1;
        }
        try
        {
            var m = db.SetMember(new OrgMember(org.Id, settings.Person, settings.Login, provider));
            AnsiConsole.MarkupLine($"{Markup.Escape(m.Login.Length == 0 ? "The global login" : m.Login)} ({Markup.Escape(m.Provider)}) is {Markup.Escape(m.Person)}'s, in {Markup.Escape(org.Name)}.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}

/// <summary>
/// <c>orchestrator orgs repo</c> (M.ORCH-T753): which org a repo's tickets belong to. They're dispatched only to that org's
/// members' logins (and paced on them); a repo not set is the default org's.
/// </summary>
internal sealed class OrgsRepoCommand : Command<OrgsRepoCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[REPO]")]
        [Description("The repo, by name. Left out: lists every repo set to an org.")]
        public string? Repo { get; init; }

        [CommandArgument(1, "[ORG]")]
        [Description("The org's id; default (or left out with --default) puts the repo back in the default org.")]
        public string? Org { get; init; }

        [CommandOption("--default")]
        [Description("Put the repo back in the default org.")]
        public bool Default { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var orgs = store.Db.LoadOrgs();
        var routing = new OrgRouting(orgs, store.LoadSettings().RepoOrgs);
        if (string.IsNullOrWhiteSpace(settings.Repo))
        {
            var set = routing.RepoOrgs.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToList();
            if (set.Count == 0)
            {
                AnsiConsole.MarkupLine($"Every repo is {Markup.Escape(orgs.Orgs[0].Name)}'s (the default org). Set one with [blue]orchestrator orgs repo <repo> <org>[/].");
                return 0;
            }
            var table = new Table().Border(TableBorder.Rounded).AddColumn("Repo").AddColumn("Org");
            foreach (var (repo, id) in set)
                table.AddRow(Markup.Escape(repo), Markup.Escape(orgs.Find(id) is { } o ? $"{o.Name} ({o.Id})" : $"{id} (no such org now: the default org's)"));
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[grey]Any other repo is the default org's. A repo's tickets go only to its org's members' logins.[/]");
            return 0;
        }
        var name = settings.Repo.Trim();
        var orgId = settings.Default || string.IsNullOrWhiteSpace(settings.Org) ? Organization.DefaultId : settings.Org.Trim();
        if (!settings.Default && string.IsNullOrWhiteSpace(settings.Org))
        {
            AnsiConsole.MarkupLine($"{Markup.Escape(name)} is {Markup.Escape(routing.NameOf(routing.OrgOfRepo(name)))}'s.");
            return 0;
        }
        if (orgs.Find(orgId) is not { } org)
        {
            AnsiConsole.MarkupLine($"[red]No org {Markup.Escape(orgId)} (orchestrator orgs lists them).[/]");
            return 1;
        }
        store.Update<MindAttic.Orchestrator.Settings.OrchestratorSettings>(MindAttic.Orchestrator.Settings.JsonStore.SettingsName, s =>
        {
            foreach (var key in s.RepoOrgs.Keys.Where(k => k.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList()) s.RepoOrgs.Remove(key);
            if (!org.IsDefault) s.RepoOrgs[name] = org.Id;
        });
        AnsiConsole.MarkupLine($"{Markup.Escape(name)}'s tickets are {Markup.Escape(org.Name)}'s: they go only to its members' logins" +
            (orgs.HasOrgs ? "." : " (with one org, that's every login)."));
        return 0;
    }
}

internal sealed class OrgsUnmemberCommand : Command<OrgsUnmemberCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<LOGIN>")]
        [Description("The login: for Claude its email, or global.")]
        public string Login { get; init; } = "";

        [CommandOption("--provider <NAME>")]
        [Description("The login's LLM provider (default Claude).")]
        [DefaultValue(Providers.Claude)]
        public string Provider { get; init; } = Providers.Claude;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!Ctx.Store().Db.RemoveMember(settings.Provider, settings.Login))
        {
            AnsiConsole.MarkupLine($"[red]No org names {Markup.Escape(settings.Login)}: it's the default org's already.[/]");
            return 1;
        }
        AnsiConsole.MarkupLine($"{Markup.Escape(settings.Login)} is the default org's again.");
        return 0;
    }
}
