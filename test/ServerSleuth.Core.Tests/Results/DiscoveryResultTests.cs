using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;

namespace ServerSleuth.Core.Tests.Results;

public class DiscoveryResultTests
{
    [Fact]
    public void Success_ProducesSupportedStatusWithNoErrors()
    {
        var entity = new Software { Id = "s1", Name = "Test", Type = "Software", Source = "Registry" };

        var result = DiscoveryResult.Success("windows-software-scanner", [entity]);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Single(result.Entities);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_ProducesFailedStatusWithErrorAndNoEntities()
    {
        var error = new DiscoveryError
        {
            ScannerId = "windows-com-scanner",
            Message = "Access to registry key denied.",
            IsPermissionFailure = true
        };

        var result = DiscoveryResult.Failure("windows-com-scanner", error);

        Assert.Equal(ScannerStatus.Failed, result.Status);
        Assert.Empty(result.Entities);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public void PartiallySupported_CanCarryBothEntitiesAndErrors()
    {
        var entity = new Service { Id = "svc-1", Name = "Spooler", Type = "Service", Source = "ServiceControlManager" };
        var error = new DiscoveryError { ScannerId = "windows-service-scanner", Message = "One service query timed out." };

        var result = new DiscoveryResult
        {
            ScannerId = "windows-service-scanner",
            Status = ScannerStatus.PartiallySupported,
            Entities = [entity],
            Errors = [error]
        };

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
        Assert.Single(result.Errors);
    }
}
