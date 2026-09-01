namespace ServerSleuth.Reporting.Json.Dto;

/// <summary>
/// GUI-8C: A flattened, serialization-safe projection of a single discovered entity —
/// covers all entity types via optional fields so one generic DTO serves every inventory
/// section without a separate class per entity kind. <c>EntityType</c> is the
/// discriminator; the HTML renderer reads only the fields relevant to that type.
/// No credential-shaped fields: <c>SecretDetected</c> from <c>Configuration</c> is
/// deliberately excluded (same rationale as <c>ConfigurationComponentRow</c> in the GUI).
/// </summary>
public sealed record InventoryEntityDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string EntityType { get; init; }
    public string? Version { get; init; }
    public string? Architecture { get; init; }
    public string? Path { get; init; }
    public string? Status { get; init; }
    public string? Publisher { get; init; }
    public string? ApplicationName { get; init; }

    // Service-specific
    public string? DisplayName { get; init; }
    public string? StartType { get; init; }
    public string? ServiceAccount { get; init; }
    public string? ExecutablePath { get; init; }

    // COM-specific
    public string? Clsid { get; init; }
    public string? ProgId { get; init; }
    public string? InprocServer32 { get; init; }
    public string? ThreadingModel { get; init; }

    // Certificate-specific
    public string? Subject { get; init; }
    public string? Issuer { get; init; }
    public string? Thumbprint { get; init; }
    public string? ValidFrom { get; init; }
    public string? ValidTo { get; init; }

    // ScheduledTask-specific
    public string? Folder { get; init; }
    public string? Trigger { get; init; }
    public string? TaskAction { get; init; }
    public string? RunAsAccount { get; init; }
    public string? Enabled { get; init; }

    // Software-specific
    public string? InstallLocation { get; init; }
    public string? InstallDate { get; init; }

    // Configuration-specific
    public string? Format { get; init; }

    // ExternalDependency-specific
    public string? Kind { get; init; }
    public string? Endpoint { get; init; }
}
