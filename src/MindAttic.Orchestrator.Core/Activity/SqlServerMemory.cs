using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// Keeps localhost's SQL Server (MSSQLSERVER) at <see cref="OrchestratorSettings.SqlServerMaxMemoryMb"/> (M.ORCH-T1621), so its
/// memory cap is live-dialable from the Vault. 0 leaves SQL Server's own setting alone; any other value is raised to at least
/// <see cref="OrchestratorSettings.SqlServerMaxMemoryFloorMb"/>, since 1024 MB crashed the instance (error 17300). The instance
/// is read and changed once per change of the target (and once at the daemon's start), never every tick; a failure is
/// reported and not tried again until the target changes.
/// </summary>
public sealed class SqlServerMemory
{
    /// <summary>The target last handled (applied, already in use, or failed); null until the first look.</summary>
    private int? handled;

    /// <summary>The value to apply for <paramref name="setting"/>: 0 for "leave it alone" (0, negative or not a number),
    /// otherwise at least the floor.</summary>
    public static int Target(double setting) =>
        !double.IsFinite(setting) || setting <= 0 ? 0
        : (int)Math.Clamp(Math.Ceiling(setting), OrchestratorSettings.SqlServerMaxMemoryFloorMb, int.MaxValue);

    /// <summary>The instance's max server memory (MB) in use; a test's stands in for the real instance.</summary>
    internal Func<int> ReadInUse { get; init; } = ReadInUseFromServer;

    /// <summary>Sets the instance's max server memory (MB); a test's stands in for the real instance.</summary>
    internal Action<int> Apply { get; init; } = ApplyToServer;

    /// <summary>What happened this call, for the daemon's log: null when nothing was looked at.</summary>
    public string? Observe(OrchestratorSettings s)
    {
        var target = Target(s.SqlServerMaxMemoryMb);
        if (target == handled) return null;
        handled = target;
        if (target == 0) return null;
        try
        {
            var inUse = ReadInUse();
            if (inUse == target) return null;
            Apply(target);
            return $"SQL Server max server memory set to {target} MB (was {inUse} MB)" +
                (target != s.SqlServerMaxMemoryMb ? $"; {s.SqlServerMaxMemoryMb:0.#} MB was raised to the {OrchestratorSettings.SqlServerMaxMemoryFloorMb} MB floor" : "");
        }
        catch (Exception ex)
        {
            return $"couldn't set SQL Server's max server memory to {target} MB (not tried again until the setting changes): {ex.Message}";
        }
    }

    private static Microsoft.Data.SqlClient.SqlConnection OpenMaster()
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnection(OrchestratorServer.DefaultConnection(database: "master"));
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static int ReadInUseFromServer()
    {
        using var connection = OpenMaster();
        return Convert.ToInt32(Sql.Scalar(connection, null,
            "SELECT CONVERT(int, value_in_use) FROM sys.configurations WHERE name = N'max server memory (MB)';"), CultureInfo.InvariantCulture);
    }

    private static void ApplyToServer(int mb)
    {
        if (mb < OrchestratorSettings.SqlServerMaxMemoryFloorMb) throw new ArgumentOutOfRangeException(nameof(mb), mb, "under the floor");
        using var connection = OpenMaster();
        // max server memory is an advanced option: shown for the change, then put back as it was.
        Sql.Run(connection, null, """
            DECLARE @advanced int = (SELECT CONVERT(int, value_in_use) FROM sys.configurations WHERE name = N'show advanced options');
            IF @advanced = 0 BEGIN EXEC sp_configure N'show advanced options', 1; RECONFIGURE; END;
            EXEC sp_configure N'max server memory (MB)', @mb; RECONFIGURE;
            IF @advanced = 0 BEGIN EXEC sp_configure N'show advanced options', 0; RECONFIGURE; END;
            """, ("@mb", mb));
    }
}
