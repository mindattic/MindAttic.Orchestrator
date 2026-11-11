using System.Data.Common;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One member of the organization (M.ORCH-T670): a row of the <c>members</c> table, keyed by email.</summary>
public sealed record Member
{
    /// <summary>The member's email, normalized as <see cref="AccountDirs.Normalize"/> does: the key, and how a member is
    /// matched to this machine's sign-ins (<see cref="AccountRow.Email"/>) and per-CLI logins.</summary>
    public string Email { get; init; } = "";
    /// <summary>The name the member goes by; null until one is given (the dashboard falls back to the login's alias).</summary>
    public string? Name { get; init; }
    /// <summary>One of <see cref="MemberRoster.Roles"/>.</summary>
    public string Role { get; init; } = MemberRoster.Roles[^1];
    /// <summary>One of <see cref="MemberRoster.Tiers"/>: the seat the member has.</summary>
    public string Tier { get; init; } = MemberRoster.Tiers[0];
    /// <summary>One of <see cref="MemberRoster.Statuses"/>: active once joined, pending while invited.</summary>
    public string Status { get; init; } = MemberRoster.Statuses[0];
    public DateTime JoinedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
}

/// <summary>
/// The organization's members (M.ORCH-T670), in the <c>members</c> table of <c>data/orchestrator.db</c> (or SQL Server once
/// configured), with <c>data/orchestrator.sql</c> rewritten on each change: the one record of who's in the organization, which
/// the dashboard's person tabs come from. Unlike <see cref="AccountRoster"/>, which only knows an account once it's signed in
/// on this machine, a member is entered by hand (<c>orchestrator members add</c>) and kept whether signed in anywhere or not.
/// The allowed values of each column are the lists below: a new role, tier or status is one more entry there.
/// </summary>
public static class MemberRoster
{
    /// <summary>The roles a member can have, highest first.</summary>
    public static readonly string[] Roles = ["primary-owner", "owner", "admin", "member"];

    /// <summary>The seat tiers a member can have.</summary>
    public static readonly string[] Tiers = ["premium", "standard"];

    /// <summary>Whether a member has joined (active) or is still invited (pending).</summary>
    public static readonly string[] Statuses = ["active", "pending"];

