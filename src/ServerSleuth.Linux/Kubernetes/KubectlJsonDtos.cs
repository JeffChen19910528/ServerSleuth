using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServerSleuth.Linux.Kubernetes;

// Internal DTOs mirroring `kubectl get <resource> -o json` output shapes — see skill.md
// (Phase 6D) §4-19. Only the fields this scanner actually consumes are modeled; anything not
// explicitly mapped here is discarded by the deserializer, never carried forward as an opaque
// blob (skill.md §19 — "do not store complete raw kubectl JSON").

internal sealed record KubeList<T>
{
    [JsonPropertyName("items")]
    public List<T>? Items { get; init; }
}

internal sealed record KubeObjectMeta
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

    [JsonPropertyName("uid")]
    public string? Uid { get; init; }

    [JsonPropertyName("creationTimestamp")]
    public string? CreationTimestamp { get; init; }

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; init; }
}

// --- kubectl version -o json ---

internal sealed record KubectlVersionResponse
{
    [JsonPropertyName("serverVersion")]
    public KubectlComponentVersion? ServerVersion { get; init; }
}

internal sealed record KubectlComponentVersion
{
    [JsonPropertyName("gitVersion")]
    public string? GitVersion { get; init; }
}

// --- Namespace ---

internal sealed record NamespaceItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("status")]
    public NamespaceStatus? Status { get; init; }
}

internal sealed record NamespaceStatus
{
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }
}

// --- Node ---

internal sealed record NodeItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("status")]
    public NodeStatus? Status { get; init; }
}

internal sealed record NodeStatus
{
    [JsonPropertyName("nodeInfo")]
    public NodeInfo? NodeInfo { get; init; }

    [JsonPropertyName("conditions")]
    public List<NodeCondition>? Conditions { get; init; }
}

internal sealed record NodeInfo
{
    [JsonPropertyName("kubeletVersion")]
    public string? KubeletVersion { get; init; }

    [JsonPropertyName("osImage")]
    public string? OsImage { get; init; }

    [JsonPropertyName("containerRuntimeVersion")]
    public string? ContainerRuntimeVersion { get; init; }

    [JsonPropertyName("kernelVersion")]
    public string? KernelVersion { get; init; }
}

internal sealed record NodeCondition
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

// --- Pod ---

internal sealed record PodItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public PodSpec? Spec { get; init; }

    [JsonPropertyName("status")]
    public PodStatus? Status { get; init; }
}

internal sealed record PodSpec
{
    [JsonPropertyName("nodeName")]
    public string? NodeName { get; init; }
}

internal sealed record PodStatus
{
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("podIP")]
    public string? PodIp { get; init; }

    [JsonPropertyName("hostIP")]
    public string? HostIp { get; init; }

    [JsonPropertyName("containerStatuses")]
    public List<ContainerStatus>? ContainerStatuses { get; init; }
}

internal sealed record ContainerStatus
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("imageID")]
    public string? ImageId { get; init; }

    [JsonPropertyName("ready")]
    public bool? Ready { get; init; }

    [JsonPropertyName("restartCount")]
    public int? RestartCount { get; init; }

    [JsonPropertyName("state")]
    public ContainerState? State { get; init; }
}

internal sealed record ContainerState
{
    [JsonPropertyName("running")]
    public JsonElement? Running { get; init; }

    [JsonPropertyName("waiting")]
    public JsonElement? Waiting { get; init; }

    [JsonPropertyName("terminated")]
    public JsonElement? Terminated { get; init; }
}

// --- Deployment / StatefulSet (structurally identical for the fields consumed here) ---

internal sealed record ReplicatedWorkloadItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public ReplicatedWorkloadSpec? Spec { get; init; }

    [JsonPropertyName("status")]
    public ReplicatedWorkloadStatus? Status { get; init; }
}

internal sealed record ReplicatedWorkloadSpec
{
    [JsonPropertyName("replicas")]
    public int? Replicas { get; init; }

    [JsonPropertyName("selector")]
    public LabelSelector? Selector { get; init; }

    [JsonPropertyName("template")]
    public PodTemplate? Template { get; init; }
}

internal sealed record ReplicatedWorkloadStatus
{
    [JsonPropertyName("readyReplicas")]
    public int? ReadyReplicas { get; init; }
}

// --- DaemonSet (different spec/status shape — no desired replica count field) ---

internal sealed record DaemonSetItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public DaemonSetSpec? Spec { get; init; }

    [JsonPropertyName("status")]
    public DaemonSetStatus? Status { get; init; }
}

internal sealed record DaemonSetSpec
{
    [JsonPropertyName("selector")]
    public LabelSelector? Selector { get; init; }

    [JsonPropertyName("template")]
    public PodTemplate? Template { get; init; }
}

internal sealed record DaemonSetStatus
{
    [JsonPropertyName("desiredNumberScheduled")]
    public int? DesiredNumberScheduled { get; init; }

    [JsonPropertyName("numberReady")]
    public int? NumberReady { get; init; }
}

internal sealed record LabelSelector
{
    [JsonPropertyName("matchLabels")]
    public Dictionary<string, string>? MatchLabels { get; init; }
}

