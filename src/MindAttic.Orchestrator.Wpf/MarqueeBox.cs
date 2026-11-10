using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// A name too long for its room scrolls across it (M.ORCH-T1509, a CLI tab's name): set <see cref="EnabledProperty"/> True on a
/// Canvas holding one child, with the Canvas's own <see cref="FrameworkElement.MaxWidth"/> as the box's cap. A Canvas is the
/// one layout panel the library-only XAML rule already allows (LibraryOnlyXamlTests) that never shrinks or stretches a child
/// to its own bounds: it always measures the child at its own full size and arranges it there too, so the child's whole
/// natural width is what <see cref="Scroller.Resize"/> reads off <see cref="FrameworkElement.ActualWidth"/> after every
/// layout pass. The Canvas's own Width/Height are then set to the (possibly narrower) box and ClipToBounds hides the rest,
/// while a RenderTransform shifts the child inside it. No wider than the cap, it holds the start for a moment, scrolls to the
/// end, blinks 3 times there, then starts over, forever. A name that fits sits still. With Windows' animations off it doesn't
/// move (the tab's tooltip still has the whole name). This is a plain attached behaviour, not a custom element: the XAML it
/// attaches to is only a Canvas and its one TextBlock child, both the library-only rule's own primitives.
/// </summary>
public static class MarqueeBox
{
    /// <summary>How long the start of the name is held before it scrolls.</summary>
    internal static readonly TimeSpan Hold = TimeSpan.FromSeconds(1.5);

    /// <summary>How fast it scrolls, in pixels a second.</summary>
    internal const double PixelsPerSecond = 40;

    /// <summary>How many times it blinks at the end.</summary>
    internal const int Blinks = 3;

    /// <summary>Each blink's off half and on half.</summary>
    internal static readonly TimeSpan BlinkHalf = TimeSpan.FromSeconds(0.25);

    /// <summary>Set True on a Canvas with exactly one child to make it scroll when that child overflows the Canvas's own
    /// MaxWidth.</summary>
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(MarqueeBox), new PropertyMetadata(false, OnEnabledChanged));

    public static void SetEnabled(Canvas canvas, bool value) => canvas.SetValue(EnabledProperty, value);
    public static bool GetEnabled(Canvas canvas) => (bool)canvas.GetValue(EnabledProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Canvas canvas || e.NewValue is not true) return;
        // The child isn't added yet when this fires from the Canvas's own opening tag (its attributes are set before its
        // nested content), so wait for Loaded, by when the whole visual tree, children included, is there.
        if (canvas.IsLoaded) Start(canvas);
        else
        {
            // Once: the Scroller handles every later Loaded itself. Left subscribed, each reload (a tab dragged to a new place)
            // made another Scroller over the same TextBlock, their animations fighting.
            void OnLoaded(object s, RoutedEventArgs a)
            {
                canvas.Loaded -= OnLoaded;
                Start(canvas);
            }
            canvas.Loaded += OnLoaded;
        }
    }

    /// <summary>The Scroller already running on a Canvas, so a second Start never adds another.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Canvas, Scroller> Running = new();

    private static void Start(Canvas canvas)
    {
        if (canvas.Children.Count != 1 || canvas.Children[0] is not FrameworkElement child || Running.TryGetValue(canvas, out _)) return;
        Running.Add(canvas, new Scroller(canvas, child));
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

    /// <summary>One Canvas/child pair's own running state: the shift transform, and how far (if at all) it's animating.</summary>
    private sealed class Scroller
    {
        private readonly Canvas canvas;
        private readonly FrameworkElement child;
        private readonly TranslateTransform shift = new();
        private double running = -1;

        internal Scroller(Canvas canvas, FrameworkElement child)
        {
            this.canvas = canvas;
            this.child = child;
            canvas.ClipToBounds = true;
            child.RenderTransform = shift;
            canvas.Loaded += (_, _) => Resize();
            canvas.Unloaded += (_, _) => Stop();
            child.SizeChanged += (_, _) => Resize();
            Resize(); // the Loaded that created this Scroller has already fired; size now rather than wait for the next one
        }

        /// <summary>The box shrinks to the child's own width, capped at the Canvas's MaxWidth; <see cref="Run"/> is (re)started
        /// for whatever's hidden past that cap.</summary>
        private void Resize()
        {
            var cap = canvas.MaxWidth;
            var width = double.IsPositiveInfinity(cap) ? child.ActualWidth : Math.Min(child.ActualWidth, cap);
            canvas.Width = width;
            canvas.Height = child.ActualHeight;
            Run(child.ActualWidth - width);
        }

        /// <summary>The loop for <paramref name="overflow"/> pixels hidden: (re)started when that changed, stopped when nothing is.</summary>
        private void Run(double overflow)
        {
            var moves = overflow > 1 && canvas.IsLoaded && SystemParameters.ClientAreaAnimation;
            var want = moves ? Math.Round(overflow) : 0;
            if (Math.Abs(want - running) < 0.5) return;
            Stop();
            running = want;
            if (!moves) return;
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
            child.BeginAnimation(UIElement.OpacityProperty, blink);
        }

        private void Stop()
        {
            running = -1;
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.X = 0;
            child.BeginAnimation(UIElement.OpacityProperty, null);
        }
    }
}
