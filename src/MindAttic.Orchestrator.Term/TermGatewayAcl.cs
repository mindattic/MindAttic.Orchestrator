using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>An active member, as the gateway's ACL names them: their normalized email and name (M.ORCH-T934).</summary>
public sealed record TermAclMember(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string? Name);

/// <summary>A Claude login placed in an org (<c>org_members</c>): the login (an email, "" for the global one), its org and person.</summary>
public sealed record TermAclLogin(
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("org")] string Org,
    [property: JsonPropertyName("person")] string Person);

/// <summary>One <c>term_grants</c> row: <paramref name="Viewer"/> may <paramref name="Level"/> (watch or interact) the sessions
/// of <paramref name="Owner"/>, all of them when <paramref name="Session"/> is empty, until <paramref name="ExpiresUtc"/> (null: no
/// expiry, which only a watch grant may have).</summary>
public sealed record TermAclGrant(
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("viewer")] string Viewer,
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("expiresUtc")] DateTime? ExpiresUtc);

/// <summary>
/// What the team gateway decides access from (M.ORCH-T934): the active members, the Claude logins placed in orgs and the grants,
/// as <c>orchestrator.db</c> held them when the daemon or <c>orchestrator gateway grant|revoke</c> last wrote this file. The gateway
/// references only the Term library, never Core's SQL stack, so the database reaches it as this snapshot, <c>acl.json</c> in its
/// data folder: rewritten within a daemon tick of a member or org change and at once by a grant or revoke, and read by the
/// gateway whenever its time changes. Never holds a secret.
/// </summary>
public sealed record TermGatewayAclSnapshot
{
    [JsonPropertyName("members")] public List<TermAclMember> Members { get; init; } = [];
    [JsonPropertyName("logins")] public List<TermAclLogin> Logins { get; init; } = [];
    [JsonPropertyName("grants")] public List<TermAclGrant> Grants { get; init; } = [];
}

/// <summary>
/// One gateway audit record (M.ORCH-T934): who attached to, detached from or typed into which session, when, at what level.
/// Input is counted, frames and bytes, never its content (which can hold secrets); nothing here is ever a token or a key.
/// </summary>
public sealed record TermGatewayAuditRecord
{
    /// <summary>A random id, so importing the spool twice (after a crash between insert and delete) adds a record once.</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("atUtc")] public DateTime AtUtc { get; init; }

    /// <summary><see cref="TermGatewayAudit.Attach"/>, <see cref="TermGatewayAudit.Detach"/>, <see cref="TermGatewayAudit.Input"/>
    /// or <see cref="TermGatewayAudit.Refused"/>.</summary>
    [JsonPropertyName("event")] public string Event { get; init; } = "";

    /// <summary>The token's subject: a member's email, or the machine owner's identity.</summary>
    [JsonPropertyName("viewer")] public string Viewer { get; init; } = "";

    /// <summary>The gateway's owner, whose session it is.</summary>
    [JsonPropertyName("owner")] public string? Owner { get; init; }
    [JsonPropertyName("session")] public string Session { get; init; } = "";

    /// <summary>watch, interact, or none for a refusal.</summary>
    [JsonPropertyName("level")] public string Level { get; init; } = "";

    /// <summary>The viewer's address.</summary>
    [JsonPropertyName("remote")] public string? Remote { get; init; }

    /// <summary>Input frames (an input record) or frames up (a detach).</summary>
    [JsonPropertyName("frames")] public long Frames { get; init; }

    /// <summary>Input payload bytes (an input record) or bytes down (a detach).</summary>
    [JsonPropertyName("bytes")] public long Bytes { get; init; }

    /// <summary>A short note: how a detach ended and what was dropped, why a refusal. Never content.</summary>
    [JsonPropertyName("detail")] public string? Detail { get; init; }
}

/// <summary>
/// The gateway's ACL snapshot and audit spool files (M.ORCH-T934), in its data folder (<see cref="TermGateway.DefaultDirectory"/>).
/// The gateway appends each audit record to <c>audit.jsonl</c>; the daemon (each tick) and <c>orchestrator gateway audit</c> move it
/// aside and import it into <c>orchestrator.db</c>'s <c>term_audit</c>. Nothing here throws on an I/O failure but
/// <see cref="Drain"/>'s commit.
/// </summary>
public static class TermGatewayAudit
{
    public const string Attach = "attach", Detach = "detach", Input = "input", Refused = "refused";

    public const string AclFile = "acl.json", SpoolFile = "audit.jsonl";
    private const string DrainingSuffix = ".draining";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly object AppendGate = new();

    public static string AclPath(string? directory = null) => Path.Combine(directory ?? TermGateway.DefaultDirectory, AclFile);

    /// <summary>Writes the snapshot atomically when its text changed; true when it wrote.</summary>
    public static bool WriteAcl(TermGatewayAclSnapshot snapshot, string? directory = null)
    {
        var dir = directory ?? TermGateway.DefaultDirectory;
        var path = AclPath(dir);
        var text = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            if (File.Exists(path) && File.ReadAllText(path) == text) return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(dir);
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

    /// <summary>The snapshot; null when there is none or it can't be read.</summary>
    public static TermGatewayAclSnapshot? ReadAcl(string? directory = null)
    {
        try
        {
            var path = AclPath(directory);
            return File.Exists(path) ? JsonSerializer.Deserialize<TermGatewayAclSnapshot>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Appends <paramref name="record"/> to the spool; false when it couldn't be (retried a few times on a sharing
    /// violation, as a drain may be moving the file that moment).</summary>
    public static bool Append(TermGatewayAuditRecord record, string? directory = null)
    {
        var dir = directory ?? TermGateway.DefaultDirectory;
        var line = JsonSerializer.Serialize(record) + "\n";
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                lock (AppendGate)
                {
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, SpoolFile), line, new UTF8Encoding(false));
                }
                return true;
            }
            catch (IOException) { Thread.Sleep(20 * (attempt + 1)); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
        }
        return false;
    }

    /// <summary>
    /// Moves the spool aside and reads every record in it (and in any file a drain that died left aside). The caller imports them
    /// and then calls the returned commit, which deletes those files; without it, the next drain reads them again (records keep
    /// their ids, so an import that skips ids already there loses and repeats nothing).
    /// </summary>
    public static (IReadOnlyList<TermGatewayAuditRecord> Records, Action Commit) Drain(string? directory = null)
    {
        var dir = directory ?? TermGateway.DefaultDirectory;
        var spool = Path.Combine(dir, SpoolFile);
        try
        {
            if (File.Exists(spool)) File.Move(spool, Path.Combine(dir, $"{SpoolFile}.{Guid.NewGuid():N}{DrainingSuffix}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // the gateway is writing: next time
        string[] files;
        try { files = Directory.Exists(dir) ? Directory.GetFiles(dir, $"{SpoolFile}.*{DrainingSuffix}") : []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { files = []; }
        var records = new List<TermGatewayAuditRecord>();
        var read = new List<string>();
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            try
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { if (JsonSerializer.Deserialize<TermGatewayAuditRecord>(line, Json) is { } r) records.Add(r); }
                    catch (JsonException) { } // a torn last line: the rest still counts
                }
                read.Add(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (records, () => { foreach (var f in read) File.Delete(f); });
    }
}
