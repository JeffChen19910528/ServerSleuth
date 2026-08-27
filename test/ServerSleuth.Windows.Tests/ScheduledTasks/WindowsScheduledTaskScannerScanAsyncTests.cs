using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.ScheduledTasks;

namespace ServerSleuth.Windows.Tests.ScheduledTasks;

internal sealed class FakeTaskSchedulerProvider(TaskSchedulerProbeResult result) : ITaskSchedulerProvider
{
    public TaskSchedulerProbeResult GetSnapshot() => result;
}

public class WindowsScheduledTaskScannerScanAsyncTests
{
    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None };

    private static WindowsScheduledTaskScanner MakeScanner(TaskSchedulerProbeResult result) =>
        new(new FakeTaskSchedulerProvider(result), new FileSystemReader(), new SecretRedactor(), NullLogger<WindowsScheduledTaskScanner>.Instance);

    private static ScheduledTaskRow MakeTask(string path) => new()
    {
        Path = path,
        Name = path[(path.LastIndexOf('\\') + 1)..],
        Enabled = true,
        State = "Ready"
    };

    [Fact]
    public async Task ScanAsync_AccessDenied_ReturnsAccessDeniedNotFailed()
    {
        var scanner = MakeScanner(TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.AccessDenied, "denied"));

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.AccessDenied, result.Status);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_Failed_ReturnsFailedWithError()
    {
        var scanner = MakeScanner(TaskSchedulerProbeResult.Failure(TaskSchedulerAvailability.Failed, "boom"));

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.Status);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_AvailableNoPartialFailures_ReturnsSupported()
    {
        var scanner = MakeScanner(TaskSchedulerProbeResult.Available([MakeTask(@"\ERP\Job1"), MakeTask(@"\ERP\Job2")]));

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_PartialFailures_ReturnsPartiallySupportedKeepingGoodTasks()
    {
        var scanner = MakeScanner(TaskSchedulerProbeResult.Available([MakeTask(@"\ERP\Job1")], ["Task '\\ERP\\Broken': access denied"]));

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Entities);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_NoTasks_ReturnsSupportedWithEmptyEntities()
    {
        var scanner = MakeScanner(TaskSchedulerProbeResult.Available([]));

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Empty(result.Entities);
    }
}
