using System.Globalization;
using System.Windows.Data;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The usage chart under a CLI's terminal (M.ORCH-T1887, MainWindow.xaml's TerminalUsageChart): a fixed share of the
/// terminal frame's height, so it grows and shrinks with the window rather than sitting at one pixel size, floored so a
/// short window still draws a readable line and capped so a tall one doesn't spend the terminal's rows on it. The floor
/// gives way in a very short frame (the phone layout, M.ORCH-T1543, at the terminal's own 160px floor): the chart never
/// takes more than <see cref="MaxShare"/> of the frame, so the terminal always keeps most of it. The chart draws Compact,
/// whose margins, line and one label all follow its own size, so any width the window has (desk or phone) renders whole.
/// </summary>
public static class TerminalChartHeight
{
    /// <summary>The chart's share of the terminal frame's height.</summary>
    public const double Share = 0.12;

    /// <summary>The floor, in pixels: never shorter than this unless that's over <see cref="MaxShare"/> of the frame.</summary>
    public const double MinHeight = 48;

    /// <summary>Never more than this share of the frame, even when that's under <see cref="MinHeight"/>.</summary>
    public const double MaxShare = 0.25;

    /// <summary>The cap, in pixels: never taller than this however tall the frame.</summary>
    public const double MaxHeight = 220;

    /// <summary>The chart's height for a frame <paramref name="frameHeight"/> tall.</summary>
    public static double For(double frameHeight) =>
        double.IsFinite(frameHeight) && frameHeight > 0
            ? Math.Clamp(frameHeight * Share, Math.Min(MinHeight, frameHeight * MaxShare), MaxHeight)
            : 0;

    /// <summary>Binds the frame's ActualHeight to the chart's Height.</summary>
    public static IValueConverter Converter { get; } = new HeightConverter();

    private sealed class HeightConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            For(value is double h ? h : 0);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
