using ServerSleuth.Core.Enums;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Kubernetes;

namespace ServerSleuth.Linux.Tests.Kubernetes;

public class LinuxKubernetesScannerBuildEntityTests
{
    private static readonly ISecretRedactor Redactor = new SecretRedactor();

    [Fact]
    public void BuildClusterEntity_MapsServerVersionContextAndIdentifier()
    {
        var row = new ClusterRow { ServerVersion = "v1.29.2", ContextName = "prod-cluster", IsCurrentContext = true, ClusterIdentifier = "uid-kube-system" };

        var entity = LinuxKubernetesScanner.BuildClusterEntity(row, "prod-cluster");

        Assert.Equal("cluster:prod-cluster", entity.Id);
        Assert.Equal("v1.29.2", entity.ServerVersion);
        Assert.Equal("uid-kube-system", entity.ClusterIdentifier);
    }

    [Fact]
    public void BuildNamespaceEntity_DeterministicIdIncludesClusterScope()
    {
        var row = new NamespaceRow { Name = "erp", Uid = "uid-1", Phase = "Active" };

        var entityA = LinuxKubernetesScanner.BuildNamespaceEntity(row, "cluster-a");
        var entityB = LinuxKubernetesScanner.BuildNamespaceEntity(row, "cluster-b");

        Assert.Equal("namespace:cluster-a:erp", entityA.Id);
        Assert.NotEqual(entityA.Id, entityB.Id);
    }

    [Fact]
    public void BuildNodeEntity_ReadyTrue_MapsRunningStatus()
    {
        var row = new NodeRow { Name = "node-1", Roles = ["control-plane"], Ready = true };

        var entity = LinuxKubernetesScanner.BuildNodeEntity(row, "cluster");

        Assert.Equal(EntityStatus.Running, entity.Status);
        Assert.Contains("control-plane", entity.Roles);
    }

    [Fact]
    public void BuildNodeEntity_ReadyFalse_MapsConfiguredStatus_NeverRunning()
    {
        var row = new NodeRow { Name = "node-1", Ready = false };

        var entity = LinuxKubernetesScanner.BuildNodeEntity(row, "cluster");

        Assert.Equal(EntityStatus.Configured, entity.Status);
    }

    [Fact]
    public void BuildPodEntity_UsesUidWhenAvailable_ForDeterministicIdentity()
    {
        var row = new PodRow { Name = "web-1", Namespace = "erp", Uid = "uid-pod-1", Phase = "Running" };

        var entityA = LinuxKubernetesScanner.BuildPodEntity(row, "cluster");
        var entityB = LinuxKubernetesScanner.BuildPodEntity(row, "cluster");

        Assert.Equal(entityA.Id, entityB.Id);
        Assert.Equal("pod:cluster:uid-pod-1", entityA.Id);
    }

    [Fact]
    public void BuildPodEntity_FallsBackToNamespaceName_WhenUidMissing()
    {
        var row = new PodRow { Name = "web-1", Namespace = "erp", Phase = "Running" };

        var entity = LinuxKubernetesScanner.BuildPodEntity(row, "cluster");

        Assert.Equal("pod:cluster:erp/web-1", entity.Id);
    }

    [Fact]
    public void BuildPodEntity_NeverProbesPodIpOrHostIp_OnlyRecordsThem()
    {
        var row = new PodRow { Name = "web-1", Namespace = "erp", PodIp = "10.0.0.5", HostIp = "192.168.1.1" };

        var entity = LinuxKubernetesScanner.BuildPodEntity(row, "cluster");

        Assert.Equal("10.0.0.5", entity.PodIp);
        Assert.Equal("192.168.1.1", entity.HostIp);
    }

    [Fact]
    public void BuildPodContainerEntities_NeverMergedWithHostContainer_UsesKubernetesContainerType()
    {
        var row = new PodRow
        {
            Name = "web-1",
            Namespace = "erp",
            Uid = "uid-pod-1",
            Containers = [new PodContainerRow { Name = "web", Image = "erp/web:1.0", State = "running", Ready = true, RestartCount = 1 }]
        };

        var containers = LinuxKubernetesScanner.BuildPodContainerEntities(row, "cluster");

        var container = Assert.Single(containers);
        Assert.Equal("KubernetesContainer", container.Type);
        Assert.Equal("podcontainer:cluster:uid-pod-1:web", container.Id);
        Assert.Equal("erp/web:1.0", container.ImageTag);
    }

