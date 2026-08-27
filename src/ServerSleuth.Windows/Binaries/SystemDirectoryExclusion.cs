namespace ServerSleuth.Windows.Binaries;

/// <summary>
/// Excludes OS-owned system directories (%windir% and everything under it — System32,
/// SysWOW64, WinSxS, assembly, etc.) from becoming full scan roots. These directories hold
/// thousands of files each and are not "application" directories in any migration-relevant
/// sense — walking all of System32 because one COM registration or service happens to point
/// into it produces tens of thousands of irrelevant OS-shipped binaries and makes discovery
/// unbounded in practice (measured: ~30,000 binaries, ~6.5 minutes on a real dev machine
/// before this exclusion existed). The *specific* referenced file is still checked directly
/// (skill.md §16) — only the "also scan everything else in this directory" behavior is
/// skipped for these paths.
/// </summary>
public static class SystemDirectoryExclusion
{
    private static readonly string WindowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public static bool IsSystemOwned(string path) =>
        WindowsDirectory.Length > 0 &&
        path.StartsWith(WindowsDirectory, StringComparison.OrdinalIgnoreCase);
}
