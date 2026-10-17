using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// A dashed ring that turns on its own UI thread (M.ORCH-T200). WPF ticks animations on the thread that owns them, so a
/// spinner on the window's thread freezes exactly when it matters: while the first refresh applies the snapshot and the
/// dashboard lays itself out behind the loader. This one draws into a <see cref="HostVisual"/> whose content lives on a
/// private dispatcher thread, which the render thread composes no matter how busy the window's thread is. The thread
/// runs only while the spinner is visible and shuts down when it's hidden.
/// </summary>
public sealed class ThreadedSpinner : FrameworkElement
{
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(ThreadedSpinner), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(ThreadedSpinner), new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PeriodProperty = DependencyProperty.Register(
        nameof(Period), typeof(Duration), typeof(ThreadedSpinner), new FrameworkPropertyMetadata(new Duration(TimeSpan.FromSeconds(1.2))));

    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }

    /// <summary>One full turn.</summary>
    public Duration Period { get => (Duration)GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    private HostVisual? host;
    private Dispatcher? worker;
    private RotateTransform? turn; // created on, and only touched from, the worker thread

    public ThreadedSpinner()
    {
        IsVisibleChanged += (_, _) => { if (IsVisible) Start(); else Stop(); };
        Unloaded += (_, _) => Stop();
        Loaded += (_, _) => { if (IsVisible) Start(); };
    }

    /// <summary>Whether the ring's own thread is up; for tests and diagnostics.</summary>
    internal bool IsSpinning => worker is not null;

    /// <summary>The thread the ring turns on, null while hidden; for tests.</summary>
    internal Thread? RingThread => worker?.Thread;

    /// <summary>The ring's current angle, read on its own thread; null while hidden. For tests.</summary>
    internal double? Angle => worker?.Invoke(() => turn?.Angle);

    protected override int VisualChildrenCount => host is null ? 0 : 1;
    protected override Visual GetVisualChild(int index) => host ?? throw new ArgumentOutOfRangeException(nameof(index));

    private void Start()
    {
        if (worker is not null || !IsLoaded) return;
        double size = Math.Min(Width, Height), thickness = StrokeThickness;
        if (double.IsNaN(size) || size <= 0) size = 44;
        var stroke = Stroke.CloneCurrentValue();
        stroke.Freeze(); // crosses to the worker thread
        var period = Period;

        host = new HostVisual();
        AddVisualChild(host);
        var target = host;
        using var ready = new ManualResetEventSlim();
        Dispatcher? started = null;
        var thread = new Thread(() =>
        {
            started = Dispatcher.CurrentDispatcher;
            var source = new VisualTarget(target);
            source.RootVisual = Ring(size, thickness, stroke, period, out turn);
            ready.Set();
            Dispatcher.Run();
            source.Dispose();
        }) { IsBackground = true, Name = "Loader spinner" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        worker = started;
    }

    private void Stop()
    {
        worker?.BeginInvokeShutdown(DispatcherPriority.Send);
        worker = null;
        turn = null;
        if (host is not null) RemoveVisualChild(host);
        host = null;
    }

    /// <summary>The ring itself, built and animated on the worker thread: a dashed circle turned about its centre.</summary>
    private static DrawingVisual Ring(double size, double thickness, Brush stroke, Duration period, out RotateTransform turn)
    {
        var pen = new Pen(stroke, thickness) { DashStyle = new DashStyle([6, 4], 0), DashCap = PenLineCap.Round };
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var r = (size - thickness) / 2;
            dc.DrawEllipse(null, pen, new Point(size / 2, size / 2), r, r);
        }
        turn = new RotateTransform(0, size / 2, size / 2);
        visual.Transform = turn;
        turn.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, period) { RepeatBehavior = RepeatBehavior.Forever });
        return visual;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsNaN(Width) ? 44 : Width, h = double.IsNaN(Height) ? 44 : Height;
        return new Size(w, h);
    }
}
