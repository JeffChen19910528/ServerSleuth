namespace ServerSleuth.Linux.Native;

/// <summary>
/// Parses ELF header/dynamic-section facts from already-read bytes — never opens, executes, or
/// disassembles a binary itself. Kept separate from dependency resolution (`ILibraryResolver`)
/// and entity creation (`LinuxNativeDependencyScanner`) per skill.md (Phase 6F) §3.
/// </summary>
public interface ILinuxElfParser
{
    ElfAnalysisResult Parse(ReadOnlySpan<byte> data);
}
