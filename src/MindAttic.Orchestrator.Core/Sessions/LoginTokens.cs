using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MindAttic.Vault.Credentials;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One per-CLI login's long-lived token (M.ORCH-T696), as it's kept: <see cref="Protected"/> is the token sealed with
/// Windows DPAPI for this Windows user, never the token itself.</summary>
public sealed record LoginToken(string Email, DateTime MintedUtc, DateTime ExpiresUtc, string Protected)
{
    public bool Expired(DateTime nowUtc) => nowUtc >= ExpiresUtc;

    /// <summary>Within <see cref="LoginTokens.WarnBefore"/> of its expiry (and not past it).</summary>
    public bool ExpiringSoon(DateTime nowUtc) => !Expired(nowUtc) && ExpiresUtc - nowUtc <= LoginTokens.WarnBefore;

    /// <summary>Usable for a new tab: not expired.</summary>
    public bool Valid(DateTime nowUtc) => !Expired(nowUtc);
}

/// <summary>How a <see cref="LoginTokens.MintFor"/> went.</summary>
public enum MintOutcome { Minted, NoToken, WrongAccount, Failed }

public sealed record MintResult(MintOutcome Outcome, string Message, LoginToken? Token = null)
{
    public bool Ok => Outcome == MintOutcome.Minted;
}

/// <summary>
/// Long-lived logins for per-CLI folders (M.ORCH-T696, from spike M.ORCH-T627). <c>claude setup-token</c> mints a one-year
/// OAuth token that bills the seat's own Claude subscription (inference only: no Remote Control, no claude.ai connectors). It's
/// printed once and never saved by Claude Code, so <see cref="MintFor"/> runs it under the account's folder, takes the token from
/// its output and keeps it in the MindAttic Vault (<see cref="Bucket"/>), sealed with DPAPI: never plaintext, never in the
/// folder's <c>.credentials.json</c>. A tab on that login gets it as <see cref="EnvVar"/> next to <c>CLAUDE_CONFIG_DIR</c>
/// (<see cref="TabEnv"/>), unsealed only inside the tab's own PowerShell; Claude Code ranks that variable above the folder's
/// <c>/login</c>, so the token (not the folder) picks the account, and one token is kept per folder. <c>--bare</c> ignores it.
/// </summary>
public sealed partial class LoginTokens(TokenStore? store = null)
{
    /// <summary>What Claude Code reads the long-lived token from.</summary>
    public const string EnvVar = "CLAUDE_CODE_OAUTH_TOKEN";

    /// <summary>The Vault bucket the tokens are kept in: <c>%APPDATA%\MindAttic\ClaudeLogins\tokens.json</c>, keyed by email.</summary>
    public const string Bucket = "ClaudeLogins";

    /// <summary>Variables that outrank <see cref="EnvVar"/> in Claude Code's auth order, unset in a tab that bills the token.</summary>
    public static readonly IReadOnlyList<string> Outranking = ["ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN"];

    /// <summary>How long a minted token lasts (Anthropic: one year).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    /// <summary>How long before its expiry a token is said to need minting again.</summary>
    public static readonly TimeSpan WarnBefore = TimeSpan.FromDays(30);

    /// <summary>DPAPI's extra secret for the seal, so another app's sealed blob can't be passed off as one of these.</summary>
    internal const string Entropy = "MindAttic.Orchestrator.ClaudeLoginToken";

    /// <summary>A Claude OAuth token as <c>claude setup-token</c> prints it.</summary>
    private static readonly Regex TokenPattern = new(@"sk-ant-oat\d{2}-[A-Za-z0-9_\-]{20,}", RegexOptions.CultureInvariant);

    public static LoginTokens Default { get; } = new();

    private TokenStore Store => store ?? TokenStore.ForBucket(Bucket);

