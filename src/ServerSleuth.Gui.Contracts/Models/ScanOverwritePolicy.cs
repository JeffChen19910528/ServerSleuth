namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2's presentation-layer mirror of <c>ServerSleuth.Reporting.Export.ReportOverwritePolicy</c>
/// — see <see cref="ScanOutputFormat"/>'s doc comment for why a mirror, not a direct reference,
/// is the correct choice given GUI-1's dependency boundary (<c>ServerSleuth.Reporting</c> is not
/// referenced by <c>ServerSleuth.Gui</c>).
/// </summary>
public enum ScanOverwritePolicy
{
    FailIfExists,
    Overwrite
}
