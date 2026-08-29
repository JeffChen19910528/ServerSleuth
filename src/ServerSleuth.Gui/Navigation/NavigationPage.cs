namespace ServerSleuth.Gui.Navigation;

/// <summary>
/// The closed set of top-level pages GUI-1 establishes navigation for — see ARCHITECTURE.md's
/// GUI-1 addendum. The enum exists so navigation state is explicit and testable rather than a
/// raw string/index, per GUI-1 §6's "current page is explicit" requirement. As of GUI-7A,
/// Dashboard/Scan/Results/Inventory are real pages; Migration/Reports/Settings remain
/// placeholders (skill.md GUI-1 §Objective) until a later phase.
/// </summary>
public enum NavigationPage
{
    Dashboard,
    Scan,
    Inventory,
    Results,
    Migration,
    Reports,
    Settings
}
