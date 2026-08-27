using ServerSleuth.Infrastructure.Runtimes;
using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Windows.Binaries;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.COM;
using ServerSleuth.Windows.Common;
using ServerSleuth.Windows.Configuration;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Networking;
using ServerSleuth.Windows.OperatingSystem;
using ServerSleuth.Windows.Process;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Runtimes;
using ServerSleuth.Windows.Runtimes.Detectors;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;
using ServerSleuth.Windows.Software;
using Xunit.Abstractions;
using CoreOperatingSystem = ServerSleuth.Core.Models.OperatingSystem;

namespace ServerSleuth.Windows.Tests.Integration;

/// <summary>
/// Not the final CLI (Phase 9) and not an HTML report (Phase 8) — this is the minimal
/// diagnostic entry point skill.md §28 asks for: confirmation that ServerSleuth's Windows
/// discovery engine can actually read the machine it runs on. Run it directly with
/// `dotnet test --filter WindowsDiscoverySmokeTest` to see the summary.
/// </summary>
public class WindowsDiscoverySmokeTest(ITestOutputHelper output)
{
    [Fact]
    public async Task RunAllWindowsScanners_AgainstCurrentMachine_AndPrintSummary()
    {
        var context = new DiscoveryContext { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None };
        var registryReader = new WindowsRegistryReader();

        var osResult = await new WindowsOsScanner(registryReader, NullLogger<WindowsOsScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var processResult = await new WindowsProcessScanner(new ProcessEnumerator(), new ProcessWmiProvider(NullLogger<ProcessWmiProvider>.Instance), NullLogger<WindowsProcessScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var portInspector = new WindowsPortInspector(new NetworkTableProvider(NullLogger<NetworkTableProvider>.Instance), new ProcessNameResolver());
        var portResult = await new WindowsPortScanner(portInspector, NullLogger<WindowsPortScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var serviceResult = await new WindowsServiceScanner(new ServiceEnumerator(), registryReader, NullLogger<WindowsServiceScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var softwareResult = await new WindowsInstalledSoftwareScanner(registryReader, NullLogger<WindowsInstalledSoftwareScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var iisResult = await new IisScanner(new IisConfigurationProvider(NullLogger<IisConfigurationProvider>.Instance), new FileSystemReader(), NullLogger<IisScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var comResult = await new WindowsComScanner(registryReader, new FileSystemReader(), new FileVersionMetadataReader(), new SecretRedactor(), NullLogger<WindowsComScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var taskResult = await new WindowsScheduledTaskScanner(new TaskSchedulerProvider(NullLogger<TaskSchedulerProvider>.Instance), new FileSystemReader(), new SecretRedactor(), NullLogger<WindowsScheduledTaskScanner>.Instance).ScanAsync(context, CancellationToken.None);
        var certResult = await new WindowsCertificateScanner(new CertificateStoreProvider(NullLogger<CertificateStoreProvider>.Instance), NullLogger<WindowsCertificateScanner>.Instance).ScanAsync(context, CancellationToken.None);

        var fileSystemReader = new FileSystemReader();
        var processRunner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var executableLocator = new ExecutableLocator(fileSystemReader);
        var secretRedactor = new SecretRedactor();
        IRuntimeDetector[] runtimeDetectors =
        [
            new DotNetFrameworkDetector(registryReader),
            new DotNetRuntimeDetector(executableLocator, processRunner),
            new DotNetSdkDetector(executableLocator, processRunner),
            new JavaDetector(registryReader, executableLocator, processRunner, fileSystemReader),
            new PythonDetector(executableLocator, processRunner, fileSystemReader),
            new NodeDetector(executableLocator, processRunner),
            new PhpDetector(executableLocator, processRunner),
            new GoDetector(executableLocator, processRunner, secretRedactor)
        ];
        var runtimeResult = await new RuntimeDiscoveryScanner(runtimeDetectors, NullLogger<RuntimeDiscoveryScanner>.Instance).ScanAsync(context, CancellationToken.None);

        var configIisScanner = new IisScanner(new IisConfigurationProvider(NullLogger<IisConfigurationProvider>.Instance), fileSystemReader, NullLogger<IisScanner>.Instance);
        var configServiceScanner = new WindowsServiceScanner(new ServiceEnumerator(), registryReader, NullLogger<WindowsServiceScanner>.Instance);
        var configTaskScanner = new WindowsScheduledTaskScanner(new TaskSchedulerProvider(NullLogger<TaskSchedulerProvider>.Instance), fileSystemReader, secretRedactor, NullLogger<WindowsScheduledTaskScanner>.Instance);
        var configResult = await new WindowsConfigurationScanner(configIisScanner, configServiceScanner, configTaskScanner, fileSystemReader, secretRedactor, NullLogger<WindowsConfigurationScanner>.Instance).ScanAsync(context, CancellationToken.None);

        var binaryComScanner = new WindowsComScanner(registryReader, fileSystemReader, new FileVersionMetadataReader(), secretRedactor, NullLogger<WindowsComScanner>.Instance);
        var binaryResult = await new WindowsBinaryDiscoveryScanner(configIisScanner, configServiceScanner, configTaskScanner, binaryComScanner, fileSystemReader, new FileVersionMetadataReader(), new PeAnalyzer(), secretRedactor, NullLogger<WindowsBinaryDiscoveryScanner>.Instance).ScanAsync(context, CancellationToken.None);

        var os = osResult.Entities.OfType<CoreOperatingSystem>().Single();

        output.WriteLine("ServerSleuth Windows Discovery");
        output.WriteLine("");
        output.WriteLine($"OS:");
        output.WriteLine($"{os.Name} ({os.Architecture})");
        output.WriteLine("");
        output.WriteLine($"Processes:");
        output.WriteLine($"{processResult.Entities.Count} ({processResult.Status})");
        output.WriteLine("");
        output.WriteLine($"Services:");
        output.WriteLine($"{serviceResult.Entities.Count} ({serviceResult.Status})");
        output.WriteLine("");
        output.WriteLine($"Listening Ports:");
        output.WriteLine($"{portResult.Entities.Count} ({portResult.Status})");
        output.WriteLine("");
        output.WriteLine($"Installed Software:");
        output.WriteLine($"{softwareResult.Entities.Count} ({softwareResult.Status})");
        output.WriteLine("");
        output.WriteLine($"IIS Installed:");
        output.WriteLine(iisResult.Status == ScannerStatus.NotInstalled ? "No" : "Yes");
        output.WriteLine("");
        output.WriteLine($"IIS Status:");
        output.WriteLine($"{iisResult.Status}");
        output.WriteLine("");
        output.WriteLine($"Sites:");
        output.WriteLine($"{iisResult.Entities.OfType<Core.Models.WebSite>().Count()}");
        output.WriteLine("");
        output.WriteLine($"Applications:");
        output.WriteLine($"{iisResult.Entities.OfType<Core.Models.Application>().Count()}");
        output.WriteLine("");
        output.WriteLine($"Application Pools:");
        output.WriteLine($"{iisResult.Entities.OfType<Core.Models.ApplicationPool>().Count()}");
        output.WriteLine("");

        var comComponents = comResult.Entities.Cast<Core.Models.ComComponent>().ToList();
        var byView = comComponents.GroupBy(c => c.Metadata["RegistryView"]).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine($"Windows COM Discovery");
        output.WriteLine("");
        output.WriteLine($"Registry64:");
        output.WriteLine($"{byView.GetValueOrDefault("Registry64")}");
        output.WriteLine("");
        output.WriteLine($"Registry32:");
        output.WriteLine($"{byView.GetValueOrDefault("Registry32")}");
        output.WriteLine("");
        output.WriteLine($"HKCU:");
        output.WriteLine($"{byView.GetValueOrDefault("Default")}");
        output.WriteLine("");
        output.WriteLine($"Total:");
        output.WriteLine($"{comComponents.Count}");
        output.WriteLine("");
        output.WriteLine($"Missing Server Files:");
        output.WriteLine($"{comComponents.Count(c => c.Metadata.GetValueOrDefault("ServerPathStatus") == "NotFound")}");
        output.WriteLine("");
        output.WriteLine($"AccessDenied:");
        output.WriteLine($"{comComponents.Count(c => c.Metadata.GetValueOrDefault("ServerPathStatus") == "AccessDenied")}");
        output.WriteLine("");
        output.WriteLine($"Status:");
        output.WriteLine($"{comResult.Status}");
        output.WriteLine("");

        var tasks = taskResult.Entities.Cast<Core.Models.ScheduledTask>().ToList();
        output.WriteLine($"Windows Scheduled Task Discovery");
        output.WriteLine("");
        output.WriteLine($"Tasks:");
        output.WriteLine($"{tasks.Count}");
        output.WriteLine("");
        output.WriteLine($"Enabled:");
        output.WriteLine($"{tasks.Count(t => t.Enabled)}");
        output.WriteLine("");
        output.WriteLine($"Disabled:");
        output.WriteLine($"{tasks.Count(t => !t.Enabled)}");
        output.WriteLine("");
        output.WriteLine($"With Actions:");
        output.WriteLine($"{tasks.Count(t => t.Action is not null)}");
        output.WriteLine("");
        output.WriteLine($"Scanner Status:");
        output.WriteLine($"{taskResult.Status}");
        output.WriteLine("");

        var certificates = certResult.Entities.Cast<Core.Models.Certificate>().ToList();
        output.WriteLine($"Windows Certificate Discovery");
        output.WriteLine("");
        output.WriteLine($"Certificates:");
        output.WriteLine($"{certificates.Count}");
        output.WriteLine("");
        output.WriteLine($"Valid:");
        output.WriteLine($"{certificates.Count(c => c.Metadata["CertificateStatus"] == "Valid")}");
        output.WriteLine("");
        output.WriteLine($"Expired:");
        output.WriteLine($"{certificates.Count(c => c.Metadata["CertificateStatus"] == "Expired")}");
        output.WriteLine("");
        output.WriteLine($"HasPrivateKey:");
        output.WriteLine($"{certificates.Count(c => c.Metadata["HasPrivateKey"] == "True")}");
        output.WriteLine("");
        output.WriteLine($"Scanner Status:");
        output.WriteLine($"{certResult.Status}");
        output.WriteLine("");

        var byFamily = runtimeResult.Entities.GroupBy(e => e.Metadata["Family"]).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine($"Windows Runtime/SDK Discovery");
        output.WriteLine("");
        foreach (var family in new[] { "DotNetFramework", "DotNetRuntime", "DotNetSdk", "Java", "Python", "Node", "Npm", "Php", "Go" })
        {
            output.WriteLine($"{family}:");
            output.WriteLine($"{byFamily.GetValueOrDefault(family)}");
            output.WriteLine("");
        }
        output.WriteLine($"Scanner Status:");
        output.WriteLine($"{runtimeResult.Status}");
        output.WriteLine("");

        var configurations = configResult.Entities.Cast<Core.Models.Configuration>().ToList();
        var byConfigFormat = configurations.GroupBy(c => c.Format).ToDictionary(g => g.Key ?? "null", g => g.Count());
        output.WriteLine($"Windows Configuration Discovery");
        output.WriteLine("");
        output.WriteLine($"Configuration Files:");
        output.WriteLine($"{configurations.Count}");
        output.WriteLine("");
        foreach (var format in new[] { "Json", "Xml", "Ini", "Yaml", "Properties", "Unknown" })
        {
            output.WriteLine($"{format}:");
            output.WriteLine($"{byConfigFormat.GetValueOrDefault(format)}");
            output.WriteLine("");
        }
        output.WriteLine($"Secrets Detected:");
        output.WriteLine($"{configurations.Count(c => c.SecretDetected)}");
        output.WriteLine("");
        output.WriteLine($"Scanner Status:");
        output.WriteLine($"{configResult.Status}");
        output.WriteLine("");

        var binaries = binaryResult.Entities.Cast<Core.Models.Dll>().ToList();
        var byBinaryType = binaries.GroupBy(b => b.Type).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine($"Windows DLL / Native Dependency Discovery");
        output.WriteLine("");
        output.WriteLine($"Binaries Discovered:");
        output.WriteLine($"{binaries.Count}");
        output.WriteLine("");
        foreach (var type in new[] { "ManagedDll", "NativeDll", "Exe", "Ocx", "UnknownPe" })
        {
            output.WriteLine($"{type}:");
            output.WriteLine($"{byBinaryType.GetValueOrDefault(type)}");
            output.WriteLine("");
        }
        output.WriteLine($"Missing References:");
        output.WriteLine($"{binaries.Count(b => b.Metadata.GetValueOrDefault("FileStatus") == "NotFound")}");
        output.WriteLine("");
        output.WriteLine($"Scanner Status:");
        output.WriteLine($"{binaryResult.Status}");

        Assert.NotEqual(ScannerStatus.Failed, osResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, processResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, serviceResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, portResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, softwareResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, iisResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, comResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, taskResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, certResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, runtimeResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, configResult.Status);
        Assert.NotEqual(ScannerStatus.Failed, binaryResult.Status);

        Assert.True(processResult.Entities.Count > 0);
        Assert.True(serviceResult.Entities.Count > 0);
    }
}
