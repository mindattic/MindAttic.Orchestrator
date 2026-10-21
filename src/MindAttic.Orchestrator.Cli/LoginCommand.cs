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
/// <para><c>--account &lt;email&gt;</c> (M.ORCH-T559) signs in a per-CLI login instead: that account's own folder
/// (<see cref="AccountDirs"/>), once, so <c>orchestrator start &lt;repo&gt; --account &lt;email&gt;</c> runs a CLI on its token
/// pool beside the others. The global login every other CLI uses is left as it is.</para>
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

        [CommandOption("--account <EMAIL>")]
        [Description("Sign in a per-CLI login: this account's own folder, for CLIs started with 'orchestrator start <repo> --account <email>'. The global login is left alone.")]
        public string? OwnLogin { get; init; }

        [CommandOption("--sso")]
        [Description("Force the SSO login flow (it's used anyway when the account's org signs in through SSO).")]
        public bool Sso { get; init; }
    }

    /// <summary>The billing switch and the Launcher tabs it sees; tests point both elsewhere (never the real ~/.claude.json).</summary>
    internal static Func<(BillingSwitch Switch, IReadOnlyList<HostSession> Hosts)>? BillingOverride { get; set; }

    /// <summary>Where per-CLI logins' folders are; tests point it elsewhere (never the real ~/.claude-accounts).</summary>
    internal static Func<AccountDirs>? DirsOverride { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.OwnLogin is not null)
        {
            if (settings.ApiKey || settings.Account is not null)
            {
                AnsiConsole.MarkupLine("[red]--account signs in a per-CLI login on its own: leave out the account argument and --api-key.[/]");
                return 1;
            }
            return SignInOwn(DirsOverride?.Invoke() ?? new AccountDirs(), settings.OwnLogin, settings.Sso);
        }
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
        if (AccountSwitch.Find(rows, settings.Account!, out var several) is not { } row)
        {
            AnsiConsole.MarkupLine(several.Count > 1
                ? $"[red]\"{Markup.Escape(settings.Account!.Trim())}\" matches more than one account ({Markup.Escape(string.Join(", ", several.Select(r => r.Label)))}): name its whole email.[/]"
                : $"[red]No one account here is \"{Markup.Escape(settings.Account!)}\": name its email or the part before the @.[/]");
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

    /// <summary>Prepares <paramref name="email"/>'s own folder and signs it in (<c>claude auth login --email</c> under it).</summary>
    internal static int SignInOwn(AccountDirs dirs, string email, bool sso)
    {
        if (AccountDirs.Normalize(email) is null)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(email)}\" isn't an email address: --account takes the account's whole email.[/]");
            return 1;
        }
        var dir = dirs.For(email);
        foreach (var line in dirs.Prepare(dir)) AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
        var command = $"CLAUDE_CONFIG_DIR={dir.Directory} {AccountSwitch.LoginCommand(dir.Email, sso)}";
        AnsiConsole.MarkupLine($"Opening {Markup.Escape(dir.Email)}'s login in the browser ({Markup.Escape(command)}).");
        int code;
        try { code = AccountSwitch.LoginInto(dir.Email, sso, dir.Directory); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Couldn't run {Markup.Escape(command)}: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        if (code != 0)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(command)} ended with exit code {code}; {Markup.Escape(dir.Email)} isn't signed in.[/]");
            return 1;
        }
        if (!dir.SignedIn)
        {
            AnsiConsole.MarkupLine($"[red]The login finished but no Claude account is signed in to in {Markup.Escape(dir.Directory)}.[/]");
            return 1;
        }
        if (dir.SignedInEmail is { } signed && !signed.Equals(dir.Email, StringComparison.OrdinalIgnoreCase))
            AnsiConsole.MarkupLine($"[yellow]Signed in as {Markup.Escape(signed)}, not {Markup.Escape(dir.Email)}: CLIs started with --account {Markup.Escape(dir.Email)} spend {Markup.Escape(signed)}'s tokens.[/]");
        AnsiConsole.MarkupLine($"Signed in: {Markup.Escape(dir.Email)} has its own login in {Markup.Escape(dir.Directory)}. " +
            $"Start a CLI on it with [bold]orchestrator start <repo> --account {Markup.Escape(dir.Email)}[/]; every other CLI stays on the global login.");
        return 0;
    }
}
