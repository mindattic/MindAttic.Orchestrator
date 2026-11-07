using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// A name too long for its room scrolls across it (M.ORCH-T1509, a CLI tab's name): no wider than <see cref="FrameworkElement.MaxWidth"/>,
/// it holds the start for a moment, scrolls to the end, blinks 3 times there, then starts over, forever. A name that fits sits
/// still. The child is laid out at its full width and clipped to the box. With Windows' animations off it doesn't move (the
/// tab's tooltip still has the whole name).
/// </summary>
public sealed class MarqueeBox : Decorator
{
    /// <summary>How long the start of the name is held before it scrolls.</summary>
    internal static readonly TimeSpan Hold = TimeSpan.FromSeconds(1.5);

    /// <summary>How fast it scrolls, in pixels a second.</summary>
    internal const double PixelsPerSecond = 40;

    /// <summary>How many times it blinks at the end.</summary>
    internal const int Blinks = 3;

    /// <summary>Each blink's off half and on half.</summary>
    internal static readonly TimeSpan BlinkHalf = TimeSpan.FromSeconds(0.25);

    private readonly TranslateTransform shift = new();
    private double running = -1;

    public MarqueeBox()
    {
        ClipToBounds = true;
        Unloaded += (_, _) => Stop();
        Loaded += (_, _) => InvalidateArrange();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is not { } child) return default;
        child.Measure(new Size(double.PositiveInfinity, constraint.Height));
        var want = child.DesiredSize;
        return new Size(double.IsInfinity(constraint.Width) ? want.Width : Math.Min(want.Width, constraint.Width), want.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is not { } child) return arrangeSize;
        if (!ReferenceEquals(child.RenderTransform, shift)) child.RenderTransform = shift;
        var full = Math.Max(child.DesiredSize.Width, arrangeSize.Width);
        child.Arrange(new Rect(0, 0, full, arrangeSize.Height));
        Run(full - arrangeSize.Width);
        return arrangeSize;
    }

    /// <summary>The loop for <paramref name="overflow"/> pixels hidden: (re)started when that changed, stopped when nothing is.</summary>
    private void Run(double overflow)
    {
        var moves = overflow > 1 && IsLoaded && SystemParameters.ClientAreaAnimation;
        var want = moves ? Math.Round(overflow) : 0;
        if (Math.Abs(want - running) < 0.5) return;
        Stop();
        running = want;
        if (!moves || Child is not { } child) return;
        var (scroll, blinkAt, end) = Timeline(want);

        var x = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = end };
        x.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(Hold)));
        x.KeyFrames.Add(new LinearDoubleKeyFrame(-want, KeyTime.FromTimeSpan(Hold + scroll)));
        x.KeyFrames.Add(new DiscreteDoubleKeyFrame(-want, KeyTime.FromTimeSpan(end)));

        var blink = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = end };
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        for (var i = 0; i < Blinks; i++)
        {
            var at = blinkAt + TimeSpan.FromTicks(BlinkHalf.Ticks * 2 * i);
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(at)));
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(at + BlinkHalf)));
        }
        blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(end)));

        shift.BeginAnimation(TranslateTransform.XProperty, x);
        child.BeginAnimation(OpacityProperty, blink);
    }

    /// <summary>One loop's times for <paramref name="overflow"/> pixels hidden: how long the scroll takes, when the blinks start
    /// (a short beat after it stops) and when the loop ends (a short beat after the last blink).</summary>
    internal static (TimeSpan Scroll, TimeSpan BlinkAt, TimeSpan End) Timeline(double overflow)
    {
        var scroll = TimeSpan.FromSeconds(Math.Max(0.5, overflow / PixelsPerSecond));
        var blinkAt = Hold + scroll + BlinkHalf;
        var end = blinkAt + TimeSpan.FromTicks(BlinkHalf.Ticks * 2 * Blinks) + BlinkHalf;
        return (scroll, blinkAt, end);
    }

    private void Stop()
    {
        running = -1;
        shift.BeginAnimation(TranslateTransform.XProperty, null);
        shift.X = 0;
        Child?.BeginAnimation(OpacityProperty, null);
    }
}
