using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Containers;

namespace ServerSleuth.Linux.Tests.Containers;

public class LinuxContainerScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };

    private sealed class FakeProvider(string name, ContainerRuntimeSnapshot snapshot) : IContainerRuntimeProvider
    {
        public string RuntimeName => name;
        public Task<ContainerRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private static LinuxContainerScanner Scanner(params IContainerRuntimeProvider[] providers) =>
        new(providers, new SecretRedactor(), NullLogger<LinuxContainerScanner>.Instance);

    [Fact]
    public async Task ScanAsync_DockerOnly_ReturnsSupported()
    {
        var docker = new FakeProvider("docker", new ContainerRuntimeSnapshot
        {
            Status = ContainerRuntimeAvailability.Supported,
            Containers = [new ContainerRow { ContainerId = "c1", State = "running" }]
        });
        var podman = new FakeProvider("podman", new ContainerRuntimeSnapshot { Status = ContainerRuntimeAvailability.NotInstalled });

        var result = await Scanner(docker, podman).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        Assert.Single(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_BothRuntimesPresent_AggregatesBoth()
    {
        var docker = new FakeProvider("docker", new ContainerRuntimeSnapshot
        {
            Status = ContainerRuntimeAvailability.Supported,
            Containers = [new ContainerRow { ContainerId = "c1" }]
        });
        var podman = new FakeProvider("podman", new ContainerRuntimeSnapshot
        {
            Status = ContainerRuntimeAvailability.Supported,
            Containers = [new ContainerRow { ContainerId = "c2" }]
        });

        var result = await Scanner(docker, podman).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(2, result.Entities.Count);
    }

    [Fact]
    public async Task ScanAsync_NeitherRuntimeInstalled_ReturnsNotInstalled()
    {
        var docker = new FakeProvider("docker", new ContainerRuntimeSnapshot { Status = ContainerRuntimeAvailability.NotInstalled });
        var podman = new FakeProvider("podman", new ContainerRuntimeSnapshot { Status = ContainerRuntimeAvailability.NotInstalled });

        var result = await Scanner(docker, podman).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_DockerAccessDenied_ReturnsPartiallySupported_NeverCrashes()
    {
        var docker = new FakeProvider("docker", new ContainerRuntimeSnapshot { Status = ContainerRuntimeAvailability.AccessDenied, ErrorMessage = "denied" });

        var result = await Scanner(docker).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_ImagesVolumesAndNetworks_AllProduceEntities()
    {
        var docker = new FakeProvider("docker", new ContainerRuntimeSnapshot
        {
            Status = ContainerRuntimeAvailability.Supported,
            Images = [new ImageRow { ImageId = "sha256:aaa", Repository = "erp/web", Tag = "1.0" }],
            Volumes = [new VolumeRow { Name = "erp-data" }],
            Networks = [new NetworkRow { NetworkId = "net1", Name = "erp-net" }]
        });

        var result = await Scanner(docker).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(3, result.Entities.Count);
        Assert.Contains(result.Entities, e => e.Type == "Image");
        Assert.Contains(result.Entities, e => e.Type == "Volume");
        Assert.Contains(result.Entities, e => e.Type == "Network");
    }
}
