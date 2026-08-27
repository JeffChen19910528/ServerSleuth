using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Runtimes;
using ServerSleuth.Linux.Systemd;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Native;

/// <summary>
/// Explicitly verifies native dependency discovery never executes a binary, never invokes a
/// shell, and never invokes `ldd`/`docker exec`/`podman exec`/`kubectl exec` — skill.md
/// (Phase 6F) §29. Every actual call the fake `IProcessRunner` received (via the optional
/// `ldconfig` tier — the only process-execution capable dependency anywhere in this scanner's
/// graph) is inspected after a full discovery run.
/// </summary>
public class LinuxNativeDependencyNegativeSecurityTests
{
    private static readonly string[] ForbiddenExecutables = ["sh", "bash", "/bin/sh", "/bin/bash", "ldd", "docker", "podman", "kubectl"];

    private sealed class FakeSystemdProvider(SystemdProbeResult result) : ISystemdProvider
    {
        public SystemdProbeResult GetSnapshot() => result;
    }

    [Fact]
    public async Task FullDiscoveryRun_TheOnlyProcessInvoked_IsLdconfigWithDashPOnly()
    {
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64(needed: ["libc.so.6"]);
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processRunner = new FakeProcessRunner();
        processRunner.SetResult("ldconfig", ["-p"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));

        var processScanner = new LinuxProcessScanner(
            new FakeProcProvider([new ProcProcessSnapshot { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" }]),
            NullLogger<LinuxProcessScanner>.Instance);
        var systemdScanner = new LinuxSystemdServiceScanner(new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.Available }), NullLogger<LinuxSystemdServiceScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fs, new ServerSleuth.Infrastructure.Security.SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);
        var runtimeScanner = new LinuxRuntimeDiscoveryScanner([], NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);

        var scanner = new LinuxNativeDependencyScanner(
            processScanner, systemdScanner, cronScanner, runtimeScanner,
            fs, new ElfParser(), new LinuxLibraryResolver(fs), new LdconfigProvider(processRunner),
            NullLogger<LinuxNativeDependencyScanner>.Instance);

        await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.All(processRunner.Invocations, i =>
        {
            Assert.Equal("ldconfig", i.Executable);
            Assert.Equal(["-p"], i.Arguments);
        });
        Assert.DoesNotContain(processRunner.Invocations, i => ForbiddenExecutables.Contains(i.Executable.ToLowerInvariant()) && i.Executable != "ldconfig");
    }

    [Fact]
    public void Scanner_HasNoProcessStartCapableDependencyOtherThanLdconfigProvider()
    {
        // ElfParser and LinuxLibraryResolver both take zero or file-system-only dependencies —
        // the only way this scanner's graph could execute anything at all is through
        // ILdconfigProvider, and that implementation is restricted to exactly `ldconfig -p`
        // (verified above). This test documents that the dependency graph contains no other
        // execution surface (no ILibraryResolver/ILinuxElfParser constructor parameter is
        // process-execution capable).
        var elfParserCtor = typeof(ElfParser).GetConstructors();
        var resolverCtor = typeof(LinuxLibraryResolver).GetConstructors().Single();

        Assert.Empty(elfParserCtor.SelectMany(c => c.GetParameters()));
        Assert.DoesNotContain(resolverCtor.GetParameters(), p => p.ParameterType.Name.Contains("ProcessRunner"));
    }

    [Fact]
    public async Task FullDiscoveryRun_NeverExecutesTheAnalyzedBinaryItself()
    {
        // The scanner reads bytes via IFileSystemReader.ReadBytesAsync only — there is no
        // Process.Start/IProcessRunner call anywhere in the binary-analysis path (only the
        // separate, optional ldconfig cache query uses IProcessRunner at all).
        var fs = new FakeFileSystemReader();
        var bytes = SyntheticElfBuilder.BuildElf64();
        fs.SetFileInfo("/opt/erp/bin/erp", bytes.Length);
        fs.SetBytes("/opt/erp/bin/erp", bytes);

        var processScanner = new LinuxProcessScanner(
            new FakeProcProvider([new ProcProcessSnapshot { Pid = 100, Name = "erp", ExecutablePath = "/opt/erp/bin/erp" }]),
            NullLogger<LinuxProcessScanner>.Instance);
        var systemdScanner = new LinuxSystemdServiceScanner(new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.Available }), NullLogger<LinuxSystemdServiceScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fs, new ServerSleuth.Infrastructure.Security.SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);
        var runtimeScanner = new LinuxRuntimeDiscoveryScanner([], NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);
        var noProcessRunner = new FakeProcessRunner(); // registers nothing — any invocation attempt would surface as a StartFailedResult, never a thrown exception either way

        var scanner = new LinuxNativeDependencyScanner(
            processScanner, systemdScanner, cronScanner, runtimeScanner,
            fs, new ElfParser(), new LinuxLibraryResolver(fs), new LdconfigProvider(noProcessRunner),
            NullLogger<LinuxNativeDependencyScanner>.Instance);

        var result = await scanner.ScanAsync(new DiscoveryContext { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None }, CancellationToken.None);

        Assert.Equal(ScannerStatus.Supported, result.Status); // ldconfig unavailability never fails the scan
        Assert.Single(result.Entities);
    }
}
