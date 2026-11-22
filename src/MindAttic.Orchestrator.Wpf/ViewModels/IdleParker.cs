using System.Windows.Threading;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The park trigger (M.ORCH-T2074, part of M.ORCH-T1739): parks every CLI tab whose CLI has been idle past ParkIdleCliMinutes and
/// that <see cref="ParkCheck"/> passes, through <see cref="TerminalsViewModel.Park"/>. It runs in the dashboard, which owns the
/// panes. No fixed poll: a one-shot timer is set for the soonest moment a CLI's idle time runs out (<see cref="ParkVerdict.IdleDueUtc"/>),
/// and at the latest one idle time on (a CLI busy now can't come due sooner than that once it goes idle, and a held one, by a
/// wake or a pace hold, is looked at again then). The tab on screen is never parked (it resumes only when shown, so clicking it
/// would do nothing), nor one whose CLI was just sent input, nor one whose tab follows another session than the one judged.
/// The scan runs off the UI thread; the parking on it. Never throws.
/// </summary>
internal sealed class IdleParker
{
    /// <summary>The soonest the timer comes back, so a CLI just short of its deadline doesn't spin it.</summary>
    internal static readonly TimeSpan MinWait = TimeSpan.FromSeconds(30);

    private readonly TerminalsViewModel terminals;
    private readonly DispatcherTimer timer;
    private TimeSpan idleFor;
    private bool scanning;

    /// <summary>A parker for <paramref name="terminals"/>'s tabs; off until <see cref="SetIdleFor"/> gives it an idle time.</summary>
    public IdleParker(TerminalsViewModel terminals)
    {
        this.terminals = terminals;
        timer = new DispatcherTimer(DispatcherPriority.Background);
        timer.Tick += (_, _) => { timer.Stop(); _ = ScanAsync(); };
    }

    /// <summary>The idle time from the settings (ParkIdleCliMinutes, applied at each refresh): zero or less turns parking off;
    /// a change sets the timer again from now.</summary>
    public void SetIdleFor(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        if (value == idleFor) return;
        idleFor = value;
        timer.Stop();
        if (idleFor <= TimeSpan.Zero) return;
        if (!scanning) Arm(MinWait);
    }

    private void Arm(TimeSpan wait)
    {
        timer.Stop();
        if (idleFor <= TimeSpan.Zero) return;
        timer.Interval = wait < MinWait ? MinWait : wait;
        timer.Start();
    }

    private async Task ScanAsync()
    {
        if (scanning || idleFor <= TimeSpan.Zero) return;
        scanning = true;
        var asked = idleFor;
        var next = asked;
        try
        {
            var verdicts = await Task.Run(() => ParkCheck.Scan(asked));
            var now = DateTime.UtcNow;
            foreach (var v in verdicts)
            {
                if (v.Safe) Park(v);
                else if (v.IdleDueUtc is { } due && due > now && due - now < next) next = due - now;
            }
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "The park check failed; contained, tried again in {Minutes:0} min", next.TotalMinutes);
        }
        finally
        {
            scanning = false;
            // The idle time changed during the scan: start over from its own value.
            Arm(idleFor == asked ? next : MinWait);
        }
    }

    private void Park(ParkVerdict v)
    {
        if (v.SessionId is not { Length: > 0 } session) return;
        var pane = terminals.Panes.FirstOrDefault(p => !p.Ended && !p.Parked && string.Equals(p.SessionId, session, StringComparison.OrdinalIgnoreCase)
            && (p.StatusRow?.CliPid is not { } pid || pid == v.Pid));
        // Not one of the dashboard's tabs (a CLI in Windows Terminal or elsewhere): left alone.
        if (pane is null) return;
        if (pane.IsSelected || pane.IsPresumedBusy) return;
        if (terminals.Park(pane) is { } why)
            OrchestratorLog.For("Terminals").Information("{Title:l} passed the park check but wasn't parked: {Why:l}", pane.Title, why);
    }
}
