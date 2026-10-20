using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator login [email-or-short-name] [--api-key] [--sso]</c> (M.ORCH-T492): the dashboard's account menu from the
/// command line. Bare, it prints the roster table (the signed-in account marked, the one with the most headroom suggested);
/// with an account, it runs <c>claude auth login --email &lt;address&gt;</c> (with <c>--sso</c> when the org uses SSO), which opens
/// the browser on that account's login; <c>--api-key</c> switches every CLI to the Vault's API key, as the menu's last entry does.
/// </summary>
internal sealed class LoginCommand : Command<LoginCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ACCOUNT]")]
        [Description("The account to sign in to: its email, or the part before the @ (as 'orchestrator login' lists them).")]
        public string? Account { get; init; }

        [CommandOption("--api-key")]
        [Description("Bill every CLI to the MindAttic Vault's API key instead (emergencies only).")]
        public bool ApiKey { get; init; }

        [CommandOption("--sso")]
        [Description("Force the SSO login flow (it's used anyway when the account's org signs in through SSO).")]
        public bool Sso { get; init; }
    }

    /// <summary>The billing switch and the Launcher tabs it sees; tests point both elsewhere (never the real ~/.claude.json).</summary>
    internal static Func<(BillingSwitch Switch, IReadOnlyList<HostSession> Hosts)>? BillingOverride { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.ApiKey && settings.Account is not null)
        {
            AnsiConsole.MarkupLine("[red]Name an account or --api-key, not both.[/]");
            return 1;
        }
        var (rows, current, now) = AccountsCommand.Load();
        if (settings.Account is null && !settings.ApiKey)
        {
            AccountsCommand.Write(rows, current, now);
            AnsiConsole.MarkupLine("[grey]orchestrator login <email-or-short-name> switches to an account; --api-key bills the Vault's API key (emergencies only).[/]");
            return 0;
        }
        var (billing, hosts) = BillingOverride?.Invoke() ?? (new BillingSwitch(), new HostSessionScanner(OrchestratorOptions.Load()).Scan());
        if (settings.ApiKey)
        {
            var switched = billing.Switch(BillingModes.Api, "cli", hosts, now);
            if (switched.Error is { } error)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(error)}[/]");
                return 1;
            }
            foreach (var line in switched.Lines) AnsiConsole.MarkupLine(Markup.Escape(line));
            BillingCommand.Write(switched.Status, now);
            return 0;
        }
        if (AccountSwitch.Find(rows, settings.Account!) is not { } row)
        {
            AnsiConsole.MarkupLine($"[red]No one account here is \"{Markup.Escape(settings.Account!)}\": name its email or the part before the @.[/]");
            AccountsCommand.Write(rows, current, now);
            return 1;
        }
        var result = AccountSwitch.SignIn(row, current, billing, hosts, "cli", settings.Sso, now);
        foreach (var line in result.Lines) AnsiConsole.MarkupLine(Markup.Escape(line));
        if (result.Error is { } failed)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(failed)}[/]");
            return 1;
        }
        return 0;
    }
}
