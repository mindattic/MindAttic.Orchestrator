namespace MindAttic.Orchestrator;

/// <summary>
/// Maps a working directory to the repo that contains it. Longest path wins so a worktree or a
/// nested folder never lands on a sibling whose name is a prefix (MindAttic.Ideas vs
/// MindAttic.Ideas.Library).
/// </summary>
public sealed class RepoMatcher
{
    private readonly (string Name, string Path)[] repos;

    public RepoMatcher(IEnumerable<(string Name, string Path)> repos)
    {
        this.repos = [.. repos
            .Select(r => (r.Name, Normalize(r.Path)))
            .OrderByDescending(r => r.Item2.Length)];
    }

    public string? Match(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var dir = Normalize(directory);
        foreach (var (name, path) in repos)
        {
            if (dir.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                dir.StartsWith(path + '\\', StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }

    internal static string Normalize(string path)
    {
        try { path = System.IO.Path.GetFullPath(path); } catch (ArgumentException) { }
        return path.Replace('/', '\\').TrimEnd('\\');
    }
}
