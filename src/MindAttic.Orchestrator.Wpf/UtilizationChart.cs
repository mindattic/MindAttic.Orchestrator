using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The 5-hour window as a line: the pace line (dashed, straight to the target at the reset), the target itself, the
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
        var hour = u.StartUtc.ToLocalTime();
        hour = new DateTime(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, hour.Kind).AddHours(1);
        for (; hour.ToUniversalTime() < u.EndUtc; hour = hour.AddHours(1))
        {
            var at = hour.ToUniversalTime();
            var x = P(at, 0).X;
            if (x - Left < 48 || ActualWidth - Right - x < 48) continue; // leave the start and reset labels room
            dc.DrawLine(hourPen, new Point(x, Top), new Point(x, Top + h));
            var f = new FormattedText(hour.ToString("t"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), FontSize, muted, dpi);
            dc.DrawText(f, new Point(x - f.Width / 2, labelY - f.Height / 2));
        }

        // The target, and the pace line that reaches it exactly at the reset.
        dc.DrawLine(new Pen(accent, 1), P(u.StartUtc, u.TargetPercent), P(u.EndUtc, u.TargetPercent));
        Label($"{u.TargetPercent:0}% target", accent, new Point(Left + 6, P(u.StartUtc, u.TargetPercent).Y - 10));
        dc.DrawLine(new Pen(muted, 1.5) { DashStyle = DashStyles.Dash }, P(u.StartUtc, 0), P(u.EndUtc, u.TargetPercent));

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
        Label($"now {u.NowPercent:0}%", text, new Point(now.X + 6, Math.Max(Top + 8, now.Y - 18)));
        Label($"{u.ProjectedEndPercent:0}% at reset", lands, new Point(end.X - 4, Math.Max(Top + 8, end.Y - 12)), right: true);
    }

    private Brush Brush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
}
