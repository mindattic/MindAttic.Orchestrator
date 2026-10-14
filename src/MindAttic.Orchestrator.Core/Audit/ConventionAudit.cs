using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Audit;

/// <summary>
/// Tier 1: the agent files every repo is supposed to share (<c>.claude/commands</c>, hooks, skills,
/// <c>.prose/commands</c>, the <c>AGENTS.md</c>/<c>CLAUDE.md</c> pointers). For each one, the version
/// most repos have is taken as canonical (after replacing the repo's own name and path, which
/// legitimately differ), and every other version is drift. Nothing here assumes what the canonical
/// text should be; the workspace votes.
/// </summary>
public static partial class ConventionAudit
{
    /// <summary>A file must appear in at least this many repos to have a convention at all.</summary>
    public const int MinRepos = 3;

    /// <summary>Repos lacking a file count as "missing" when this share of agent-enabled repos have it.</summary>
    public const double MissingThreshold = 0.6;

    public static (IReadOnlyList<DriftFinding> Drift, IReadOnlyList<MissingFinding> Missing) Run(IReadOnlyList<(string Name, string Path)> repos)
    {
        var byKey = new Dictionary<string, List<(string Repo, string RepoPath, string File, string Normalized)>>(StringComparer.OrdinalIgnoreCase);
        var agentRepos = new List<string>();

        foreach (var (name, path) in repos)
        {
            var files = AgentFiles(path).ToList();
            if (Directory.Exists(System.IO.Path.Combine(path, ".claude"))) agentRepos.Add(name);
            foreach (var (key, file) in files)
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch (IOException) { continue; }
                (byKey.TryGetValue(key, out var list) ? list : byKey[key] = []).Add((name, path, file, Normalize(text, name, path)));
            }
        }

        var drift = new List<DriftFinding>();
        var missing = new List<MissingFinding>();
        foreach (var (key, copies) in byKey.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (copies.Count < MinRepos) continue;
            var groups = copies.GroupBy(c => Hash(c.Normalized)).OrderByDescending(g => g.Count()).ToList();
            var canonical = groups[0].First();
            if (groups.Count > 1 && groups[0].Count() > groups[1].Count())
            {
                foreach (var g in groups.Skip(1))
                    foreach (var c in g)
                        drift.Add(new DriftFinding(key, c.Repo, c.File, canonical.File, groups[0].Count(), copies.Count, groups.Count,
                            LineSimilarity(canonical.Normalized, c.Normalized)));
            }

            var have = copies.Select(c => c.Repo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var eligible = agentRepos.Count;
            if (eligible >= MinRepos && have.Count >= eligible * MissingThreshold)
            {
                foreach (var r in agentRepos.Where(r => !have.Contains(r)))
                    missing.Add(new MissingFinding(key, r, have.Count, eligible, canonical.File));
            }
        }
        return (drift.OrderBy(d => d.Similarity).ToList(), missing);
    }

    /// <summary>(key, full path) for every shared agent file in a repo.</summary>
    internal static IEnumerable<(string Key, string File)> AgentFiles(string repoPath)
    {
        foreach (var top in new[] { "AGENTS.md", "CLAUDE.md" })
        {
            var f = System.IO.Path.Combine(repoPath, top);
            if (File.Exists(f)) yield return (top, f);
        }
        foreach (var (dir, pattern) in new[] { (".claude/commands", "*.md"), (".claude/hooks", "*.*"), (".prose/commands", "*.md") })
        {
            var d = System.IO.Path.Combine(repoPath, dir);
            if (!Directory.Exists(d)) continue;
            foreach (var f in Directory.EnumerateFiles(d, pattern))
                yield return ($"{dir}/{System.IO.Path.GetFileName(f)}", f);
        }
        var skills = System.IO.Path.Combine(repoPath, ".claude", "skills");
        if (Directory.Exists(skills))
        {
            foreach (var s in Directory.EnumerateDirectories(skills))
            {
                var f = System.IO.Path.Combine(s, "SKILL.md");
                if (File.Exists(f)) yield return ($".claude/skills/{System.IO.Path.GetFileName(s)}/SKILL.md", f);
            }
        }
    }

    /// <summary>Line endings, trailing space and the repo's own name/path don't make a file different.</summary>
    internal static string Normalize(string text, string repoName, string repoPath)
    {
        var s = text.Replace("\r\n", "\n");
        s = s.Replace(repoPath.TrimEnd('\\'), "{PATH}", StringComparison.OrdinalIgnoreCase)
             .Replace(repoPath.TrimEnd('\\').Replace('\\', '/'), "{PATH}", StringComparison.OrdinalIgnoreCase)
             .Replace(repoName, "{REPO}", StringComparison.OrdinalIgnoreCase);
        return string.Join('\n', s.Split('\n').Select(l => TrailingSpace().Replace(l, ""))).Trim();
    }

    /// <summary>Share of lines the two texts have in common (multiset Dice coefficient).</summary>
    internal static double LineSimilarity(string a, string b)
    {
        var la = a.Split('\n');
        var lb = b.Split('\n');
        if (la.Length + lb.Length == 0) return 1;
        var counts = la.GroupBy(l => l).ToDictionary(g => g.Key, g => g.Count());
        var common = 0;
        foreach (var l in lb)
        {
            if (counts.TryGetValue(l, out var n) && n > 0) { common++; counts[l] = n - 1; }
        }
        return 2.0 * common / (la.Length + lb.Length);
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    [GeneratedRegex(@"\s+$")]
    private static partial Regex TrailingSpace();
}
