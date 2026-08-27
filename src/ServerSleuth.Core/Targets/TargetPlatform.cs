namespace ServerSleuth.Core.Targets;

/// <summary>
/// What is actually KNOWN about a <see cref="ScanTarget"/>'s operating system — never the
/// result of probing a remote machine (skill.md Phase 10C §9 explicitly forbids that in this
/// phase). For a local target this is resolved once, cheaply, from the current process's own
/// runtime (<c>OperatingSystem.IsWindows()</c>/<c>IsLinux()</c>) — not a network operation.
/// <see cref="Unknown"/> is the correct, honest value for anything not yet determined this way.
/// Deliberately distinct from <see cref="ServerSleuth.Core.Enums.PlatformSupport"/>, which
/// describes what platforms a SCANNER supports — this describes what a TARGET actually is.
/// </summary>
public enum TargetPlatform
{
    Unknown,
    Windows,
    Linux
}
