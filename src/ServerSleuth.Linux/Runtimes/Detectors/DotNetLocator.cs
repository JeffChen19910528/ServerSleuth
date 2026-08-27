namespace ServerSleuth.Linux.Runtimes.Detectors;

/// <summary>Shared `dotnet` executable resolution for the Linux runtime/SDK detectors.</summary>
internal static class DotNetLocator
{
    public static readonly IReadOnlyList<string> KnownDirectories =
    [
        "/usr/share/dotnet",
        "/usr/lib/dotnet",
        "/opt/dotnet"
    ];

    public static string? Locate(IExecutableLocator locator) => locator.Locate("dotnet", KnownDirectories);
}
