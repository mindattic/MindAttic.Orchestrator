using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>Someone <c>/org</c>'s &lt;who&gt; can name (M.ORCH-T686): a member's email, with what they go by.</summary>
/// <param name="Email">Normalized, as <see cref="AccountDirs.Normalize"/> makes it.</param>
/// <param name="Name">The member's name ("Ryan DeBraal"); null when they have none.</param>
/// <param name="Aliases">Their logins' names (<see cref="LoginLook.Alias"/>).</param>
/// <param name="Initials">Their logins' badges (<see cref="LoginLook.Initials"/>); the name's own initials are added on their own.</param>
/// <param name="Alias">Their member row's own alias (<see cref="Member.Alias"/>, M.ORCH-T2221): a short display name given to
/// tell two members apart whose emails' parts before the @ clash; null when they have none.</param>
public sealed record OrgPerson(string Email, string? Name, IReadOnlyList<string> Aliases, IReadOnlyList<string> Initials, string? Alias = null)
{
    /// <summary>"Ryan DeBraal (ryandebraal@mindattic.com)", by the member's own alias when they have one (M.ORCH-T2245), or
    /// the bare email with neither.</summary>
    public string Label => (Alias ?? Name) is { } n ? $"{n} ({Email})" : Email;

    /// <summary>The first letter of each word of the name: "RD" for Ryan DeBraal, "E" for Erin; null without a name.</summary>
    public string? NameInitials => Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } words
        ? string.Concat(words.Select(w => char.ToUpperInvariant(w[0]))) : null;

    /// <summary>Every way this person can be named: the email, the part before the @, the member's own alias, the name, its
    /// first word, each login alias and initials.</summary>
    public IEnumerable<string> Keys()
    {
        yield return Email;
        if (LoginLook.DefaultAlias(Email) is { } local) yield return local;
        if (Alias is { } alias) yield return alias;
        if (Name is { } name)
        {
            yield return name;
            yield return name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }
        if (NameInitials is { } ni) yield return ni;
        foreach (var a in Aliases) yield return a;
        foreach (var i in Initials) yield return i;
    }
}

/// <summary>
/// Who <c>orchestrator org</c>'s &lt;who&gt; means (M.ORCH-T686): any member by their email, the part before the @ (ohleo199),
/// their name (Ryan DeBraal), first name (Erin), a login's alias, or initials (RD, E), case-insensitive. Exact matches only:
/// a word that names more than one person, or nobody, comes back as everyone it could mean (or no one), never a guess.
/// </summary>
public static class MemberLookup
{
    /// <summary>Everyone <paramref name="who"/> names (one is a match; none or several isn't). When several match, but exactly
    /// one of them by its full email or its own alias (<see cref="OrgPerson.Alias"/>, M.ORCH-T2221) exactly, that one wins over
    /// the rest, who only matched a weaker key (the email's part before the @, a name's first word, and so on): a full email or
    /// alias is unambiguous by construction, so it's taken over a guess. Still several exact matches (shouldn't normally happen,
    /// since emails and aliases are unique) is left as several: no guessing.</summary>
    public static IReadOnlyList<OrgPerson> Match(IReadOnlyList<OrgPerson> people, string who)
    {
        var w = string.Join(' ', who.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (w.Length == 0) return [];
        var found = (IReadOnlyList<OrgPerson>)
            [.. people.Where(p => p.Keys().Any(k => string.Equals(k.Trim(), w, StringComparison.OrdinalIgnoreCase)))
                .DistinctBy(p => p.Email, StringComparer.OrdinalIgnoreCase)];
        if (found.Count <= 1) return found;
        var exact = found.Where(p => string.Equals(p.Email, w, StringComparison.OrdinalIgnoreCase)
            || (p.Alias is { } alias && string.Equals(alias, w, StringComparison.OrdinalIgnoreCase))).ToList();
        return exact.Count == 1 ? exact : found;
    }

    /// <summary>The members table's people, plus anyone signed in on this machine who isn't in it, each with their logins'
    /// aliases and initials: the per-CLI logins in <paramref name="dirs"/> and the global login when it's theirs.</summary>
    public static IReadOnlyList<OrgPerson> People(OrchestratorDb db, AccountDirs dirs)
    {
        var looks = new Dictionary<string, List<LoginLook>>(StringComparer.Ordinal);
        void Add(string? email, Func<LoginLook> look)
        {
            if (AccountDirs.Normalize(email) is not { } e) return;
            try { (looks.TryGetValue(e, out var l) ? l : looks[e] = []).Add(look()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        foreach (var dir in dirs.All()) Add(dir.Email, () => LoginLook.For(dir.Directory, dir.Email));
        if (LoginLook.GlobalEmail(dirs) is { } global) Add(global, () => LoginLook.For(dirs.ClaudeDir, AccountDirs.Normalize(global)));

        var people = new List<OrgPerson>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        OrgPerson Person(string email, string? name, string? alias = null)
        {
            var mine = looks.GetValueOrDefault(email) ?? [];
            return new OrgPerson(email, name, [.. mine.Select(l => l.Alias).Distinct()], [.. mine.Select(l => l.Initials).Distinct()], alias);
        }
        foreach (var m in MemberRoster.Load(db))
            if (seen.Add(m.Email)) people.Add(Person(m.Email, m.Name, m.Alias));
        foreach (var a in AccountRoster.Load(db))
            if (AccountDirs.Normalize(a.Email) is { } e && seen.Add(e)) people.Add(Person(e, null));
        foreach (var e in looks.Keys)
            if (seen.Add(e)) people.Add(Person(e, null));
        return people;
    }
}
