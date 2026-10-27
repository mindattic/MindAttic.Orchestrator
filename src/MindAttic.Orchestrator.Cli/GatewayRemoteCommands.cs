using System.ComponentModel;
using System.Globalization;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator gateway remote add &lt;name&gt; &lt;url&gt; --pin &lt;sha256&gt; [--person &lt;who&gt;] [--token &lt;token&gt;]</c>
/// (M.ORCH-T936): adds a teammate's gateway to the team wall, or replaces the one of that name. The token is the one the teammate
/// issued for this member (<c>orchestrator gateway token &lt;who&gt;</c> on their machine, M.ORCH-T934); it is asked for, hidden,
/// when <c>--token</c> is left out (<c>--token -</c> reads it from standard input), and kept DPAPI-sealed for this Windows user.
/// It is never printed or logged.
/// </summary>
internal sealed class GatewayRemoteAddCommand : Command<GatewayRemoteAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("A short name for it: letters, digits, dots, dashes and underscores.")]
        public string Name { get; init; } = "";

        [CommandArgument(1, "<URL>")]
        [Description("The gateway: https://host:port, or host:port (port 7443 when left out).")]
        public string Url { get; init; } = "";

        [CommandOption("--pin <SHA256>")]
        [Description("Its certificate's SHA-256, as orchestrator gateway prints it on their machine; \"none\" trusts Windows' usual checks instead.")]
        public string? Pin { get; init; }

        [CommandOption("--person <WHO>")]
        [Description("Whose CLIs they are, as the wall labels them (default: the name).")]
        public string? Person { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("The token they issued you; \"-\" reads it from standard input. Asked for, hidden, when left out.")]
        public string? Token { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!GatewayRemotes.IsValidName(settings.Name)) { AnsiConsole.MarkupLine("[red]A name is 1 to 40 letters, digits, dots, dashes or underscores.[/]"); return 1; }
        if (GatewayRemotes.NormalizeUrl(settings.Url) is not { } url) { AnsiConsole.MarkupLine("[red]The URL must be https://host:port (or host:port).[/]"); return 1; }
        string? pin = null;
        if (settings.Pin is null) { AnsiConsole.MarkupLine("[red]Say --pin <sha256> (orchestrator gateway prints it on their machine), or --pin none for a certificate Windows trusts.[/]"); return 1; }
        if (!settings.Pin.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            pin = GatewayRemotes.NormalizePin(settings.Pin);
            if (pin is null) { AnsiConsole.MarkupLine("[red]--pin is the certificate's SHA-256: 64 hex characters.[/]"); return 1; }
        }
        var token = ReadToken(settings.Token);
        if (string.IsNullOrWhiteSpace(token)) { AnsiConsole.MarkupLine("[red]No token given.[/]"); return 1; }
        if (GatewayRemotes.ExpiresUtc(token) is not { } expires)
            AnsiConsole.MarkupLine("[yellow]That doesn't look like a gateway token (v1.…); kept anyway.[/]");
        else if (expires <= DateTime.UtcNow)
            AnsiConsole.MarkupLine("[yellow]That token has already expired; ask for a new one.[/]");

        var name = settings.Name.Trim();
        var remote = new GatewayRemote
        {
            Name = name,
            Person = string.IsNullOrWhiteSpace(settings.Person) ? null : settings.Person.Trim(),
            Url = url,
            CertSha256 = pin,
            SealedToken = GatewayRemotes.Seal(token),
        };
        var store = Ctx.Store();
        var s = store.LoadSettingsToChange();
        var remotes = s.Gateway.Remotes.Where(r => !string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var replaced = remotes.Count != s.Gateway.Remotes.Count;
        remotes.Add(remote);
        store.SaveSettings(s with { Gateway = s.Gateway with { Remotes = remotes } });
        AnsiConsole.MarkupLine($"[green]{(replaced ? "Replaced" : "Added")}[/] {Markup.Escape(name)} ({Markup.Escape(remote.Who)}) at {Markup.Escape(url)}" +
                               (pin is null ? ", trusting Windows' certificate checks." : ", pinned."));
        AnsiConsole.MarkupLine("[grey]The dashboard's team wall picks it up on its next look.[/]");
        return 0;
    }

    private static string? ReadToken(string? given)
    {
        if (given is { } t && t != "-") return t.Trim();
        // Only an explicit "--token -" reads stdin (M.ORCH-T1161): omitting --token always prompts, even when stdin
        // happens to be redirected, so a forgotten flag never silently seals whatever's on stdin as the token.
        if (given == "-") return Console.In.ReadLine()?.Trim();
        return AnsiConsole.Prompt(new TextPrompt<string>("Token:").Secret()).Trim();
    }
}

/// <summary><c>orchestrator gateway remote remove &lt;name&gt;</c> (M.ORCH-T936).</summary>
internal sealed class GatewayRemoteRemoveCommand : Command<GatewayRemoteRemoveCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        public string Name { get; init; } = "";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var s = store.LoadSettingsToChange();
        var remotes = s.Gateway.Remotes.Where(r => !string.Equals(r.Name, settings.Name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (remotes.Count == s.Gateway.Remotes.Count) { AnsiConsole.MarkupLine($"[yellow]No gateway is called {Markup.Escape(settings.Name)}.[/]"); return 1; }
        store.SaveSettings(s with { Gateway = s.Gateway with { Remotes = remotes } });
        AnsiConsole.MarkupLine($"[green]Removed[/] {Markup.Escape(settings.Name.Trim())}.");
        return 0;
    }
}

/// <summary><c>orchestrator gateway remote [list] [--check]</c> (M.ORCH-T936): the teammates' gateways, never their tokens.</summary>
internal sealed class GatewayRemoteListCommand : Command<GatewayRemoteListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--check")]
        [Description("Also ask each gateway for its sessions: online with how many, or why not.")]
        public bool Check { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var remotes = Ctx.Settings.Gateway.Remotes;
        if (remotes.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No teammates' gateways yet: orchestrator gateway remote add <name> <url> --pin <sha256>.[/]");
            return 0;
        }
        var table = new Table().AddColumns("Name", "Person", "URL", "Pin", "Token");
        if (settings.Check) table.AddColumn("Now");
        foreach (var r in remotes)
        {
            using var client = new GatewayClient(r);
            var tokenText = !client.HasToken ? "[red]none here[/]"
                : client.TokenExpiresUtc is { } exp
                    ? exp <= DateTime.UtcNow ? "[red]expired[/]" : "expires " + exp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                    : "kept";
            var pin = r.CertSha256 is { Length: > 12 } p ? p[..12] + "…" : "[grey]Windows trust[/]";
            var row = new List<string> { Markup.Escape(r.Name), Markup.Escape(r.Who), Markup.Escape(r.Url), pin, tokenText };
            if (settings.Check)
            {
                var listing = client.ListAsync(CancellationToken.None).GetAwaiter().GetResult();
                row.Add(listing.Online ? $"[green]online[/], {listing.Sessions.Count} CLI(s)" : "[red]offline[/]: " + Markup.Escape(listing.Problem ?? ""));
            }
            table.AddRow([.. row]);
        }
        AnsiConsole.Write(table);
        return 0;
    }
}
