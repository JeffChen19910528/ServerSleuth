using ServerSleuth.Infrastructure.Runtimes;
using ServerSleuth.Windows.Runtimes;

namespace ServerSleuth.Windows.Runtimes.Detectors;

/// <summary>Shared `dotnet` executable resolution for the runtime and SDK detectors — avoids
/// duplicating this logic in both. Output-line parsing itself is platform-neutral and lives in
/// <see cref="DotNetCliOutputParser"/> (Infrastructure), reused by Linux's dotnet detectors too.</summary>
internal static class DotNetCliLocator
{
    public static readonly IReadOnlyList<string> KnownDirectories =
    [
        @"C:\Program Files\dotnet",
        @"C:\Program Files (x86)\dotnet"
    ];

    public static string? Locate(IExecutableLocator locator) => locator.Locate("dotnet.exe", KnownDirectories);

    public static (string? Name, string? Version, string? Path) ParseLine(string line) => DotNetCliOutputParser.ParseLine(line);
}
