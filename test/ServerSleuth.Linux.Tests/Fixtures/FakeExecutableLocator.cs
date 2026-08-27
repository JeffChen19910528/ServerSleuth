using ServerSleuth.Linux.Runtimes;

namespace ServerSleuth.Linux.Tests.Fixtures;

public sealed class FakeExecutableLocator : IExecutableLocator
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);

    public void SetPath(string fileName, string path) => _paths[fileName] = path;

    public string? Locate(string fileName, IReadOnlyList<string> additionalDirectories) =>
        _paths.GetValueOrDefault(fileName);
}
