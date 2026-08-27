using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ServerSleuth.Gui.Resources;

/// <summary>
/// GUI-4 §Step19: drives the "No application findings."/"No migration issues detected."/
/// "No external dependencies detected." empty-state text — <see cref="Visibility.Visible"/>
/// only when the bound count is exactly 0, so the explicit empty-state message and the actual
/// list are never both shown (or both hidden) at once. Deliberately generic (any <c>int</c>
/// count), not one converter per section — the empty-state RULE is the same everywhere.
/// </summary>
public sealed class CountToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(CountToInverseVisibilityConverter)} is one-way only.");
}
