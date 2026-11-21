using System.Globalization;
using System.Windows.Data;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The usage chart under a CLI's terminal (M.ORCH-T1887, MainWindow.xaml's TerminalUsageChart): a fixed share of the
/// terminal frame's height, so it grows and shrinks with the window rather than sitting at one pixel size, floored so a
/// short window still draws a readable line and capped so a tall one doesn't spend the terminal's rows on it.
/// </summary>
public static class TerminalChartHeight
{
    /// <summary>The chart's share of the terminal frame's height.</summary>
    public const double Share = 0.12;

    /// <summary>Never shorter than this.</summary>
    public const double MinHeight = 48;

    /// <summary>Never taller than this.</summary>
    public const double MaxHeight = 220;

    /// <summary>The chart's height for a frame <paramref name="frameHeight"/> tall.</summary>
    public static double For(double frameHeight) =>
        double.IsFinite(frameHeight) && frameHeight > 0 ? Math.Clamp(frameHeight * Share, MinHeight, MaxHeight) : MinHeight;

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
