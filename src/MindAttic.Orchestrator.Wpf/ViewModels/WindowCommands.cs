using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>Keyboard commands the window handles itself (they touch focus, not data).</summary>
public static class WindowCommands
{
    /// <summary>The focus search.</summary>
    public static readonly RoutedCommand FocusSearch = new(nameof(FocusSearch), typeof(WindowCommands));
    /// <summary>The escape.</summary>
    public static readonly RoutedCommand Escape = new(nameof(Escape), typeof(WindowCommands));
}

/// <summary>The converters type.</summary>
public static class Converters
{
    /// <summary>The empty to visible.</summary>
    public static readonly IValueConverter EmptyToVisible = new EmptyConverter(whenEmpty: Visibility.Visible);
    /// <summary>The empty to collapsed.</summary>
    public static readonly IValueConverter EmptyToCollapsed = new EmptyConverter(whenEmpty: Visibility.Collapsed);
    /// <summary>The false to visible.</summary>
    public static readonly IValueConverter FalseToVisible = new FalseToVisibleConverter();

    /// <summary>A length times its ConverterParameter ("0.5"): a share of another element's size, e.g. the login list's cap
    /// at half the sidebar so the ticket cards under it keep the rest (M.ORCH-T1439).</summary>
    public static readonly IValueConverter Share = new ShareConverter();

    /// <summary>AgentsBody's Grid.Row (M.ORCH-T1759): tree layout (true) starts it at row 1, under the menu bar, so it spans
    /// the whole right side; flat layout (false) keeps it at row 3, under the login strip and the CLI tab bar.</summary>
    public static readonly IValueConverter BodyRow = new BoolToIntConverter(whenTrue: 1, whenFalse: 3);

    /// <summary>AgentsBody's Grid.RowSpan, the inverse of <see cref="BodyRow"/>: tree layout spans rows 1-3 (3); flat layout
    /// spans only its own row 3 (1), unchanged from today.</summary>
    public static readonly IValueConverter BodyRowSpan = new BoolToIntConverter(whenTrue: 3, whenFalse: 1);

    private sealed class BoolToIntConverter(int whenTrue, int whenFalse) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? whenTrue : whenFalse;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>A tree node's expand/collapse chevron glyph (M.ORCH-T1759): "▾" expanded, "▸" collapsed.</summary>
    public static readonly IValueConverter Chevron = new ChevronConverter();

    private sealed class ChevronConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? "▾" : "▸";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class ShareConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is double length && double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var share) && length > 0
                ? length * share : double.PositiveInfinity;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class FalseToVisibleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is false ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyConverter(Visibility whenEmpty) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) == (whenEmpty == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
