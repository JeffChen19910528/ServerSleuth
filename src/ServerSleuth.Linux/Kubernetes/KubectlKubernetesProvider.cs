using System.Text.Json;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Process;

namespace ServerSleuth.Linux.Kubernetes;

/// <summary>
/// Read-only Kubernetes discovery via `kubectl`, using the currently configured context — see
/// skill.md (Phase 6D) §1-5. Every call is a single, fixed, bulk `get <kind> --all-namespaces
/// -o json` (or the cluster-scoped equivalent for Nodes/PVs) — never one call per object, never
/// `exec`/`cp`/`apply`/`create`/`delete`/`patch`/`edit`/`rollout`/`port-forward`, never a shell.
/// </summary>
public sealed class KubectlKubernetesProvider(IProcessRunner processRunner) : IKubernetesProvider
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private static readonly Dictionary<string, string> KnownVolumeSourceDisplayNames = new(StringComparer.Ordinal)
    {
        ["nfs"] = "NFS",
        ["csi"] = "CSI",
        ["awsElasticBlockStore"] = "AWSElasticBlockStore",
        ["azureDisk"] = "AzureDisk",
        ["azureFile"] = "AzureFile",
        ["iscsi"] = "iSCSI",
        ["cephfs"] = "CephFS",
        ["glusterfs"] = "GlusterFS",
        ["local"] = "Local",
        ["gcePersistentDisk"] = "GCEPersistentDisk",
        ["fc"] = "FibreChannel",
        ["cinder"] = "Cinder",
        ["rbd"] = "RBD",
        ["vsphereVolume"] = "vSphereVolume",
        ["portworxVolume"] = "PortworxVolume",
        ["scaleIO"] = "ScaleIO",
        ["storageos"] = "StorageOS",
        ["photonPersistentDisk"] = "PhotonPersistentDisk"
    };

    public async Task<KubernetesSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var versionResult = await Run(["version", "-o", "json"], cancellationToken);
        if (!versionResult.Success)
        {
            return Classify(versionResult);
        }

        var partialFailures = new List<string>();
        var serverVersion = TryDeserialize<KubectlVersionResponse>(versionResult.StandardOutput)?.ServerVersion?.GitVersion;
        var contextName = await TryGetCurrentContext(cancellationToken);

        var namespaces = await ReadNamespaces(partialFailures, cancellationToken);
        var nodes = await ReadNodes(partialFailures, cancellationToken);
        var pods = await ReadPods(partialFailures, cancellationToken);
        var deployments = await ReadReplicatedWorkloads("deployments", "Deployment", partialFailures, cancellationToken);
        var statefulSets = await ReadReplicatedWorkloads("statefulsets", "StatefulSet", partialFailures, cancellationToken);
        var daemonSets = await ReadDaemonSets(partialFailures, cancellationToken);
        var services = await ReadServices(partialFailures, cancellationToken);
        var ingresses = await ReadIngresses(partialFailures, cancellationToken);
        var configMaps = await ReadConfigMaps(partialFailures, cancellationToken);
        var secrets = await ReadSecrets(partialFailures, cancellationToken);
        var pvcs = await ReadPvcs(partialFailures, cancellationToken);
        var pvs = await ReadPvs(partialFailures, cancellationToken);

        // A stable cluster identifier is only ever taken from a fact the API already reported —
        // never invented — see skill.md §6/§22. kube-system's own UID is a well-known, always
        // present, effectively cluster-lifetime-scoped value.
        var clusterIdentifier = namespaces.FirstOrDefault(n => n.Name == "kube-system")?.Uid;

        var cluster = new ClusterRow
        {
            ServerVersion = serverVersion,
            ContextName = contextName,
            IsCurrentContext = contextName is not null ? true : null,
            ClusterIdentifier = clusterIdentifier
        };

        var status = partialFailures.Count > 0 ? KubernetesAvailability.PartiallySupported : KubernetesAvailability.Supported;

        return new KubernetesSnapshot
        {
            Status = status,
            Cluster = cluster,
            Namespaces = namespaces,
            Nodes = nodes,
            Pods = pods,
            Workloads = [.. deployments, .. statefulSets, .. daemonSets],
            Services = services,
            Ingresses = ingresses,
            ConfigMaps = configMaps,
            Secrets = secrets,
            Pvcs = pvcs,
            Pvs = pvs,
            PartialFailures = partialFailures
        };
    }

    private async Task<string?> TryGetCurrentContext(CancellationToken cancellationToken)
    {
        var result = await Run(["config", "current-context"], cancellationToken);
        if (!result.Success)
        {
            return null;
        }

        var name = result.StandardOutput.Trim();
        return name.Length > 0 ? name : null;
    }

    private async Task<List<NamespaceRow>> ReadNamespaces(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<NamespaceItem>(["get", "namespaces", "-o", "json"], "namespaces", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new NamespaceRow
        {
            Name = i.Metadata!.Name!,
            Uid = i.Metadata.Uid,
            Phase = i.Status?.Phase,
            Labels = i.Metadata.Labels ?? new Dictionary<string, string>()
        }).ToList();
    }

    private async Task<List<NodeRow>> ReadNodes(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<NodeItem>(["get", "nodes", "-o", "json"], "nodes", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new NodeRow
        {
            Name = i.Metadata!.Name!,
            Uid = i.Metadata.Uid,
            Roles = ExtractRoles(i.Metadata.Labels),
            KubeletVersion = i.Status?.NodeInfo?.KubeletVersion,
            OsImage = i.Status?.NodeInfo?.OsImage,
            ContainerRuntimeVersion = i.Status?.NodeInfo?.ContainerRuntimeVersion,
            KernelVersion = i.Status?.NodeInfo?.KernelVersion,
            Ready = i.Status?.Conditions?.FirstOrDefault(c => c.Type == "Ready") is { Status: "True" }
        }).ToList();
    }

    private async Task<List<PodRow>> ReadPods(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<PodItem>(["get", "pods", "--all-namespaces", "-o", "json"], "pods", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new PodRow
        {
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            Uid = i.Metadata.Uid,
            Phase = i.Status?.Phase,
            NodeName = i.Spec?.NodeName,
            PodIp = i.Status?.PodIp,
            HostIp = i.Status?.HostIp,
            Created = TryParseTimestamp(i.Metadata.CreationTimestamp),
            Containers = (i.Status?.ContainerStatuses ?? []).Where(c => c.Name is not null).Select(c => new PodContainerRow
            {
                Name = c.Name!,
                Image = c.Image,
                ImageId = c.ImageId,
                State = DescribeContainerState(c.State),
                Ready = c.Ready,
                RestartCount = c.RestartCount
            }).ToList()
        }).ToList();
    }

    private async Task<List<WorkloadRow>> ReadReplicatedWorkloads(string resource, string kind, List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<ReplicatedWorkloadItem>(["get", resource, "--all-namespaces", "-o", "json"], resource, partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new WorkloadRow
        {
            Kind = kind,
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            Uid = i.Metadata.Uid,
            DesiredReplicas = i.Spec?.Replicas,
            ReadyReplicas = i.Status?.ReadyReplicas,
            SelectorLabels = i.Spec?.Selector?.MatchLabels ?? new Dictionary<string, string>(),
            TemplateContainerImages = (i.Spec?.Template?.Spec?.Containers ?? []).Where(c => c.Image is not null).Select(c => c.Image!).ToList()
        }).ToList();
    }

    private async Task<List<WorkloadRow>> ReadDaemonSets(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<DaemonSetItem>(["get", "daemonsets", "--all-namespaces", "-o", "json"], "daemonsets", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new WorkloadRow
        {
            Kind = "DaemonSet",
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            Uid = i.Metadata.Uid,
            DesiredReplicas = i.Status?.DesiredNumberScheduled,
            ReadyReplicas = i.Status?.NumberReady,
            SelectorLabels = i.Spec?.Selector?.MatchLabels ?? new Dictionary<string, string>(),
            TemplateContainerImages = (i.Spec?.Template?.Spec?.Containers ?? []).Where(c => c.Image is not null).Select(c => c.Image!).ToList()
        }).ToList();
    }

    private async Task<List<ServiceRow>> ReadServices(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<ServiceItem>(["get", "services", "--all-namespaces", "-o", "json"], "services", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new ServiceRow
        {
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            Uid = i.Metadata.Uid,
            ServiceType = i.Spec?.Type,
            ClusterIp = i.Spec?.ClusterIp,
            ExternalIps = i.Spec?.ExternalIps ?? [],
            Ports = (i.Spec?.Ports ?? []).Select(p => new ServicePortRow
            {
                Port = p.Port,
                TargetPort = DescribeIntOrString(p.TargetPort),
                NodePort = p.NodePort,
                Protocol = p.Protocol
            }).ToList(),
            SelectorLabels = i.Spec?.Selector ?? new Dictionary<string, string>()
        }).ToList();
    }

    private async Task<List<IngressRow>> ReadIngresses(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<IngressItem>(["get", "ingress", "--all-namespaces", "-o", "json"], "ingress", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i =>
        {
            var rules = i.Spec?.Rules ?? [];
            var hosts = rules.Where(r => r.Host is not null).Select(r => r.Host!).Distinct().ToList();
            var paths = new List<string>();
            foreach (var rule in rules)
            {
                foreach (var path in rule.Http?.Paths ?? [])
                {
                    var svc = path.Backend?.Service;
                    var port = svc?.Port?.Number?.ToString() ?? svc?.Port?.Name ?? "?";
                    paths.Add($"{rule.Host}{path.Path} -> {svc?.Name ?? "?"}:{port}");
                }
            }

            return new IngressRow
            {
                Name = i.Metadata!.Name!,
                Namespace = i.Metadata.Namespace ?? "default",
                Uid = i.Metadata.Uid,
                IngressClassName = i.Spec?.IngressClassName,
                Hosts = hosts,
                Paths = paths,
                TlsSecretNames = (i.Spec?.Tls ?? []).Where(t => t.SecretName is not null).Select(t => t.SecretName!).ToList()
            };
        }).ToList();
    }

    private async Task<List<ConfigMapRow>> ReadConfigMaps(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<ConfigMapItem>(["get", "configmaps", "--all-namespaces", "-o", "json"], "configmaps", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new ConfigMapRow
        {
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            RawTextData = i.Data ?? new Dictionary<string, string>(),
            BinaryDataKeys = i.BinaryData?.Keys.ToList() ?? []
        }).ToList();
    }

    private async Task<List<SecretRow>> ReadSecrets(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<SecretItem>(["get", "secrets", "--all-namespaces", "-o", "json"], "secrets", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new SecretRow
        {
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            SecretType = i.Type,
            Keys = i.Data?.Keys.ToList() ?? [] // values are never read from i.Data — keys only
        }).ToList();
    }

    private async Task<List<PvcRow>> ReadPvcs(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<PvcItem>(["get", "pvc", "--all-namespaces", "-o", "json"], "pvc", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i => new PvcRow
        {
            Name = i.Metadata!.Name!,
            Namespace = i.Metadata.Namespace ?? "default",
            Phase = i.Status?.Phase,
            Capacity = i.Status?.Capacity?.GetValueOrDefault("storage"),
            AccessModes = i.Spec?.AccessModes ?? [],
            StorageClassName = i.Spec?.StorageClassName,
            VolumeName = i.Spec?.VolumeName,
            VolumeMode = i.Spec?.VolumeMode
        }).ToList();
    }

    private async Task<List<PvRow>> ReadPvs(List<string> partialFailures, CancellationToken cancellationToken)
    {
        var list = await GetList<PvItem>(["get", "pv", "-o", "json"], "pv", partialFailures, cancellationToken);
        return list.Where(i => i.Metadata?.Name is not null).Select(i =>
        {
            var (sourceType, hostPath) = DescribeVolumeSource(i.Spec);
            return new PvRow
            {
                Name = i.Metadata!.Name!,
                Phase = i.Status?.Phase,
                Capacity = i.Spec?.Capacity?.GetValueOrDefault("storage"),
                AccessModes = i.Spec?.AccessModes ?? [],
                StorageClassName = i.Spec?.StorageClassName,
                ReclaimPolicy = i.Spec?.ReclaimPolicy,
                VolumeSourceType = sourceType,
                HostPath = hostPath
            };
        }).ToList();
    }

    private static (string? sourceType, string? hostPath) DescribeVolumeSource(PvSpec? spec)
    {
        if (spec is null)
        {
            return (null, null);
        }

        if (spec.HostPath is not null)
        {
            return ("HostPath", spec.HostPath.Path);
        }

        foreach (var (key, displayName) in KnownVolumeSourceDisplayNames)
        {
            if (spec.ExtensionData?.ContainsKey(key) == true)
            {
                return (displayName, null);
            }
        }

        return (null, null);
    }

    private static IReadOnlyList<string> ExtractRoles(Dictionary<string, string>? labels)
    {
        const string rolePrefix = "node-role.kubernetes.io/";
        if (labels is null)
        {
            return [];
        }

        var roles = labels.Keys
            .Where(k => k.StartsWith(rolePrefix, StringComparison.Ordinal))
            .Select(k => k[rolePrefix.Length..])
            .Where(r => r.Length > 0)
            .ToList();

        return roles;
    }

    private static string? DescribeContainerState(ContainerState? state)
    {
        if (state is null)
        {
            return null;
        }

        if (state.Running is not null) return "running";
        if (state.Waiting is not null) return "waiting";
        if (state.Terminated is not null) return "terminated";
        return null;
    }

    private static string? DescribeIntOrString(JsonElement? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString(),
            _ => null
        };
    }

    private async Task<List<T>> GetList<T>(IReadOnlyList<string> arguments, string resourceLabel, List<string> partialFailures, CancellationToken cancellationToken)
    {
        var result = await Run(arguments, cancellationToken);
        if (!result.Success)
        {
            partialFailures.Add($"{resourceLabel}: {result.Status}");
            return [];
        }

        var list = TryDeserialize<KubeList<T>>(result.StandardOutput);
        if (list is null)
        {
            partialFailures.Add($"{resourceLabel}: malformed JSON output");
            return [];
        }

        return list.Items ?? [];
    }

    private static KubernetesSnapshot Classify(ProcessResult result)
    {
        // A missing executable can surface as either OperationStatus.StartFailed (the generic
        // process-start failure) or OperationStatus.NotFound (ProcessRunner's more specific
        // classification for the common "no such file" case, on both Windows and Linux) —
        // found via Phase 6G's real Linux (WSL Ubuntu) execution with kubectl genuinely absent,
        // where this provider was misclassifying NotInstalled as Unavailable/Failed because only
        // StartFailed was checked. Fixture tests never caught this since FakeProcessRunner's
        // default "no match" result hard-codes StartFailed, never exercising NotFound.
        if (result.Status is OperationStatus.StartFailed or OperationStatus.NotFound)
        {
            return new KubernetesSnapshot { Status = KubernetesAvailability.NotInstalled };
        }

        var stderr = result.StandardError;
        var status = stderr.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
                     stderr.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
                     stderr.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase)
            ? KubernetesAvailability.AccessDenied
            : KubernetesAvailability.Unavailable;

        return new KubernetesSnapshot { Status = status, ErrorMessage = stderr.Length > 0 ? stderr : result.Status.ToString() };
    }

    private Task<ProcessResult> Run(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        processRunner.RunAsync(new ProcessRequest { Executable = "kubectl", Arguments = arguments, Timeout = CommandTimeout }, cancellationToken);

    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null; // malformed output — skipped, never guessed at
        }
    }

    private static DateTimeOffset? TryParseTimestamp(string? value) =>
        value is not null && DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}
