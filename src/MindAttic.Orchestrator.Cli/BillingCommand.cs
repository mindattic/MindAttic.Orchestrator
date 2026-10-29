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
        [Description("team (the Claude Team login, for everyday tokens) or api (the MindAttic Vault's Claude key, for emergencies only); or credit, the API key person's starting credit (M.ORCH-T906).")]
        public string? Mode { get; init; }

        [CommandArgument(1, "[USD]")]
        [Description("With credit: the API key person's starting credit in US dollars (spend counts against it from now); 0 clears it. Without, prints its spend.")]
        public string? Amount { get; init; }
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
            WriteApiKeyPerson(now);
            return 0;
        }
        if (settings.Mode.Trim().Equals("credit", StringComparison.OrdinalIgnoreCase)) return Credit(settings.Amount, now);
        if (BillingModes.Parse(settings.Mode) is not { } mode)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(settings.Mode)}\" isn't a billing mode: use team (Claude Team) or api (the Vault's API key).[/]");
            return 1;
        }
        // An amount is only meaningful with credit (M.ORCH-T906): caught here rather than silently discarded, as when a
        // "billing credit 50" is mistyped "billing team 50" and the 50 would otherwise vanish with no sign it was ignored.
        if (settings.Amount is not null)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(settings.Amount)}\" isn't expected here: an amount is only for billing credit <usd>.[/]");
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

    /// <summary><c>orchestrator billing credit [usd]</c> (M.ORCH-T906): sets the API key person's starting credit (0 clears it), or
    /// with no amount prints its spend.</summary>
    private static int Credit(string? amount, DateTime nowUtc)
    {
        if (amount is not null)
        {
            if (!double.TryParse(amount.Trim().TrimStart('$'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var usd) ||
                usd < 0 || double.IsNaN(usd) || double.IsInfinity(usd))
            {
                AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(amount)}\" isn't an amount of US dollars: e.g. orchestrator billing credit 50.[/]");
                return 1;
            }
            var store = Ctx.Store();
            // LoadSettingsToChange: a settings row that can't be read is never saved over with defaults (bughunt 2026-10-27).
            store.SaveSettings(store.LoadSettingsToChange() with
            {
                ApiCreditUsd = usd > 0 ? usd : null,
                ApiCreditSinceUtc = usd > 0 ? nowUtc : null,
            });
            AnsiConsole.MarkupLine(usd > 0
                ? Markup.Escape($"The API key's starting credit is {ApiSpend.Usd(usd)}: what its CLIs spend from now on counts against it.")
                : "The API key's starting credit is cleared: its page shows only what it spends.");
        }
        WriteApiKeyPerson(nowUtc);
        return 0;
    }

    /// <summary>The API key person's spend line (M.ORCH-T906), from the ledger the daemon keeps; never throws.</summary>
    private static void WriteApiKeyPerson(DateTime nowUtc)
    {
        try
        {
            var store = Ctx.Store();
            var s = store.LoadSettings();
            var spend = new ApiSpendLedger(Path.Combine(store.Directory, ApiSpendLedger.FileName))
                .Spend(s.ApiCreditUsd, s.ApiCreditSinceUtc, nowUtc, DateTime.Today.ToUniversalTime());
            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(ApiKeyPerson.Name)} ({Markup.Escape(ApiKeyPerson.Email)}):[/] {Markup.Escape(spend.Line)}" +
                (spend.RunsOutUtc(nowUtc) is { } at ? Markup.Escape($"; runs out around {at.ToLocalTime():ddd MMM d h:mm tt} at that rate") : ""));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AnsiConsole.MarkupLine($"[yellow]Couldn't read the API key's spend: {Markup.Escape(ex.Message)}[/]");
        }
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
