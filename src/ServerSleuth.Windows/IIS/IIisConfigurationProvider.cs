namespace ServerSleuth.Windows.IIS;

/// <summary>
/// Reads IIS's live configuration (sites, applications, bindings, application pools). The
/// Mapper (IisScanner.BuildEntities) and its tests depend only on this interface and the raw
/// Iis*Row DTOs — never on Microsoft.Web.Administration types directly — so scanner logic is
/// unit-testable without IIS installed. See skill.md's Phase 4A "IIS Provider → Mapper →
/// Domain Entity" testability requirement.
/// </summary>
public interface IIisConfigurationProvider
{
    IisProbeResult GetSnapshot();
}
