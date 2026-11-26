using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>A TermHost the gateway started under a member's own Windows account (hub phase 2b, M.ORCH-T1667).</summary>
public sealed record TermMemberHost
{
    /// <summary>The session id it was started with.</summary>
    [JsonPropertyName("id")] public string Id { get; init; } = "";

    /// <summary>The member's Windows account it runs under, as <c>machine isolate</c> recorded it.</summary>
    [JsonPropertyName("account")] public string Account { get; init; } = "";

    /// <summary>The Claude login (an email) it runs on: the member's own.</summary>
    [JsonPropertyName("login")] public string Login { get; init; } = "";

    /// <summary>The repo it runs in.</summary>
    [JsonPropertyName("repo")] public string Repo { get; init; } = "";

    /// <summary>The TermHost's pid and start time (UTC): a process with that pid that started at another time is a reused pid.</summary>
    [JsonPropertyName("hostPid")] public int HostPid { get; init; }

    /// <summary>When the TermHost process started (UTC); checked against <see cref="HostPid"/> to catch a reused pid.</summary>
    [JsonPropertyName("hostStartUtc")] public DateTime HostStartUtc { get; init; }

    /// <summary>The folder the host writes its registry file in (the member's own folder under <see cref="TermMemberHosts.DefaultRegistryRoot"/>).</summary>
    [JsonPropertyName("registryDir")] public string RegistryDir { get; init; } = "";

    /// <summary>Its pipe (<see cref="TermProtocol.PipeName"/> for the member's user name).</summary>
    [JsonPropertyName("pipe")] public string Pipe { get; init; } = "";

    /// <summary>When this host record was started (UTC).</summary>
    [JsonPropertyName("startedUtc")] public DateTime StartedUtc { get; init; }
}

/// <summary>
/// The gateway's launch ledger (hub phase 2b, M.ORCH-T1667): <c>member-hosts.json</c> in the gateway's data folder, which only the
/// host's own Windows user can read or write. A member's TermHost writes its registry file in a folder that member can write, so
/// nothing in that file is trusted on its own: a session counts as running under a member's account only when this ledger, written
/// by the gateway as it started that host, names its id, pid and start time (<see cref="Of"/>). The account, login, repo and pipe
/// the gateway acts on come from here, never from the member's file.
/// <para>Entries whose host has gone are dropped as the ledger is read; it holds at most <see cref="MaxEntries"/>. Nothing here
/// throws on an I/O failure: an unreadable ledger reads as empty (so no session counts as a member's) and is never written over, and
/// a write that fails returns false.</para>
/// </summary>
public sealed class TermMemberHosts
{
    /// <summary>The ledger's file name in the gateway's data folder.</summary>
    public const string FileName = "member-hosts.json";

    /// <summary>The most hosts kept; past it, the oldest is let go.</summary>
    public const int MaxEntries = 256;

    /// <summary>The environment variable that moves <see cref="DefaultRegistryRoot"/>.</summary>
    public const string RegistryRootVariable = "MINDATTIC_MEMBER_TERMS";

    /// <summary>Where members' TermHosts write their registry files, one folder per account (made and ACL'd by an admin once,
    /// docs/REMOTE_HOST.md): <c>%ProgramData%\MindAttic\MindAttic.Orchestrator\member-terms</c>, or <see cref="RegistryRootVariable"/>.</summary>
    public static string DefaultRegistryRoot =>
        Environment.GetEnvironmentVariable(RegistryRootVariable) is { Length: > 0 } set
            ? set
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MindAttic", "MindAttic.Orchestrator", "member-terms");

    /// <summary>The user-name part of an account name (<c>DOMAIN\user</c> → <c>user</c>): what the host's <c>%USERNAME%</c>, and so its
    /// pipe name, will be.</summary>
    public static string UserPart(string account)
    {
        var t = account.Trim();
        var at = t.LastIndexOf('\\');
        return at < 0 ? t : t[(at + 1)..].Trim();
    }

    /// <summary>The registry folder of <paramref name="account"/>'s hosts under <paramref name="root"/>.</summary>
    public static string RegistryDirOf(string account, string? root = null) =>
        Path.Combine(root ?? DefaultRegistryRoot, TermProtocol.Sanitize(UserPart(account).ToLowerInvariant()));

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly string path;
    private readonly Func<int, DateTime?> hostStart;
    private readonly Func<TimeSpan> startWait;
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock gate = new();

