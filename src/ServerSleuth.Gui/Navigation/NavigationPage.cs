namespace ServerSleuth.Gui.Navigation;

/// <summary>
/// The closed set of top-level pages GUI-1 establishes navigation for — see ARCHITECTURE.md's
/// GUI-1 addendum. Every page is a PLACEHOLDER in this phase (skill.md GUI-1 §Objective: "prove
/// the shell/navigation architecture," not implement the workflows) — the enum exists so
/// navigation state is explicit and testable rather than a raw string/index, per GUI-1 §6's
/// "current page is explicit" requirement.
/// </summary>
public enum NavigationPage
{
    Dashboard,
    Scan,
    Results,
    Migration,
    Reports,
    Settings
}
