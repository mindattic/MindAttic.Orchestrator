using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

public sealed partial class Daemon
{
    private bool diskHeld;
    private string? diskReclaimLogged;

    /// <summary>Why the daemon's own heavy work (a main-line suite, a branch build) may not start now, or null: the memory guard
    /// past ok, or the disk under its floor (M.ORCH-T1679). Neither stops one that's running.</summary>
    internal static string? MachineHeld() =>
        Activity.MemoryGuard.Shared.Last is { Level: > Activity.MemoryLevel.Ok } m ? $"RAM is low ({m.Reading.Describe()})"
        : Activity.DiskGuard.Shared.Last?.Short;

    /// <summary>Every tick, the disk floor's judgment (M.ORCH-T1679; a drive read is cheap, and under the floor the self-cleanup
    /// runs first): a hold starting or lifting is logged, and a new hold alerted with how much space lifts it. The holding
    /// itself is done where work starts (allocate, new CLIs, main-line tests, branch builds); nothing running is stopped.</summary>
    internal void WatchDiskHold(OrchestratorSettings s, Activity.DiskGuard? watched = null)
    {
        var guard = watched ?? Activity.DiskGuard.Shared;
        var hold = guard.Observe(s, DateTime.UtcNow);
        if (guard.LastReclaim is { } note && note != diskReclaimLogged)
        {
            diskReclaimLogged = note;
            Say(LogEventLevel.Information, $"disk floor: under {s.DiskHoldGb:0.#} GB free, so the self-cleanup ran first: {note}");
        }
        if ((hold is not null) == diskHeld) return;
        diskHeld = hold is not null;
        if (hold is null)
        {
            Say(LogEventLevel.Information, "disk floor: space is back; new work may start again");
            return;
        }
        var text = $"{hold.Why}.";
        Say(LogEventLevel.Error, $"disk floor: holding new work. {text}");
        Alert("Disk full: holding new work", text, urgent: true);
    }
}