    /// <param name="directory">The gateway's data folder.</param>
    /// <param name="hostStart">When a pid's process started (UTC), or null when none has it; <see cref="TermSessions.HostStartOf"/>
    /// by default, tests pass their own.</param>
    /// <param name="startWait">How long a just-started host may stay unseen before it counts as gone (the hub's start wait).</param>
    public TermMemberHosts(string directory, Func<int, DateTime?>? hostStart = null, Func<TimeSpan>? startWait = null)
    {
        path = Path.Combine(directory, FileName);
        this.hostStart = hostStart ?? TermSessions.HostStartOf;
        this.startWait = startWait ?? (() => TimeSpan.FromSeconds(TermHubPolicy.DefaultStartWaitSeconds));
    }

    /// <summary>The hosts still running, oldest first (the ledger is rewritten without the gone ones).</summary>
    public IReadOnlyList<TermMemberHost> Live()
    {
        lock (gate)
        {
            if (Load() is not { } all) return [];
            var live = all.Where(IsAlive).ToList();
            seen.UnionWith(live.Select(h => h.Id));
            var kept = all.Where(h => live.Contains(h) || Starting(h)).ToList();
            seen.IntersectWith(kept.Select(h => h.Id));
            if (kept.Count != all.Count) Save(kept);
            return live;
        }
    }

    /// <summary>The ledger entry <paramref name="session"/> is, when the gateway started it under a member's account and it still
    /// runs: same id, same pid, same start time. Null for every other session (the host's user's own, or a stranger's file).</summary>
    public TermMemberHost? Of(TermSessionRecord session) =>
        Live().FirstOrDefault(h => Matches(h, session));

    /// <summary>The live entry with session id <paramref name="id"/>, or null.</summary>
    public TermMemberHost? Find(string id) => Live().FirstOrDefault(h => string.Equals(h.Id, id, StringComparison.Ordinal));

    /// <summary>Records a host the gateway just started; false when the ledger couldn't be written.</summary>
    public bool Add(TermMemberHost host)
    {
        lock (gate)
        {
            if (Load() is not { } loaded) return false;
            var all = loaded.Where(h => (IsAlive(h) || Starting(h)) && h.Id != host.Id).ToList();
            all.Add(host);
            while (all.Count > MaxEntries) all.RemoveAt(0);
            return Save(all);
        }
    }

    /// <summary>Forgets session <paramref name="id"/> (stopped, or never came up); false when it wasn't there or couldn't be written.</summary>
    public bool Remove(string id)
    {
        lock (gate)
        {
            if (Load() is not { } all) return false;
            seen.Remove(id);
            var kept = all.Where(h => h.Id != id).ToList();
            return kept.Count != all.Count && Save(kept);
        }
    }

    /// <summary>Whether <paramref name="session"/> is the host <paramref name="host"/> names: same id, pid and start time.</summary>
    public static bool Matches(TermMemberHost host, TermSessionRecord session) =>
        string.Equals(host.Id, session.Id, StringComparison.Ordinal) && host.HostPid == session.HostPid
        && (host.HostStartUtc - session.HostStartUtc).Duration() <= TermSessions.StartTolerance;

    private bool IsAlive(TermMemberHost host) =>
        host.HostPid > 0 && hostStart(host.HostPid) is { } started && (started - host.HostStartUtc).Duration() <= TermSessions.StartTolerance;

    // A new host looks gone until it grants the gateway query rights (MemberHost.GrantPeer); dropping it then would orphan it, so an
    // entry never seen is kept, though not listed, for the start wait (M.ORCH-T2305).
    private bool Starting(TermMemberHost host) => !seen.Contains(host.Id) && DateTime.UtcNow - host.StartedUtc < startWait();

    /// <summary>The ledger's entries: empty when it's missing or isn't valid JSON, null when it couldn't be read (a lock or a
    /// denied read), so a write never replaces entries it couldn't see.</summary>
    private List<TermMemberHost>? Load()
    {
        string text;
        try
        {
            if (!File.Exists(path)) return [];
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        try
        {
            return (JsonSerializer.Deserialize<List<TermMemberHost>>(text, Json) ?? [])
                .Where(h => h is { Id.Length: > 0, Account.Length: > 0, HostPid: > 0 }).OrderBy(h => h.StartedUtc).ToList();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { return []; }
    }

    private bool Save(List<TermMemberHost> hosts)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(hosts, Json), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { File.Delete(temp); } catch (Exception inner) when (inner is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }
}
