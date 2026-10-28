namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The team gateway's settings (M.ORCH-T933), <see cref="OrchestratorSettings.Gateway"/>. Nothing secret lives here: a PFX
/// password comes from the <c>MINDATTIC_GATEWAY_CERT_PASSWORD</c> environment variable, and the keys are the gateway's own files.
/// </summary>
public sealed record GatewaySettings
{
    /// <summary>Off by default. On, the daemon starts the gateway and keeps it running; off, it stops the one it started.</summary>
    public bool Enabled { get; init; }

    /// <summary>The address it listens on: 127.0.0.1 by default, so turning it on exposes nothing until this is a Tailscale or VPN
    /// address (or 0.0.0.0).</summary>
    public string Bind { get; init; } = "127.0.0.1";

    public int Port { get; init; } = Term.TermGateway.DefaultPort;

    /// <summary>A PFX certificate to serve.</summary>
    public string? CertPath { get; init; }

    /// <summary>Or a certificate's thumbprint, in CurrentUser\My or LocalMachine\My. With neither, a self-signed dev certificate
    /// that clients pin.</summary>
    public string? CertThumbprint { get; init; }

    /// <summary>Extra DNS names or IPs the dev certificate covers (a Tailscale MagicDNS name or 100.x address).</summary>
    public List<string> DevCertNames { get; init; } = [];

    /// <summary>The machine owner's identity, its tokens' subject; null is the Windows user name.</summary>
    public string? Owner { get; init; }

    /// <summary>How long a token lives, 1 to 60 minutes.</summary>
    public int TokenMinutes { get; init; } = 15;

    /// <summary>Teammates' gateways this machine's Team Wall watches (M.ORCH-T936), changed with <c>orchestrator gateway remote
    /// add|remove|list</c>. Not part of <see cref="Arguments"/>: they never restart this machine's gateway. Each token is sealed
    /// with DPAPI for this Windows user (<see cref="GatewayRemote.SealedToken"/>), so this list still holds no plain secret.</summary>
    public List<GatewayRemote> Remotes { get; init; } = [];

    /// <summary>The gateway's arguments for these settings (never a secret): what it runs with, and how the daemon tells a change.</summary>
    public IReadOnlyList<string> Arguments()
    {
        var args = new List<string> { "serve", "--bind", Bind, "--port", Port.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(CertThumbprint)) args.AddRange(["--cert-thumbprint", CertThumbprint.Trim()]);
        else if (!string.IsNullOrWhiteSpace(CertPath)) args.AddRange(["--cert-path", CertPath.Trim()]);
        foreach (var name in DevCertNames.Where(n => !string.IsNullOrWhiteSpace(n))) args.AddRange(["--dev-cert-name", name.Trim()]);
        if (!string.IsNullOrWhiteSpace(Owner)) args.AddRange(["--owner", Owner.Trim()]);
        args.AddRange(["--token-minutes", Math.Clamp(TokenMinutes, 1, 60).ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        return args;
    }
}
