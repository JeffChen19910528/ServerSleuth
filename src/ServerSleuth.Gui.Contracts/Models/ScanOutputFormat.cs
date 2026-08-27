namespace ServerSleuth.Gui.Models;

/// <summary>
/// GUI-2's presentation-layer mirror of <c>ServerSleuth.Cli.Options.ReportFormatOption</c> —
/// NOT a reuse of that type directly, because <c>ServerSleuth.Cli</c> is outside GUI-1's own
/// established dependency boundary (the GUI must not depend on a separate entry-point project,
/// and `ServerSleuth.Cli` itself already depends on `ServerSleuth.Reporting`, which the GUI also
/// does not reference). Same three members, same names, same meaning — a GUI-3 phase maps this
/// 1:1 onto the real enum at the composition boundary.
/// </summary>
public enum ScanOutputFormat
{
    Json,
    Html,
    Both
}
