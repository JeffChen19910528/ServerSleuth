using Microsoft.Extensions.Logging;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Models;
using ServerSleuth.Core.Results;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Security;

namespace ServerSleuth.Linux.Kubernetes;

/// <summary>
/// Discovers Kubernetes cluster/namespace/node/pod/workload/service/ingress/ConfigMap/Secret
/// (metadata only)/PVC/PV resources via the currently configured kubectl context. Read-only
/// throughout — never `exec`/`cp`/`apply`/`create`/`delete`/`patch`/`edit`/`rollout`/
/// `port-forward`, never a shell. See skill.md (Phase 6D) §1-19. Pod containers reuse
/// `Core.Models.Container` (Type = "KubernetesContainer") — never merged with a host-level
/// Docker/Podman container from the Phase 6C scanner; that relationship, if ever drawn, belongs
/// to later Analysis.
/// </summary>
public sealed class LinuxKubernetesScanner(
    IKubernetesProvider provider,
    ISecretRedactor secretRedactor,
    ILogger<LinuxKubernetesScanner> logger) : IDiscoveryScanner
{
    public string Id => "linux-kubernetes-scanner";
    public PlatformSupport PlatformSupport => PlatformSupport.Linux;

    public async Task<DiscoveryResult> ScanAsync(DiscoveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(ScannerLogEvents.ScannerStarted, "{ScannerId} started", Id);

        KubernetesSnapshot snapshot;
        try
        {
            snapshot = await provider.GetSnapshotAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Kubernetes provider threw unexpectedly");
            snapshot = new KubernetesSnapshot { Status = KubernetesAvailability.Unavailable, ErrorMessage = ex.Message };
        }

        if (snapshot.Status is KubernetesAvailability.NotInstalled or KubernetesAvailability.AccessDenied or KubernetesAvailability.Unavailable)
        {
            var scannerStatus = snapshot.Status switch
            {
                KubernetesAvailability.NotInstalled => ScannerStatus.NotInstalled,
                KubernetesAvailability.AccessDenied => ScannerStatus.AccessDenied,
                _ => ScannerStatus.Failed
            };

            var errors = new List<DiscoveryError>();
            if (snapshot.Status != KubernetesAvailability.NotInstalled)
            {
                errors.Add(new DiscoveryError
                {
                    ScannerId = Id,
                    Message = snapshot.ErrorMessage ?? snapshot.Status.ToString(),
                    IsPermissionFailure = snapshot.Status == KubernetesAvailability.AccessDenied
                });
            }

            logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);
            return new DiscoveryResult { ScannerId = Id, Status = scannerStatus, Errors = errors };
        }

        var clusterScope = ClusterScope(snapshot.Cluster);
        var entities = new List<DiscoveryEntity>();

        if (snapshot.Cluster is not null)
        {
            entities.Add(BuildClusterEntity(snapshot.Cluster, clusterScope));
        }

        entities.AddRange(snapshot.Namespaces.Select(n => BuildNamespaceEntity(n, clusterScope)));
        entities.AddRange(snapshot.Nodes.Select(n => BuildNodeEntity(n, clusterScope)));

        foreach (var pod in snapshot.Pods)
        {
            entities.Add(BuildPodEntity(pod, clusterScope));
            entities.AddRange(BuildPodContainerEntities(pod, clusterScope));
        }

        entities.AddRange(snapshot.Workloads.Select(w => BuildWorkloadEntity(w, clusterScope)));
        entities.AddRange(snapshot.Services.Select(s => BuildServiceEntity(s, clusterScope)));
        entities.AddRange(snapshot.Ingresses.Select(i => BuildIngressEntity(i, clusterScope)));
        entities.AddRange(snapshot.ConfigMaps.Select(c => BuildConfigMapEntity(c, clusterScope, secretRedactor)));
        entities.AddRange(snapshot.Secrets.Select(s => BuildSecretEntity(s, clusterScope)));
        entities.AddRange(snapshot.Pvcs.Select(p => BuildPvcEntity(p, clusterScope)));
        entities.AddRange(snapshot.Pvs.Select(p => BuildPvEntity(p, clusterScope)));

        var discoveryErrors = snapshot.PartialFailures
            .Select(f => new DiscoveryError { ScannerId = Id, Message = f })
            .ToList();

        logger.LogInformation(ScannerLogEvents.DiscoveryCount, "{ScannerId} discovered {Count} entities", Id, entities.Count);
        logger.LogInformation(ScannerLogEvents.ScannerCompleted, "{ScannerId} completed", Id);

        var status = snapshot.Status == KubernetesAvailability.PartiallySupported ? ScannerStatus.PartiallySupported : ScannerStatus.Supported;

        return new DiscoveryResult { ScannerId = Id, Status = status, Entities = entities, Errors = discoveryErrors };
    }

    private static string ClusterScope(ClusterRow? cluster) => cluster?.ContextName ?? "cluster";

    internal static Cluster BuildClusterEntity(ClusterRow row, string clusterScope)
    {
        var entity = new Cluster
        {
            Id = $"cluster:{clusterScope}",
            Name = row.ContextName ?? "cluster",
            Type = "Cluster",
            Source = "kubectl",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            ServerVersion = row.ServerVersion,
            ContextName = row.ContextName,
            IsCurrentContext = row.IsCurrentContext,
            ClusterIdentifier = row.ClusterIdentifier
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl version -o json" });
        return entity;
    }

    internal static KubernetesNamespace BuildNamespaceEntity(NamespaceRow row, string clusterScope)
    {
        var entity = new KubernetesNamespace
        {
            Id = $"namespace:{clusterScope}:{row.Name}",
            Name = row.Name,
            Type = "KubernetesNamespace",
            Source = "kubectl",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Phase = row.Phase,
            Uid = row.Uid
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get namespaces -o json", Detail = row.Name });

        foreach (var (key, value) in row.Labels)
        {
            entity.SetMetadata($"Label.{key}", value);
        }

        return entity;
    }

    internal static KubernetesNode BuildNodeEntity(NodeRow row, string clusterScope)
    {
        var entity = new KubernetesNode
        {
            Id = $"node:{clusterScope}:{row.Name}",
            Name = row.Name,
            Type = "KubernetesNode",
            Source = "kubectl",
            Status = row.Ready == true ? EntityStatus.Running : EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Roles = row.Roles,
            KubernetesVersion = row.KubeletVersion,
            OsImage = row.OsImage,
            ContainerRuntimeVersion = row.ContainerRuntimeVersion,
            KernelVersion = row.KernelVersion,
            Ready = row.Ready
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get nodes -o json", Detail = row.Name });
        if (row.Uid is not null) entity.SetMetadata("Uid", row.Uid);

        return entity;
    }

    internal static KubernetesPod BuildPodEntity(PodRow row, string clusterScope)
    {
        var entity = new KubernetesPod
        {
            Id = $"pod:{clusterScope}:{row.Uid ?? $"{row.Namespace}/{row.Name}"}",
            Name = row.Name,
            Type = "KubernetesPod",
            Source = "kubectl",
            Status = string.Equals(row.Phase, "Running", StringComparison.OrdinalIgnoreCase) ? EntityStatus.Running : EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Namespace = row.Namespace,
            Uid = row.Uid,
            Phase = row.Phase,
            NodeName = row.NodeName,
            PodIp = row.PodIp,
            HostIp = row.HostIp
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get pods --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });
        if (row.Created is not null) entity.SetMetadata("Created", row.Created.Value.ToString("O"));

        return entity;
    }

    internal static List<Container> BuildPodContainerEntities(PodRow row, string clusterScope)
    {
        var podKey = row.Uid ?? $"{row.Namespace}/{row.Name}";

        return row.Containers.Select(c =>
        {
            var entity = new Container
            {
                Id = $"podcontainer:{clusterScope}:{podKey}:{c.Name}",
                Name = c.Name,
                Type = "KubernetesContainer",
                Source = "kubectl",
                Status = string.Equals(c.State, "running", StringComparison.OrdinalIgnoreCase) ? EntityStatus.Running : EntityStatus.Configured,
                Confidence = Confidence.VeryHigh(),
                ImageTag = c.Image
            };

            entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get pods --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}/{c.Name}" });

            entity.SetMetadata("PodNamespace", row.Namespace);
            entity.SetMetadata("PodName", row.Name);
            if (c.ImageId is not null) entity.SetMetadata("ImageId", c.ImageId);
            if (c.State is not null) entity.SetMetadata("State", c.State);
            if (c.Ready is not null) entity.SetMetadata("Ready", c.Ready.Value.ToString());
            if (c.RestartCount is not null) entity.SetMetadata("RestartCount", c.RestartCount.Value.ToString());

            return entity;
        }).ToList();
    }

    internal static KubernetesWorkload BuildWorkloadEntity(WorkloadRow row, string clusterScope)
    {
        var entity = new KubernetesWorkload
        {
            Id = $"workload:{clusterScope}:{row.Kind}:{row.Namespace}:{row.Uid ?? row.Name}",
            Name = row.Name,
            Type = "KubernetesWorkload",
            Source = "kubectl",
            Status = row.ReadyReplicas is > 0 ? EntityStatus.Running : EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Kind = row.Kind,
            Namespace = row.Namespace,
            Uid = row.Uid,
            DesiredReplicas = row.DesiredReplicas,
            ReadyReplicas = row.ReadyReplicas,
            SelectorLabels = row.SelectorLabels.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
            TemplateContainerImages = row.TemplateContainerImages
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = $"kubectl get {row.Kind.ToLowerInvariant()}s --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });

        return entity;
    }

    internal static KubernetesService BuildServiceEntity(ServiceRow row, string clusterScope)
    {
        var entity = new KubernetesService
        {
            Id = $"service:{clusterScope}:{row.Namespace}:{row.Uid ?? row.Name}",
            Name = row.Name,
            Type = "KubernetesService",
            Source = "kubectl",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Namespace = row.Namespace,
            Uid = row.Uid,
            ServiceType = row.ServiceType,
            ClusterIp = row.ClusterIp,
            ExternalIps = row.ExternalIps,
            Ports = row.Ports.Select(FormatServicePort).ToList(),
            SelectorLabels = row.SelectorLabels.Select(kv => $"{kv.Key}={kv.Value}").ToList()
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get services --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });

        return entity;
    }

    internal static KubernetesIngress BuildIngressEntity(IngressRow row, string clusterScope)
    {
        var entity = new KubernetesIngress
        {
            Id = $"ingress:{clusterScope}:{row.Namespace}:{row.Uid ?? row.Name}",
            Name = row.Name,
            Type = "KubernetesIngress",
            Source = "kubectl",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Namespace = row.Namespace,
            Uid = row.Uid,
            IngressClassName = row.IngressClassName,
            Hosts = row.Hosts,
            Paths = row.Paths,
            TlsSecretNames = row.TlsSecretNames
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get ingress --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });

        return entity;
    }

    internal static KubernetesConfigMap BuildConfigMapEntity(ConfigMapRow row, string clusterScope, ISecretRedactor secretRedactor)
    {
        var keys = row.RawTextData.Keys.Concat(row.BinaryDataKeys).ToList();

        var entity = new KubernetesConfigMap
        {
            Id = $"configmap:{clusterScope}:{row.Namespace}:{row.Name}",
            Name = row.Name,
            Type = "KubernetesConfigMap",
            Source = "kubectl",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Namespace = row.Namespace,
            Keys = keys
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get configmaps --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });

        foreach (var (key, value) in row.RawTextData)
        {
            entity.SetMetadata($"Data.{key}", secretRedactor.Redact(value));
        }

        return entity;
    }

    internal static KubernetesSecret BuildSecretEntity(SecretRow row, string clusterScope)
    {
        var entity = new KubernetesSecret
        {
            Id = $"secret:{clusterScope}:{row.Namespace}:{row.Name}",
            Name = row.Name,
            Type = "KubernetesSecret",
            Source = "kubectl",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Namespace = row.Namespace,
            SecretType = row.SecretType,
            Keys = row.Keys
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get secrets --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });

        return entity;
    }

    internal static KubernetesPersistentVolumeClaim BuildPvcEntity(PvcRow row, string clusterScope)
    {
        var entity = new KubernetesPersistentVolumeClaim
        {
            Id = $"pvc:{clusterScope}:{row.Namespace}:{row.Name}",
            Name = row.Name,
            Type = "KubernetesPersistentVolumeClaim",
            Source = "kubectl",
            Status = string.Equals(row.Phase, "Bound", StringComparison.OrdinalIgnoreCase) ? EntityStatus.Configured : EntityStatus.Unknown,
            Confidence = Confidence.VeryHigh(),
            Namespace = row.Namespace,
            Phase = row.Phase,
            Capacity = row.Capacity,
            AccessModes = row.AccessModes,
            StorageClassName = row.StorageClassName,
            VolumeName = row.VolumeName,
            VolumeMode = row.VolumeMode
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get pvc --all-namespaces -o json", Detail = $"{row.Namespace}/{row.Name}" });

        return entity;
    }

    internal static KubernetesPersistentVolume BuildPvEntity(PvRow row, string clusterScope)
    {
        var entity = new KubernetesPersistentVolume
        {
            Id = $"pv:{clusterScope}:{row.Name}",
            Name = row.Name,
            Type = "KubernetesPersistentVolume",
            Source = "kubectl",
            Status = string.Equals(row.Phase, "Bound", StringComparison.OrdinalIgnoreCase) ? EntityStatus.Configured : EntityStatus.Unknown,
            Confidence = Confidence.VeryHigh(),
            Phase = row.Phase,
            Capacity = row.Capacity,
            AccessModes = row.AccessModes,
            StorageClassName = row.StorageClassName,
            ReclaimPolicy = row.ReclaimPolicy,
            VolumeSourceType = row.VolumeSourceType,
            HostPath = row.HostPath
        };

        entity.AddEvidence(new EvidenceRecord { Type = EvidenceType.KubernetesApi, Location = "kubectl get pv -o json", Detail = row.Name });

        return entity;
    }

    private static string FormatServicePort(ServicePortRow port)
    {
        var baseText = $"{port.Port}:{port.TargetPort ?? port.Port?.ToString()}/{port.Protocol ?? "TCP"}";
        return port.NodePort is not null ? $"{baseText}@{port.NodePort}" : baseText;
    }
}
