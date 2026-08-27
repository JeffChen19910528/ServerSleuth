namespace ServerSleuth.Windows.Binaries;

/// <summary>Shared bounds so no single pathological application tree can make discovery
/// unbounded — see skill.md §21-22.</summary>
public static class BinaryDiscoveryDefaults
{
    public const int MaxDirectoryDepth = 8;
    public const int MaxFilesPerRoot = 10_000;

    public static readonly IReadOnlyList<string> SearchPatterns = ["*.dll", "*.exe", "*.ocx"];
}
