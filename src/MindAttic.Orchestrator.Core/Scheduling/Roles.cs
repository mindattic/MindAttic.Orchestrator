using System.Data.Common;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The role a handed-out ticket is worked as (M.ORCH-T1746, roadmap Phase 1): its <c>role:&lt;name&gt;</c> label, else
/// <see cref="Default"/>. The role's model, from the <c>roles</c> table (M.ORCH-T1742), is the one its subagent runs on;
/// a role that names no model (none, <c>inherit</c>, or no row for it) leaves the choice to <see cref="ModelChoice.For"/>.
/// </summary>
public static class Roles
{
    /// <summary>The role of a ticket that names none: every dispatched task is implementation work until a pipeline says
    /// otherwise.</summary>
    public const string Default = "implementer";

    /// <summary>The role <paramref name="named"/> (a ticket's <c>role:</c> label), else <see cref="Default"/>.</summary>
    public static string Of(string? named) => string.IsNullOrWhiteSpace(named) ? Default : named.Trim().ToLowerInvariant();

    /// <summary>The model <paramref name="role"/>'s row in <paramref name="db"/>'s <c>roles</c> table names; null when it
    /// names none, has no row, or the table can't be read (the caller falls back to <see cref="ModelChoice.For"/>).</summary>
    public static string? ModelOf(OrchestratorDb? db, string role)
    {
        if (db is null || string.IsNullOrWhiteSpace(role)) return null;
        try
        {
            using var connection = db.OpenData();
            return Sql.Read(connection, null, "SELECT model FROM roles WHERE name = @n;",
                r => r.IsDBNull(0) ? null : r.GetString(0), ("@n", role)).FirstOrDefault();
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or IOException)
        {
            return null;
        }
    }
}
