using System.Globalization;
using System.Windows.Data;
using ServerSleuth.Gui.Models;

namespace ServerSleuth.Gui.Resources;

/// <summary>Binds a <see cref="ScanOverwritePolicy"/> to a single "Overwrite existing report"
/// checkbox — <see cref="ScanOverwritePolicy.Overwrite"/> is <c>true</c>, everything else
/// (currently only <see cref="ScanOverwritePolicy.FailIfExists"/>) is <c>false</c>.</summary>
public sealed class OverwritePolicyToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ScanOverwritePolicy.Overwrite;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ScanOverwritePolicy.Overwrite : ScanOverwritePolicy.FailIfExists;
}
