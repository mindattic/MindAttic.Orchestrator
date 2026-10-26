using System.ComponentModel;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator gateway [on|off]</c> (M.ORCH-T933): shows the team gateway's settings and whether it runs, turns it on or off,
/// and changes where it listens and what certificate it serves. The daemon starts, restarts or stops it within a tick.
/// </summary>
internal sealed class GatewayCommand : Command<GatewayCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ON_OFF]")]
        [Description("on or off; without it, only shows the gateway.")]
        public string? Switch { get; init; }

        [CommandOption("--bind <ADDRESS>")]
        [Description("Listen address: 127.0.0.1 (default), a Tailscale/VPN address, or 0.0.0.0.")]
        public string? Bind { get; init; }

        [CommandOption("--port <PORT>")]
        public int? Port { get; init; }

        [CommandOption("--cert-path <PFX>")]
        [Description("A PFX certificate (its password from MINDATTIC_GATEWAY_CERT_PASSWORD); \"none\" clears.")]
        public string? CertPath { get; init; }

        [CommandOption("--cert-thumbprint <HEX>")]
        [Description("A certificate in CurrentUser\\My or LocalMachine\\My; \"none\" clears. With neither, a pinned self-signed dev certificate.")]
        public string? CertThumbprint { get; init; }

        [CommandOption("--dev-cert-names <NAMES>")]
        [Description("Extra DNS names or IPs for the dev certificate, comma-separated (a Tailscale name or 100.x address); \"none\" clears.")]
        public string? DevCertNames { get; init; }

        [CommandOption("--owner <IDENTITY>")]
        [Description("The machine owner's identity, its tokens' subject; \"none\" goes back to the Windows user.")]
        public string? Owner { get; init; }

        [CommandOption("--token-minutes <N>")]
        public int? TokenMinutes { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var store = Ctx.Store();
        var s = store.LoadSettingsToChange();
        var g = s.Gateway;
        static string? Clear(string value) => value.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
        if (settings.Switch is { } sw)
        {
            if (sw.Equals("on", StringComparison.OrdinalIgnoreCase)) g = g with { Enabled = true };
            else if (sw.Equals("off", StringComparison.OrdinalIgnoreCase)) g = g with { Enabled = false };
            else { AnsiConsole.MarkupLine("[red]Say on or off.[/]"); return 1; }
        }
        if (settings.Bind is { } bind)
        {
            if (!(bind is "*" or "0.0.0.0" or "::" or "localhost" || System.Net.IPAddress.TryParse(bind, out _))) { AnsiConsole.MarkupLine("[red]--bind takes an IP address.[/]"); return 1; }
            g = g with { Bind = bind };
        }
        if (settings.Port is { } port)
        {
            if (port is < 1 or > 65535) { AnsiConsole.MarkupLine("[red]--port is 1 to 65535.[/]"); return 1; }
            g = g with { Port = port };
        }
        if (settings.CertPath is { } path) g = g with { CertPath = Clear(path) };
        if (settings.CertThumbprint is { } thumb) g = g with { CertThumbprint = Clear(thumb)?.Replace(" ", "").Replace(":", "") };
        if (settings.DevCertNames is { } names)
            g = g with { DevCertNames = Clear(names) is { } list ? [.. list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)] : [] };
        if (settings.Owner is { } owner) g = g with { Owner = Clear(owner) };
        if (settings.TokenMinutes is { } minutes)
        {
            if (minutes is < 1 or > 60) { AnsiConsole.MarkupLine("[red]--token-minutes is 1 to 60.[/]"); return 1; }
            g = g with { TokenMinutes = minutes };
        }
        if (g != s.Gateway)
        {
            store.SaveSettings(s with { Gateway = g });
            AnsiConsole.MarkupLine("[green]Saved.[/] [grey]The daemon starts, restarts or stops the gateway within a tick.[/]");
        }

        AnsiConsole.MarkupLine($"Team gateway: {(g.Enabled ? "[green]on[/]" : "[grey]off[/]")} · https://{Markup.Escape(g.Bind)}:{g.Port} · owner {Markup.Escape(g.Owner ?? Environment.UserName)} · tokens {g.TokenMinutes} min");
        AnsiConsole.MarkupLine("Certificate: " + Markup.Escape(
            !string.IsNullOrWhiteSpace(g.CertThumbprint) ? $"thumbprint {g.CertThumbprint}"
            : !string.IsNullOrWhiteSpace(g.CertPath) ? g.CertPath
            : "self-signed dev certificate" + (g.DevCertNames.Count > 0 ? $" (also {string.Join(", ", g.DevCertNames)})" : "")));
        if (TermGateway.Running() is { } running)
            AnsiConsole.MarkupLine($"Running: pid {running.Pid}, {Markup.Escape(running.Bind)}:{running.Port}, version {Markup.Escape(running.Version)}" +
                                   $"{(running.Managed ? "" : " (started by hand)")}, certificate SHA-256 [aqua]{running.CertSha256}[/]");
        else AnsiConsole.MarkupLine("[grey]Not running.[/]");
        AnsiConsole.MarkupLine($"[grey]A token: {TermGateway.ExeName} token [[--scope watch]]; the owner key for POST /auth/token: {TermGateway.ExeName} owner-key.[/]");
        return 0;
    }
}
