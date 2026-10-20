using System.Windows.Threading;
using MindAttic.Components.Wpf.Mvvm;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>The header's live clock with seconds (e.g. 7:31:39 PM). It ticks on its own one-second timer, independent of
/// the data refresh, so it keeps moving while a refresh is slow, failing or slowed while minimized (M.ORCH-T193).</summary>
public sealed class ClockViewModel : ObservableObject
{
    private readonly Func<DateTime> now;
    private readonly DispatcherTimer timer;
    private string text;

    public ClockViewModel(Func<DateTime>? now = null, TimeSpan? interval = null)
    {
        this.now = now ?? (() => DateTime.Now);
        text = Format(this.now());
        // Normal priority, not Background: the refresh's own Background-priority work shouldn't hold the second hand back.
        timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = interval ?? TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick();
    }

    public string Text { get => text; private set => Set(ref text, value); }

    public bool IsRunning => timer.IsEnabled;

    /// <summary>The time as the clock shows it: the culture's long time, which carries the seconds.</summary>
    public static string Format(DateTime time) => time.ToString("T");

    public void Tick() => Text = Format(now());

    public void Start()
    {
        Tick();
        timer.Start();
    }

    public void Stop() => timer.Stop();
}
