namespace ServerSleuth.Analysis.Migration.Preparation;

/// <summary>
/// GUI-9B, relocated to Analysis by GUI-10 so both <c>ServerSleuth.Reporting</c> (JSON/HTML) and
/// <c>ServerSleuth.Gui</c> (the WPF ViewModels, which may never reference
/// <c>ServerSleuth.Reporting</c> — see <c>NoDirectPlatformAccessTests</c>) can consume the exact
/// same category → intent vocabulary without either side duplicating it: a closed,
/// inventory-derived vocabulary describing what a developer must prepare on the destination
/// server for a discovered item — see <see cref="MigrationIntentCatalog"/> for the category →
/// intent mapping. Deliberately separate from
/// <see cref="ServerSleuth.Analysis.Migration.Actions.MigrationActionType"/>, which is
/// Issue/Dependency-derived (an entity with zero Risk findings never produces a
/// <c>MigrationAction</c>) and therefore cannot answer "what must happen to every discovered
/// item" — only "what must happen to problematic items." These are descriptive labels only:
/// nothing in this project ever executes an Install/Create/Configure/Register/Deploy/Bind
/// action — see skill.md GUI-9B §3, §14 and GUI-10 §4, §11, §20.
/// </summary>
public enum MigrationIntent
{
    Deploy,
    Install,
    Create,
    Register,
    Configure,
    Verify,
    Review
}
