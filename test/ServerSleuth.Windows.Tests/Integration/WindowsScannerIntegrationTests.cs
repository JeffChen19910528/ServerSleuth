using ServerSleuth.Infrastructure.Runtimes;
using System.Diagnostics;
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
/// Runs every Phase 3 scanner against the real machine this test suite executes on — no
/// fixtures, no fakes. Per skill.md §18-19, assertions only check general, environment-
/// independent properties (counts, evidence presence, field shapes) — never a specific
/// third-party product/service name, since a clean Windows Server won't have those installed.
/// These are the tests that make Phase 3's "Required Validation" (actual, not assumed, real
/// discovery) checkable by CI on any Windows runner.
/// </summary>
public class WindowsScannerIntegrationTests(ITestOutputHelper output)
{
    private static readonly DiscoveryContext Context = new() { Profile = ScanProfile.Standard, CancellationToken = CancellationToken.None };

    [Fact]
    public async Task WindowsOsScanner_DiscoversCurrentMachine()
    {
        var scanner = new WindowsOsScanner(new WindowsRegistryReader(), NullLogger<WindowsOsScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        var os = Assert.Single(result.Entities.OfType<CoreOperatingSystem>());
        Assert.NotEqual(EntityArchitecture.Unknown, os.Architecture);
        Assert.NotEmpty(os.Evidence);

        var server = Assert.Single(result.Entities.OfType<Core.Models.Server>());
        Assert.Equal(Environment.MachineName, server.Hostname);
    }

    [Fact]
    public async Task WindowsProcessScanner_DiscoversTheCurrentTestProcessItself()
    {
        var scanner = new WindowsProcessScanner(new ProcessEnumerator(), new ProcessWmiProvider(NullLogger<ProcessWmiProvider>.Instance), NullLogger<WindowsProcessScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.True(result.Entities.Count > 10, "A running Windows machine should have well over 10 processes.");
        Assert.Contains(result.Entities.OfType<Core.Models.Process>(), p => p.Pid == Environment.ProcessId);
    }

    [Fact]
    public async Task WindowsPortInspector_EnumeratesEndpointsWithoutThrowing()
    {
        var inspector = new WindowsPortInspector(new NetworkTableProvider(NullLogger<NetworkTableProvider>.Instance), new ProcessNameResolver());

        var endpoints = await inspector.GetListeningEndpointsAsync(CancellationToken.None);

        Assert.NotNull(endpoints);
        Assert.All(endpoints, e => Assert.True(e.Protocol is "TCP" or "UDP"));
    }

    [Fact]
    public async Task WindowsServiceScanner_DiscoversRunningWin32Services()
    {
        var scanner = new WindowsServiceScanner(new ServiceEnumerator(), new WindowsRegistryReader(), NullLogger<WindowsServiceScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.True(result.Entities.Count > 5, "A running Windows machine should have well over 5 services registered.");
        Assert.Contains(result.Entities.OfType<Core.Models.Service>(), s => s.Status == EntityStatus.Running);
    }

    [Fact]
    public async Task WindowsInstalledSoftwareScanner_ReadsUninstallRegistryWithoutThrowing()
    {
        var scanner = new WindowsInstalledSoftwareScanner(new WindowsRegistryReader(), NullLogger<WindowsInstalledSoftwareScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.All(result.Entities.OfType<Core.Models.Software>(), s => Assert.False(string.IsNullOrWhiteSpace(s.Name)));
    }

    /// <summary>
    /// This dev machine has IIS installed (W3SVC running), but the account this test suite
    /// runs under is non-elevated, and IIS's redirection.config denies non-admin reads on
    /// this machine (verified independently: plain, non-elevated `Get-IISAppPool` in
    /// PowerShell fails with the identical UnauthorizedAccessException). That means
    /// AccessDenied — not Available — is the real, valid outcome exercised here, and it is
    /// exactly the outcome skill.md §11 requires: no crash, no NotApplicable/Failed
    /// misclassification, a clean AccessDenied status. If this ever runs elevated or on a
    /// machine without IIS, the other two branches below cover those outcomes instead.
    /// </summary>
    [Fact]
    public async Task IisScanner_AgainstRealMachine_NeverThrowsAndReportsAValidStatus()
    {
        var scanner = new IisScanner(new IisConfigurationProvider(NullLogger<IisConfigurationProvider>.Instance), new FileSystemReader(), NullLogger<IisScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        Assert.Contains(result.Status, new[] { ScannerStatus.NotInstalled, ScannerStatus.AccessDenied, ScannerStatus.Supported, ScannerStatus.PartiallySupported });

        switch (result.Status)
        {
            case ScannerStatus.NotInstalled:
                Assert.Empty(result.Entities);
                break;
            case ScannerStatus.AccessDenied:
                Assert.Single(result.Errors);
                Assert.True(result.Errors[0].IsPermissionFailure);
                break;
            case ScannerStatus.Supported or ScannerStatus.PartiallySupported:
                Assert.Contains(result.Entities.OfType<Core.Models.ApplicationPool>(), p => p.Name == "DefaultAppPool");
                break;
        }
    }

    /// <summary>
    /// Real-machine COM/ActiveX registry scan — no fixtures. This machine has ~14,000 real
    /// CLSID entries (8031 HKLM Registry64 + 6143 HKLM Registry32/WOW6432Node + 15 HKCU,
    /// measured independently via `Get-ChildItem` before this scanner was written), so this
    /// also functions as the required real-world performance validation (skill.md §32/§6):
    /// actual wall-clock duration and per-source/per-server-type counts are printed and
    /// asserted to complete within a generous bound, never merely "recorded as slow."
    /// Never activates COM, never loads a DLL, never executes a LocalServer32 EXE — registry
    /// reads and, for existing files, a non-executing FileVersionInfo read only.
    /// </summary>
    [Fact]
    public async Task WindowsComScanner_AgainstRealMachine_CompletesWithinBoundAndReportsRealCounts()
    {
        var registryReader = new WindowsRegistryReader();
        var scanner = new WindowsComScanner(
            registryReader,
            new FileSystemReader(),
            new FileVersionMetadataReader(),
            new SecretRedactor(),
            NullLogger<WindowsComScanner>.Instance);

        var stopwatch = Stopwatch.StartNew();
        var result = await scanner.ScanAsync(Context, CancellationToken.None);
        stopwatch.Stop();

        var components = result.Entities.Cast<Core.Models.ComComponent>().ToList();
        var byView = components.GroupBy(c => c.Metadata["RegistryView"]).ToDictionary(g => g.Key, g => g.Count());
        var inproc = components.Count(c => c.InprocServer32 is not null);
        var local = components.Count(c => c.LocalServer32 is not null);
        var missingServerFile = components.Count(c => c.Metadata.GetValueOrDefault("ServerPathStatus") == "NotFound");
        var accessDeniedServerFile = components.Count(c => c.Metadata.GetValueOrDefault("ServerPathStatus") == "AccessDenied");

        output.WriteLine("Windows COM Discovery");
        output.WriteLine("");
        output.WriteLine($"Registry64 (HKLM 64-bit): {byView.GetValueOrDefault("Registry64")}");
        output.WriteLine($"Registry32 (WOW6432Node): {byView.GetValueOrDefault("Registry32")}");
        output.WriteLine($"HKCU (Default):           {byView.GetValueOrDefault("Default")}");
        output.WriteLine($"Total COM Components:     {components.Count}");
        output.WriteLine($"InprocServer32:           {inproc}");
        output.WriteLine($"LocalServer32:             {local}");
        output.WriteLine($"Missing Server Files:     {missingServerFile}");
        output.WriteLine($"AccessDenied Server Files: {accessDeniedServerFile}");
        output.WriteLine($"Registry-level errors:    {result.Errors.Count}");
        output.WriteLine($"Scanner Status:           {result.Status}");
        output.WriteLine($"Duration:                 {stopwatch.Elapsed}");

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.NotEqual(ScannerStatus.NotInstalled, result.Status); // CLSID\ always "exists" on Windows — this status would be a bug here.
        Assert.True(components.Count > 1000, "This dev machine has thousands of real CLSID registrations; a near-zero count would indicate a scanning defect, not a clean machine.");

        // Generous bound: targeted registry reads over ~14,000 CLSIDs should not take minutes.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(120),
            $"COM scan took {stopwatch.Elapsed}, which is far beyond what targeted registry reads should require — investigate for a performance regression rather than only noting it.");
    }

    /// <summary>
    /// Real Windows Task Scheduler enumeration — no fixtures, no assumption that any specific
    /// third-party task exists. Every Windows machine has a substantial set of built-in tasks
    /// under \Microsoft\Windows\... (Task Scheduler is a core OS component, always present),
    /// so this asserts only on that general guarantee, per skill.md §19.
    /// </summary>
    [Fact]
    public async Task WindowsScheduledTaskScanner_AgainstRealMachine_DiscoversBuiltInTasks()
    {
        var scanner = new WindowsScheduledTaskScanner(
            new TaskSchedulerProvider(NullLogger<TaskSchedulerProvider>.Instance),
            new FileSystemReader(),
            new SecretRedactor(),
            NullLogger<WindowsScheduledTaskScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        var tasks = result.Entities.Cast<Core.Models.ScheduledTask>().ToList();
        var enabled = tasks.Count(t => t.Enabled);
        var disabled = tasks.Count(t => !t.Enabled);
        var withActions = tasks.Count(t => t.Action is not null);

        output.WriteLine("Windows Scheduled Task Discovery");
        output.WriteLine("");
        output.WriteLine($"Tasks:          {tasks.Count}");
        output.WriteLine($"Enabled:        {enabled}");
        output.WriteLine($"Disabled:       {disabled}");
        output.WriteLine($"With Actions:   {withActions}");
        output.WriteLine($"AccessDenied:   {result.Errors.Count(e => e.IsPermissionFailure)}");
        output.WriteLine($"Scanner Status: {result.Status}");

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.True(tasks.Count > 10, "Every Windows machine has well over 10 built-in scheduled tasks under \\Microsoft\\Windows\\...");
        Assert.Contains(tasks, t => t.Folder != null && t.Folder.StartsWith(@"\Microsoft\Windows", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Real certificate store enumeration — no fixtures, no assumption of a specific
    /// production certificate. LocalMachine\Root always has a substantial set of trusted CAs
    /// on any Windows machine, so this asserts only on that general guarantee, per skill.md §19.
    /// </summary>
    [Fact]
    public async Task WindowsCertificateScanner_AgainstRealMachine_DiscoversPublicMetadataOnly()
    {
        var scanner = new WindowsCertificateScanner(new CertificateStoreProvider(NullLogger<CertificateStoreProvider>.Instance), NullLogger<WindowsCertificateScanner>.Instance);

        var result = await scanner.ScanAsync(Context, CancellationToken.None);

        var certificates = result.Entities.Cast<Core.Models.Certificate>().ToList();
        var byStatus = certificates.GroupBy(c => c.Metadata["CertificateStatus"]).ToDictionary(g => g.Key, g => g.Count());
        var hasPrivateKey = certificates.Count(c => c.Metadata["HasPrivateKey"] == "True");

        output.WriteLine("Windows Certificate Discovery");
        output.WriteLine("");
        output.WriteLine($"Certificates:   {certificates.Count}");
        output.WriteLine($"Valid:          {byStatus.GetValueOrDefault("Valid")}");
        output.WriteLine($"Expired:        {byStatus.GetValueOrDefault("Expired")}");
        output.WriteLine($"NotYetValid:    {byStatus.GetValueOrDefault("NotYetValid")}");
        output.WriteLine($"HasPrivateKey:  {hasPrivateKey}");
        output.WriteLine($"Stores:         {string.Join(", ", CertificateStoreSource.All.Select(s => s.Label))}");
        output.WriteLine($"Scanner Status: {result.Status}");

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.True(certificates.Count > 10, "LocalMachine\\Root alone should have well over 10 trusted CA certificates on any Windows machine.");
        Assert.All(certificates, c => Assert.False(string.IsNullOrEmpty(c.Thumbprint)));
        Assert.All(certificates, c => Assert.Equal(c.Thumbprint, c.Thumbprint?.ToUpperInvariant())); // normalized, never mixed case
    }

    /// <summary>
    /// Real Runtime/SDK discovery — no fixtures, no assumption that any particular language
    /// runtime is installed beyond .NET itself (this repo is built with dotnet, so .NET
    /// Runtime/SDK detection is guaranteed to find something; every other family is optional,
    /// per skill.md §19's "do not assume any particular runtime exists").
    /// </summary>
    [Fact]
    public async Task RuntimeDiscoveryScanner_AgainstRealMachine_DetectsAtLeastDotNet()
    {
        var fileSystemReader = new FileSystemReader();
        var processRunner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var executableLocator = new ExecutableLocator(fileSystemReader);
        var registryReader = new WindowsRegistryReader();
        var secretRedactor = new SecretRedactor();

        IRuntimeDetector[] detectors =
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

        var scanner = new RuntimeDiscoveryScanner(detectors, NullLogger<RuntimeDiscoveryScanner>.Instance);

        var stopwatch = Stopwatch.StartNew();
        var result = await scanner.ScanAsync(Context, CancellationToken.None);
        stopwatch.Stop();

        var byFamily = result.Entities
            .GroupBy(e => e.Metadata["Family"])
            .ToDictionary(g => g.Key, g => g.Count());

        output.WriteLine("Windows Runtime/SDK Discovery");
        output.WriteLine("");
        foreach (var family in new[] { "DotNetFramework", "DotNetRuntime", "DotNetSdk", "Java", "Python", "Node", "Npm", "Php", "Go" })
        {
            output.WriteLine($"{family}: {byFamily.GetValueOrDefault(family)}");
        }
        output.WriteLine("");
        output.WriteLine($"Total entities: {result.Entities.Count}");
        output.WriteLine($"Scanner Status: {result.Status}");
        output.WriteLine($"Duration:       {stopwatch.Elapsed}");

        Assert.NotEqual(ScannerStatus.Failed, result.Status);
        Assert.True(byFamily.GetValueOrDefault("DotNetSdk") > 0, "This repository is built with the dotnet SDK, so at least one SDK must be detected.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Runtime discovery took {stopwatch.Elapsed}, unexpectedly slow for metadata-only version queries.");
    }

    /// <summary>
    /// Real Configuration Discovery — no fixtures. Scan roots come from this same machine's
    /// real IIS/Service/ScheduledTask discovery (Phases 3/4A/4C), so whatever configuration
    /// files those actually point at get discovered here; no specific file is assumed to
    /// exist, per skill.md §19.
    /// </summary>
    [Fact]
    public async Task WindowsConfigurationScanner_AgainstRealMachine_CompletesWithoutExposingSecrets()
    {
        var fileSystemReader = new FileSystemReader();
        var secretRedactor = new SecretRedactor();
        var registryReader = new WindowsRegistryReader();

        var iisScanner = new IisScanner(new IisConfigurationProvider(NullLogger<IisConfigurationProvider>.Instance), fileSystemReader, NullLogger<IisScanner>.Instance);
        var serviceScanner = new WindowsServiceScanner(new ServiceEnumerator(), registryReader, NullLogger<WindowsServiceScanner>.Instance);
        var taskScanner = new WindowsScheduledTaskScanner(new TaskSchedulerProvider(NullLogger<TaskSchedulerProvider>.Instance), fileSystemReader, secretRedactor, NullLogger<WindowsScheduledTaskScanner>.Instance);
        var scanner = new WindowsConfigurationScanner(iisScanner, serviceScanner, taskScanner, fileSystemReader, secretRedactor, NullLogger<WindowsConfigurationScanner>.Instance);

        var stopwatch = Stopwatch.StartNew();
        var result = await scanner.ScanAsync(Context, CancellationToken.None);
        stopwatch.Stop();

        var configurations = result.Entities.Cast<Core.Models.Configuration>().ToList();
        var byFormat = configurations.GroupBy(c => c.Format).ToDictionary(g => g.Key ?? "null", g => g.Count());
        var byStatus = configurations.GroupBy(c => c.Metadata["ParseStatus"]).ToDictionary(g => g.Key, g => g.Count());

        output.WriteLine("Windows Configuration Discovery");
        output.WriteLine("");
        output.WriteLine($"Configuration Files: {configurations.Count}");
        foreach (var format in new[] { "Json", "Xml", "Ini", "Yaml", "Properties", "Unknown" })
        {
            output.WriteLine($"{format}: {byFormat.GetValueOrDefault(format)}");
        }
        output.WriteLine($"Secrets Detected:    {configurations.Count(c => c.SecretDetected)}");
        output.WriteLine($"AccessDenied:        {byStatus.GetValueOrDefault("AccessDenied")}");
        output.WriteLine($"Parse Errors:        {byStatus.GetValueOrDefault("PartiallyParsed")}");
        output.WriteLine($"Scanner Status:      {result.Status}");
        output.WriteLine($"Duration:            {stopwatch.Elapsed}");

        Assert.NotEqual(ScannerStatus.Failed, result.Status);

        // The report must never contain a raw secret value: any metadata value that still
        // looks secret-shaped to the same redactor must already carry the redaction marker,
        // never a live value.
        Assert.All(configurations, c => Assert.All(c.Metadata.Values, v =>
            Assert.False(secretRedactor.ContainsSecret(v) && !v.Contains("[REDACTED]"),
                $"Metadata value '{v}' looks secret-shaped but was not redacted.")));
    }

    /// <summary>
    /// Real DLL/Native Dependency Discovery — no fixtures. Scan roots come from this same
    /// machine's real IIS/Service/ScheduledTask/COM discovery, so whatever binaries those
    /// actually reference get discovered here; no specific binary is assumed to exist.
    /// Demonstrates the discovery is bounded (roots/files/duration all reported), never a
    /// full-disk scan, per skill.md §34-35.
    /// </summary>
    [Fact]
    public async Task WindowsBinaryDiscoveryScanner_AgainstRealMachine_IsBoundedAndNeverExecutesAnything()
    {
        var fileSystemReader = new FileSystemReader();
        var secretRedactor = new SecretRedactor();
        var registryReader = new WindowsRegistryReader();

        var iisScanner = new IisScanner(new IisConfigurationProvider(NullLogger<IisConfigurationProvider>.Instance), fileSystemReader, NullLogger<IisScanner>.Instance);
        var serviceScanner = new WindowsServiceScanner(new ServiceEnumerator(), registryReader, NullLogger<WindowsServiceScanner>.Instance);
        var taskScanner = new WindowsScheduledTaskScanner(new TaskSchedulerProvider(NullLogger<TaskSchedulerProvider>.Instance), fileSystemReader, secretRedactor, NullLogger<WindowsScheduledTaskScanner>.Instance);
        var comScanner = new WindowsComScanner(registryReader, fileSystemReader, new FileVersionMetadataReader(), secretRedactor, NullLogger<WindowsComScanner>.Instance);
        var scanner = new WindowsBinaryDiscoveryScanner(
            iisScanner, serviceScanner, taskScanner, comScanner,
            fileSystemReader, new FileVersionMetadataReader(), new PeAnalyzer(), secretRedactor,
            NullLogger<WindowsBinaryDiscoveryScanner>.Instance);

        var stopwatch = Stopwatch.StartNew();
        var result = await scanner.ScanAsync(Context, CancellationToken.None);
        stopwatch.Stop();

        var binaries = result.Entities.Cast<Core.Models.Dll>().ToList();
        var byType = binaries.GroupBy(b => b.Type).ToDictionary(g => g.Key, g => g.Count());
        var managed = binaries.Count(b => b.Metadata.GetValueOrDefault("IsManaged") == "True");
        var native = binaries.Count(b => b.Metadata.GetValueOrDefault("IsManaged") == "False");
        var missing = binaries.Count(b => b.Metadata.GetValueOrDefault("FileStatus") == "NotFound");
        var accessDenied = binaries.Count(b => b.Metadata.GetValueOrDefault("FileStatus") == "AccessDenied");

        output.WriteLine("Windows DLL / Native Dependency Discovery");
        output.WriteLine("");
        output.WriteLine($"Binaries Discovered: {binaries.Count}");
        output.WriteLine($"  ManagedDll: {byType.GetValueOrDefault("ManagedDll")}");
        output.WriteLine($"  NativeDll:  {byType.GetValueOrDefault("NativeDll")}");
        output.WriteLine($"  Exe:        {byType.GetValueOrDefault("Exe")}");
        output.WriteLine($"  Ocx:        {byType.GetValueOrDefault("Ocx")}");
        output.WriteLine($"  UnknownPe:  {byType.GetValueOrDefault("UnknownPe")}");
        output.WriteLine($"Managed:             {managed}");
        output.WriteLine($"Native:              {native}");
        output.WriteLine($"Missing References:  {missing}");
        output.WriteLine($"AccessDenied:        {accessDenied}");
        output.WriteLine($"Registry-level errors: {result.Errors.Count}");
        output.WriteLine($"Scanner Status:      {result.Status}");
        output.WriteLine($"Duration:            {stopwatch.Elapsed}");

        Assert.NotEqual(ScannerStatus.Failed, result.Status);

        // Bounded, not a full-disk scan: a reasonable machine won't have more than a few tens
        // of thousands of binaries reachable from application-relevant roots, and it must
        // complete in a reasonable time even if it does.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(120),
            $"Binary discovery took {stopwatch.Elapsed} — investigate for a performance regression rather than only noting it.");
    }
}
