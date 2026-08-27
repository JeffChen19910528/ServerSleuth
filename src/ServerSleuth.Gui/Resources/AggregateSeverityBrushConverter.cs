using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Gui.Resources;

/// <summary>
/// GUI-4 §Step17: the aggregate-summary counterpart of <see cref="RiskSeverityBrushConverter"/>.
/// <see cref="AggregateSeverity"/> reuses the SAME "Risk.Info"/"Risk.Low"/"Risk.Medium"/
/// "Risk.High"/"Risk.Critical" resources Theme.xaml already declares for the five
/// <see cref="RiskSeverity"/> values (they share identical names by design — see
/// <c>AggregateSeverityExtensions.ToAggregateSeverity</c>) — never a new "Risk.None" brush is
/// invented for <see cref="AggregateSeverity.None"/>; that case falls back to the existing,
/// already-neutral <c>App.MutedForeground</c> resource instead.
/// </summary>
public sealed class AggregateSeverityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AggregateSeverity severity)
        {
            return DependencyProperty.UnsetValue;
        }

        var resourceKey = severity == AggregateSeverity.None ? "App.MutedForeground" : $"Risk.{severity}";
        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(AggregateSeverityBrushConverter)} is one-way only.");
}
