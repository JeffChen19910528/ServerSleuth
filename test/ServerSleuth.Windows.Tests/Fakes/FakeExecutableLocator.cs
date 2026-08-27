using ServerSleuth.Windows.Runtimes;

namespace ServerSleuth.Windows.Tests.Fakes;

internal sealed class FakeExecutableLocator(Dictionary<string, string?> byFileName) : IExecutableLocator
{
    public string? Locate(string fileName, IReadOnlyList<string> additionalDirectories) =>
        byFileName.GetValueOrDefault(fileName);
}
