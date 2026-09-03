namespace ServerSleuth.Reporting.Html;

/// <summary>
/// Where a discovered entity sits on the "what did IT actually deploy here?" spectrum —
/// used only to decide what the Server Deployment Inventory report shows by default. Never
/// serialized, never affects Risk/Migration analysis; purely a rendering-time classification
/// computed from path, publisher, and application-boundary membership signals already present
/// on the mapped inventory DTOs.
/// </summary>
internal enum DeploymentClassification
{
    /// <summary>Windows/OS built-in component — hidden from the report by default.</summary>
    System,

    /// <summary>Externally published vendor software/service not tied to any of the server's
    /// own deployed applications (e.g. 7-Zip, WinSCP, Trend Micro, Microsoft SQL Server).</summary>
    ThirdParty,

    /// <summary>Part of a deployed business application, installed under a standard vendor-style
    /// layout (e.g. Program Files).</summary>
    Business,

    /// <summary>Part of a deployed business application, installed under a server-specific,
    /// non-standard path — the strongest signal of an in-house/self-authored component.</summary>
    Custom,

    /// <summary>No signal was strong enough to classify reliably. Never guessed into System.</summary>
    Unknown
}
