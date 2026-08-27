using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Kubernetes;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Kubernetes;

public class KubectlKubernetesProviderTests
{
    private const string VersionOk = """{"serverVersion":{"gitVersion":"v1.29.2"}}""";
    private static readonly string[] EmptyResourceArgs =
        ["namespaces", "nodes", "pods --all-namespaces", "deployments --all-namespaces", "statefulsets --all-namespaces",
         "daemonsets --all-namespaces", "services --all-namespaces", "ingress --all-namespaces", "configmaps --all-namespaces",
         "secrets --all-namespaces", "pvc --all-namespaces", "pv"];

    private static FakeProcessRunner RunnerWithEverythingEmpty()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("kubectl", ["version", "-o", "json"], ProcessResult.Ok(0, VersionOk, "", TimeSpan.Zero));
        runner.SetResult("kubectl", ["config", "current-context"], ProcessResult.Ok(0, "my-context\n", "", TimeSpan.Zero));
        foreach (var argsLabel in EmptyResourceArgs)
        {
            var args = argsLabel.Split(' ').Concat(["-o", "json"]).ToArray();
            runner.SetResult("kubectl", ["get", .. args], ProcessResult.Ok(0, """{"items":[]}""", "", TimeSpan.Zero));
        }
        return runner;
    }

    private static void SetResource(FakeProcessRunner runner, string resource, bool allNamespaces, string json) =>
        runner.SetResult("kubectl", allNamespaces ? ["get", resource, "--all-namespaces", "-o", "json"] : ["get", resource, "-o", "json"],
            ProcessResult.Ok(0, json, "", TimeSpan.Zero));

    [Fact]
    public async Task GetSnapshotAsync_KubectlNotOnPath_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner(); // nothing registered — RunAsync default is StartFailedResult

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.NotInstalled, snapshot.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_KubectlNotOnPath_RealProcessRunnerNotFoundStatus_StillReturnsNotInstalled()
    {
        // The real ProcessRunner (not the fake) classifies a missing executable as
        // OperationStatus.NotFound, not StartFailed — found via Phase 6G's real Linux (WSL
        // Ubuntu) execution, where kubectl was genuinely absent and this provider was
        // misclassifying NotInstalled as Unavailable/Failed because only StartFailed was
        // checked. This test pins the fix against the exact status the real runner produces.
        var runner = new FakeProcessRunner();
        runner.SetResult("kubectl", ["version", "-o", "json"], ProcessResult.StartFailedResult(TimeSpan.Zero) with { Status = ServerSleuth.Infrastructure.Common.OperationStatus.NotFound });

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.NotInstalled, snapshot.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_VersionQueryPermissionDenied_ReturnsAccessDenied()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("kubectl", ["version", "-o", "json"], new ProcessResult
        {
            Status = ServerSleuth.Infrastructure.Common.OperationStatus.ExecutionFailed,
            ExitCode = 1,
            StandardError = "error: You must be logged in to the server (Unauthorized)"
        });

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.AccessDenied, snapshot.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_ClusterUnreachable_ReturnsUnavailable()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("kubectl", ["version", "-o", "json"], new ProcessResult
        {
            Status = ServerSleuth.Infrastructure.Common.OperationStatus.ExecutionFailed,
            ExitCode = 1,
            StandardError = "Unable to connect to the server: dial tcp: lookup refused"
        });

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.Unavailable, snapshot.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_AllEmpty_ReturnsSupported_WithClusterVersionAndContext()
    {
        var runner = RunnerWithEverythingEmpty();

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.Supported, snapshot.Status);
        Assert.Equal("v1.29.2", snapshot.Cluster?.ServerVersion);
        Assert.Equal("my-context", snapshot.Cluster?.ContextName);
    }

    [Fact]
    public async Task GetSnapshotAsync_MultipleNamespaces_ParsedAndClusterIdentifierTakenFromKubeSystem()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "namespaces", false, """
        {"items":[
          {"metadata":{"name":"default","uid":"uid-1"},"status":{"phase":"Active"}},
          {"metadata":{"name":"kube-system","uid":"uid-kube-system"},"status":{"phase":"Active"}}
        ]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.Namespaces.Count);
        Assert.Equal("uid-kube-system", snapshot.Cluster?.ClusterIdentifier);
    }

    [Fact]
    public async Task GetSnapshotAsync_NodeParsing_ExtractsRolesVersionsAndReadyCondition()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "nodes", false, """
        {"items":[{
          "metadata":{"name":"node-1","uid":"uid-n1","labels":{"node-role.kubernetes.io/control-plane":""}},
          "status":{
            "nodeInfo":{"kubeletVersion":"v1.29.2","osImage":"Ubuntu 22.04","containerRuntimeVersion":"containerd://1.7.0","kernelVersion":"5.15.0"},
            "conditions":[{"type":"Ready","status":"True"}]
          }
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var node = Assert.Single(snapshot.Nodes);
        Assert.Contains("control-plane", node.Roles);
        Assert.Equal("v1.29.2", node.KubeletVersion);
        Assert.Equal("Ubuntu 22.04", node.OsImage);
        Assert.True(node.Ready);
    }

    [Fact]
    public async Task GetSnapshotAsync_PodParsing_ExtractsPhaseIpsAndContainers()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "pods", true, """
        {"items":[{
          "metadata":{"name":"erp-web-abc","namespace":"erp","uid":"uid-pod-1","creationTimestamp":"2026-01-01T00:00:00Z"},
          "spec":{"nodeName":"node-1"},
          "status":{
            "phase":"Running","podIP":"10.0.0.5","hostIP":"192.168.1.10",
            "containerStatuses":[{"name":"web","image":"erp/web:1.0","imageID":"docker-pullable://erp/web@sha256:abc","ready":true,"restartCount":2,"state":{"running":{"startedAt":"2026-01-01T00:00:00Z"}}}]
          }
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var pod = Assert.Single(snapshot.Pods);
        Assert.Equal("Running", pod.Phase);
        Assert.Equal("10.0.0.5", pod.PodIp);
        Assert.Equal("192.168.1.10", pod.HostIp);
        var container = Assert.Single(pod.Containers);
        Assert.Equal("web", container.Name);
        Assert.Equal("running", container.State);
        Assert.Equal(2, container.RestartCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_DeploymentParsing_ExtractsReplicasSelectorAndImages()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "deployments", true, """
        {"items":[{
          "metadata":{"name":"erp-web","namespace":"erp","uid":"uid-dep-1"},
          "spec":{"replicas":3,"selector":{"matchLabels":{"app":"erp-web"}},"template":{"spec":{"containers":[{"name":"web","image":"erp/web:1.0"}]}}},
          "status":{"readyReplicas":3}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var deployment = Assert.Single(snapshot.Workloads, w => w.Kind == "Deployment");
        Assert.Equal(3, deployment.DesiredReplicas);
        Assert.Equal(3, deployment.ReadyReplicas);
        Assert.Contains("erp/web:1.0", deployment.TemplateContainerImages);
        Assert.Equal("erp-web", deployment.SelectorLabels["app"]);
    }

    [Fact]
    public async Task GetSnapshotAsync_StatefulSetParsing_UsesSameShapeAsDeployment()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "statefulsets", true, """
        {"items":[{
          "metadata":{"name":"erp-db","namespace":"erp","uid":"uid-sts-1"},
          "spec":{"replicas":1,"selector":{"matchLabels":{"app":"erp-db"}},"template":{"spec":{"containers":[{"name":"db","image":"postgres:16"}]}}},
          "status":{"readyReplicas":1}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var sts = Assert.Single(snapshot.Workloads, w => w.Kind == "StatefulSet");
        Assert.Equal(1, sts.DesiredReplicas);
        Assert.Contains("postgres:16", sts.TemplateContainerImages);
    }

    [Fact]
    public async Task GetSnapshotAsync_DaemonSetParsing_UsesDesiredNumberScheduledAndNumberReady()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "daemonsets", true, """
        {"items":[{
          "metadata":{"name":"log-agent","namespace":"kube-system","uid":"uid-ds-1"},
          "spec":{"selector":{"matchLabels":{"app":"log-agent"}},"template":{"spec":{"containers":[{"name":"agent","image":"fluentd:1.0"}]}}},
          "status":{"desiredNumberScheduled":3,"numberReady":3}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var ds = Assert.Single(snapshot.Workloads, w => w.Kind == "DaemonSet");
        Assert.Equal(3, ds.DesiredReplicas);
        Assert.Equal(3, ds.ReadyReplicas);
    }

    [Fact]
    public async Task GetSnapshotAsync_ServiceParsing_ExtractsPortsAndSelector()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "services", true, """
        {"items":[{
          "metadata":{"name":"erp-web","namespace":"erp","uid":"uid-svc-1"},
          "spec":{"type":"NodePort","clusterIP":"10.96.0.5","externalIPs":[],"ports":[{"port":80,"targetPort":8080,"nodePort":30080,"protocol":"TCP"}],"selector":{"app":"erp-web"}}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var svc = Assert.Single(snapshot.Services);
        Assert.Equal("NodePort", svc.ServiceType);
        Assert.Equal("10.96.0.5", svc.ClusterIp);
        var port = Assert.Single(svc.Ports);
        Assert.Equal(80, port.Port);
        Assert.Equal("8080", port.TargetPort);
        Assert.Equal(30080, port.NodePort);
    }

    [Fact]
    public async Task GetSnapshotAsync_IngressParsing_ExtractsHostsPathsAndTlsSecrets()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "ingress", true, """
        {"items":[{
          "metadata":{"name":"erp-ingress","namespace":"erp","uid":"uid-ing-1"},
          "spec":{
            "ingressClassName":"nginx",
            "rules":[{"host":"erp.example.com","http":{"paths":[{"path":"/","backend":{"service":{"name":"erp-web","port":{"number":80}}}}]}}],
            "tls":[{"hosts":["erp.example.com"],"secretName":"erp-tls"}]
          }
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var ingress = Assert.Single(snapshot.Ingresses);
        Assert.Contains("erp.example.com", ingress.Hosts);
        Assert.Contains("erp.example.com/ -> erp-web:80", ingress.Paths);
        Assert.Contains("erp-tls", ingress.TlsSecretNames);
    }

    [Fact]
    public async Task GetSnapshotAsync_ConfigMapParsing_CapturesKeysAndTextValues()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "configmaps", true, """
        {"items":[{
          "metadata":{"name":"erp-config","namespace":"erp"},
          "data":{"LOG_LEVEL":"Info"},
          "binaryData":{"cert.bin":"aGVsbG8="}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var cm = Assert.Single(snapshot.ConfigMaps);
        Assert.Equal("Info", cm.RawTextData["LOG_LEVEL"]);
        Assert.Contains("cert.bin", cm.BinaryDataKeys);
    }

    [Fact]
    public async Task GetSnapshotAsync_SecretParsing_CapturesOnlyKeyNames_NeverValues()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "secrets", true, """
        {"items":[{
          "metadata":{"name":"erp-db","namespace":"erp"},
          "type":"Opaque",
          "data":{"DB_PASSWORD":"U3VwZXJTZWNyZXQxMjM=","DB_USER":"ZXJwYWRtaW4="}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var secret = Assert.Single(snapshot.Secrets);
        Assert.Equal("Opaque", secret.SecretType);
        Assert.Contains("DB_PASSWORD", secret.Keys);
        Assert.Contains("DB_USER", secret.Keys);
    }

    [Fact]
    public async Task GetSnapshotAsync_PvcParsing_ExtractsCapacityAndAccessModes()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "pvc", true, """
        {"items":[{
          "metadata":{"name":"erp-data","namespace":"erp"},
          "spec":{"accessModes":["ReadWriteOnce"],"storageClassName":"standard","volumeName":"pv-1","volumeMode":"Filesystem"},
          "status":{"phase":"Bound","capacity":{"storage":"10Gi"}}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var pvc = Assert.Single(snapshot.Pvcs);
        Assert.Equal("Bound", pvc.Phase);
        Assert.Equal("10Gi", pvc.Capacity);
        Assert.Contains("ReadWriteOnce", pvc.AccessModes);
    }

    [Fact]
    public async Task GetSnapshotAsync_PvParsing_HostPathSource_RecordsPathWithoutAccessingIt()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "pv", false, """
        {"items":[{
          "metadata":{"name":"pv-1"},
          "spec":{"capacity":{"storage":"10Gi"},"accessModes":["ReadWriteOnce"],"storageClassName":"standard","persistentVolumeReclaimPolicy":"Retain","hostPath":{"path":"/data/erp"}},
          "status":{"phase":"Bound"}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var pv = Assert.Single(snapshot.Pvs);
        Assert.Equal("HostPath", pv.VolumeSourceType);
        Assert.Equal("/data/erp", pv.HostPath);
    }

    [Fact]
    public async Task GetSnapshotAsync_PvParsing_NfsSource_DetectedWithoutHostPath()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "pv", false, """
        {"items":[{
          "metadata":{"name":"pv-nfs"},
          "spec":{"capacity":{"storage":"5Gi"},"accessModes":["ReadWriteMany"],"nfs":{"server":"nfs.internal","path":"/exports/erp"}},
          "status":{"phase":"Bound"}
        }]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        var pv = Assert.Single(snapshot.Pvs);
        Assert.Equal("NFS", pv.VolumeSourceType);
        Assert.Null(pv.HostPath);
    }

    [Fact]
    public async Task GetSnapshotAsync_MalformedResourceJson_SkippedWithoutFailingWholeScan()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "services", true, "{not valid json");

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.PartiallySupported, snapshot.Status);
        Assert.Empty(snapshot.Services);
        Assert.NotEmpty(snapshot.PartialFailures);
    }

    [Fact]
    public async Task GetSnapshotAsync_OnePodResourceFails_OtherResourcesStillReturned()
    {
        var runner = RunnerWithEverythingEmpty();
        runner.SetResult("kubectl", ["get", "pods", "--all-namespaces", "-o", "json"], new ProcessResult
        {
            Status = ServerSleuth.Infrastructure.Common.OperationStatus.ExecutionFailed,
            ExitCode = 1,
            StandardError = "etcdserver: request timed out"
        });
        SetResource(runner, "namespaces", false, """{"items":[{"metadata":{"name":"default","uid":"uid-1"},"status":{"phase":"Active"}}]}""");

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(KubernetesAvailability.PartiallySupported, snapshot.Status);
        Assert.Empty(snapshot.Pods);
        Assert.Single(snapshot.Namespaces);
    }

    [Fact]
    public async Task GetSnapshotAsync_LargeResourceList_ParsesAllItems()
    {
        var runner = RunnerWithEverythingEmpty();
        var items = string.Join(",", Enumerable.Range(0, 500).Select(i =>
            "{\"metadata\":{\"name\":\"pod-" + i + "\",\"namespace\":\"erp\",\"uid\":\"uid-" + i + "\"},\"status\":{\"phase\":\"Running\"}}"));
        SetResource(runner, "pods", true, "{\"items\":[" + items + "]}");

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(500, snapshot.Pods.Count);
    }

    [Fact]
    public async Task GetSnapshotAsync_DuplicateNamedResourcesInDifferentNamespaces_BothReturned()
    {
        var runner = RunnerWithEverythingEmpty();
        SetResource(runner, "services", true, """
        {"items":[
          {"metadata":{"name":"web","namespace":"staging","uid":"uid-a"},"spec":{"type":"ClusterIP"}},
          {"metadata":{"name":"web","namespace":"production","uid":"uid-b"},"spec":{"type":"ClusterIP"}}
        ]}
        """);

        var snapshot = await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(2, snapshot.Services.Count);
        Assert.Contains(snapshot.Services, s => s.Namespace == "staging");
        Assert.Contains(snapshot.Services, s => s.Namespace == "production");
    }
}
