using System.Net;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// The gateway's command line (M.ORCH-T933). The daemon builds it from <c>OrchestratorSettings.Gateway</c>; it is never given a
/// secret (a PFX password comes from <see cref="TermGateway.CertPasswordVariable"/>).
/// </summary>
internal sealed record GatewayOptions
{
    public const string Usage = """
        MindAttic.Orchestrator.Gateway [serve] [options]   run the gateway
        MindAttic.Orchestrator.Gateway token [--scope watch|interact] [--minutes N]
                                                            print a bearer token for the machine owner
        MindAttic.Orchestrator.Gateway owner-key           print the owner key POST /auth/token takes
        MindAttic.Orchestrator.Gateway fingerprint         print the SHA-256 pin of the certificate it serves

        Options:
          --bind <address>            listen address (default 127.0.0.1; 0.0.0.0 or a Tailscale/VPN address for a team)
          --port <n>                  HTTPS port (default 7443)
          --cert-path <file.pfx>      a PFX certificate (its password, if any, from MINDATTIC_GATEWAY_CERT_PASSWORD)
          --cert-thumbprint <hex>     a certificate in CurrentUser\My or LocalMachine\My
          --dev-cert-name <name>      an extra DNS name or IP for the self-signed dev certificate (repeatable)
          --owner <identity>          the machine owner's identity, the subject of its tokens (default the Windows user)
          --token-minutes <n>         token lifetime, 1 to 60 (default 15)
          --data-dir <dir>            keys, status, dev certificate and log (default %LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\gateway)
        With neither --cert-path nor --cert-thumbprint it serves a self-signed dev certificate: clients pin its SHA-256.
        """;

    public const int MaxTokenMinutes = 60;

    public string Command { get; init; } = "serve";
    public string Bind { get; init; } = "127.0.0.1";
    public int Port { get; init; } = TermGateway.DefaultPort;
    public string? CertPath { get; init; }
    public string? CertThumbprint { get; init; }
    public IReadOnlyList<string> DevCertNames { get; init; } = [];
    public string Owner { get; init; } = Environment.UserName;
    public int TokenMinutes { get; init; } = 15;
    public string DataDir { get; init; } = TermGateway.DefaultDirectory;

    // token's own.
    public string Scope { get; init; } = GatewayScopes.Interact;
    public int? Minutes { get; init; }

    /// <summary>The arguments as given, minus the executable: what the status file records so the daemon can tell a settings change.</summary>
    public string Config { get; init; } = "";

    public bool UsesDevCert => string.IsNullOrWhiteSpace(CertPath) && string.IsNullOrWhiteSpace(CertThumbprint);

    /// <summary>The listen address; null when <see cref="Bind"/> isn't one.</summary>
    public IPAddress? BindAddress => Bind.Trim().ToLowerInvariant() switch
    {
        "*" or "0.0.0.0" or "any" => IPAddress.Any,
        "::" => IPAddress.IPv6Any,
        "localhost" or "loopback" => IPAddress.Loopback,
        var text => IPAddress.TryParse(text, out var ip) ? ip : null,
    };

    public static GatewayOptions? Parse(IReadOnlyList<string> args, out string error)
    {
        error = "";
        var o = new GatewayOptions { Config = string.Join(' ', args) };
        var names = new List<string>();
        var i = 0;
        if (args.Count > 0 && !args[0].StartsWith('-'))
        {
            var command = args[0].ToLowerInvariant();
            if (command is not ("serve" or "token" or "owner-key" or "fingerprint")) { error = $"Unknown command \"{args[0]}\"."; return null; }
            o = o with { Command = command };
            i = 1;
        }
        for (; i < args.Count; i++)
        {
            var name = args[i];
            if (name is "-h" or "--help" or "/?") { error = ""; return o with { Command = "help" }; }
            if (i + 1 >= args.Count) { error = $"{name} needs a value."; return null; }
            var value = args[++i];
            switch (name)
            {
                case "--bind": o = o with { Bind = value }; break;
                case "--port" when int.TryParse(value, out var port) && port is > 0 and < 65536: o = o with { Port = port }; break;
                case "--cert-path": o = o with { CertPath = value }; break;
                case "--cert-thumbprint": o = o with { CertThumbprint = value.Replace(" ", "").Replace(":", "") }; break;
                case "--dev-cert-name": names.Add(value); break;
                case "--owner" when value.Trim().Length > 0: o = o with { Owner = value.Trim() }; break;
                case "--token-minutes" when int.TryParse(value, out var tm) && tm is >= 1 and <= MaxTokenMinutes: o = o with { TokenMinutes = tm }; break;
                case "--data-dir": o = o with { DataDir = value }; break;
                case "--scope" when GatewayScopes.IsValid(value): o = o with { Scope = value.ToLowerInvariant() }; break;
                case "--minutes" when int.TryParse(value, out var m) && m is >= 1 and <= MaxTokenMinutes: o = o with { Minutes = m }; break;
                default: error = $"Bad option or value: {name} {value}"; return null;
            }
        }
        o = o with { DevCertNames = names };
        if (o.BindAddress is null) { error = $"--bind {o.Bind} isn't an IP address."; return null; }
        return o;
    }
}
