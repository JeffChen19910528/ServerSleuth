using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Containers;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Containers;

/// <summary>Exercises both `DockerContainerRuntimeProvider` and `PodmanContainerRuntimeProvider`
/// via a fake `IProcessRunner` — both share the same underlying implementation
/// (`ContainerCliRuntimeProvider`), so these tests double as coverage for that shared logic.</summary>
public class ContainerRuntimeProviderTests
{
    private const string ContainerInspectJson = """
        [
          {
            "Id": "abc123",
            "Created": "2024-01-15T10:00:00.000000000Z",
            "Name": "/erp-web",
            "Image": "sha256:deadbeef",
            "State": { "Status": "running", "Pid": 4242 },
            "Config": {
              "Image": "erp/web:1.0",
              "Entrypoint": ["nginx"],
              "Cmd": ["-g", "daemon off;"],
              "Env": ["PATH=/usr/bin", "DB_PASSWORD=SuperSecret123"],
              "Labels": { "maintainer": "ops@example.com" }
            },
            "HostConfig": { "RestartPolicy": { "Name": "always" } },
            "Mounts": [ { "Type": "bind", "Source": "/host/data", "Destination": "/data", "RW": true, "Propagation": "rprivate" } ],
            "NetworkSettings": {
              "Networks": { "bridge": {} },
              "Ports": { "8080/tcp": [ { "HostIp": "0.0.0.0", "HostPort": "80" } ], "443/tcp": null }
            }
          }
        ]
        """;

    private static FakeProcessRunner NewRunnerWithPsAndInspect(string executable, string psOutput, string inspectOutput)
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(executable, ["ps", "-aq", "--no-trunc"], ProcessResult.Ok(0, psOutput, "", TimeSpan.Zero));
        if (psOutput.Trim().Length > 0)
        {
            var ids = psOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            runner.SetResult(executable, ["inspect", .. ids], ProcessResult.Ok(0, inspectOutput, "", TimeSpan.Zero));
        }

        runner.SetResult(executable, ["images", "--no-trunc", "--format", "{{json .}}"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));
        runner.SetResult(executable, ["volume", "ls", "--format", "{{json .}}"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));
        runner.SetResult(executable, ["network", "ls", "-q", "--no-trunc"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));

