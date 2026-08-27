namespace ServerSleuth.Linux.Systemd;

/// <summary>Abstraction over systemd discovery, so `LinuxSystemdServiceScanner`'s mapping logic
/// is unit-testable via a fake without a real systemd instance.</summary>
public interface ISystemdProvider
{
    SystemdProbeResult GetSnapshot();
}
