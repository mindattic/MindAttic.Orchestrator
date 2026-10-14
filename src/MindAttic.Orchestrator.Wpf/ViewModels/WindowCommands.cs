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
