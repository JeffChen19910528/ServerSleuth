using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ServerSleuth.Gui.Resources;

/// <summary>GUI-5 §2: the exact inverse of <see cref="CountToInverseVisibilityConverter"/> —
/// <see cref="Visibility.Visible"/> only when the bound count is greater than 0. Used to show the
/// Report Viewer's file picker/"Open Report" row only once there is at least one report file to
/// pick from (the "No report files were written." text already covers the zero case).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(CountToVisibilityConverter)} is one-way only.");
}
