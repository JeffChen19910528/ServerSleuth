namespace ServerSleuth.Core.Enums;

public enum EntityArchitecture
{
    Unknown,
    X86,
    X64,
    Arm,
    Arm64,
    AnyCpu,

    /// <summary>Added Phase 6F for ELF `e_machine == EM_RISCV` binaries.</summary>
    RiscV
}
