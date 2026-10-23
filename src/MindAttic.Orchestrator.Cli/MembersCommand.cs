using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator members</c> (M.ORCH-T670): the organization's members, from the members table in the repo's
/// <c>data/orchestrator.db</c>, each with whether it has signed in on this machine (the accounts table, joined by email).</summary>
internal sealed class MembersCommand : Command<MembersCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write the members as JSON.")]
        public bool Json { get; init; }
    }

    internal static OrchestratorDb Db() => new(OrchestratorDb.LivePath);

    public override int Execute(CommandContext context, Settings settings)
    {
        var db = Db();
        var members = MemberRoster.Load(db);
        var accounts = AccountRoster.Load(db).Where(a => a.Email is not null)
            .GroupBy(a => AccountDirs.Normalize(a.Email) ?? a.Email!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(a => a.SignedInUtc), StringComparer.Ordinal);
        var own = OwnLogins();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(members.Select(m => new
            {
                m.Email, m.Name, m.Role, m.Tier, m.Status, m.JoinedUtc, m.UpdatedUtc,
                SignedInHere = accounts.ContainsKey(m.Email) || own.Contains(m.Email),
                SignedInOwnLogin = own.Contains(m.Email),
                SignedInFallbackLogin = accounts.ContainsKey(m.Email),
                SignedInUtc = accounts.GetValueOrDefault(m.Email),
            }), new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (members.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No members yet: orchestrator members add <email> --name \"<name>\" --role <role> --tier <tier> adds one.[/]");
            return 0;
        }
        var now = DateTime.UtcNow;
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Name", "Email", "Role", "Tier", "Status", "Signed in here");
        foreach (var m in members)
            table.AddRow(
                m.Name is { } name ? Markup.Escape(name) : "[grey]—[/]",
                Markup.Escape(m.Email),
                Markup.Escape(m.Role),
                Markup.Escape(m.Tier),
                m.Status == "active" ? m.Status : $"[yellow]{Markup.Escape(m.Status)}[/]",
                SignedInCell(own.Contains(m.Email), accounts.TryGetValue(m.Email, out var at), at, now));
        AnsiConsole.Write(table);
        return 0;
    }

    /// <summary>The emails with a Claude login signed in under their own folder in ~/.claude-accounts (M.ORCH-T671).</summary>
    internal static HashSet<string> OwnLogins() =>
        (LoginCommand.DirsOverride?.Invoke() ?? new AccountDirs()).SignedIn()
            .Select(d => AccountDirs.Normalize(d.Email)).OfType<string>().ToHashSet(StringComparer.Ordinal);

    /// <summary>The "Signed in here" cell: yes through its own login and/or the fallback (global) login, else no.</summary>
    internal static string SignedInCell(bool ownLogin, bool fallbackLogin, DateTime? fallbackAt, DateTime now)
    {
        if (!ownLogin && !fallbackLogin) return "[grey]no[/]";
        var via = new List<string>();
        if (ownLogin) via.Add("own login");
        if (fallbackLogin) via.Add("fallback login" + (fallbackAt is { } t ? $", last {Formatting.Ago(t, now)}" : ""));
        return $"[green]yes[/] [grey]({Markup.Escape(string.Join("; ", via))})[/]";
    }

    /// <summary>Runs a change, saying an invalid value or a missing/duplicate member in one red line (exit 1).</summary>
    internal static int Change(Func<string> change)
    {
        try
        {
            AnsiConsole.MarkupLine(change());
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }

    internal static string Describe(Member m) =>
        $"[bold]{Markup.Escape(m.Name ?? m.Email)}[/] [grey]{Markup.Escape(m.Email)}[/]: {Markup.Escape(m.Role)}, {Markup.Escape(m.Tier)}, {Markup.Escape(m.Status)}";

    internal static string Allowed => $"roles: {string.Join(", ", MemberRoster.Roles)}; tiers: {string.Join(", ", MemberRoster.Tiers)}; statuses: {string.Join(", ", MemberRoster.Statuses)}";
}

/// <summary><c>orchestrator members add &lt;email&gt; --name --role --tier [--status]</c> (M.ORCH-T670).</summary>
internal sealed class MembersAddCommand : Command<MembersAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<EMAIL>")]
        [Description("The member's email.")]
        public string Email { get; init; } = "";

        [CommandOption("--name <NAME>")]
        [Description("The name they go by (quote one with spaces); their dashboard tab's title.")]
        public string? Name { get; init; }

        [CommandOption("--role <ROLE>")]
        [Description("primary-owner, owner, admin or member.")]
        public string? Role { get; init; }

        [CommandOption("--tier <TIER>")]
        [Description("premium or standard.")]
        public string? Tier { get; init; }

        [CommandOption("--status <STATUS>")]
        [Description("active (the default) or pending.")]
        public string? Status { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.Role is null || settings.Tier is null)
        {
            AnsiConsole.MarkupLine($"[red]Give the new member's --role and --tier ({Markup.Escape(MembersCommand.Allowed)}).[/]");
            return 1;
        }
        return MembersCommand.Change(() => "Added " + MembersCommand.Describe(
            MemberRoster.Add(MembersCommand.Db(), settings.Email, settings.Name, settings.Role, settings.Tier, settings.Status, DateTime.UtcNow)) + ".");
    }
}

/// <summary><c>orchestrator members set &lt;email&gt; [--name] [--role] [--tier] [--status]</c> (M.ORCH-T670).</summary>
internal sealed class MembersSetCommand : Command<MembersSetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<EMAIL>")]
        [Description("The member's email.")]
        public string Email { get; init; } = "";

        [CommandOption("--name <NAME>")]
        [Description("The name they go by; \"\" clears it.")]
        public string? Name { get; init; }

        [CommandOption("--role <ROLE>")]
        [Description("primary-owner, owner, admin or member.")]
        public string? Role { get; init; }

        [CommandOption("--tier <TIER>")]
        [Description("premium or standard.")]
        public string? Tier { get; init; }

        [CommandOption("--status <STATUS>")]
        [Description("active or pending.")]
        public string? Status { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings is { Name: null, Role: null, Tier: null, Status: null })
        {
            AnsiConsole.MarkupLine("[red]Nothing to change: give --name, --role, --tier or --status.[/]");
            return 1;
        }
        return MembersCommand.Change(() => MembersCommand.Describe(
            MemberRoster.Set(MembersCommand.Db(), settings.Email, settings.Name, settings.Role, settings.Tier, settings.Status, DateTime.UtcNow)) + ".");
    }
}

/// <summary><c>orchestrator members remove &lt;email&gt;</c> (M.ORCH-T670).</summary>
internal sealed class MembersRemoveCommand : Command<MembersRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<EMAIL>")]
        [Description("The member's email.")]
        public string Email { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings) =>
        MembersCommand.Change(() => MemberRoster.Remove(MembersCommand.Db(), settings.Email)
            ? $"Removed {Markup.Escape(AccountDirs.Normalize(settings.Email)!)}."
            : throw new InvalidOperationException($"{AccountDirs.Normalize(settings.Email)} isn't a member."));
}
