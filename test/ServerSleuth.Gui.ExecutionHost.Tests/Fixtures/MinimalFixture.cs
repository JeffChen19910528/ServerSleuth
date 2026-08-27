using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Gui.ExecutionHost.Tests.Fixtures;

/// <summary>A small, self-contained two-scanner fixture — deliberately NOT a copy of
/// <c>ServerSleuth.Cli.Tests.Fixtures.ErpFixture</c> (that project's own comment notes "no
/// precedent for test-to-test project references in this repo"; this fixture is small enough
/// not to need one). One scanner reports <see cref="ScannerStatus.Supported"/>, the other
/// <see cref="ScannerStatus.PartiallySupported"/> — enough to exercise partial-coverage
/// classification without a large synthetic dataset.</summary>
internal static class MinimalFixture
{
    public static AggregateDiscoveryResult Build()
    {
        var serviceA = new Service
        {
            Id = "service:alpha", Name = "Alpha", Type = "Service", Source = "ServiceControlManager",
            Status = EntityStatus.Running, Confidence = new Confidence(0.95), ExecutablePath = @"C:\Apps\Alpha\alpha.exe"
        };
        var serviceB = new Service
        {
            Id = "service:beta", Name = "Beta", Type = "Service", Source = "ServiceControlManager",
            Status = EntityStatus.Running, Confidence = new Confidence(0.95), ExecutablePath = @"C:\Apps\Beta\beta.exe"
        };

        var scannerAResult = DiscoveryResult.Success("services-scanner", [serviceA]);
        var scannerBResult = new DiscoveryResult
        {
            ScannerId = "registry-scanner",
            Status = ScannerStatus.PartiallySupported,
            Entities = [serviceB],
            Errors = [new DiscoveryError { ScannerId = "registry-scanner", Message = "Access denied to one registry key.", IsPermissionFailure = true }]
        };

        return new AggregateDiscoveryResult
        {
            Entities = [serviceA, serviceB],
            Errors = scannerBResult.Errors,
            ScannerResults = [scannerAResult, scannerBResult],
            ScannerStatuses = new Dictionary<string, ScannerStatus>
            {
                ["services-scanner"] = ScannerStatus.Supported,
                ["registry-scanner"] = ScannerStatus.PartiallySupported
            }
        };
    }
}