    /// <summary>The token kept for <paramref name="email"/>; null when none is, or it can't be read.</summary>
    public LoginToken? For(string? email)
    {
        if (AccountDirs.Normalize(email) is not { } e) return null;
        try
        {
            if (Store.Get(e) is not { } raw || JsonNode.Parse(raw) is not JsonObject o) return null;
            if (o["sealed"]?.GetValue<string>() is not { Length: > 0 } sealedText) return null;
            var minted = o["minted"]?.GetValue<DateTime>() ?? DateTime.MinValue;
            var expires = o["expires"]?.GetValue<DateTime>() ?? minted + Lifetime;
            return new LoginToken(e, DateTime.SpecifyKind(minted, DateTimeKind.Utc), DateTime.SpecifyKind(expires, DateTimeKind.Utc), sealedText);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Seals <paramref name="token"/> and keeps it for <paramref name="email"/>, replacing any before it.</summary>
    public LoginToken Save(string email, string token, DateTime mintedUtc)
    {
        var e = AccountDirs.Normalize(email) ?? throw new ArgumentException($"\"{email}\" isn't an email address.", nameof(email));
        var kept = new LoginToken(e, mintedUtc, mintedUtc + Lifetime, Seal(token.Trim()));
        Store.Set(e, new JsonObject
        {
            ["sealed"] = kept.Protected,
            ["minted"] = kept.MintedUtc.ToString("O"),
            ["expires"] = kept.ExpiresUtc.ToString("O"),
        }.ToJsonString());
        return kept;
    }

    /// <summary>Forgets <paramref name="email"/>'s token (its tabs then use the folder's own <c>/login</c>).</summary>
    public bool Forget(string email) => AccountDirs.Normalize(email) is { } e && Store.Remove(e);

    /// <summary>The token itself; null when it can't be unsealed (another Windows user's, or damaged).</summary>
    public static string? Reveal(LoginToken token)
    {
        try { return Dpapi.Unprotect(Convert.FromBase64String(token.Protected), Encoding.UTF8.GetBytes(Entropy)) is { } b ? Encoding.UTF8.GetString(b) : null; }
        catch (FormatException) { return null; }
    }

    internal static string Seal(string token) =>
        Convert.ToBase64String(Dpapi.Protect(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(Entropy)));

    /// <summary>The token in <paramref name="output"/> (the last one, should it be printed twice); null when there's none.</summary>
    public static string? Find(string? output) =>
        output is null ? null : TokenPattern.Matches(output) is { Count: > 0 } m ? m[^1].Value : null;

    /// <summary><paramref name="text"/> with any token in it masked, for saying what <c>setup-token</c> printed.</summary>
    public static string Mask(string text) => TokenPattern.Replace(text, t => t.Value[..14] + "…" + t.Value[^4..]);

    /// <summary>
    /// The PowerShell statements a tab on <paramref name="configDir"/> runs before its Launcher host: with a token kept for the
    /// folder's account and not expired, the variables that would outrank it are removed and <see cref="EnvVar"/> is set to it,
    /// unsealed there and then (only the sealed form is ever on a command line); otherwise <see cref="EnvVar"/> is removed, so a
    /// token the starting process happens to carry never picks the account of a tab meant for another login (or the global one).
    /// Ends in "; ".
    /// </summary>
    public string TabEnv(string? configDir, DateTime nowUtc)
    {
        var remove = $"Remove-Item Env:{EnvVar} -ErrorAction SilentlyContinue; ";
        if (configDir is null || AccountDirs.Normalize(Path.GetFileName(Path.TrimEndingDirectorySeparator(configDir))) is not { } email) return remove;
        if (For(email) is not { } token || !token.Valid(nowUtc)) return remove;
        var outranking = string.Join(',', Outranking.Select(v => "Env:" + v));
        // Unsealed or not at all: a seal that won't open leaves the folder's own login, never a stray token.
        return $"Remove-Item {outranking} -ErrorAction SilentlyContinue; " +
            $"try {{ Add-Type -AssemblyName System.Security; $env:{EnvVar} = [Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect(" +
            $"[Convert]::FromBase64String({AccountDirs.Ps(token.Protected)}), [Text.Encoding]::UTF8.GetBytes({AccountDirs.Ps(Entropy)}), 'CurrentUser')) }} " +
            $"catch {{ {remove.TrimEnd(' ', ';')} }}; ";
    }

    /// <summary>What to say about kept tokens: each one expired or within <see cref="WarnBefore"/> of expiring, with how to
    /// mint it again.</summary>
    public IReadOnlyList<string> Warnings(IEnumerable<string> emails, DateTime nowUtc)
    {
        var lines = new List<string>();
        foreach (var email in emails.Select(AccountDirs.Normalize).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            if (For(email) is not { } t) continue;
            if (t.Expired(nowUtc))
                lines.Add($"{email}'s long-lived token expired {t.ExpiresUtc.ToLocalTime():yyyy-MM-dd}: its CLIs use the folder's own login until you run orchestrator login --account {email} to mint a new one.");
            else if (t.ExpiringSoon(nowUtc))
                lines.Add($"{email}'s long-lived token expires {t.ExpiresUtc.ToLocalTime():yyyy-MM-dd} ({Math.Max(1, (int)Math.Ceiling((t.ExpiresUtc - nowUtc).TotalDays))} days): run orchestrator login --account {email} --new-token to mint the next one.");
        }
        return lines;
    }

    /// <summary>Whether <paramref name="email"/> needs a token minted: none kept, it's expired or expiring soon, or it can't be
    /// unsealed here.</summary>
    public bool NeedsMint(string email, DateTime nowUtc) =>
        For(email) is not { } t || t.Expired(nowUtc) || t.ExpiringSoon(nowUtc) || Reveal(t) is null;

    /// <summary>
    /// Mints <paramref name="dir"/>'s long-lived token: runs <c>claude setup-token</c> under the folder (<see cref="Run"/>),
    /// takes the token from what it printed, checks with <c>claude auth status</c> (<see cref="StatusEmail"/>) that it's the
    /// folder's account when Claude Code can say, and keeps it (<see cref="Save"/>). A token for anybody else is never kept.
    /// </summary>
    /// <param name="paste">Asks the person for the token when <c>setup-token</c>'s output had none to take (its screen can print
    /// it where a pipe doesn't see it); null where nobody can be asked (the dashboard).</param>
    public MintResult MintFor(AccountDir dir, string? opener, DateTime nowUtc, Func<string?>? paste = null)
    {
        string output;
        try { output = Run(dir, opener); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or TimeoutException)
        {
            return new(MintOutcome.Failed, $"Couldn't run claude setup-token for {dir.Email}: {ex.Message}");
        }
        if ((Find(output) ?? Find(paste?.Invoke())) is not { } token)
            return new(MintOutcome.NoToken, $"claude setup-token printed no token for {dir.Email}, so it has no long-lived token: its CLIs use the folder's own login.");
        string? signed;
        try { signed = StatusEmail(dir, token); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { signed = null; }
        if (signed is not null && !signed.Equals(dir.Email, StringComparison.OrdinalIgnoreCase))
            return new(MintOutcome.WrongAccount, $"The token minted is {signed}'s, not {dir.Email}'s, so it wasn't kept: run orchestrator login --account {dir.Email} --new-token and sign in as {dir.Email}.");
        try
        {
            var kept = Save(dir.Email, token, nowUtc);
            return new(MintOutcome.Minted,
                $"{dir.Email} has a long-lived token (in the MindAttic Vault, sealed for this Windows user) until {kept.ExpiresUtc.ToLocalTime():yyyy-MM-dd}" +
                (signed is null ? "; /status in a CLI on it shows its account." : $", confirmed as {signed}."), kept);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicFailure)
        {
            return new(MintOutcome.Failed, $"Couldn't keep {dir.Email}'s token in the MindAttic Vault: {ex.Message}");
        }
    }

    /// <summary>The seam <see cref="MintFor"/> runs <c>claude setup-token</c> through: under the account's folder, its sign-in
    /// opening through <paramref name="opener"/> (its private browser window, <see cref="LoginBrowser"/>) when there is one,
    /// with every variable that would pick another account removed. Returns what it printed. Tests replace it, so no test opens a
    /// browser or mints a token.</summary>
    internal static TimeSpan MintTimeout { get; set; } = TimeSpan.FromMinutes(10);

    public static Func<AccountDir, string?, string> Run { get; set; } = (dir, opener) =>
    {
        var psi = Psi(dir, null, "setup-token");
        psi.RedirectStandardOutput = true;
        if (opener is not null) psi.Environment["BROWSER"] = opener;
        else psi.Environment.Remove("BROWSER");
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("claude didn't start");
        var outTask = p.StandardOutput.ReadToEndAsync();
        if (!p.WaitForExit((int)MintTimeout.TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            throw new TimeoutException($"claude setup-token didn't finish within {MintTimeout.TotalMinutes:0} minutes.");
        }
        var output = outTask.GetAwaiter().GetResult();
        // Whatever it said, masked, so the person sees how it went without the token ever reaching the screen or a log.
        foreach (var line in output.Split('\n').Select(l => Mask(l.TrimEnd('\r'))).Where(l => l.Trim().Length > 0))
            Console.Out.WriteLine(line);
        return output;
    };

    /// <summary>The seam that asks Claude Code who <paramref name="token"/> signs in as (<c>claude auth status --json</c> with
    /// it set under the account's folder): the email, or null when it can't say (the token's scope is inference only).</summary>
    public static Func<AccountDir, string, string?> StatusEmail { get; set; } = (dir, token) =>
    {
        var psi = Psi(dir, token, "auth", "status", "--json");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("claude didn't start");
        var outTask = p.StandardOutput.ReadToEndAsync();
        _ = p.StandardError.ReadToEndAsync(); // drained so a full stderr pipe can't block the process
        if (!p.WaitForExit(30_000))
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return null;
        }
        return ParseStatusEmail(outTask.GetAwaiter().GetResult());
    };

    /// <summary>The email in <c>claude auth status --json</c>'s output; null when it has none.</summary>
    internal static string? ParseStatusEmail(string output)
    {
        try { return JsonNode.Parse(output)?["email"]?.GetValue<string>() is { Length: > 0 } e ? e.Trim() : null; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    private static ProcessStartInfo Psi(AccountDir dir, string? token, params string[] args)
    {
        var psi = new ProcessStartInfo("claude") { UseShellExecute = false, CreateNoWindow = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment[AccountDirs.EnvVar] = dir.Directory;
        foreach (var v in Outranking) psi.Environment.Remove(v);
        if (token is null) psi.Environment.Remove(EnvVar);
        else psi.Environment[EnvVar] = token;
        return psi;
    }

    /// <summary>DPAPI failed to seal or unseal.</summary>
    public sealed class CryptographicFailure(string message) : Exception(message);

    /// <summary>Windows DPAPI for the current user (<c>CryptProtectData</c>), the same seal PowerShell's
    /// <c>ProtectedData.Unprotect(…, 'CurrentUser')</c> opens.</summary>
    internal static partial class Dpapi
    {
        private const int UiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob { public int Size; public IntPtr Data; }

        [LibraryImport("crypt32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CryptProtectData(ref Blob input, string? description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob output);

        [LibraryImport("crypt32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CryptUnprotectData(ref Blob input, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, int flags, ref Blob output);

        [LibraryImport("kernel32.dll")]
        private static partial IntPtr LocalFree(IntPtr memory);

        public static byte[] Protect(byte[] data, byte[] entropy) =>
            Call(data, entropy, (ref Blob i, ref Blob e, ref Blob o) => CryptProtectData(ref i, null, ref e, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref o))
            ?? throw new CryptographicFailure($"DPAPI couldn't seal the token (error {Marshal.GetLastPInvokeError()}).");

        public static byte[]? Unprotect(byte[] data, byte[] entropy) =>
            Call(data, entropy, (ref Blob i, ref Blob e, ref Blob o) => CryptUnprotectData(ref i, IntPtr.Zero, ref e, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref o));

        private delegate bool Crypt(ref Blob input, ref Blob entropy, ref Blob output);

        private static byte[]? Call(byte[] data, byte[] entropy, Crypt crypt)
        {
            var hData = GCHandle.Alloc(data, GCHandleType.Pinned);
            var hEntropy = GCHandle.Alloc(entropy, GCHandleType.Pinned);
            var output = default(Blob);
            try
            {
                var input = new Blob { Size = data.Length, Data = hData.AddrOfPinnedObject() };
                var extra = new Blob { Size = entropy.Length, Data = hEntropy.AddrOfPinnedObject() };
                if (!crypt(ref input, ref extra, ref output)) return null;
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally
            {
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
                hData.Free();
                hEntropy.Free();
            }
        }
    }
}
