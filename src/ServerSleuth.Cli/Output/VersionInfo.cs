using System.Reflection;

namespace ServerSleuth.Cli.Output;

/// <summary>Single source of truth for the CLI's own version — see skill.md (Phase 10A) §14:
/// "Do not hard-code a version string in multiple locations." Reads the assembly version, itself
/// generated once from `ServerSleuth.Cli.csproj`'s own `&lt;Version&gt;` element — never a second,
/// separately-maintained string.</summary>
internal static class VersionInfo
{
    public static string Version => typeof(VersionInfo).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
