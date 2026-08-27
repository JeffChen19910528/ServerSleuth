using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ServerSleuth.Analysis.Risk.Models;

namespace ServerSleuth.Gui.Resources;

/// <summary>
/// Maps the EXISTING <see cref="RiskSeverity"/> domain enum (Phase 7A, <c>ServerSleuth.Analysis</c>)
/// onto the "Risk.*" brush resources declared in <c>Theme.xaml</c> — see skill.md GUI-1 §7:
/// "risk severity mapping must be based on the existing domain enum... do not invent new
/// severity values." Not yet used by any View in GUI-1 (no risk display exists yet) — this
/// exists so the visual-mapping FOUNDATION is in place and provably correct (every enum value
/// maps to a real resource, verified by <c>RiskSeverityBrushConverterTests</c>) for the GUI
/// phase that actually renders risk findings.
/// </summary>
public sealed class RiskSeverityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RiskSeverity severity)
        {
            return DependencyProperty.UnsetValue;
        }

        var resourceKey = $"Risk.{severity}";
        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(RiskSeverityBrushConverter)} is one-way only.");
}
