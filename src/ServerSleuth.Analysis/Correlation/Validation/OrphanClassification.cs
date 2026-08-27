namespace ServerSleuth.Analysis.Correlation.Validation;

/// <summary>An entity with no edges at all is not automatically an error — see skill.md (Phase
/// 5D) §14. Classification tells an auditor how much attention it deserves.</summary>
public enum OrphanClassification
{
    /// <summary>Normal and common for this entity type — e.g. a COM registration with no
    /// application evidence, an installed-but-unreferenced Runtime, an unused Certificate.</summary>
    Expected,

    /// <summary>Plausible but worth a glance — e.g. a discovered Dll or Configuration file with
    /// no owner at all.</summary>
    Potential,

    /// <summary>An entity type that structurally should almost always participate in at least
    /// one relationship (a Service, Scheduled Task, IIS Site, or Application) but doesn't —
    /// the most worth an analyst's attention.</summary>
    Unresolved
}
