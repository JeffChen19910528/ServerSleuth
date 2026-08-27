using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Configuration;
using ServerSleuth.Linux.Containers;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Kubernetes;
using ServerSleuth.Linux.Native;
using ServerSleuth.Linux.Networking;
using ServerSleuth.Linux.OperatingSystem;
using ServerSleuth.Linux.Packages;
using ServerSleuth.Linux.Process;
using ServerSleuth.Linux.Runtimes;
using ServerSleuth.Linux.Runtimes.Detectors;
using ServerSleuth.Linux.Systemd;
using Xunit.Abstractions;

namespace ServerSleuth.Linux.Tests.Integration;

/// <summary>
/// Runs all eleven Phase 6A/6B/6C/6D/6E/6F scanners against the real machine — but only when
/// actually executing on Linux. See skill.md (Phase 6A §18 / Phase 6B §24 / Phase 6C §25 /
/// Phase 6D §26 / Phase 6E §33 / Phase 6F §30): a Windows development host must never fake a
/// "real Linux" result. This test explicitly reports non-execution rather than silently
/// passing, so nobody reading the test output mistakes "skipped" for "validated."
/// </summary>
public class LinuxRealMachineSmokeTest(ITestOutputHelper output)
{
    [Fact]
    public async Task AllElevenScanners_AgainstRealMachine_WhenRunningOnLinux()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            output.WriteLine("Linux real-machine smoke test not executed because development host is Windows.");
            output.WriteLine("Fixture/fake coverage for Phase 6A/6B/6C/6D/6E/6F is provided by the other test classes in this project instead.");
            return;
        }

        var fileSystemReader = new FileSystemReader();
        var processRunner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var secretRedactor = new SecretRedactor();
        var executableLocator = new LinuxExecutableLocator(fileSystemReader);

        var osScanner = new LinuxOsScanner(fileSystemReader, processRunner, NullLogger<LinuxOsScanner>.Instance);
        var processScanner = new LinuxProcessScanner(new LinuxProcProvider(fileSystemReader), NullLogger<LinuxProcessScanner>.Instance);
        var portScanner = new LinuxPortScanner(
            new LinuxPortInspector(fileSystemReader, new SocketOwnershipResolver(fileSystemReader)),
            NullLogger<LinuxPortScanner>.Instance);
        var systemdScanner = new LinuxSystemdServiceScanner(new SystemctlProvider(processRunner), NullLogger<LinuxSystemdServiceScanner>.Instance);
        var packageScanner = new LinuxPackageScanner(
            [new DpkgPackageProvider(processRunner), new RpmPackageProvider(processRunner), new ApkPackageProvider(processRunner)],
            NullLogger<LinuxPackageScanner>.Instance);
        var runtimeScanner = new LinuxRuntimeDiscoveryScanner(
            [
                new DotNetRuntimeDetector(executableLocator, processRunner),
                new DotNetSdkDetector(executableLocator, processRunner),
                new JavaDetector(executableLocator, processRunner, fileSystemReader),
                new PythonDetector(executableLocator, processRunner, fileSystemReader),
                new NodeDetector(executableLocator, processRunner),
                new PhpDetector(executableLocator, processRunner),
                new GoDetector(executableLocator, processRunner, secretRedactor)
            ],
            NullLogger<LinuxRuntimeDiscoveryScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fileSystemReader, secretRedactor, NullLogger<LinuxScheduledTaskScanner>.Instance);
        var containerScanner = new LinuxContainerScanner(
            [new DockerContainerRuntimeProvider(processRunner), new PodmanContainerRuntimeProvider(processRunner)],
            secretRedactor,
            NullLogger<LinuxContainerScanner>.Instance);
        var kubernetesScanner = new LinuxKubernetesScanner(
            new KubectlKubernetesProvider(processRunner),
            secretRedactor,
            NullLogger<LinuxKubernetesScanner>.Instance);
        var configurationScanner = new LinuxConfigurationScanner(
            systemdScanner,
            cronScanner,
            fileSystemReader,
            secretRedactor,
            NullLogger<LinuxConfigurationScanner>.Instance);
        var nativeDependencyScanner = new LinuxNativeDependencyScanner(
            processScanner,
            systemdScanner,
            cronScanner,
            runtimeScanner,
            fileSystemReader,
            new ElfParser(),
            new LinuxLibraryResolver(fileSystemReader),
            new LdconfigProvider(processRunner),
            NullLogger<LinuxNativeDependencyScanner>.Instance);

        var context = new DiscoveryContext { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None };

        var osResult = await osScanner.ScanAsync(context, CancellationToken.None);
        var processResult = await processScanner.ScanAsync(context, CancellationToken.None);
        var portResult = await portScanner.ScanAsync(context, CancellationToken.None);
        var systemdResult = await systemdScanner.ScanAsync(context, CancellationToken.None);
        var packageResult = await packageScanner.ScanAsync(context, CancellationToken.None);
        var runtimeResult = await runtimeScanner.ScanAsync(context, CancellationToken.None);
        var cronResult = await cronScanner.ScanAsync(context, CancellationToken.None);
        var containerResult = await containerScanner.ScanAsync(context, CancellationToken.None);
        var kubernetesResult = await kubernetesScanner.ScanAsync(context, CancellationToken.None);
        var configurationResult = await configurationScanner.ScanAsync(context, CancellationToken.None);
        var nativeDependencyResult = await nativeDependencyScanner.ScanAsync(context, CancellationToken.None);

        output.WriteLine($"OS: {osResult.Status}, {osResult.Entities.Count} entities");
        output.WriteLine($"Processes: {processResult.Status}, {processResult.Entities.Count} entities");
        output.WriteLine($"Listening Ports: {portResult.Status}, {portResult.Entities.Count} entities");
        output.WriteLine($"systemd Services: {systemdResult.Status}, {systemdResult.Entities.Count} entities");
        output.WriteLine($"Packages: {packageResult.Status}, {packageResult.Entities.Count} entities");
        output.WriteLine($"Runtimes/SDKs: {runtimeResult.Status}, {runtimeResult.Entities.Count} entities");
        output.WriteLine($"Cron Jobs: {cronResult.Status}, {cronResult.Entities.Count} entities");
        output.WriteLine($"Containers (Docker+Podman): {containerResult.Status}, {containerResult.Entities.Count} entities");
        output.WriteLine($"Kubernetes: {kubernetesResult.Status}, {kubernetesResult.Entities.Count} entities");
        output.WriteLine($"Configuration: {configurationResult.Status}, {configurationResult.Entities.Count} entities");
        output.WriteLine($"Native Dependencies: {nativeDependencyResult.Status}, {nativeDependencyResult.Entities.Count} entities");

        Assert.NotEmpty(osResult.Entities);
        Assert.NotEmpty(processResult.Entities); // at minimum PID 1 and this test's own process
    }
}
