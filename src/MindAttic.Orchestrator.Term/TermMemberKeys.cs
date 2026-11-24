using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>One org member's key to the host's gateway (hub phase 1, M.ORCH-T1640): who it is for, the SHA-256 of the key (never
/// the key itself), when and by whom it was minted, and when it was revoked (null while it works).</summary>
public sealed record TermMemberKey
{
    /// <summary>The member's email, lower case: the subject of every token the key mints.</summary>
    [JsonPropertyName("email")] public string Email { get; init; } = "";

    /// <summary>The key's SHA-256, base64url: what a presented key is checked against.</summary>
    [JsonPropertyName("hash")] public string Hash { get; init; } = "";

    /// <summary>When the key was minted (UTC).</summary>
    [JsonPropertyName("createdUtc")] public DateTime CreatedUtc { get; init; }

    /// <summary>Who minted it (the host's Windows user).</summary>
    [JsonPropertyName("createdBy")] public string? CreatedBy { get; init; }

    /// <summary>When it was revoked (UTC); null while it works.</summary>
    [JsonPropertyName("revokedUtc")] public DateTime? RevokedUtc { get; init; }

    /// <summary>Hub phase 2 (M.ORCH-T1641): the Windows account this member's CLIs on the host run under, set by <c>orchestrator
    /// machine isolate</c>; null (and left out of the file) when none is. A name only, never a password.</summary>
    [JsonPropertyName("windowsUser")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WindowsUser { get; init; }

    /// <summary>Whether it still mints tokens: true until it is revoked.</summary>
    [JsonIgnore] public bool IsLive => RevokedUtc is null;
}

/// <summary>The member keys file's contents.</summary>
public sealed record TermMemberKeyFile
{
    /// <summary>One entry per member who was ever invited: the newest invite replaces an older one.</summary>
    [JsonPropertyName("keys")] public List<TermMemberKey> Keys { get; init; } = [];
}

/// <summary>
/// Per-member keys to the host's gateway (hub phase 1, M.ORCH-T1640; docs/research/hub-and-spoke.md §3.3). <c>orchestrator machine
/// invite &lt;member&gt;</c> mints one, the member's PC seals it with DPAPI (<c>machine connect --member-key</c>) and trades it at
/// <c>POST /auth/token</c> for short-lived tokens whose subject is the member's email, never the owner's. Only the key's SHA-256 is
/// kept here, in <see cref="KeysFile"/> beside <c>signing.key</c> and <c>owner.key</c> in the gateway's data folder (which only
/// this Windows user reads); the key itself is printed once, in the connect line. A key carries no password and no Claude
/// credential. <c>machine revoke</c> marks the entry revoked: the gateway reads the file again within a second, refuses the key and
/// every token for that member, and closes their open streams at its next access check. The gateway records when each member was
/// last seen in <see cref="SeenFile"/>, which the host dashboard's connected-members readout reads.
/// </summary>
public static class TermMemberKeys
{
    /// <summary>The member keys file, in the gateway's data folder.</summary>
    public const string KeysFile = "members.json";

    /// <summary>When the gateway last saw each member (a token minted, sessions listed, a tickets command), in its data folder.</summary>
    public const string SeenFile = "members-seen.json";

    /// <summary>What every member key starts with, so it is never mistaken for the owner key.</summary>
    public const string Prefix = "mk1_";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>The keys file's path.</summary>
    public static string KeysPath(string? directory = null) => Path.Combine(directory ?? TermGateway.DefaultDirectory, KeysFile);

    /// <summary>The seen file's path.</summary>
    public static string SeenPath(string? directory = null) => Path.Combine(directory ?? TermGateway.DefaultDirectory, SeenFile);

    /// <summary>A member's email as keys are filed under it: trimmed, lower case.</summary>
    public static string Normalize(string? email) => email?.Trim().ToLowerInvariant() ?? "";

    /// <summary>The SHA-256 of a presented key, base64url.</summary>
    public static string Hash(string key) => TermGatewayTokens.Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim())));

    /// <summary>Mints a new key for <paramref name="email"/>, replacing (and so revoking) any key they had, and returns it: the only
    /// time it is ever seen.</summary>
    public static string Invite(string email, string? createdBy, DateTime nowUtc, string? directory = null)
    {
        var who = Normalize(email);
        if (who.Length == 0 || !who.Contains('@', StringComparison.Ordinal)) throw new ArgumentException($"\"{email}\" isn't an email.", nameof(email));
        var key = Prefix + TermGatewayTokens.Base64Url(RandomNumberGenerator.GetBytes(32));
        Update(directory, file =>
        {
            // A new invite keeps the Windows account the member was given (hub phase 2, M.ORCH-T1641): it's about them, not the key.
            var account = file.Keys.FirstOrDefault(k => k is not null && Normalize(k.Email) == who)?.WindowsUser;
            var keys = file.Keys.Where(k => k is not null && Normalize(k.Email) != who).ToList();
            keys.Add(new TermMemberKey { Email = who, Hash = Hash(key), CreatedUtc = nowUtc, CreatedBy = createdBy, WindowsUser = account });
            return file with { Keys = [.. keys.OrderBy(k => k.Email, StringComparer.Ordinal)] };
        });
        return key;
    }

    /// <summary>Revokes <paramref name="email"/>'s key; false when they had no live one.</summary>
    public static bool Revoke(string email, DateTime nowUtc, string? directory = null)
    {
        var who = Normalize(email);
        var revoked = false;
        Update(directory, file =>
        {
            var keys = file.Keys.Where(k => k is not null).Select(k =>
            {
                if (Normalize(k.Email) != who || !k.IsLive) return k;
                revoked = true;
                return k with { RevokedUtc = nowUtc };
            }).ToList();
            return file with { Keys = keys };
        });
        return revoked;
    }

    /// <summary>Sets (or, with null, clears) the Windows account <paramref name="email"/>'s CLIs on the host run under (hub phase 2,
    /// M.ORCH-T1641); false when they were never invited. Only the name is kept; the file holds no password.</summary>
    public static bool SetWindowsUser(string email, string? account, string? directory = null)
    {
        var who = Normalize(email);
        var found = false;
        Update(directory, file =>
        {
            var keys = file.Keys.Where(k => k is not null).Select(k =>
            {
                if (Normalize(k.Email) != who) return k;
                found = true;
                return k with { WindowsUser = string.IsNullOrWhiteSpace(account) ? null : account.Trim() };
            }).ToList();
            return file with { Keys = keys };
        });
        return found;
    }

    /// <summary>Every member key on file (live and revoked), by email; empty when there is none or it can't be read.</summary>
    public static IReadOnlyList<TermMemberKey> List(string? directory = null) =>
        TryRead(directory, out var file) && file is not null ? [.. file.Keys.Where(k => k is not null)] : [];

    /// <summary>Reads the keys file: false when it couldn't be read just now (locked, no access), so a caller keeps what it had.
    /// True otherwise, with an empty file when there is none and null when it isn't one.</summary>
    public static bool TryRead(string? directory, out TermMemberKeyFile? file)
    {
        file = null;
        try
        {
            var path = KeysPath(directory);
            if (!File.Exists(path)) { file = new TermMemberKeyFile(); return true; }
            try { file = JsonSerializer.Deserialize<TermMemberKeyFile>(File.ReadAllText(path), Json); }
            catch (JsonException) { file = null; }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The member whose live key <paramref name="presented"/> is, or null. Every live entry is compared in constant time,
    /// so the answer's timing doesn't say how close a guess came.</summary>
    public static string? Match(TermMemberKeyFile? file, string? presented)
    {
        if (file is null || string.IsNullOrWhiteSpace(presented) || presented.Length > 256 || !presented.Trim().StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var given = Encoding.ASCII.GetBytes(Hash(presented));
        string? found = null;
        foreach (var k in file.Keys)
        {
            if (k is null || !k.IsLive || string.IsNullOrEmpty(k.Hash)) continue;
            var expected = Encoding.ASCII.GetBytes(k.Hash);
            if (expected.Length == given.Length && CryptographicOperations.FixedTimeEquals(expected, given)) found ??= Normalize(k.Email);
        }
        return found;
    }

    /// <summary>Whether <paramref name="email"/> was invited and every key they had is revoked: then neither their key nor any token
    /// minted for them is honoured. A member who was never invited isn't revoked (their access is the ACL's).</summary>
    public static bool IsRevoked(TermMemberKeyFile? file, string? email)
    {
        if (file is null) return false;
        var who = Normalize(email);
        var mine = file.Keys.Where(k => k is not null && Normalize(k.Email) == who).ToList();
        return mine.Count > 0 && mine.All(k => !k.IsLive);
    }

    /// <summary>Whether <paramref name="email"/> holds a live key.</summary>
    public static bool HasLiveKey(TermMemberKeyFile? file, string? email)
    {
        var who = Normalize(email);
        return file is not null && file.Keys.Any(k => k is not null && k.IsLive && Normalize(k.Email) == who);
    }

    /// <summary>When the gateway last saw each member, by email; empty when it never has or the file can't be read.</summary>
    public static IReadOnlyDictionary<string, DateTime> ReadSeen(string? directory = null)
    {
        try
        {
            var path = SeenPath(directory);
            if (!File.Exists(path)) return new Dictionary<string, DateTime>();
            return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(path), Json) ?? new Dictionary<string, DateTime>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new Dictionary<string, DateTime>(); }
    }

    /// <summary>Writes when the gateway last saw each member; false when it couldn't be written just now.</summary>
    public static bool WriteSeen(IReadOnlyDictionary<string, DateTime> seen, string? directory = null) =>
        WriteAtomic(SeenPath(directory), JsonSerializer.Serialize(seen, Json));

    /// <summary>The members with a live key that the gateway saw within <paramref name="window"/> of <paramref name="nowUtc"/>,
    /// by email: who is connected to this host now.</summary>
    public static IReadOnlyList<string> Connected(DateTime nowUtc, TimeSpan window, string? directory = null)
    {
        if (!TryRead(directory, out var file) || file is null) return [];
        return [.. ReadSeen(directory)
            .Where(s => nowUtc - s.Value <= window && HasLiveKey(file, s.Key))
            .Select(s => Normalize(s.Key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Changes the member keys file under a lock shared by every writer: an in-process lock alone gave
    /// no protection against a separate <c>orchestrator machine invite|revoke|isolate</c> process doing its own
    /// read-modify-write at the same time, which could silently lose this one's change (e.g. a revoke reverted by a
    /// concurrent invite/isolate that read the file before the revoke wrote it) - the same hazard <see cref="TermBroadcast.Update"/>
    /// already guards against with a named cross-process mutex (bughunt 2026-10-27).</summary>
    private static void Update(string? directory, Func<TermMemberKeyFile, TermMemberKeyFile> change)
    {
        using var mutex = new Mutex(false, @"Local\mindattic-orchestrator-member-keys");
        var owned = false;
        try
        {
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned) throw new IOException($"{KeysPath(directory)} is locked by another process; try again.");
            if (!TryRead(directory, out var file)) throw new IOException($"{KeysPath(directory)} can't be read just now; try again.");
            var next = change(file ?? new TermMemberKeyFile());
            if (!WriteAtomic(KeysPath(directory), JsonSerializer.Serialize(next, Json)))
                throw new IOException($"{KeysPath(directory)} couldn't be written.");
        }
        finally { if (owned) mutex.ReleaseMutex(); }
    }

    private static bool WriteAtomic(string path, string text)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { File.Delete(temp); } catch (Exception) { }
            return false;
        }
    }
}