    /// <summary><paramref name="value"/> as one of <paramref name="allowed"/> ("Primary owner", "primary_owner" and
    /// "PRIMARY-OWNER" are all <c>primary-owner</c>), or null when it isn't one.</summary>
    public static string? Canonical(string? value, IReadOnlyList<string> allowed)
    {
        if (value is null) return null;
        var v = string.Join('-', value.Trim().ToLowerInvariant().Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries));
        return allowed.FirstOrDefault(a => a == v);
    }

    /// <summary>Every member, by email.</summary>
    public static IReadOnlyList<Member> Load(OrchestratorDb db)
    {
        using var connection = db.OpenData();
        return Rows(connection, null);
    }

    /// <summary><paramref name="email"/>'s row, or null when it isn't a member.</summary>
    public static Member? Find(OrchestratorDb db, string email)
    {
        var key = AccountDirs.Normalize(email);
        return key is null ? null : Load(db).FirstOrDefault(m => m.Email == key);
    }

    /// <summary>Adds a member, checked first (<see cref="Check"/>); fails when <paramref name="email"/> already is one, or when
    /// its name, initials or email is one another member already answers to (<see cref="Clash"/>, M.ORCH-T1445).</summary>
    /// <exception cref="ArgumentException">An email, role, tier or status that isn't valid.</exception>
    /// <exception cref="InvalidOperationException">Already a member, or another member already answers to its name, initials or email.</exception>
    public static Member Add(OrchestratorDb db, string email, string? name, string role, string tier, string? status, DateTime nowUtc)
    {
        var member = Check(email, name, role, tier, status ?? Statuses[0]) with { JoinedUtc = nowUtc, UpdatedUtc = nowUtc };
        db.WriteData((connection, tx) =>
        {
            var rows = Rows(connection, tx);
            if (rows.Any(m => m.Email == member.Email))
                throw new InvalidOperationException($"{member.Email} is already a member (orchestrator members set changes it).");
            if (Clash(rows, member) is { } clash) throw new InvalidOperationException(clash);
            Upsert(connection, tx, member);
        });
        StoreChanges.Note(db, StoreChanges.Members);
        return member;
    }

    /// <summary>Changes what's given of a member (null leaves it; an empty <paramref name="name"/> clears it); fails when
    /// <paramref name="email"/> isn't one. Written only when something changed. With <paramref name="unique"/> (<c>orchestrator
    /// members set</c>, M.ORCH-T1445), a new name another member already answers to is refused (<see cref="Clash"/>); keeping its
    /// own name, or changing only its role, tier or status, never is.</summary>
    /// <exception cref="ArgumentException">An email, role, tier or status that isn't valid.</exception>
    /// <exception cref="InvalidOperationException">Not a member, or (with <paramref name="unique"/>) a name another member answers to.</exception>
    public static Member Set(OrchestratorDb db, string email, string? name, string? role, string? tier, string? status, DateTime nowUtc, bool unique = false)
    {
        var key = Key(email);
        Member? result = null;
        var changed = false;
        db.WriteDataIf((connection, tx) =>
        {
            var rows = Rows(connection, tx);
            var was = rows.FirstOrDefault(m => m.Email == key)
                ?? throw new InvalidOperationException($"{key} isn't a member (orchestrator members add adds one).");
            var next = Check(key, name is null ? was.Name : name, role ?? was.Role, tier ?? was.Tier, status ?? was.Status)
                with { JoinedUtc = was.JoinedUtc, UpdatedUtc = was.UpdatedUtc };
            if (next == was) { result = was; return false; }
            if (unique && !string.Equals(next.Name, was.Name, StringComparison.OrdinalIgnoreCase) && Clash(rows, next, nameOnly: true) is { } clash)
                throw new InvalidOperationException(clash);
            result = next with { UpdatedUtc = nowUtc };
            Upsert(connection, tx, result);
            return changed = true;
        });
        if (changed) StoreChanges.Note(db, StoreChanges.Members);
        return result!;
    }

    /// <summary>Writes claude.ai's <paramref name="profile"/> over its member's row (M.ORCH-T701): its name and role, when
    /// claude.ai gave them, and active, since the member has signed in. Whether anything changed; false when the login isn't a
    /// member (members are still added by hand).</summary>
    /// <exception cref="ArgumentException">Not an email address.</exception>
    public static bool Sync(OrchestratorDb db, ClaudeProfile profile, DateTime nowUtc)
    {
        var key = Key(profile.Email);
        var changed = false;
        db.WriteDataIf((connection, tx) =>
        {
            if (Rows(connection, tx).FirstOrDefault(m => m.Email == key) is not { } was) return false;
            var next = was with
            {
                Name = string.IsNullOrWhiteSpace(profile.Name) ? was.Name : profile.Name.Trim(),
                Role = Canonical(profile.Role, Roles) ?? was.Role,
                Status = Statuses[0],
            };
            if (next == was) return false;
            Upsert(connection, tx, next with { UpdatedUtc = nowUtc });
            return changed = true;
        });
        if (changed) StoreChanges.Note(db, StoreChanges.Members);
        return changed;
    }

    /// <summary>Removes a member; false when <paramref name="email"/> wasn't one.</summary>
    /// <exception cref="ArgumentException">Not an email address.</exception>
    public static bool Remove(OrchestratorDb db, string email)
    {
        var key = Key(email);
        var removed = false;
        db.WriteDataIf((connection, tx) => removed = Sql.Run(connection, tx, "DELETE FROM members WHERE email = @email;", ("@email", key)) > 0);
        if (removed) StoreChanges.Note(db, StoreChanges.Members);
        return removed;
    }

    /// <summary>A member made of what was given, each value checked and made canonical.</summary>
    /// <exception cref="ArgumentException">An email, role, tier or status that isn't valid.</exception>
    public static Member Check(string email, string? name, string role, string tier, string status) => new()
    {
        Email = Key(email),
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
        Role = Canonical(role, Roles) ?? throw new ArgumentException($"\"{role}\" isn't a role: one of {string.Join(", ", Roles)}."),
        Tier = Canonical(tier, Tiers) ?? throw new ArgumentException($"\"{tier}\" isn't a tier: one of {string.Join(", ", Tiers)}."),
        Status = Canonical(status, Statuses) ?? throw new ArgumentException($"\"{status}\" isn't a status: one of {string.Join(", ", Statuses)}."),
    };

    /// <summary>What <paramref name="member"/> answers to (M.ORCH-T1445), each with what it is: the email, the part before the
    /// @, the name, and the name's initials when it has two or more words: the ways <c>/org &lt;who&gt;</c>
    /// (<see cref="OrgPerson.Keys"/>) and the generated <c>/&lt;member&gt;</c> commands (MindAttic.Agency's
    /// shared/member-commands.ps1) name a member.</summary>
    public static IEnumerable<(string Value, string What)> AnswersTo(Member member, bool nameOnly = false)
    {
        if (!nameOnly)
        {
            yield return (member.Email, "email");
            if (LoginLook.DefaultAlias(member.Email) is { } local) yield return (local, "email's part before the @");
        }
        if (Collapse(member.Name) is not { Length: > 0 } name) yield break;
        yield return (name, "name");
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2) yield return (string.Concat(words.Select(w => char.ToUpperInvariant(w[0]))), "initials");
    }

    /// <summary>Why <paramref name="candidate"/> can't be added or renamed (M.ORCH-T1445): the first of its name, initials or
    /// email (only its name and initials with <paramref name="nameOnly"/>) that another of <paramref name="members"/> already
    /// answers to, naming that member; null when none clashes. Two values clash when they're equal ignoring case and spacing, as
    /// <see cref="MemberLookup.Match"/> compares them, or when they make the same <c>/&lt;member&gt;</c> command (their letters and
    /// digits, lowercased). The candidate's own row (same email) is never a clash.</summary>
    public static string? Clash(IEnumerable<Member> members, Member candidate, bool nameOnly = false)
    {
        var mine = AnswersTo(candidate, nameOnly).ToList();
        foreach (var other in members)
        {
            if (string.Equals(other.Email, candidate.Email, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (theirs, theirWhat) in AnswersTo(other))
                foreach (var (value, what) in mine)
                    if (Same(value, theirs))
                        return $"The {what} \"{value}\" clashes with {other.Name ?? other.Email} ({other.Email}), who already answers to " +
                            $"\"{theirs}\" (their {theirWhat}): /<member> commands and /org <who> need every member's name, initials and " +
                            $"email to be unique, so give {(what.StartsWith("email", StringComparison.Ordinal) ? "another email" : "another --name")}.";
        }
        return null;
    }

    private static bool Same(string a, string b)
    {
        if (string.Equals(Collapse(a), Collapse(b), StringComparison.OrdinalIgnoreCase)) return true;
        var sa = Slug(a);
        return sa.Length > 0 && sa == Slug(b);
    }

    /// <summary>Trimmed, with each run of spaces made one, as <see cref="MemberLookup.Match"/> reads a &lt;who&gt;.</summary>
    private static string? Collapse(string? s) => s is null ? null : string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The <c>/&lt;member&gt;</c> command a value makes: its ASCII letters and digits, lowercased (member-commands.ps1's Slug).</summary>
    private static string Slug(string s) => string.Concat(s.ToLowerInvariant().Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9'));

    private static string Key(string email) =>
        AccountDirs.Normalize(email) ?? throw new ArgumentException($"\"{email}\" isn't an email address.");

    private static List<Member> Rows(DbConnection connection, DbTransaction? tx) =>
        Sql.Read(connection, tx, "SELECT email, name, role, tier, status, joined_at, updated_at FROM members ORDER BY email;", r => new Member
        {
            Email = r.GetString(0),
            Name = Sql.OptionalText(r, 1),
            Role = r.GetString(2),
            Tier = r.GetString(3),
            Status = r.GetString(4),
            JoinedUtc = At(Sql.Real(r, 5)),
            UpdatedUtc = At(Sql.Real(r, 6)),
        });

    private static void Upsert(DbConnection connection, DbTransaction tx, Member m) =>
        Sql.Upsert(connection, tx, "members", ["email"],
            ("email", m.Email), ("name", m.Name), ("role", m.Role), ("tier", m.Tier), ("status", m.Status),
            ("joined_at", Epoch(m.JoinedUtc)), ("updated_at", Epoch(m.UpdatedUtc)));

    private static double Epoch(DateTime utc) => (utc - DateTime.UnixEpoch).TotalSeconds;

    /// <summary>Back from Unix seconds, to the millisecond, as <see cref="AccountRoster"/> reads its times.</summary>
    private static DateTime At(double seconds) => DateTime.UnixEpoch.AddMilliseconds(Math.Round(seconds * 1000));

    /// <summary>The name a member's tab and list row go by: its name, else <paramref name="alias"/> (its login's), else the
    /// email's part before the @.</summary>
    public static string TitleOf(Member member, string? alias) =>
        member.Name ?? (string.IsNullOrWhiteSpace(alias) ? LoginLook.DefaultAlias(member.Email) ?? member.Email : alias);
}
