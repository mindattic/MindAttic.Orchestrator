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

    /// <summary>Adds a member, checked first (<see cref="Check"/>); fails when <paramref name="email"/> already is one.</summary>
    /// <exception cref="ArgumentException">An email, role, tier or status that isn't valid.</exception>
    /// <exception cref="InvalidOperationException">Already a member.</exception>
    public static Member Add(OrchestratorDb db, string email, string? name, string role, string tier, string? status, DateTime nowUtc)
    {
        var member = Check(email, name, role, tier, status ?? Statuses[0]) with { JoinedUtc = nowUtc, UpdatedUtc = nowUtc };
        db.WriteData((connection, tx) =>
        {
            if (Rows(connection, tx).Any(m => m.Email == member.Email))
                throw new InvalidOperationException($"{member.Email} is already a member (orchestrator members set changes it).");
            Upsert(connection, tx, member);
        });
        return member;
    }

    /// <summary>Changes what's given of a member (null leaves it; an empty <paramref name="name"/> clears it); fails when
    /// <paramref name="email"/> isn't one. Written only when something changed.</summary>
    /// <exception cref="ArgumentException">An email, role, tier or status that isn't valid.</exception>
    /// <exception cref="InvalidOperationException">Not a member.</exception>
    public static Member Set(OrchestratorDb db, string email, string? name, string? role, string? tier, string? status, DateTime nowUtc)
    {
        var key = Key(email);
        Member? result = null;
        db.WriteDataIf((connection, tx) =>
        {
            var was = Rows(connection, tx).FirstOrDefault(m => m.Email == key)
                ?? throw new InvalidOperationException($"{key} isn't a member (orchestrator members add adds one).");
            var next = Check(key, name is null ? was.Name : name, role ?? was.Role, tier ?? was.Tier, status ?? was.Status)
                with { JoinedUtc = was.JoinedUtc, UpdatedUtc = was.UpdatedUtc };
            if (next == was) { result = was; return false; }
            result = next with { UpdatedUtc = nowUtc };
            Upsert(connection, tx, result);
            return true;
        });
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
        return changed;
    }

    /// <summary>Removes a member; false when <paramref name="email"/> wasn't one.</summary>
    /// <exception cref="ArgumentException">Not an email address.</exception>
    public static bool Remove(OrchestratorDb db, string email)
    {
        var key = Key(email);
        var removed = false;
        db.WriteDataIf((connection, tx) => removed = Sql.Run(connection, tx, "DELETE FROM members WHERE email = @email;", ("@email", key)) > 0);
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
