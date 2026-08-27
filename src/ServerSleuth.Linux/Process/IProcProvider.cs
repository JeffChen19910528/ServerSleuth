namespace ServerSleuth.Linux.Process;

/// <summary>Abstraction over `/proc/&lt;pid&gt;/*` enumeration, so `LinuxProcessScanner`'s
/// mapping logic is unit-testable via a fake without a real Linux `/proc` filesystem.</summary>
public interface IProcProvider
{
    IReadOnlyList<ProcProcessSnapshot> GetProcessSnapshots();
}
