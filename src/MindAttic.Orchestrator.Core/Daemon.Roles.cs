using MindAttic.Orchestrator.Scheduling;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>The role catalog kept in step with its files (M.ORCH-T2066, on <see cref="RoleCatalog"/>).</summary>
public sealed partial class Daemon
{
    private string? rolesFingerprint;

    /// <summary>
    /// Syncs the <c>roles</c> table from the role files under <paramref name="workspaceRoot"/> (<see cref="RoleCatalog.Sync"/>) the
    /// first tick and whenever their <see cref="RoleCatalog.Fingerprint"/> changes (a file added, edited or removed, or another
    /// workspace); cheap otherwise: a listing of a dozen files. A sync that throws is retried next tick.
    /// </summary>
    internal void SyncRolesIfChanged(string workspaceRoot, DateTime nowUtc)
    {
        var fingerprint = RoleCatalog.Fingerprint(workspaceRoot);
        if (fingerprint is null || fingerprint == rolesFingerprint) return;
        var result = RoleCatalog.Sync(store.Db, workspaceRoot, nowUtc);
        rolesFingerprint = fingerprint;
        if (result is null) return;
        if (result.Any || result.Skipped.Count > 0)
            Say(result.Skipped.Count > 0 ? LogEventLevel.Warning : LogEventLevel.Information, $"roles: {result}");
    }
}
