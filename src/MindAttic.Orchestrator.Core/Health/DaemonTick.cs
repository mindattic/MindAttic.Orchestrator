using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Health;

/// <summary>
/// The daemon's tick timing as the links probe reads it (M.ORCH-T2032): which daemon (<see cref="Pid"/>), when its current or
/// last tick started and ended, and how long the last finished one took. The daemon saves it as a volatile state row
/// (<see cref="StateName"/>, this machine's, never in the dump) at the start and end of every tick, so a probe in another
/// process sees a tick that is still running as well as one that ran long: the M.ORCH-T1922 stall held one tick for many
/// minutes while nothing outside the daemon could tell.
/// </summary>
public sealed record DaemonTick(int Pid, DateTime StartedUtc, DateTime? EndedUtc, double? LastTookSeconds)
{
    /// <summary>The state row's name.</summary>
    public const string StateName = "daemon-tick";

    /// <summary>The check id (unit <c>s</c>) and its instance.</summary>
    public const string Check = "daemon:tick", Component = "daemon";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Whether a tick is under way (started after the last one ended).</summary>
    public bool Running => EndedUtc is not { } ended || ended < StartedUtc;

    /// <summary>The reading at <paramref name="nowUtc"/>: the tick under way so far, or the last one's length when that was
    /// longer (a slow tick stays visible until a faster one finishes); 0 before any tick has finished.</summary>
    public double SecondsAt(DateTime nowUtc) =>
        Math.Max(Running ? Math.Max(0, (nowUtc - StartedUtc).TotalSeconds) : 0, LastTookSeconds ?? 0);

    /// <summary>The row's JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The tick kept in <paramref name="db"/>; null when none is kept or it can't be read.</summary>
    public static DaemonTick? Load(OrchestratorDb db)
    {
        try { return db.LoadState(StateName) is { } row ? JsonSerializer.Deserialize<DaemonTick>(row.Value, Json) : null; }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or System.Data.Common.DbException) { return null; }
    }

    /// <summary>Keeps this tick in <paramref name="db"/>.</summary>
    public void Save(OrchestratorDb db) => db.SaveState(StateName, ToJson());
}
