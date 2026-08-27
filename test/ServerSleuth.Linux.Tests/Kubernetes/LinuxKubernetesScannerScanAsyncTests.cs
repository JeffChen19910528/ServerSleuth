using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Kubernetes;

namespace ServerSleuth.Linux.Tests.Kubernetes;

public class LinuxKubernetesScannerScanAsyncTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None };

    private sealed class FakeProvider(KubernetesSnapshot snapshot) : IKubernetesProvider
    {
        public Task<KubernetesSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private static LinuxKubernetesScanner Scanner(KubernetesSnapshot snapshot) =>
        new(new FakeProvider(snapshot), new SecretRedactor(), NullLogger<LinuxKubernetesScanner>.Instance);

    [Fact]
    public async Task ScanAsync_KubectlNotInstalled_ReturnsNotInstalled_NoErrors()
    {
        var result = await Scanner(new KubernetesSnapshot { Status = KubernetesAvailability.NotInstalled }).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.NotInstalled, result.Status);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Entities);
    }

    [Fact]
    public async Task ScanAsync_AccessDenied_ReturnsAccessDenied_WithPermissionError()
    {
        var result = await Scanner(new KubernetesSnapshot { Status = KubernetesAvailability.AccessDenied, ErrorMessage = "Unauthorized" })
            .ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.AccessDenied, result.Status);
        Assert.Single(result.Errors);
        Assert.True(result.Errors[0].IsPermissionFailure);
    }

    [Fact]
    public async Task ScanAsync_ClusterUnreachable_ReturnsFailed_NeverThrows()
    {
        var result = await Scanner(new KubernetesSnapshot { Status = KubernetesAvailability.Unavailable, ErrorMessage = "connection refused" })
            .ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.Status);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ScanAsync_Supported_AggregatesEveryResourceKindIntoEntities()
    {
        var snapshot = new KubernetesSnapshot
        {
            Status = KubernetesAvailability.Supported,
            Cluster = new ClusterRow { ContextName = "prod", ServerVersion = "v1.29.2" },
            Namespaces = [new NamespaceRow { Name = "erp" }],
            Nodes = [new NodeRow { Name = "node-1" }],
            Pods = [new PodRow { Name = "web-1", Namespace = "erp", Containers = [new PodContainerRow { Name = "web" }] }],
            Workloads = [new WorkloadRow { Kind = "Deployment", Name = "erp-web", Namespace = "erp" }],
            Services = [new ServiceRow { Name = "erp-web", Namespace = "erp" }],
            Ingresses = [new IngressRow { Name = "erp-ingress", Namespace = "erp" }],
            ConfigMaps = [new ConfigMapRow { Name = "erp-config", Namespace = "erp" }],
            Secrets = [new SecretRow { Name = "erp-db", Namespace = "erp" }],
            Pvcs = [new PvcRow { Name = "erp-data", Namespace = "erp" }],
            Pvs = [new PvRow { Name = "pv-1" }]
        };

        var result = await Scanner(snapshot).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status);
        // cluster + namespace + node + pod + podcontainer + workload + service + ingress + configmap + secret + pvc + pv = 12
        Assert.Equal(12, result.Entities.Count);
        Assert.Contains(result.Entities, e => e.Type == "Cluster");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesNamespace");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesNode");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesPod");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesContainer");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesWorkload");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesService");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesIngress");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesConfigMap");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesSecret");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesPersistentVolumeClaim");
        Assert.Contains(result.Entities, e => e.Type == "KubernetesPersistentVolume");
    }

    [Fact]
    public async Task ScanAsync_PartiallySupported_ReturnsPartiallySupported_WithPartialFailureErrors()
    {
        var snapshot = new KubernetesSnapshot
        {
            Status = KubernetesAvailability.PartiallySupported,
            Cluster = new ClusterRow { ContextName = "prod" },
            Namespaces = [new NamespaceRow { Name = "erp" }],
            PartialFailures = ["pods: Timeout"]
        };

        var result = await Scanner(snapshot).ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.PartiallySupported, result.Status);
        Assert.Single(result.Errors);
        Assert.Equal(2, result.Entities.Count); // cluster + namespace still returned
    }

    [Fact]
    public async Task ScanAsync_ProviderThrows_NeverPropagates_DegradesToFailed()
    {
        var throwingProvider = new ThrowingProvider();
        var scanner = new LinuxKubernetesScanner(throwingProvider, new SecretRedactor(), NullLogger<LinuxKubernetesScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.Equal(ScannerStatus.Failed, result.Status);
    }

    private sealed class ThrowingProvider : IKubernetesProvider
    {
        public Task<KubernetesSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }
}
