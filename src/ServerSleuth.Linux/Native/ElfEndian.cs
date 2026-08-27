namespace ServerSleuth.Linux.Native;

/// <summary>ELF byte order, from `e_ident[EI_DATA]` — see skill.md (Phase 6F) §6. Only
/// little-endian ELF is fully parsed; big-endian is recognized but never guessed at further.</summary>
public enum ElfEndian
{
    Unknown,
    Little,
    Big
}
