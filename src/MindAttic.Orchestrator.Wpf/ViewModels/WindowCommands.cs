using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>Keyboard commands the window handles itself (they touch focus, not data).</summary>
public static class WindowCommands
{
    public static readonly RoutedCommand FocusSearch = new(nameof(FocusSearch), typeof(WindowCommands));
    public static readonly RoutedCommand Escape = new(nameof(Escape), typeof(WindowCommands));
}

public static class Converters
{
    public static readonly IValueConverter EmptyToVisible = new EmptyConverter(whenEmpty: Visibility.Visible);
    public static readonly IValueConverter EmptyToCollapsed = new EmptyConverter(whenEmpty: Visibility.Collapsed);
    public static readonly IValueConverter FalseToVisible = new FalseToVisibleConverter();

    /// <summary>A length times its ConverterParameter ("0.5"): a share of another element's size, e.g. the login list's cap
    /// at half the sidebar so the ticket cards under it keep the rest (M.ORCH-T1439).</summary>
    public static readonly IValueConverter Share = new ShareConverter();

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
