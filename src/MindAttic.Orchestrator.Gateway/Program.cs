using System.Diagnostics;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Term;

// The team gateway (M.ORCH-T933): see GatewayOptions.Usage and docs/spikes/cli-host-viewer.md, "Phase 3 gateway".
var options = GatewayOptions.Parse(args, out var error);
if (options is null || options.Command == "help")
{
    if (error.Length > 0) Console.Error.WriteLine(error);
    Console.Error.WriteLine(GatewayOptions.Usage);
    return options is null ? 2 : 0;
}

var log = new GatewayLog(options.Command == "serve" ? options.DataDir : null);
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    log.Write($"unhandled exception (terminating={e.IsTerminating}): {(e.ExceptionObject as Exception)?.GetType().Name}: {(e.ExceptionObject as Exception)?.Message}");
TaskScheduler.UnobservedTaskException += (_, e) => { log.Error(e.Exception, "unobserved task exception; contained"); e.SetObserved(); };

try
{
    switch (options.Command)
    {
        case "token":
        {
            // Reading the signing key proves you are this machine's owner, so the token is minted here, not over HTTP.
            var tokens = new GatewayTokens(GatewayKeys.LoadOrCreate(options.DataDir, GatewayKeys.SigningKeyFile));
            var (token, _) = tokens.Issue(options.Owner, options.Scope, TimeSpan.FromMinutes(options.Minutes ?? options.TokenMinutes));
            Console.WriteLine(token);
            return 0;
        }
        case "owner-key":
            Console.WriteLine(GatewayKeys.Text(GatewayKeys.LoadOrCreate(options.DataDir, GatewayKeys.OwnerKeyFile)));
            return 0;
        case "fingerprint":
        {
            using var cert = GatewayCertificates.Load(options);
            Console.WriteLine(GatewayCertificates.Pin(cert));
            return 0;
        }
    }

    // One gateway per user session: a second exits at once, leaving the first serving.
    using var single = new Mutex(initiallyOwned: true, @"Local\mindattic-orchestrator-gateway", out var first);
    if (!first)
    {
        log.Write("another gateway is already running; exiting");
        return 3;
    }

    var certificate = GatewayCertificates.Load(options);
    var pin = GatewayCertificates.Pin(certificate);
    // M.ORCH-T935: the Vault's secret values, held in memory only, so the masker hides them as literal strings on remote streams.
    var secrets = new SecretMasker(VaultSecrets.Load(log.Write));
    log.Write($"masking token shapes and {secrets.LiteralCount} Vault-held values on remote streams");
    using var broadcast = new BroadcastFile(options.DataDir);
    var watchers = new WatcherBoard(options.DataDir, message => TermWatchersBus.TrySend(message));
    var services = new GatewayServices
    {
        Options = options,
        Tokens = new GatewayTokens(GatewayKeys.LoadOrCreate(options.DataDir, GatewayKeys.SigningKeyFile)),
        OwnerKey = GatewayKeys.LoadOrCreate(options.DataDir, GatewayKeys.OwnerKeyFile),
        // M.ORCH-T934: members' grants and org membership from orchestrator.db (the snapshot the daemon writes), and the audit
        // spool the daemon imports into it.
        Grants = new AclGrants(options.Owner, options.DataDir),
        Sessions = new RegistrySessions(),
        Pipes = new NamedPipeConnector(),
        Log = log,
        Audit = new SpoolAudit(options.DataDir, log),
        Broadcast = broadcast,
        Secrets = secrets,
        Watchers = watchers,
    };

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
    builder.WebHost.ConfigureKestrel(k =>
    {
        k.AddServerHeader = false;
        k.Limits.MaxConcurrentConnections = 256;
        k.Listen(options.BindAddress!, options.Port, listen => listen.UseHttps(certificate));
    });
    var app = GatewayApp.Build(builder, services);
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        using var self = Process.GetCurrentProcess();
        TermGateway.WriteStatus(new TermGatewayStatus
        {
            Pid = Environment.ProcessId, ProcessStartUtc = self.StartTime.ToUniversalTime(), Bind = options.Bind, Port = options.Port,
            CertSha256 = pin, Config = options.Config, Version = GatewayApp.Version,
            Managed = Environment.GetEnvironmentVariable(TermGateway.ManagedVariable) == "1",
        }, options.DataDir);
        // Whatever a previous run left: nobody is attached yet.
        watchers.Reset();
        log.Write($"gateway {GatewayApp.Version} listening on https://{options.Bind}:{options.Port} for {options.Owner}, certificate " +
                  $"{(options.UsesDevCert ? "self-signed dev" : "configured")} SHA-256 {pin}");
    });
    await app.RunAsync().ConfigureAwait(false);
    watchers.Reset();
    watchers.Drain(TimeSpan.FromSeconds(1));
    TermGateway.DeleteStatus(options.DataDir);
    log.Write("gateway stopped");
    return 0;
}
catch (Exception ex)
{
    log.Error(ex, "the gateway failed; exiting");
    Console.Error.WriteLine($"Gateway failed: {ex.Message}");
    return 1;
}
