namespace ServerSleuth.Core.Enums;

/// <summary>
/// Relationship kind between two graph nodes — see skill.md §21.
/// </summary>
public enum DependencyEdgeType
{
    Hosts,
    Runs,
    Binds,
    Uses,
    DependsOn,
    Loads,
    ListensOn,
    HostedBy,
    ConfiguredBy,
    References,
    Calls,
    ConnectsTo,

    /// <summary>An entity's bounded application root contains a discovered artifact
    /// (e.g. an IIS Application's physical path contains a DLL) — weaker than DependsOn,
    /// since mere containment is not evidence of actual use. Added Phase 5A.</summary>
    Contains,

    /// <summary>A binary's PE import table names another binary, resolved to a specific
    /// discovered file in the same directory. Added Phase 5A.</summary>
    Imports,

    /// <summary>A configuration file belongs to / configures an application. Added Phase 5A.</summary>
    Configures
}
