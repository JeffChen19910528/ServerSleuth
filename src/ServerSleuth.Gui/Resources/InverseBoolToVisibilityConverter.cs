using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ServerSleuth.Gui.Resources;

/// <summary>GUI-5 §4-5: shows a failure message only when the bound flag is <c>false</c> — used
/// for <c>LastExportResult.Success</c>/<c>ReportViewResult.Success</c> so the export/viewer error
/// text and the successful-result content are never both visible at once.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(InverseBoolToVisibilityConverter)} is one-way only.");
}