    [Fact]
    public void BuildWorkloadEntity_CapturesSelectorLabelsAndTemplateImages()
    {
        var row = new WorkloadRow
        {
            Kind = "Deployment",
            Name = "erp-web",
            Namespace = "erp",
            Uid = "uid-dep-1",
            DesiredReplicas = 3,
            ReadyReplicas = 3,
            SelectorLabels = new Dictionary<string, string> { ["app"] = "erp-web" },
            TemplateContainerImages = ["erp/web:1.0"]
        };

        var entity = LinuxKubernetesScanner.BuildWorkloadEntity(row, "cluster");

        Assert.Equal("workload:cluster:Deployment:erp:uid-dep-1", entity.Id);
        Assert.Contains("app=erp-web", entity.SelectorLabels);
        Assert.Contains("erp/web:1.0", entity.TemplateContainerImages);
    }

    [Fact]
    public void BuildServiceEntity_FormatsPortsIncludingNodePort()
    {
        var row = new ServiceRow
        {
            Name = "erp-web",
            Namespace = "erp",
            ServiceType = "NodePort",
            Ports = [new ServicePortRow { Port = 80, TargetPort = "8080", NodePort = 30080, Protocol = "TCP" }]
        };

        var entity = LinuxKubernetesScanner.BuildServiceEntity(row, "cluster");

        Assert.Contains("80:8080/TCP@30080", entity.Ports);
    }

    [Fact]
    public void BuildIngressEntity_NeverContactsHosts_OnlyRecordsThem()
    {
        var row = new IngressRow { Name = "erp-ingress", Namespace = "erp", Hosts = ["erp.example.com"], TlsSecretNames = ["erp-tls"] };

        var entity = LinuxKubernetesScanner.BuildIngressEntity(row, "cluster");

        Assert.Contains("erp.example.com", entity.Hosts);
        Assert.Contains("erp-tls", entity.TlsSecretNames);
    }

    [Fact]
    public void BuildConfigMapEntity_TextValueWithSecretShape_IsRedactedInMetadata()
    {
        var row = new ConfigMapRow
        {
            Name = "erp-config",
            Namespace = "erp",
            RawTextData = new Dictionary<string, string> { ["ConnectionString"] = "Password=SuperSecret123;Server=db01" }
        };

        var entity = LinuxKubernetesScanner.BuildConfigMapEntity(row, "cluster", Redactor);

        Assert.Contains("ConnectionString", entity.Keys);
        Assert.DoesNotContain("SuperSecret123", entity.Metadata["Data.ConnectionString"]);
    }

    [Fact]
    public void BuildSecretEntity_OnlyCapturesKeyNames_NeverValues_AndNeverAppearsInMetadataOrEvidence()
    {
        var row = new SecretRow { Name = "erp-db", Namespace = "erp", SecretType = "Opaque", Keys = ["DB_PASSWORD", "DB_USER"] };

        var entity = LinuxKubernetesScanner.BuildSecretEntity(row, "cluster");

        Assert.Contains("DB_PASSWORD", entity.Keys);
        Assert.Contains("DB_USER", entity.Keys);
        Assert.Empty(entity.Metadata); // no per-key metadata at all — no opportunity for a value to leak
        Assert.All(entity.Evidence, e => Assert.DoesNotContain("Password", e.Detail ?? string.Empty));
    }

    [Fact]
    public void BuildPvcEntity_MapsCapacityAndAccessModes()
    {
        var row = new PvcRow { Name = "erp-data", Namespace = "erp", Phase = "Bound", Capacity = "10Gi", AccessModes = ["ReadWriteOnce"] };

        var entity = LinuxKubernetesScanner.BuildPvcEntity(row, "cluster");

        Assert.Equal("10Gi", entity.Capacity);
        Assert.Contains("ReadWriteOnce", entity.AccessModes);
    }

    [Fact]
    public void BuildPvEntity_HostPathSource_RecordsPathValue_WithoutAnyFilesystemAccess()
    {
        var row = new PvRow { Name = "pv-1", VolumeSourceType = "HostPath", HostPath = "/data/erp" };

        var entity = LinuxKubernetesScanner.BuildPvEntity(row, "cluster");

        Assert.Equal("HostPath", entity.VolumeSourceType);
        Assert.Equal("/data/erp", entity.HostPath);
    }

    [Fact]
    public void BuildPvEntity_DeterministicIdentity_SameNameProducesSameId()
    {
        var row = new PvRow { Name = "pv-1" };

        var entityA = LinuxKubernetesScanner.BuildPvEntity(row, "cluster");
        var entityB = LinuxKubernetesScanner.BuildPvEntity(row, "cluster");

        Assert.Equal(entityA.Id, entityB.Id);
    }
}
