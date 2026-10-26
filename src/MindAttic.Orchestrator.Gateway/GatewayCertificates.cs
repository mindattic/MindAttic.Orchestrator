using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// The TLS certificate Kestrel serves (M.ORCH-T933), from the first that applies:
/// <list type="number">
/// <item><c>--cert-thumbprint</c>: a certificate with its private key in CurrentUser\My, else LocalMachine\My.</item>
/// <item><c>--cert-path</c>: a PFX file; its password, if it has one, from <see cref="TermGateway.CertPasswordVariable"/>.</item>
/// <item>Otherwise a self-signed development certificate, <c>dev-cert.pfx</c> in the data folder, made once (and again before it
/// expires or when the names it must cover change) for localhost, this machine's name and every <c>--dev-cert-name</c>.
/// No client trusts it: a client pins its SHA-256 (<see cref="Pin"/>, <c>MindAttic.Orchestrator.Gateway fingerprint</c>).</item>
/// </list>
/// </summary>
internal static class GatewayCertificates
{
    public const string DevCertFile = "dev-cert.pfx", DevCertNamesFile = "dev-cert.names";
    private static readonly TimeSpan DevLifetime = TimeSpan.FromDays(365), RenewBefore = TimeSpan.FromDays(30);

    public static X509Certificate2 Load(GatewayOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CertThumbprint)) return FromStore(options.CertThumbprint);
        if (!string.IsNullOrWhiteSpace(options.CertPath))
        {
            var password = Environment.GetEnvironmentVariable(TermGateway.CertPasswordVariable);
            var cert = X509CertificateLoader.LoadPkcs12FromFile(options.CertPath, string.IsNullOrEmpty(password) ? null : password, X509KeyStorageFlags.UserKeySet);
            if (!cert.HasPrivateKey) throw new InvalidOperationException($"The certificate in {options.CertPath} has no private key.");
            return cert;
        }
        return DevCertificate(options.DataDir, options.DevCertNames);
    }

    /// <summary>What a client pins: the certificate's SHA-256, upper-case hex.</summary>
    public static string Pin(X509Certificate2 cert) => cert.GetCertHashString(HashAlgorithmName.SHA256);

    private static X509Certificate2 FromStore(string thumbprint)
    {
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            foreach (var cert in found)
                if (cert.HasPrivateKey) return cert;
        }
        throw new InvalidOperationException($"No certificate with thumbprint {thumbprint} and its private key in CurrentUser\\My or LocalMachine\\My.");
    }

    /// <summary>The names a dev certificate covers: localhost, 127.0.0.1, ::1, this machine's name and the extra ones.</summary>
    internal static IReadOnlyList<string> DevNames(IEnumerable<string> extra) =>
        new[] { "localhost", "127.0.0.1", "::1", Environment.MachineName }.Concat(extra.Select(n => n.Trim()).Where(n => n.Length > 0))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    internal static X509Certificate2 DevCertificate(string directory, IEnumerable<string> extraNames)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DevCertFile);
        var namesPath = Path.Combine(directory, DevCertNamesFile);
        var names = DevNames(extraNames);
        var wanted = string.Join("\n", names.Order(StringComparer.OrdinalIgnoreCase));
        if (File.Exists(path) && File.Exists(namesPath) && File.ReadAllText(namesPath) == wanted)
        {
            try
            {
                var existing = X509CertificateLoader.LoadPkcs12FromFile(path, null, X509KeyStorageFlags.UserKeySet);
                if (existing.HasPrivateKey && existing.NotAfter.ToUniversalTime() - DateTime.UtcNow > RenewBefore) return existing;
                existing.Dispose();
            }
            catch (CryptographicException) { } // made again below
        }

        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=MindAttic Orchestrator Gateway (dev)", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false)); // server auth
        var san = new SubjectAlternativeNameBuilder();
        foreach (var name in names)
        {
            if (IPAddress.TryParse(name, out var ip)) san.AddIpAddress(ip);
            else san.AddDnsName(name);
        }
        request.CertificateExtensions.Add(san.Build());
        var now = DateTimeOffset.UtcNow;
        using var made = request.CreateSelfSigned(now.AddDays(-1), now + DevLifetime);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, made.Export(X509ContentType.Pfx));
        File.Move(temp, path, overwrite: true);
        File.WriteAllText(namesPath, wanted);
        // Loaded back from the file: on Windows, SChannel can't use the ephemeral key CreateSelfSigned returns.
        return X509CertificateLoader.LoadPkcs12FromFile(path, null, X509KeyStorageFlags.UserKeySet);
    }
}
