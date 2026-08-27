namespace ServerSleuth.Linux.Native;

public enum NativeBinaryFileStatus
{
    Found,
    NotFound,
    AccessDenied,

    /// <summary>The file exists and is readable but exceeds the size bound this scanner analyzes
    /// — see skill.md (Phase 6F) §27 (bounded performance). The binary is still recorded as an
    /// entity; only ELF analysis is skipped.</summary>
    SkippedTooLarge
}
