using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Containers;

namespace ServerSleuth.Linux.Tests.Containers;

public class LinuxContainerScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    [Fact]
    public void BuildContainerEntity_RunningContainer_MapsRunningStatusAndFields()
    {
        var row = new ContainerRow
        {
            ContainerId = "abc123",
            Name = "erp-web",
            Image = "erp/web:1.0",
            ImageId = "sha256:deadbeef",
            State = "running",
            RestartPolicy = "always"
        };

        var entity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.Equal("container:docker:abc123", entity.Id);
        Assert.Equal(EntityStatus.Running, entity.Status);
        Assert.Equal("erp/web:1.0", entity.ImageTag);
        Assert.Equal("always", entity.RestartPolicy);
    }

    [Fact]
    public void BuildContainerEntity_ExitedContainer_MapsConfiguredStatus_NeverRunning()
    {
        var row = new ContainerRow { ContainerId = "c1", State = "exited" };

        var entity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.Equal(EntityStatus.Configured, entity.Status);
    }

    [Fact]
    public void BuildContainerEntity_EnvironmentVariables_OnlyNamesPreserved_NeverValues()
    {
        var row = new ContainerRow
        {
            ContainerId = "c1",
            RawEnvironmentVariables = ["PATH=/usr/bin", "DB_PASSWORD=SuperSecret123"]
        };

        var entity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.Contains("PATH", entity.EnvironmentVariableNames);
        Assert.Contains("DB_PASSWORD", entity.EnvironmentVariableNames);
        Assert.DoesNotContain(entity.EnvironmentVariableNames, n => n.Contains("SuperSecret123"));
    }

    [Fact]
    public void BuildContainerEntity_LabelWithSecretLookingValue_IsRedactedInMetadata()
    {
        var row = new ContainerRow
        {
            ContainerId = "c1",
            RawLabels = new Dictionary<string, string> { ["db.connectionstring"] = "Password=SuperSecret123;Server=db01" }
        };

        var entity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.DoesNotContain("SuperSecret123", entity.Metadata["Label.db.connectionstring"]);
    }

    [Fact]
    public void BuildContainerEntity_EntrypointAndCommandWithSecret_AreRedacted()
    {
        var row = new ContainerRow
        {
            ContainerId = "c1",
            Entrypoint = "sh",
            Command = "start.sh --token=SuperSecretToken123"
        };

        var entity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.DoesNotContain("SuperSecretToken123", entity.Command!);
    }

    [Fact]
    public void BuildContainerEntity_TwoInvocationsSameContainerId_ProduceIdenticalDeterministicId()
    {
        var row = new ContainerRow { ContainerId = "abc123" };

        var entityA = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);
        var entityB = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.Equal(entityA.Id, entityB.Id);
    }

    [Fact]
    public void BuildContainerEntity_SameContainerIdDifferentRuntime_ProducesDistinctIds()
    {
        var row = new ContainerRow { ContainerId = "abc123" };

        var dockerEntity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);
        var podmanEntity = LinuxContainerScanner.BuildContainerEntity(row, "podman", Redactor);

        Assert.NotEqual(dockerEntity.Id, podmanEntity.Id);
    }

    [Fact]
    public void BuildContainerEntity_Mounts_AreRecordedAsMetadata()
    {
        var row = new ContainerRow
        {
            ContainerId = "c1",
            Mounts = [new MountRow { Type = "bind", Source = "/host", Destination = "/data", ReadOnly = true }]
        };

        var entity = LinuxContainerScanner.BuildContainerEntity(row, "docker", Redactor);

        Assert.Equal("bind", entity.Metadata["Mount0.Type"]);
        Assert.Equal("/data", entity.Metadata["Mount0.Destination"]);
        Assert.Equal("True", entity.Metadata["Mount0.ReadOnly"]);
    }

    [Fact]
    public void BuildImageEntity_MapsRepositoryTagAndInstalledStatus()
    {
        var row = new ImageRow { ImageId = "sha256:aaa", Repository = "erp/web", Tag = "1.0" };

        var entity = LinuxContainerScanner.BuildImageEntity(row, "docker");

        Assert.Equal("image:docker:sha256:aaa", entity.Id);
        Assert.Equal("erp/web:1.0", entity.ImageTag);
        Assert.Equal(EntityStatus.Installed, entity.Status);
    }

    [Fact]
    public void BuildVolumeEntity_LabelRedactedAndDriverRecorded()
    {
        var row = new VolumeRow { Name = "erp-data", Driver = "local", RawLabels = new Dictionary<string, string> { ["token"] = "Token=SuperSecretAbc123" } };

        var entity = LinuxContainerScanner.BuildVolumeEntity(row, "docker", Redactor);

        Assert.Equal("volume:docker:erp-data", entity.Id);
        Assert.Equal("local", entity.Metadata["Driver"]);
        Assert.DoesNotContain("SuperSecretAbc123", entity.Metadata["Label.token"]);
    }

    [Fact]
    public void BuildNetworkEntity_MapsSubnetGatewayAndAttachedContainers()
    {
        var row = new NetworkRow { NetworkId = "net1", Name = "erp-net", Subnet = "172.20.0.0/16", Gateway = "172.20.0.1", AttachedContainerNames = ["erp-web"] };

        var entity = LinuxContainerScanner.BuildNetworkEntity(row, "docker");

        Assert.Equal("network:docker:net1", entity.Id);
        Assert.Equal("172.20.0.0/16", entity.Metadata["Subnet"]);
        Assert.Equal("172.20.0.1", entity.Metadata["Gateway"]);
        Assert.Contains("erp-web", entity.Metadata["AttachedContainers"]);
    }
}
