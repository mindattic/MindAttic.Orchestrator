using System.Text.Json;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// The org members' keys to this host's gateway (hub phase 1, M.ORCH-T1640): which member a presented key is, whether a member
/// was revoked, and when each was last seen.
/// </summary>
internal interface IMemberKeys
{
    /// <summary>The member whose live key <paramref name="presented"/> is, or null.</summary>
    string? Match(string? presented);

    /// <summary>Whether <paramref name="subject"/> was invited and has since been revoked: their tokens are refused too.</summary>
    bool IsRevoked(string subject);

    /// <summary>Notes that <paramref name="subject"/> (a member with a live key) used the gateway just now.</summary>
    void Seen(string subject);
}

/// <summary>No member keys: only the owner key mints tokens (tests, and a gateway before hub phase 1).</summary>
internal sealed class NoMemberKeys : IMemberKeys
{
    public static readonly NoMemberKeys Instance = new();
    public string? Match(string? presented) => null;
    public bool IsRevoked(string subject) => false;
    public void Seen(string subject) { }
}

/// <summary>
/// <c>members.json</c> in the gateway's data folder as it is now (<see cref="TermMemberKeys"/>): looked at at most once every
/// <see cref="Recheck"/> and read again when it changes, so <c>orchestrator machine invite|revoke</c> apply within a second with no
/// restart. A file that can't be read just now keeps the last good list; a gone one is no members. When each member was last seen
/// is kept in memory and written to <c>members-seen.json</c> at most every <see cref="FlushEvery"/> (and at once for a member not
/// seen before), for the host dashboard's connected-members readout. Never holds or logs a key.
/// </summary>
internal sealed class MemberKeysFile : IMemberKeys
{
    public static readonly TimeSpan Recheck = TimeSpan.FromSeconds(1);

    /// <summary>How often the seen times are written at most: a fraction of the shortest "connected" window.</summary>
    public static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(15);

    private readonly string directory;
    private readonly Func<DateTime> now;
    private readonly System.Threading.Lock gate = new();
    private TermMemberKeyFile? file;
    private bool unreadable;
    private DateTime stamp = DateTime.MinValue, checkedUtc = DateTime.MinValue, flushedUtc = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> seen;

    public MemberKeysFile(string directory, Func<DateTime>? clock = null)
    {
        this.directory = directory;
        now = clock ?? (() => DateTime.UtcNow);
        seen = new Dictionary<string, DateTime>(TermMemberKeys.ReadSeen(directory), StringComparer.Ordinal);
    }

    public string? Match(string? presented) => TermMemberKeys.Match(Current(), presented);

    public bool IsRevoked(string subject)
    {
        var current = Current();
        // Unreadable with no good list to fall back on: who was revoked isn't known, so a non-owner subject is refused (the
        // caller already exempts the owner), M.ORCH-T1966.
        lock (gate) if (current is null && unreadable) return true;
        return TermMemberKeys.IsRevoked(current, subject);
    }

    public void Seen(string subject)
    {
        if (!TermMemberKeys.HasLiveKey(Current(), subject)) return;
        var who = TermMemberKeys.Normalize(subject);
        Dictionary<string, DateTime>? toWrite = null;
        lock (gate)
        {
            var at = now();
            var known = seen.ContainsKey(who);
            seen[who] = at;
            if (!known || at - flushedUtc >= FlushEvery)
            {
                flushedUtc = at;
                toWrite = new Dictionary<string, DateTime>(seen, StringComparer.Ordinal);
            }
        }
        if (toWrite is not null) TermMemberKeys.WriteSeen(toWrite, directory);
    }

    private TermMemberKeyFile? Current()
    {
        lock (gate)
        {
            var at = DateTime.UtcNow;
            if (at - checkedUtc < Recheck) return file;
            checkedUtc = at;
            DateTime written;
            try { written = File.Exists(TermMemberKeys.KeysPath(directory)) ? File.GetLastWriteTimeUtc(TermMemberKeys.KeysPath(directory)) : DateTime.MinValue; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return file; }
            if (written == stamp && file is not null) return file;
            if (!TermMemberKeys.TryRead(directory, out var read)) return file;
            // A file that isn't JSON (a half-written save, a bad hand edit) keeps the last good list rather than dropping every
            // revocation (M.ORCH-T1966); the stamp stays so it's read again next time. With no good list yet, IsRevoked denies.
            unreadable = read is null;
            if (read is null) return file;
            stamp = written;
            file = read;
            return file;
        }
    }
}

