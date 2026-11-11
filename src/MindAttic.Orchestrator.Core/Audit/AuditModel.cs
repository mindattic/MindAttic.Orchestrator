namespace MindAttic.Orchestrator.Audit;

/// <summary>A repo whose copy of a shared agent file differs from what most repos have.</summary>
public sealed record DriftFinding(
    string File,
    string Repo,
    string Path,
    string CanonicalPath,
    int CanonicalCount,
    int Total,
    int Variants,
    double Similarity)
{
    /// <summary>Under 30% alike it isn't a stale copy of the shared file, it's a different file.</summary>
    public bool IsCustom => Similarity < 0.3;
}

/// <summary>A shared agent file most repos have and this one doesn't.</summary>
public sealed record MissingFinding(string File, string Repo, int HaveIt, int Eligible, string ExamplePath);

/// <summary>One file in a <see cref="DuplicateGroup"/>: the <c>Repo</c> it lives in, its <c>Path</c> there and its <c>Lines</c> count.</summary>
public sealed record DuplicateFile(string Repo, string Path, int Lines);

/// <summary>The same code living in more than one repo.</summary>
public sealed record DuplicateGroup(
    IReadOnlyList<DuplicateFile> Files,
    bool Identical,
    double Similarity,
    int SharedLines,
    string SuggestedHome)
{
    /// <summary>How many distinct repos hold a copy.</summary>
    public int RepoCount => Files.Select(f => f.Repo).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}

/// <summary>One audit run: when it <c>RanUtc</c> and how long it <c>Took</c>, how many repos and files it scanned, and what it found:
/// <c>Drift</c> (diverged copies of shared agent files), <c>Missing</c> (shared files a repo lacks) and <c>Duplicates</c>
/// (the same code in more than one repo).</summary>
public sealed record AuditReport(
    DateTime RanUtc,
    TimeSpan Took,
    int ReposScanned,
    int FilesScanned,
    IReadOnlyList<DriftFinding> Drift,
    IReadOnlyList<MissingFinding> Missing,
    IReadOnlyList<DuplicateGroup> Duplicates)
{
    /// <summary>The report before any audit has run: nothing scanned, nothing found.</summary>
    public static AuditReport Empty { get; } = new(DateTime.MinValue, TimeSpan.Zero, 0, 0, [], [], []);
}
