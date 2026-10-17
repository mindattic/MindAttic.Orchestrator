using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The 5-hour window as a line: the pace line (dashed, straight to the target at the reset), the target itself, the
/// window's own utilization (red while above the pace line, green below) and, dotted, where the recent rate ends up.
/// The caption beside it says the same in words, so nothing is told by colour alone.
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

    private const double Left = 34, Bottom = 16, Top = 4, Right = 8;

    protected override void OnRender(DrawingContext dc)
    {
        if (Data is not { } u || ActualWidth <= Left + Right || ActualHeight <= Top + Bottom) return;
        var text = Brush("McText", Brushes.White);
        var muted = Brush("McMuted", Brushes.Gray);
        var grid = Brush("McSeparator", Brushes.DimGray);
        var accent = Brush("McAccent", Brushes.SteelBlue);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var top = Math.Max(110, Math.Min(160, Math.Ceiling(Math.Max(u.ProjectedEndPercent, u.NowPercent) / 10) * 10));
        var (w, h) = (ActualWidth - Left - Right, ActualHeight - Top - Bottom);
        var span = (u.EndUtc - u.StartUtc).TotalSeconds;
        Point P(DateTime at, double percent) => new(
            Left + w * Math.Clamp((at - u.StartUtc).TotalSeconds / span, 0, 1),
            Top + h * (1 - Math.Clamp(percent, 0, top) / top));

        void Label(string s, Brush b, Point at, bool right = false)
        {
            var f = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, b, dpi);
            dc.DrawText(f, new Point(right ? at.X - f.Width : at.X, at.Y - f.Height / 2));
        }

        foreach (var p in new[] { 0, 50, 100 })
        {
            dc.DrawLine(new Pen(grid, 1), P(u.StartUtc, p), P(u.EndUtc, p));
            Label($"{p}%", muted, new Point(Left - 4, P(u.StartUtc, p).Y), right: true);
        }
        Label(u.StartUtc.ToLocalTime().ToString("t"), muted, new Point(Left, ActualHeight - Bottom / 2));
        Label(u.EndUtc.ToLocalTime().ToString("t"), muted, new Point(ActualWidth - Right, ActualHeight - Bottom / 2), right: true);

        // The target, and the pace line that reaches it exactly at the reset.
        dc.DrawLine(new Pen(accent, 1), P(u.StartUtc, u.TargetPercent), P(u.EndUtc, u.TargetPercent));
        Label($"{u.TargetPercent:0}% target", accent, new Point(Left + 4, P(u.StartUtc, u.TargetPercent).Y - 8));
        dc.DrawLine(new Pen(muted, 1) { DashStyle = DashStyles.Dash }, P(u.StartUtc, 0), P(u.EndUtc, u.TargetPercent));

        // The window so far, each stretch coloured by whether it ended above the pace line.
        var over = new Pen(Palette.Over, 2);
        var under = new Pen(Palette.Working, 2);
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
        dc.DrawEllipse(text, null, now, 3, 3);
        Label($"{u.ProjectedEndPercent:0}%", lands, new Point(end.X - 2, Math.Max(Top + 6, end.Y - 8)), right: true);
    }

    private Brush Brush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
}
