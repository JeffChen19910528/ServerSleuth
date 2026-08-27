namespace ServerSleuth.Linux.Native;

/// <summary>ELF file class (32-bit vs 64-bit), from `e_ident[EI_CLASS]` — see skill.md
/// (Phase 6F) §4-5.</summary>
public enum ElfClass
{
    Unknown,
    Elf32,
    Elf64
}
