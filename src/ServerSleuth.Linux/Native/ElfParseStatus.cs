namespace ServerSleuth.Linux.Native;

/// <summary>Outcome of parsing one ELF file's bytes — see skill.md (Phase 6F) §4-6. A single
/// malformed/truncated/unsupported binary must never abort the whole scan; it degrades to one
/// of these instead.</summary>
public enum ElfParseStatus
{
    Parsed,

    /// <summary>The ELF header parsed, but the dynamic section (or its string table) could not
    /// be fully read — dependency information may be incomplete.</summary>
    PartiallyParsed,

    /// <summary>A valid little-endian ELF header was read, but the file is big-endian —
    /// deliberately not parsed further rather than guessed at. See skill.md §6.</summary>
    UnsupportedEndian,

    /// <summary>The file does not start with the ELF magic number (0x7F 'E' 'L' 'F').</summary>
    NotAnElf,

    /// <summary>The file is shorter than a valid ELF header/program header table requires.</summary>
    Truncated,

    /// <summary>The file has the ELF magic number but internally inconsistent structure
    /// (e.g. a program header offset pointing past end of file).</summary>
    MalformedElf
}
