using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Reporting.Tests.Fixtures;

/// <summary>Trimmed copy of <c>ServerSleuth.Analysis.Tests.Fixtures.EntityFactory</c> — kept
/// local rather than referenced across test projects (no precedent for test-to-test project
/// references in this repo), covering only the entity kinds Reporting's own fixtures need.</summary>
public static class EntityFactory
{
    public static WebSite Site(string name, string? physicalPath = null) => new()
    {
        Id = $"iis-site:{name}",
        Name = name,
        Type = "WebSite",
        Source = "IisConfiguration",
        Status = EntityStatus.Running,
        Confidence = Confidence.VeryHigh(),
        PhysicalPath = physicalPath
    };

    public static Application Application(string siteName, string virtualPath, string physicalPath, string? poolId = null, string? siteId = null)
    {
        var componentIds = new List<string> { siteId ?? $"iis-site:{siteName}" };
        if (poolId is not null) componentIds.Add(poolId);

        return new Application
        {
            Id = $"iis-application:{siteName}:{virtualPath}",
            Name = virtualPath == "/" ? siteName : $"{siteName}{virtualPath}",
            Type = "Application",
            Source = "IisConfiguration",
            Status = EntityStatus.Configured,
            Confidence = Confidence.VeryHigh(),
            Path = physicalPath,
            ComponentEntityIds = componentIds
        };
    }

    public static ApplicationPool ApplicationPool(string name) => new()
    {
        Id = $"iis-apppool:{name}",
        Name = name,
        Type = "ApplicationPool",
        Source = "IisConfiguration",
        Status = EntityStatus.Running,
        Confidence = Confidence.VeryHigh()
    };

    public static Configuration Configuration(string path, string? ownerEntityId = null, IReadOnlyList<string>? dependencyReferences = null)
    {
        var entity = new Configuration
        {
            Id = $"configuration:{path}",
            Name = System.IO.Path.GetFileName(path),
            Type = "Configuration",
            Source = "FileSystem",
            Status = EntityStatus.Configured,
            Confidence = Confidence.High(),
            Path = path,
            DetectedDependencyReferences = dependencyReferences ?? []
        };

        if (ownerEntityId is not null)
        {
            entity.SetMetadata("OwnerEntityId", ownerEntityId);
        }

        return entity;
    }

    public static Dll Dll(string path, IReadOnlyList<string>? referencedBy = null, string? importsCsv = null, bool notFound = false)
    {
        var entity = new Dll
        {
            Id = $"dll:{path}",
            Name = System.IO.Path.GetFileName(path),
            Type = "NativeDll",
            Source = "FileSystem",
            Status = notFound ? EntityStatus.Unknown : EntityStatus.Referenced,
            Confidence = Confidence.High(),
            Path = path,
            ReferencedByEntityIds = referencedBy ?? []
        };

        if (importsCsv is not null)
        {
            entity.SetMetadata("Imports", importsCsv);
        }

        entity.SetMetadata("FileStatus", notFound ? "NotFound" : "Found");

        return entity;
    }

    public static Service Service(string name, string? executablePath) => new()
    {
        Id = $"service:{name}",
        Name = name,
        Type = "Service",
        Source = "ServiceControlManager",
        Status = EntityStatus.Running,
        Confidence = Confidence.VeryHigh(),
        ExecutablePath = executablePath
    };

    public static ScheduledTask ScheduledTask(string path, string? action) => new()
    {
        Id = $"scheduledtask:{path}",
        Name = System.IO.Path.GetFileName(path),
        Type = "ScheduledTask",
        Source = "WindowsTaskScheduler",
        Status = EntityStatus.Configured,
        Confidence = Confidence.VeryHigh(),
        Action = action,
        Enabled = true
    };

    public static Certificate Certificate(string label, string thumbprint, DateTimeOffset? validTo = null, DateTimeOffset? validFrom = null) => new()
    {
        Id = $"cert:{label}:{thumbprint}",
        Name = thumbprint,
        Type = "Certificate",
        Source = "WindowsCertificateStore",
        Status = EntityStatus.Installed,
        Confidence = Confidence.VeryHigh(),
        Thumbprint = thumbprint,
        Subject = $"CN={label}",
        ValidTo = validTo,
        ValidFrom = validFrom
    };

    public static Runtime Runtime(string family, string name, string? version = null)
    {
        var entity = new Runtime
        {
            Id = $"runtime:{family}:{name}:{version ?? "unknown-version"}:none",
            Name = name,
            Type = family,
            Source = "Registry",
            Status = EntityStatus.Installed,
            Confidence = Confidence.VeryHigh(),
            Version = version
        };

        entity.SetMetadata("Family", family);
        return entity;
    }

    public static void SetBinding(WebSite site, int index, string thumbprint) =>
        site.SetMetadata($"Binding{index}.CertificateThumbprint", thumbprint);
}
