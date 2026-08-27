namespace ServerSleuth.Linux.Packages;

/// <summary>One provider per package manager — never one scanner with a hard-coded if/else per
/// distribution (mirrors the Windows Phase 4D IRuntimeDetector-per-family architecture). See
/// skill.md (Phase 6B) §2-3.</summary>
public interface IPackageManagerProvider
{
    string PackageManagerName { get; }

    Task<PackageQueryResult> QueryInstalledPackagesAsync(CancellationToken cancellationToken);
}
