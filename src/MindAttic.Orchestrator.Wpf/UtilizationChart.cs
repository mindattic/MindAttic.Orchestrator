using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The 5-hour window as a line: the pace line (dashed, to the target at the reset: straight, or the front-loaded curve), the target itself, the
/// window's own utilization (red while above the pace line, green below) and, dotted, where the recent rate ends up.
/// The caption under it says the same in words, so nothing is told by colour alone. It spans the dashboard's width
/// under the stat tiles and is tall (M.ORCH-T191), so it has room for a gridline every 25%, a time on every hour and
/// labels sized to read at a glance.
/// </summary>
public sealed class UtilizationChart : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data), typeof(Utilization), typeof(UtilizationChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Utilization? Data
    {
        get => (Utilization?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private const double Left = 44, Bottom = 24, Top = 10, Right = 12, FontSize = 12.5;

    protected override void OnRender(DrawingContext dc)
    {
        if (Data is not { } u || ActualWidth <= Left + Right || ActualHeight <= Top + Bottom) return;
        var text = Brush("McText", Brushes.White);
        var muted = Brush("McMuted", Brushes.Gray);
        var grid = Brush("McSeparator", Brushes.DimGray);
        var accent = Brush("McAccent", Brushes.SteelBlue);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Headroom above the highest point, so the projection's label clears its own line.
        var top = Math.Max(115, Math.Min(170, Math.Ceiling(Math.Max(u.ProjectedEndPercent, u.NowPercent) / 10) * 10 + 15));
        var (w, h) = (ActualWidth - Left - Right, ActualHeight - Top - Bottom);
        var span = (u.EndUtc - u.StartUtc).TotalSeconds;
        Point P(DateTime at, double percent) => new(
            Left + w * Math.Clamp((at - u.StartUtc).TotalSeconds / span, 0, 1),
            Top + h * (1 - Math.Clamp(percent, 0, top) / top));

        void Label(string s, Brush b, Point at, bool right = false)
        {
            var f = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, b, dpi);
            dc.DrawText(f, new Point(right ? at.X - f.Width : at.X, at.Y - f.Height / 2));
        }

        foreach (var p in new[] { 0, 25, 50, 75, 100 })
        {
            dc.DrawLine(new Pen(grid, p % 50 == 0 ? 1 : 0.5), P(u.StartUtc, p), P(u.EndUtc, p));
            Label($"{p}%", muted, new Point(Left - 6, P(u.StartUtc, p).Y), right: true);
        }

        // The window's start and reset at the ends, and a faint tick on every hour between them.
        var labelY = ActualHeight - Bottom / 2;
        Label(u.StartUtc.ToLocalTime().ToString("t"), muted, new Point(Left, labelY));
        Label(u.EndUtc.ToLocalTime().ToString("t"), muted, new Point(ActualWidth - Right, labelY), right: true);
        var hourPen = new Pen(grid, 0.5) { DashStyle = DashStyles.Dot };
        foreach (var at in HourTicks(u.StartUtc, u.EndUtc, TimeZoneInfo.Local))
        {
            var x = P(at, 0).X;
            if (x - Left < 48 || ActualWidth - Right - x < 48) continue; // leave the start and reset labels room
            dc.DrawLine(hourPen, new Point(x, Top), new Point(x, Top + h));
            var f = new FormattedText(TimeZoneInfo.ConvertTimeFromUtc(at, TimeZoneInfo.Local).ToString("t"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, muted, dpi);
            dc.DrawText(f, new Point(x - f.Width / 2, labelY - f.Height / 2));
        }

        // The target, and the pace line that reaches it exactly at the reset: straight on the linear profile, the active
        // profile's curve on the front-loaded one (M.ORCH-T234).
        dc.DrawLine(new Pen(accent, 1), P(u.StartUtc, u.TargetPercent), P(u.EndUtc, u.TargetPercent));
        Label($"{u.TargetPercent:0}% target", accent, new Point(Left + 6, P(u.StartUtc, u.TargetPercent).Y - 10));
        var pacePen = new Pen(muted, 1.5) { DashStyle = DashStyles.Dash };
        if (!u.Curved) dc.DrawLine(pacePen, P(u.StartUtc, 0), P(u.EndUtc, u.TargetPercent));
        else
        {
            const int steps = 60;
            var curve = new StreamGeometry();
            using (var g = curve.Open())
            {
                g.BeginFigure(P(u.StartUtc, 0), isFilled: false, isClosed: false);
                for (var i = 1; i <= steps; i++)
                {
                    var at = u.StartUtc + (u.EndUtc - u.StartUtc) * ((double)i / steps);
                    g.LineTo(P(at, u.PaceAt(at)), isStroked: true, isSmoothJoin: true);
                }
            }
            curve.Freeze();
            dc.DrawGeometry(null, pacePen, curve);
        }

        // The window so far, each stretch coloured by whether it ended above the pace line.
        var over = new Pen(Palette.Over, 2.5);
        var under = new Pen(Palette.Working, 2.5);
        for (var i = 1; i < u.Actual.Count; i++)
        {
            var (a, b) = (u.Actual[i - 1], u.Actual[i]);
            dc.DrawLine(b.Percent > u.PaceAt(b.AtUtc) ? over : under, P(a.AtUtc, a.Percent), P(b.AtUtc, b.Percent));
        }

        // Where the recent rate ends up by the reset.
        var now = P(u.NowUtc, u.NowPercent);
        var end = P(u.EndUtc, u.ProjectedEndPercent);
        var lands = u.ProjectedEndPercent > 100 ? Palette.Over : u.ProjectedEndPercent > u.TargetPercent ? Palette.Waiting : Palette.Working;
        dc.DrawLine(new Pen(lands, 2) { DashStyle = DashStyles.Dot }, now, end);
        dc.DrawEllipse(text, null, now, 4, 4);
        var nowText = Text($"now {u.NowPercent:0}%", text);
        var resetText = Text($"{u.ProjectedEndPercent:0}% at reset", lands);
        var (nowAt, resetAt) = PlaceEndLabels(now, new Size(nowText.Width, nowText.Height), end, new Size(resetText.Width, resetText.Height),
            new Rect(Left, Top, w, h));
        dc.DrawText(nowText, nowAt.TopLeft);
        if (resetAt is { } r) dc.DrawText(resetText, r.TopLeft);

        FormattedText Text(string s, Brush b) => new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, b, dpi);
    }

    /// <summary>
    /// Every whole hour on <paramref name="zone"/>'s clock strictly between <paramref name="startUtc"/> and
    /// <paramref name="endUtc"/>, as UTC instants. Stepped in UTC, a quarter hour at a time (every zone's offset is whole
    /// quarters): stepping the local clock a window across a clock change drew the spring-forward's missing 2:00 on top of
    /// 3:00, two labels in one place, and left out the fall-back's first 1:00 (M.ORCH-T464).
    /// </summary>
    internal static IEnumerable<DateTime> HourTicks(DateTime startUtc, DateTime endUtc, TimeZoneInfo zone)
    {
        var quarter = TimeSpan.FromMinutes(15);
        var t = new DateTime(startUtc.Ticks - startUtc.Ticks % quarter.Ticks, DateTimeKind.Utc);
        for (; t < endUtc; t += quarter)
            if (t > startUtc && TimeZoneInfo.ConvertTimeFromUtc(t, zone).Minute == 0) yield return t;
    }

    /// <summary>
    /// Where the 'now NN%' and 'NN% at reset' labels go. The projection's label sits just left of the reset, above its
    /// end; 'now' sits above and right of the dot. When now is close to the reset the two would overlap (M.ORCH-T194),
    /// so 'now' tries the dot's other sides in turn, and if none of them is clear the projection's label is dropped
    /// (the caption under the chart still says where the rate lands).
    /// </summary>
    internal static (Rect Now, Rect? Reset) PlaceEndLabels(Point now, Size nowSize, Point end, Size resetSize, Rect plot)
    {
        Rect At(double x, double y, Size s) => new(x, Math.Max(plot.Top, y - s.Height / 2), s.Width, s.Height);
        var reset = At(end.X - 4 - resetSize.Width, Math.Max(plot.Top + 8, end.Y - 12), resetSize);
        var aboveY = Math.Max(plot.Top + 8, now.Y - 18);
        var belowY = now.Y + 18;
        var candidates = new[]
        {
            At(now.X + 6, aboveY, nowSize),                  // above-right: the usual spot
            At(now.X - 6 - nowSize.Width, aboveY, nowSize),  // above-left
            At(now.X + 6, belowY, nowSize),                  // below-right
            At(now.X - 6 - nowSize.Width, belowY, nowSize),  // below-left
        };
        var bounds = new Rect(plot.Left, plot.Top - 2, plot.Right + 8 - plot.Left, plot.Height + 4); // the right gutter may hold a few pixels
        bool Fits(Rect r) => r.Left >= bounds.Left && r.Right <= bounds.Right && r.Top >= bounds.Top && r.Bottom <= bounds.Bottom;
        var gap = Rect.Inflate(reset, 3, 1);
        foreach (var c in candidates)
            if (Fits(c) && !c.IntersectsWith(gap)) return (c, reset);
        // Nowhere clear: keep 'now' (it's the fact; the projection is a guess) in the first spot that fits, and drop the other.
        return (candidates.FirstOrDefault(Fits, candidates[1]), null);
    }

    private Brush Brush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
}
