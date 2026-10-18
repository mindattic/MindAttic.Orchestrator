using System.Text;

namespace MindAttic.Orchestrator.Audit;

/// <summary>
/// Tier 2: code copied between repos. Each source file is reduced to its meaningful lines (no blank
/// lines, braces, comments or imports, whitespace collapsed), then fingerprinted as overlapping
/// windows of <see cref="Window"/> lines. Two files in different repos that share many windows hold
/// the same code even if it has drifted a little; files whose whole fingerprint matches are
/// identical copies. Only cross-repo duplication is reported: within a repo it's that repo's business.
/// </summary>
public static class DuplicationAudit
{
    public const int Window = 6;
    public const int MinSharedWindows = 8;
    public const double MinSimilarity = 0.5;
    private const int MaxFileBytes = 400 * 1024;
    private const int MaxFilesPerWindow = 12;

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ps1", ".psm1", ".ts", ".tsx", ".js", ".jsx", ".py", ".razor", ".xaml", ".css",
    };

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "dist", "build", "out", "artifacts", "packages",
        "TestResults", ".venv", "venv", "__pycache__", "Library", "Temp", "Logs", "UserSettings", "worktrees",
        "lib", "vendor", "third_party", "Plugins", "Migrations", "publish",
    };

    private sealed record Source(int Id, string Repo, string Path, int Lines, ulong[] Windows, ulong Whole);

    public static (IReadOnlyList<DuplicateGroup> Groups, int FilesScanned) Run(IReadOnlyList<(string Name, string Path)> repos, CancellationToken ct = default)
    {
        var sources = new List<Source>();
        foreach (var (name, path) in repos)
        {
            foreach (var file in SourceFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                var lines = SignificantLines(file);
                if (lines.Count < Window * 2) continue;
                sources.Add(new Source(sources.Count, name, file, lines.Count, Shingles(lines), Hash(string.Join('\n', lines))));
            }
        }

        var groups = new List<DuplicateGroup>();
        var claimed = new HashSet<int>();
        // One copy of each identical group stays in the near-copy search: a drifted copy in a third repo is otherwise left
        // with nothing to match and never reported (M.ORCH-T352).
        var representatives = new HashSet<int>();

        // Identical copies first: the clearest extraction candidates.
        foreach (var g in sources.GroupBy(s => s.Whole).Where(g => g.Select(s => s.Repo).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
        {
            var files = g.ToList();
            foreach (var f in files) claimed.Add(f.Id);
            representatives.Add(files[0].Id);
            groups.Add(new DuplicateGroup([.. files.Select(f => new DuplicateFile(f.Repo, f.Path, f.Lines))], true, 1, files[0].Lines, HomeFor(files[0].Path)));
        }

        // Near copies: count shared windows per cross-repo file pair through an inverted index.
        var index = new Dictionary<ulong, List<int>>();
        foreach (var s in sources.Where(s => !claimed.Contains(s.Id) || representatives.Contains(s.Id)))
            foreach (var w in s.Windows.Distinct())
                (index.TryGetValue(w, out var list) ? list : index[w] = []).Add(s.Id);

        var shared = new Dictionary<(int, int), int>();
        foreach (var ids in index.Values)
        {
            // A window in many files is boilerplate (a common ctor, a standard Main), not a copy.
            if (ids.Count < 2 || ids.Count > MaxFilesPerWindow) continue;
            for (var i = 0; i < ids.Count; i++)
                for (var j = i + 1; j < ids.Count; j++)
                {
                    var a = sources[ids[i]]; var b = sources[ids[j]];
                    if (string.Equals(a.Repo, b.Repo, StringComparison.OrdinalIgnoreCase)) continue;
                    var key = a.Id < b.Id ? (a.Id, b.Id) : (b.Id, a.Id);
                    shared[key] = shared.GetValueOrDefault(key) + 1;
                }
        }

        // Pairs that overlap enough become edges; each connected cluster is one finding, so a helper
        // copied into five repos reads as one group of five, not ten pairs.
        var edges = new List<(int A, int B, double Similarity, int Lines)>();
        foreach (var ((x, y), count) in shared.Where(kv => kv.Value >= MinSharedWindows))
        {
            var a = sources[x]; var b = sources[y];
            var similarity = (double)count / Math.Min(a.Windows.Distinct().Count(), b.Windows.Distinct().Count());
            if (similarity >= MinSimilarity) edges.Add((x, y, Math.Min(1, similarity), count + Window - 1));
        }

        var parent = new Dictionary<int, int>();
        int Find(int i)
        {
            while (parent.TryGetValue(i, out var p) && p != i)
            {
                var grandparent = parent.GetValueOrDefault(p, p);
                parent[i] = grandparent; // path halving
                i = grandparent;
            }
            return i;
        }
        foreach (var e in edges) { var ra = Find(e.A); var rb = Find(e.B); if (ra != rb) parent[ra] = rb; }

        foreach (var cluster in edges.GroupBy(e => Find(e.A)))
        {
            var members = cluster.SelectMany(e => new[] { e.A, e.B }).Distinct().Select(i => sources[i]).OrderBy(f => f.Repo, StringComparer.OrdinalIgnoreCase).ToList();
            groups.Add(new DuplicateGroup(
                [.. members.Select(f => new DuplicateFile(f.Repo, f.Path, f.Lines))],
                false, cluster.Min(e => e.Similarity), cluster.Max(e => e.Lines), HomeFor(members[0].Path)));
        }

        return ([.. groups
            .OrderByDescending(g => g.Identical ? g.SharedLines * g.RepoCount : g.SharedLines)
            .ThenByDescending(g => g.Similarity)], sources.Count);
    }

    /// <summary>Where shared code of this kind already has a home in the workspace.</summary>
    internal static string HomeFor(string path)
    {
        var p = path.Replace('\\', '/');
        if (p.Contains("/.claude/", StringComparison.OrdinalIgnoreCase) || p.Contains("/.prose/", StringComparison.OrdinalIgnoreCase))
            return "mindattic-agent-standard";
        return System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xaml" => "MindAttic.Components (WPF)",
            ".razor" or ".css" or ".js" or ".ts" or ".tsx" or ".jsx" => "MindAttic.Components (web/Blazor)",
            ".ps1" or ".psm1" => "MindAttic.Deploy or mindattic-agent-standard",
            ".py" => "a shared Python package",
            _ => "MindAttic.Helpers (or a new shared package)",
        };
    }

    internal static IEnumerable<string> SourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }

            foreach (var e in entries)
            {
                var name = System.IO.Path.GetFileName(e);
                if (Directory.Exists(e))
                {
                    var keep = name is ".claude" or ".prose" || (!SkipDirs.Contains(name) && !name.StartsWith('.'));
                    if (keep)
                        pending.Push(e);
                    continue;
                }
                if (!Extensions.Contains(System.IO.Path.GetExtension(name)) || IsGenerated(name)) continue;
                try { if (new FileInfo(e).Length > MaxFileBytes) continue; }
                catch (IOException) { continue; }
                yield return e;
            }
        }
    }

    private static bool IsGenerated(string name) =>
        name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("GlobalUsings.cs", StringComparison.OrdinalIgnoreCase);

    internal static List<string> SignificantLines(string file)
    {
        string[] raw;
        try { raw = File.ReadAllLines(file); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }

        var result = new List<string>(raw.Length);
        var inBlockComment = false;
        foreach (var r in raw)
        {
            var l = r.Trim();
            if (inBlockComment) { if (l.Contains("*/")) inBlockComment = false; continue; }
            if (l.StartsWith("/*")) { inBlockComment = !l.Contains("*/"); continue; }
            if (l.Length == 0 || l.StartsWith("//") || l.StartsWith('#') || l.StartsWith("<#") || l.StartsWith("<!--")) continue;
            if (l.StartsWith("using ") || l.StartsWith("import ") || l.StartsWith("from ") || l.StartsWith("namespace ")) continue;
            if (l.All(c => c is '{' or '}' or '(' or ')' or '[' or ']' or ';' or ',' or ' ')) continue;
            result.Add(CollapseSpace(l));
        }
        return result;
    }

    private static string CollapseSpace(string s)
    {
        var sb = new StringBuilder(s.Length);
        var space = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c)) { if (!space) sb.Append(' '); space = true; }
            else { sb.Append(c); space = false; }
        }
        return sb.ToString();
    }

    private static ulong[] Shingles(List<string> lines)
    {
        var hashes = lines.Select(l => Hash(l)).ToArray();
        var windows = new ulong[hashes.Length - Window + 1];
        for (var i = 0; i < windows.Length; i++)
        {
            ulong h = 1469598103934665603;
            for (var k = 0; k < Window; k++) h = (h ^ hashes[i + k]) * 1099511628211;
            windows[i] = h;
        }
        return windows;
    }

    /// <summary>FNV-1a over the UTF-16 code units: fast, stable across runs, plenty for fingerprints.</summary>
    private static ulong Hash(string s)
    {
        ulong h = 1469598103934665603;
        foreach (var c in s) h = (h ^ c) * 1099511628211;
        return h;
    }
}