        return runner;
    }

    [Fact]
    public async Task Docker_TypicalContainer_ParsesFullyIncludingEnvLabelsMountsPorts()
    {
        var runner = NewRunnerWithPsAndInspect("docker", "abc123", ContainerInspectJson);

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.Supported, snapshot.Status);
        var container = Assert.Single(snapshot.Containers);
        Assert.Equal("erp-web", container.Name);
        Assert.Equal("erp/web:1.0", container.Image);
        Assert.Equal("running", container.State);
        Assert.Equal(4242, container.Pid);
        Assert.Equal("always", container.RestartPolicy);
        Assert.Contains("DB_PASSWORD=SuperSecret123", container.RawEnvironmentVariables);
        Assert.Equal("ops@example.com", container.RawLabels["maintainer"]);
        var mount = Assert.Single(container.Mounts);
        Assert.Equal("bind", mount.Type);
        Assert.Equal("/data", mount.Destination);
        Assert.True(mount.ReadOnly == false);
        Assert.Equal(2, container.Ports.Count);
        Assert.Contains(container.Ports, p => p.ContainerPort == 8080 && p.HostPort == 80);
        Assert.Contains(container.Ports, p => p.ContainerPort == 443 && p.HostPort == null); // exposed, not published
    }

    [Fact]
    public async Task Podman_SameShape_ParsesIdenticallyToDocker()
    {
        var runner = NewRunnerWithPsAndInspect("podman", "abc123", ContainerInspectJson);

        var snapshot = await new PodmanContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.Supported, snapshot.Status);
        Assert.Single(snapshot.Containers);
    }

    [Fact]
    public async Task Docker_NotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner(); // "docker" never registered -> StartFailedResult

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.NotInstalled, snapshot.Status);
    }

    [Fact]
    public async Task Docker_NotInstalled_RealProcessRunnerNotFoundStatus_StillReturnsNotInstalled()
    {
        // The real ProcessRunner classifies a missing executable as OperationStatus.NotFound,
        // not StartFailed — found via Phase 6G's real Linux (WSL Ubuntu) execution with Podman
        // genuinely absent, where this provider was misclassifying NotInstalled as Unavailable
        // because only StartFailed was checked. Pins the fix against the real runner's status.
        var runner = new FakeProcessRunner();
        runner.SetResult("docker", ["ps", "-aq", "--no-trunc"], ProcessResult.StartFailedResult(TimeSpan.Zero) with { Status = ServerSleuth.Infrastructure.Common.OperationStatus.NotFound });

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.NotInstalled, snapshot.Status);
    }

    [Fact]
    public async Task Podman_NotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner();

        var snapshot = await new PodmanContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.NotInstalled, snapshot.Status);
    }

    [Fact]
    public async Task Docker_DaemonUnreachable_ReturnsUnavailable_NotSupported()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("docker", ["ps", "-aq", "--no-trunc"],
            new ProcessResult { Status = OperationStatus.ExecutionFailed, ExitCode = 1, StandardError = "Cannot connect to the Docker daemon at unix:///var/run/docker.sock" });

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.Unavailable, snapshot.Status);
    }

    [Fact]
    public async Task Docker_SocketPermissionDenied_ReturnsAccessDenied()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("docker", ["ps", "-aq", "--no-trunc"],
            new ProcessResult { Status = OperationStatus.ExecutionFailed, ExitCode = 1, StandardError = "permission denied while trying to connect to the Docker daemon socket" });

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.AccessDenied, snapshot.Status);
    }

    [Fact]
    public async Task Podman_RootlessPermissionDenied_ReturnsAccessDenied()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("podman", ["ps", "-aq", "--no-trunc"],
            new ProcessResult { Status = OperationStatus.ExecutionFailed, ExitCode = 1, StandardError = "Permission denied" });

        var snapshot = await new PodmanContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.AccessDenied, snapshot.Status);
    }

    [Fact]
    public async Task Podman_NoContainersPresent_ReturnsSupportedWithEmptyResult()
    {
        var runner = NewRunnerWithPsAndInspect("podman", "", "");

        var snapshot = await new PodmanContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.Supported, snapshot.Status);
        Assert.Empty(snapshot.Containers);
    }

    [Fact]
    public async Task MultipleContainers_BothExitedAndRunning_AreAllDiscovered()
    {
        const string twoContainers = """
            [
              { "Id": "c1", "Name": "/one", "State": { "Status": "running" }, "Config": { "Image": "img:1" } },
              { "Id": "c2", "Name": "/two", "State": { "Status": "exited" }, "Config": { "Image": "img:2" } }
            ]
            """;
        var runner = NewRunnerWithPsAndInspect("docker", "c1\nc2", twoContainers);

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.Containers.Count);
        Assert.Contains(snapshot.Containers, c => c.State == "running");
        Assert.Contains(snapshot.Containers, c => c.State == "exited");
    }

    [Fact]
    public async Task ContainerMissingImageReference_LeavesImageFieldsNull_NeverGuesses()
    {
        const string noImage = """[ { "Id": "c1", "Name": "/one", "State": { "Status": "created" } } ]""";
        var runner = NewRunnerWithPsAndInspect("docker", "c1", noImage);

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var container = Assert.Single(snapshot.Containers);
        Assert.Null(container.Image);
        Assert.Null(container.ImageId);
    }

    [Fact]
    public async Task ImageListing_TypicalOutput_ParsesFields()
    {
        var runner = NewRunnerWithPsAndInspect("docker", "", "");
        runner.SetResult("docker", ["images", "--no-trunc", "--format", "{{json .}}"],
            ProcessResult.Ok(0, """{"ID":"sha256:aaa","Repository":"erp/web","Tag":"1.0","CreatedAt":"2024-01-15 10:00:00 +0000 UTC","Size":"142MB"}""", "", TimeSpan.Zero));

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var image = Assert.Single(snapshot.Images);
        Assert.Equal("erp/web", image.Repository);
        Assert.Equal("1.0", image.Tag);
        Assert.Equal("142MB", image.SizeDisplay);
    }

    [Fact]
    public async Task VolumeListing_LabelsStringFormat_IsParsedIntoDictionary()
    {
        var runner = NewRunnerWithPsAndInspect("docker", "", "");
        runner.SetResult("docker", ["volume", "ls", "--format", "{{json .}}"],
            ProcessResult.Ok(0, """{"Name":"erp-data","Driver":"local","Mountpoint":"/var/lib/docker/volumes/erp-data/_data","Labels":"env=prod,team=erp"}""", "", TimeSpan.Zero));

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var volume = Assert.Single(snapshot.Volumes);
        Assert.Equal("erp-data", volume.Name);
        Assert.Equal("local", volume.Driver);
        Assert.Equal("prod", volume.RawLabels["env"]);
    }

    [Fact]
    public async Task NetworkInspect_TypicalOutput_ParsesSubnetGatewayAndAttachedContainers()
    {
        var runner = NewRunnerWithPsAndInspect("docker", "", "");
        runner.SetResult("docker", ["network", "ls", "-q", "--no-trunc"], ProcessResult.Ok(0, "net1", "", TimeSpan.Zero));
        runner.SetResult("docker", ["network", "inspect", "net1"], ProcessResult.Ok(0, """
            [ { "Id": "net1", "Name": "erp-net", "Driver": "bridge",
                "IPAM": { "Config": [ { "Subnet": "172.20.0.0/16", "Gateway": "172.20.0.1" } ] },
                "Containers": { "abc123": { "Name": "erp-web" } } } ]
            """, "", TimeSpan.Zero));

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var network = Assert.Single(snapshot.Networks);
        Assert.Equal("erp-net", network.Name);
        Assert.Equal("172.20.0.0/16", network.Subnet);
        Assert.Equal("172.20.0.1", network.Gateway);
        Assert.Contains("erp-web", network.AttachedContainerNames);
    }

    [Fact]
    public async Task MalformedImageLine_IsSkipped_NeverFailsWholeScan()
    {
        var runner = NewRunnerWithPsAndInspect("docker", "", "");
        runner.SetResult("docker", ["images", "--no-trunc", "--format", "{{json .}}"],
            ProcessResult.Ok(0, "{not valid json at all", "", TimeSpan.Zero));

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.Supported, snapshot.Status);
        Assert.Empty(snapshot.Images);
    }

    [Fact]
    public async Task ContainerInspectFails_RecordsPartialFailure_ScannerStillReturnsOtherData()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("docker", ["ps", "-aq", "--no-trunc"], ProcessResult.Ok(0, "c1", "", TimeSpan.Zero));
        // "inspect" intentionally not registered -> StartFailedResult, simulating a failure
        runner.SetResult("docker", ["images", "--no-trunc", "--format", "{{json .}}"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));
        runner.SetResult("docker", ["volume", "ls", "--format", "{{json .}}"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));
        runner.SetResult("docker", ["network", "ls", "-q", "--no-trunc"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));

        var snapshot = await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(ContainerRuntimeAvailability.PartiallySupported, snapshot.Status);
        Assert.Single(snapshot.PartialFailures);
    }
}
