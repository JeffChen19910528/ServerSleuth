using ServerSleuth.Reporting.Json.Dto;

namespace ServerSleuth.Reporting.Html;

/// <summary>
/// Classifies an already-mapped <see cref="InventoryEntityDto"/> as System / Third-Party /
/// Business / Custom / Unknown for the Server Deployment Inventory report — see the report
/// redesign plan §1. Deliberately never uses "Publisher == Microsoft" as a standalone rule:
/// Microsoft SQL Server / SSMS are real deployed tools and must survive classification, while
/// wuauserv / BITS / TrustedInstaller must not. Combines path-under-Windows-root, scheduled-task
/// folder convention, built-in service accounts, OS-component name patterns, and application
/// boundary membership (already resolved onto <see cref="InventoryEntityDto.ApplicationName"/>).
/// </summary>
internal static class DeploymentClassifier
{
    private static readonly string[] WindowsRootPrefixes =
    [
        @"C:\Windows\", @"%SystemRoot%\", @"%WinDir%\", @"C:\WINDOWS\"
    ];

    private static readonly string[] StandardVendorRoots =
    [
        @"\Program Files\", @"\Program Files (x86)\", @"\ProgramData\", @"\Windows\"
    ];

    private static readonly string[] BuiltInServiceAccounts =
    [
        "LocalSystem", "NT AUTHORITY\\SYSTEM", "NT AUTHORITY\\LocalService",
        "NT AUTHORITY\\NetworkService", "NT AUTHORITY\\LOCAL SERVICE", "NT AUTHORITY\\NETWORK SERVICE"
    ];

    // Name-pattern signals for OS-shipped software packages that are NOT under the Windows
    // root themselves (e.g. .NET runtime packages, VC++ redistributables) — only applied
    // together with a Microsoft-family publisher, never on name or publisher alone.
    private static readonly string[] OsComponentNameKeywords =
    [
        "Update for", "Security Update", "Language Pack", "Windows Driver",
        ".NET Framework", "Microsoft .NET Runtime", "Microsoft .NET Host",
        "Microsoft Windows Desktop Runtime", "Microsoft ASP.NET Core",
        "Visual C++ 20", "Redistributable", "Windows SDK", "Update Health Tools",
        "Hotfix", "Servicing Stack Update"
    ];

    public static DeploymentClassification Classify(InventoryEntityDto e)
    {
        if (IsSystem(e)) return DeploymentClassification.System;

        var linked = !string.IsNullOrEmpty(e.ApplicationName);
        var primaryPath = e.ExecutablePath ?? e.InstallLocation ?? e.InprocServer32 ?? e.TaskAction ?? e.Path;

        if (linked)
        {
            return IsUnderStandardVendorRoot(primaryPath)
                ? DeploymentClassification.Business
                : DeploymentClassification.Custom;
        }

        if (!string.IsNullOrWhiteSpace(e.Publisher))
        {
            return DeploymentClassification.ThirdParty;
        }

        return DeploymentClassification.Unknown;
    }

    public static bool IsSystem(InventoryEntityDto e)
    {
        // Scheduled tasks: Windows ships everything under \Microsoft\... by convention.
        if (!string.IsNullOrEmpty(e.Folder) &&
            e.Folder.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var primaryPath = e.ExecutablePath ?? e.InstallLocation ?? e.InprocServer32 ?? e.TaskAction ?? e.Path;
        if (IsUnderWindowsRoot(primaryPath)) return true;

        // Services: a built-in logon account alone isn't sufficient (custom services can also
        // run as LocalSystem) — require it alongside a Windows-rooted or absent path signal.
        if (e.EntityType == "Service" &&
            !string.IsNullOrEmpty(e.ServiceAccount) &&
            BuiltInServiceAccounts.Contains(e.ServiceAccount, StringComparer.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(primaryPath))
        {
            return true;
        }

        // Software: OS-shipped packages not physically under the Windows root (e.g. .NET
        // runtime, VC++ redistributables) — combined signal only, never Publisher alone.
        if (e.EntityType == "Software" &&
            IsMicrosoftFamilyPublisher(e.Publisher) &&
            OsComponentNameKeywords.Any(k => e.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsMicrosoftFamilyPublisher(string? publisher) =>
        !string.IsNullOrEmpty(publisher) &&
        publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    /// <summary>Exposed for callers working directly with raw discovery entities (e.g. building
    /// the Applications list from <see cref="ServerSleuth.Core.Boundaries.ApplicationBoundary"/>
    /// anchors) rather than the mapped <see cref="InventoryEntityDto"/> shape.</summary>
    internal static bool IsSystemPath(string? path) => IsUnderWindowsRoot(path);

    private static bool IsUnderWindowsRoot(string? path) =>
        !string.IsNullOrEmpty(path) &&
        (WindowsRootPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
         || path.Contains(@"\WinSxS\", StringComparison.OrdinalIgnoreCase));

    private static bool IsUnderStandardVendorRoot(string? path) =>
        !string.IsNullOrEmpty(path) &&
        StandardVendorRoots.Any(root => path.Contains(root, StringComparison.OrdinalIgnoreCase));
}