/// <summary>
/// What a member's token may run through <c>POST /tickets</c> (hub phase 1, M.ORCH-T1640; docs/research/hub-and-spoke.md §3.4):
/// <c>tickets ticket show|list|new|note|close</c>, and close only on a ticket one of their logins works or is assigned. Every other
/// verb (approvals, priorities, assignments, the database, settings) stays the owner's, and so does a verb added to the tickets CLI
/// later until it is listed here. Options that read or write a file on the host, pick another workspace, or name another author
/// are refused, so a member can't reach past the ticket database or write as someone else.
/// </summary>
internal static class MemberTickets
{
    /// <summary>The <c>tickets ticket</c> verbs a member may run.</summary>
    public static readonly IReadOnlyList<string> Verbs = ["show", "list", "new", "note", "close"];

    /// <summary>Options a member may not pass: a host path (read or written), another workspace, or another author.</summary>
    // --with-children is refused too: it would close child tickets other people work (M.ORCH-T1966).
    public static readonly IReadOnlyList<string> RefusedOptions = ["--root", "--output", "--description-file", "--author", "--with-children"];

    /// <summary>Options of the allowed verbs that take a value, so the value isn't read as the ticket id.</summary>
    private static readonly HashSet<string> Valued = new(StringComparer.OrdinalIgnoreCase)
    {
        "--ref", "--status", "--repo", "--type", "--summary", "--name", "--description", "--since", "--query", "--filter", "--label",
        "--format", "--version",
    };

    /// <summary>Why a member may not run <paramref name="args"/>, or null when they may.</summary>
    public static string? Refusal(IReadOnlyList<string> args)
    {
        if (args.Count < 2 || !string.Equals(args[0], "ticket", StringComparison.Ordinal) || !Verbs.Contains(args[1], StringComparer.Ordinal))
            return $"members may run only tickets ticket {string.Join("|", Verbs)}; \"{Verb(args)}\" is the host owner's";
        foreach (var a in args.Skip(2))
        {
            if (a == "--") return "\"--\" isn't open to members";
            // Spectre.Console.Cli takes --opt:value as well as --opt=value (M.ORCH-T1966).
            var name = a.Split(['=', ':'], 2)[0];
            if (RefusedOptions.Contains(name, StringComparer.OrdinalIgnoreCase)) return $"{name} isn't open to members";
        }
        return null;
    }

    /// <summary>The verb as the audit and the log name it: "ticket close", never the arguments' text.</summary>
    public static string Verb(IReadOnlyList<string> args) =>
        args.Count == 0 ? "" : args.Count == 1 || args[1].StartsWith('-') ? Clip(args[0]) : $"{Clip(args[0])} {Clip(args[1])}";

    /// <summary>The one ticket id <c>ticket close</c> names, or null when it names none or several.</summary>
    public static string? TicketId(IReadOnlyList<string> args)
    {
        var ids = new List<string>();
        for (var i = 2; i < args.Count; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                if (!a.Contains('=', StringComparison.Ordinal) && Valued.Contains(a)) i++;
                continue;
            }
            ids.Add(a.Trim());
        }
        return ids is [{ Length: > 0 and <= 64 } id] ? id : null;
    }

    /// <summary>Whether <c>tickets ticket show &lt;id&gt; --json</c>'s <paramref name="json"/> says one of <paramref name="logins"/>
    /// works the ticket now or is assigned it.</summary>
    public static bool WorkedBy(string json, IReadOnlySet<string> logins)
    {
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start) return false;
        try
        {
            using var doc = JsonDocument.Parse(json[start..(end + 1)]);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            bool Mine(JsonElement e) => e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s && logins.Contains(s.Trim().ToLowerInvariant());
            if (root.TryGetProperty("assignee", out var assignee) && Mine(assignee)) return true;
            return root.TryGetProperty("working", out var working) && working.ValueKind == JsonValueKind.Object
                && working.TryGetProperty("login", out var login) && Mine(login);
        }
        catch (JsonException) { return false; }
    }

    private static string Clip(string s)
    {
        var t = s.Trim();
        return t.Length <= 24 ? t : t[..24];
    }
}
