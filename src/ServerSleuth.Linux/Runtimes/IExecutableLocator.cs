namespace ServerSleuth.Linux.Runtimes;

/// <summary>
/// Resolves an executable's full path via PATH and a set of known install directories — never
/// by shelling out to `which`, and never by recursively scanning the whole filesystem (mirrors
/// the Windows Phase 4D `IExecutableLocator` contract, but with Linux PATH semantics: ':'
/// separator, no file-extension matching).
/// </summary>
public interface IExecutableLocator
{
    string? Locate(string fileName, IReadOnlyList<string> additionalDirectories);
}
