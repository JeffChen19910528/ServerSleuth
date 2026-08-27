namespace ServerSleuth.Windows.Common;

/// <summary>
/// Reads a file's VERSION resource (FileVersion/ProductVersion/CompanyName/ProductName) —
/// never loads the file as code, never executes it. See skill.md §17/§19: this is "file
/// verification," not "DLL deep analysis" (explicitly out of scope, see §2) — architecture
/// (PE Machine-type) detection is deliberately not attempted here for that reason.
/// </summary>
public interface IFileVersionMetadataReader
{
    FileVersionMetadata? TryRead(string path);
}
