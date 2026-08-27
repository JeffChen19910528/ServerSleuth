namespace ServerSleuth.Windows.Runtimes;

/// <summary>
/// Resolves an executable's full path via PATH and a set of known install directories — never
/// by shelling out to `where.exe`, and never by recursively scanning the whole filesystem
/// (skill.md §14, §31). Shared across every detector so PATH-resolution logic exists once.
/// </summary>
public interface IExecutableLocator
{
    string? Locate(string fileName, IReadOnlyList<string> additionalDirectories);
}
