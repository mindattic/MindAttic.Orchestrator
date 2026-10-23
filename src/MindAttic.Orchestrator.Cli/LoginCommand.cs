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
/// <para>Each per-CLI login also gets its long-lived token (M.ORCH-T696): <c>claude setup-token</c> under its folder, kept in the
/// MindAttic Vault (<see cref="LoginTokens"/>) and set as <c>CLAUDE_CODE_OAUTH_TOKEN</c> in its tabs, so the seat stays signed in
/// for a year. <c>--new-token</c> mints a new one even though one is kept (it's near expiry, or was revoked).</para>
/// <para><c>--all</c> (M.ORCH-T604) does that for every account in the roster with no per-CLI login yet, in turn.</para>
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

        [CommandOption("--all")]
        [Description("Sign in a per-CLI login for every account in the roster that has none yet, one after another, so every seat stays signed in side by side.")]
        public bool All { get; init; }

        [CommandOption("--new-token")]
        [Description("With --account or --all: mint a new long-lived token (claude setup-token) even though one is kept, as when it nears its one-year expiry or was revoked.")]
        public bool NewToken { get; init; }

        [CommandOption("--sso")]
        [Description("Force the SSO login flow (it's used anyway when the account's org signs in through SSO).")]
        public bool Sso { get; init; }
    }

    /// <summary>The billing switch and the Launcher tabs it sees; tests point both elsewhere (never the real ~/.claude.json).</summary>
    internal static Func<(BillingSwitch Switch, IReadOnlyList<HostSession> Hosts)>? BillingOverride { get; set; }

    /// <summary>Where per-CLI logins' folders are; tests point it elsewhere (never the real ~/.claude-accounts).</summary>
    internal static Func<AccountDirs>? DirsOverride { get; set; }

    /// <summary>Where long-lived tokens are kept (M.ORCH-T696); tests point it elsewhere (never the real Vault). Null mints none.</summary>
    internal static Func<LoginTokens?> TokensOverride { get; set; } = () => LoginTokens.Default;

    /// <summary>The minting a sign-in does: tokens kept where <see cref="TokensOverride"/> says, the token asked for when the
    /// setup-token output had none and someone's at the console to paste it.</summary>
    internal static TokenMinting? Minting(bool renew) => TokensOverride() is { } tokens
        ? new TokenMinting(tokens, renew, () => AnsiConsole.Profile.Capabilities.Interactive
            ? AnsiConsole.Prompt(new TextPrompt<string>("claude setup-token's token wasn't captured: paste it here (Enter to skip):").Secret().AllowEmpty())
            : null)
        : null;

    /// <summary>A token mint that was tried and didn't land (Failed, NoToken, WrongAccount): the sign-in itself may be fine, but
    /// the command still exits non-zero (M.ORCH-T718).</summary>
    internal static bool MintFailed(OwnLoginResult result) => result.Token is { Ok: false };

    public override int Execute(CommandContext context, Settings settings)
    {
        if (settings.NewToken && !settings.All && settings.OwnLogin is null)
        {
            AnsiConsole.MarkupLine("[red]--new-token mints a per-CLI login's token: use it with --account <email> or --all.[/]");
            return 1;
        }
        if (settings.All)
        {
            if (settings.ApiKey || settings.Account is not null || settings.OwnLogin is not null)
            {
                AnsiConsole.MarkupLine("[red]--all signs in every account's own login on its own: leave out the account, --account and --api-key.[/]");
                return 1;
            }
            return SignInAll(DirsOverride?.Invoke() ?? new AccountDirs(), AccountsCommand.Load().Rows, settings.NewToken);
        }
        if (settings.OwnLogin is not null)
        {
            if (settings.ApiKey || settings.Account is not null)
            {
                AnsiConsole.MarkupLine("[red]--account signs in a per-CLI login on its own: leave out the account argument and --api-key.[/]");
                return 1;
            }
            return SignInOwn(DirsOverride?.Invoke() ?? new AccountDirs(), settings.OwnLogin, settings.Sso, settings.NewToken);
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
            if (TokensOverride() is { } tokens)
            {
                var dirs = DirsOverride?.Invoke() ?? new AccountDirs();
                foreach (var line in tokens.Warnings(dirs.All().Select(d => d.Email).Concat(rows.Select(r => r.Email).OfType<string>()), now))
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(line)}[/]");
            }
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
    internal static int SignInOwn(AccountDirs dirs, string email, bool sso, bool newToken = false)
    {
        if (AccountDirs.Normalize(email) is null)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(email)}\" isn't an email address: --account takes the account's whole email.[/]");
            return 1;
        }
        var minting = Minting(newToken);
        // Signed in already as itself: no browser login again; only a missing or expiring token (or --new-token) is minted, as --all does.
        var result = dirs.For(email) is { SignedIn: true } signed && signed.SignedInEmail is { } e && e.Equals(signed.Email, StringComparison.OrdinalIgnoreCase)
            ? new OwnLoginResult(signed.Email, signed.Email, signed.Directory, OwnLoginOutcome.AlreadySignedIn, $"{signed.Email} already has its own login.", [])
                { Token = OwnLogins.MintSignedIn(signed, Opening, minting) }
            : OwnLogins.SignIn(dirs, email, sso, Opening, minting);
        Report(result);
        if (!result.Ok) return 1;
        AnsiConsole.MarkupLine($"Start a CLI on it with [bold]orchestrator start <repo> --account {Markup.Escape(result.Email!)}[/]; every other CLI stays on the global login.");
        return MintFailed(result) ? 1 : 0;
    }

    /// <summary><c>login --all</c> (M.ORCH-T604): every roster account with no per-CLI login yet gets its own folder and its
    /// login, one after another, so every Claude Team seat is signed in side by side. Fails when any login did.</summary>
    internal static int SignInAll(AccountDirs dirs, IReadOnlyList<AccountRow> rows, bool newToken = false)
    {
        var results = OwnLogins.SignInAll(dirs, rows, Opening, Report, Minting(newToken));
        AnsiConsole.MarkupLine(Markup.Escape(OwnLogins.Summary(results)));
        if (results.Any(r => r.Ok))
            AnsiConsole.MarkupLine("[grey]Start a CLI on any of them with orchestrator start <repo> --account <email>; the global login stays the default.[/]");
        return results.Any(r => r.Outcome is OwnLoginOutcome.Failed or OwnLoginOutcome.SignedInAsOther || MintFailed(r)) ? 1 : 0;
    }

    /// <summary>Signs <paramref name="email"/>'s own folder in for <c>start --account</c>, said as <c>login --account</c> says it.</summary>
    internal static OwnLoginResult SignInFirst(AccountDirs dirs, string email)
    {
        var result = OwnLogins.SignIn(dirs, email, sso: false, Opening, Minting(renew: false));
        Report(result);
        return result;
    }

    private static void Opening(AccountDir dir, string command) =>
        AnsiConsole.MarkupLine($"Opening {Markup.Escape(dir.Email)}'s own sign-in in " +
            (command.Contains("BROWSER=", StringComparison.Ordinal) ? "a private browser window" : "the browser") +
            $" ({Markup.Escape(command)}): sign in there as {Markup.Escape(dir.Email)}.");

    private static void Report(OwnLoginResult result)
    {
        foreach (var line in result.Notes) AnsiConsole.MarkupLine($"[grey]{Markup.Escape(line)}[/]");
        var color = result.Outcome switch
        {
            OwnLoginOutcome.Failed or OwnLoginOutcome.SignedInAsOther => "red",
            OwnLoginOutcome.NoEmail or OwnLoginOutcome.AlreadySignedIn => "grey",
            _ => "default",
        };
        AnsiConsole.MarkupLine($"[{color}]{Markup.Escape(result.Message)}[/]");
        if (result.Token is { } token)
            AnsiConsole.MarkupLine($"[{(token.Ok ? "default" : "yellow")}]{Markup.Escape(token.Message)}[/]");
    }
}
