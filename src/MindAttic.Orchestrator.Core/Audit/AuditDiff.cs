namespace MindAttic.Orchestrator.Audit;

/// <summary>What a new audit found that the previous one didn't: drifted copies and duplicated code.</summary>
public sealed record AuditNews(IReadOnlyList<DriftFinding> Drift, IReadOnlyList<DuplicateGroup> Duplicates)
{
    public bool Any => Drift.Count > 0 || Duplicates.Count > 0;

    public override string ToString() => string.Join("; ", new[]
    {
        Drift.Count == 0 ? null : $"{Drift.Count} newly drifted agent file{(Drift.Count == 1 ? "" : "s")} ({string.Join(", ", Drift.Take(3).Select(d => $"{d.Repo}/{d.File}"))}{(Drift.Count > 3 ? ", …" : "")})",
        Duplicates.Count == 0 ? null : $"{Duplicates.Count} new duplicated-code group{(Duplicates.Count == 1 ? "" : "s")} ({string.Join(", ", Duplicates.Take(3).Select(g => string.Join("+", g.Files.Select(f => f.Repo).Distinct())))}{(Duplicates.Count > 3 ? ", …" : "")})",
    }.Where(x => x is not null));

    /// <summary>A drift finding is new when that repo's copy of that file wasn't drifted before; a duplicate group when its set of files wasn't a group before.</summary>
    public static AuditNews Between(AuditReport? before, AuditReport after)
    {
        if (before is null) return new([], []);
        // Filtered the same way the after-side is (!IsCustom): a (Repo, File) that was only ever seen as a custom, unrelated
        // file in the old report mustn't permanently suppress it once it becomes genuine drift.
        var oldDrift = before.Drift.Where(d => !d.IsCustom).Select(d => (d.Repo, d.File)).ToHashSet();
        var oldGroups = before.Duplicates.Select(Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new([.. after.Drift.Where(d => !d.IsCustom && !oldDrift.Contains((d.Repo, d.File)))],
                   [.. after.Duplicates.Where(g => !oldGroups.Contains(Key(g)))]);
    }

    private static string Key(DuplicateGroup g) =>
        string.Join("|", g.Files.Select(f => $"{f.Repo}:{f.Path}").Order(StringComparer.OrdinalIgnoreCase));
}
