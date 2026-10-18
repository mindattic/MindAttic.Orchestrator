using System.Diagnostics;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Audit;

/// <summary>Runs both audit tiers over the workspace and keeps the latest report on disk.</summary>
public sealed class AuditRunner(OrchestratorOptions options, JsonStore store)
{
    private const string StoreName = "audit";

    public AuditReport Run(CancellationToken ct = default)
    {
        var clock = Stopwatch.StartNew();
        var repos = new MindAttic.Workflow.Workspace(options.WorkspaceRoot).Repos().Select(r => (r.Name, r.Path)).ToList();
        var (drift, missing) = ConventionAudit.Run(repos);
        var (duplicates, files) = DuplicationAudit.Run(repos, ct);
        var report = new AuditReport(DateTime.UtcNow, clock.Elapsed, repos.Count, files, drift, missing, duplicates);
        store.Save(StoreName, report);
        return report;
    }

    public AuditReport? Latest()
    {
        TryLatest(out var report);
        return report;
    }

    /// <summary>Like <see cref="Latest"/>, but false when the file exists and couldn't be read.</summary>
    public bool TryLatest(out AuditReport? report)
    {
        var read = store.TryLoad<AuditHolder>(StoreName, out var r);
        report = r.RanUtc == default ? null
            : new AuditReport(r.RanUtc, r.Took, r.ReposScanned, r.FilesScanned, r.Drift, r.Missing, r.Duplicates);
        return read;
    }

    // JsonStore needs a parameterless type to fall back to when the file is missing.
    private sealed class AuditHolder
    {
        public DateTime RanUtc { get; init; }
        public TimeSpan Took { get; init; }
        public int ReposScanned { get; init; }
        public int FilesScanned { get; init; }
        public List<DriftFinding> Drift { get; init; } = [];
        public List<MissingFinding> Missing { get; init; } = [];
        public List<DuplicateGroup> Duplicates { get; init; } = [];
    }
}
