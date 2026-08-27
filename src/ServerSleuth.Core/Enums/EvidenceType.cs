namespace ServerSleuth.Core.Enums;

/// <summary>
/// Source kind that caused an entity or dependency to be identified — see skill.md §6.
/// </summary>
public enum EvidenceType
{
    Registry,
    FileSystem,
    Process,
    Command,
    ServiceConfiguration,
    IisConfiguration,
    SystemdConfiguration,
    PackageManager,
    NetworkSocket,
    EnvironmentVariable,
    ConfigurationFile,
    DockerInspect,
    ScheduledTask,
    CertificateStore,
    PeMetadata,
    ElfMetadata,
    KubernetesApi,

    /// <summary>Added Phase 6F — a DT_NEEDED entry in an ELF binary's dynamic section, citing
    /// the importing binary and the required shared library name.</summary>
    BinaryImport
}