internal sealed record PodTemplate
{
    [JsonPropertyName("spec")]
    public PodTemplateSpec? Spec { get; init; }
}

internal sealed record PodTemplateSpec
{
    [JsonPropertyName("containers")]
    public List<TemplateContainer>? Containers { get; init; }
}

internal sealed record TemplateContainer
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }
}

// --- Service ---

internal sealed record ServiceItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public ServiceSpec? Spec { get; init; }
}

internal sealed record ServiceSpec
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("clusterIP")]
    public string? ClusterIp { get; init; }

    [JsonPropertyName("externalIPs")]
    public List<string>? ExternalIps { get; init; }

    [JsonPropertyName("ports")]
    public List<ServicePortDto>? Ports { get; init; }

    [JsonPropertyName("selector")]
    public Dictionary<string, string>? Selector { get; init; }
}

internal sealed record ServicePortDto
{
    [JsonPropertyName("port")]
    public int? Port { get; init; }

    /// <summary>Kubernetes' IntOrString type — may be a number or a named port string.</summary>
    [JsonPropertyName("targetPort")]
    public JsonElement? TargetPort { get; init; }

    [JsonPropertyName("nodePort")]
    public int? NodePort { get; init; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }
}

// --- Ingress ---

internal sealed record IngressItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public IngressSpec? Spec { get; init; }
}

internal sealed record IngressSpec
{
    [JsonPropertyName("ingressClassName")]
    public string? IngressClassName { get; init; }

    [JsonPropertyName("rules")]
    public List<IngressRule>? Rules { get; init; }

    [JsonPropertyName("tls")]
    public List<IngressTls>? Tls { get; init; }
}

internal sealed record IngressRule
{
    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("http")]
    public IngressHttp? Http { get; init; }
}

internal sealed record IngressHttp
{
    [JsonPropertyName("paths")]
    public List<IngressPath>? Paths { get; init; }
}

internal sealed record IngressPath
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("backend")]
    public IngressBackend? Backend { get; init; }
}

internal sealed record IngressBackend
{
    [JsonPropertyName("service")]
    public IngressBackendService? Service { get; init; }
}

internal sealed record IngressBackendService
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("port")]
    public IngressBackendServicePort? Port { get; init; }
}

internal sealed record IngressBackendServicePort
{
    [JsonPropertyName("number")]
    public int? Number { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed record IngressTls
{
    [JsonPropertyName("hosts")]
    public List<string>? Hosts { get; init; }

    [JsonPropertyName("secretName")]
    public string? SecretName { get; init; }
}

// --- ConfigMap ---

internal sealed record ConfigMapItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("data")]
    public Dictionary<string, string>? Data { get; init; }

    [JsonPropertyName("binaryData")]
    public Dictionary<string, string>? BinaryData { get; init; }
}

// --- Secret (CRITICAL — values must never be materialized as usable strings) ---

internal sealed record SecretItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Kept as opaque <see cref="JsonElement"/>s — only <c>.Keys</c> is ever read by
    /// the provider; the base64 value payload is never extracted as a string, never decoded.</summary>
    [JsonPropertyName("data")]
    public Dictionary<string, JsonElement>? Data { get; init; }
}

// --- PVC ---

internal sealed record PvcItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public PvcSpec? Spec { get; init; }

    [JsonPropertyName("status")]
    public PvcStatus? Status { get; init; }
}

internal sealed record PvcSpec
{
    [JsonPropertyName("accessModes")]
    public List<string>? AccessModes { get; init; }

    [JsonPropertyName("storageClassName")]
    public string? StorageClassName { get; init; }

    [JsonPropertyName("volumeName")]
    public string? VolumeName { get; init; }

    [JsonPropertyName("volumeMode")]
    public string? VolumeMode { get; init; }
}

internal sealed record PvcStatus
{
    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("capacity")]
    public Dictionary<string, string>? Capacity { get; init; }
}

// --- PV ---

internal sealed record PvItem
{
    [JsonPropertyName("metadata")]
    public KubeObjectMeta? Metadata { get; init; }

    [JsonPropertyName("spec")]
    public PvSpec? Spec { get; init; }

    [JsonPropertyName("status")]
    public PvcStatus? Status { get; init; } // same {phase} shape as PVC status
}

internal sealed record PvSpec
{
    [JsonPropertyName("capacity")]
    public Dictionary<string, string>? Capacity { get; init; }

    [JsonPropertyName("accessModes")]
    public List<string>? AccessModes { get; init; }

    [JsonPropertyName("storageClassName")]
    public string? StorageClassName { get; init; }

    [JsonPropertyName("persistentVolumeReclaimPolicy")]
    public string? ReclaimPolicy { get; init; }

    [JsonPropertyName("hostPath")]
    public HostPathVolumeSource? HostPath { get; init; }

    /// <summary>Every volume-source key this spec doesn't explicitly model (nfs, csi,
    /// awsElasticBlockStore, azureDisk, etc.) lands here — used only to determine which source
    /// kind is present, never to read its content.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record HostPathVolumeSource
{
    [JsonPropertyName("path")]
    public string? Path { get; init; }
}
