using ServerSleuth.Core.Models;

namespace ServerSleuth.Gui.ViewModels.Results;

/// <summary>
/// GUI-8B: safe presentation projection of <see cref="Configuration"/> — exposes only the four
/// migration-relevant display fields so that <see cref="Configuration"/> itself (which carries a
/// <c>SecretDetected: bool</c> scanner flag) never enters the public property type graph
/// reachable from <see cref="ApplicationDetailViewModel"/>. <c>SecretDetected</c> is not a stored
/// secret — it is a detection flag — but its property NAME would trigger the name-based walk in
/// <c>ResultsDashboardSecurityBoundaryTests</c>. Projecting here keeps the GUI type graph clean
/// without modifying the architecture test.
/// </summary>
public sealed record ConfigurationComponentRow(
    string Name,
    string? Format,
    string? Status,
    string? Path);

/// <summary>
/// GUI-8B: application-centric inventory — "what exists on the current server that I need to
/// prepare, copy, install, register, configure, or verify on the new server?" Built from
/// <see cref="Core.Boundaries.ApplicationBoundary.MemberEntityIds"/> (Phase 5B) resolved against
/// the raw discovered entities in <see cref="Analysis.Orchestration.ScanPipelineResult.Discovery"/>
/// and <see cref="Analysis.Orchestration.ScanPipelineResult.ExternalDependencies"/>.
///
/// Membership is ONLY through the existing <see cref="Core.Boundaries.ApplicationBoundary.MemberEntityIds"/>
/// relationship — no secondary attribution, no fabrication, no cross-application guessing.
/// Entities that belong to no boundary never appear here; entities that belong to multiple
/// boundaries appear in all of them.
///
/// All lists are sorted Name (ordinal, case-insensitive) then Id (ordinal), independent of
/// Dictionary enumeration order — deterministic for any given <see cref="Analysis.Orchestration.ScanPipelineResult"/>.
/// </summary>
public sealed class ApplicationComponentsViewModel
{
    public ApplicationComponentsViewModel(
        IReadOnlyList<DiscoveryEntity> memberEntities,
        IReadOnlyList<ExternalDependency> externalConnections)
    {
        DllBinaries = memberEntities.OfType<Dll>()
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        Runtimes = memberEntities.OfType<Runtime>()
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        Services = memberEntities.OfType<Service>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();

        ComComponents = memberEntities.OfType<ComComponent>()
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        Configurations = memberEntities.OfType<Configuration>()
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new ConfigurationComponentRow(c.Name, c.Format, c.Status.ToString(), c.Path))
            .ToList();

        Certificates = memberEntities.OfType<Certificate>()
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        ScheduledTasks = memberEntities.OfType<ScheduledTask>()
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();

        Software = memberEntities.OfType<Software>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();

        ExternalConnections = externalConnections
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
    }

    // ----- Typed component lists -----

    /// <summary>Native/managed DLL dependencies in this application's boundary. Must appear here
    /// as inventory items, not only as Risk Findings.</summary>
    public IReadOnlyList<Dll> DllBinaries { get; }

    /// <summary>Runtime environments (e.g., .NET, Java, Node) required by this application.</summary>
    public IReadOnlyList<Runtime> Runtimes { get; }

    /// <summary>Windows Services or Linux systemd units attributed to this application.</summary>
    public IReadOnlyList<Service> Services { get; }

    /// <summary>COM/ActiveX components attributed to this application.</summary>
    public IReadOnlyList<ComComponent> ComComponents { get; }

    /// <summary>Discovered configuration files attributed to this application, projected into
    /// <see cref="ConfigurationComponentRow"/> so <see cref="Configuration"/> (which has
    /// <c>SecretDetected: bool</c>) stays out of the reachable GUI type graph. Status field
    /// distinguishes Installed / AccessDenied — do not interpret AccessDenied as missing.</summary>
    public IReadOnlyList<ConfigurationComponentRow> Configurations { get; }

    /// <summary>X.509 certificates attributed to this application. Never carries private keys.</summary>
    public IReadOnlyList<Certificate> Certificates { get; }

    /// <summary>Scheduled tasks attributed to this application.</summary>
    public IReadOnlyList<ScheduledTask> ScheduledTasks { get; }

    /// <summary>Installed software entries attributed to this application.</summary>
    public IReadOnlyList<Software> Software { get; }

    /// <summary>External connections (databases, APIs, file shares, etc.) whose entity IDs
    /// appear in the application boundary's MemberEntityIds.</summary>
    public IReadOnlyList<ExternalDependency> ExternalConnections { get; }

    // ----- Visibility helpers — true only when there are actual discovered members -----

    public bool HasDllBinaries => DllBinaries.Count > 0;
    public bool HasRuntimes => Runtimes.Count > 0;
    public bool HasServices => Services.Count > 0;
    public bool HasComComponents => ComComponents.Count > 0;
    public bool HasConfigurations => Configurations.Count > 0;
    public bool HasCertificates => Certificates.Count > 0;
    public bool HasScheduledTasks => ScheduledTasks.Count > 0;
    public bool HasSoftware => Software.Count > 0;
    public bool HasExternalConnections => ExternalConnections.Count > 0;

    /// <summary>True when at least one component of any type is present — used to show/hide the
    /// entire Application Components section.</summary>
    public bool HasAnyComponents =>
        DllBinaries.Count > 0 || Runtimes.Count > 0 || Services.Count > 0 ||
        ComComponents.Count > 0 || Configurations.Count > 0 || Certificates.Count > 0 ||
        ScheduledTasks.Count > 0 || Software.Count > 0 || ExternalConnections.Count > 0;

    // ----- Migration Preparation counts -----

    public int DllBinaryCount => DllBinaries.Count;
    public int RuntimeCount => Runtimes.Count;
    public int ServiceCount => Services.Count;
    public int ComComponentCount => ComComponents.Count;
    public int ConfigurationCount => Configurations.Count;
    public int CertificateCount => Certificates.Count;
    public int ScheduledTaskCount => ScheduledTasks.Count;
    public int SoftwareCount => Software.Count;
    public int ExternalConnectionCount => ExternalConnections.Count;
}
