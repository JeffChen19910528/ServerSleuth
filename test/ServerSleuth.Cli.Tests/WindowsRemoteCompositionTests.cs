#if SERVERSLEUTH_WINDOWS
using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Cli.Composition;
using ServerSleuth.Cli.Options;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.IIS;
using ServerSleuth.Windows.Networking;
using ServerSleuth.Windows.Process;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.ScheduledTasks;
using ServerSleuth.Windows.Services;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Phase 10D-3B §20, §21, §25 (items 1-6, 25): proves the composition root wires a remote
/// Windows target to the WinRM-backed providers — never the local ones, never a network call
/// during composition (only <c>Connect()</c>, which nothing here calls, may ever touch the
/// network). Uses a never-actually-contacted host (RFC 5737 TEST-NET-1), exactly like
/// <see cref="RemoteCompositionTests"/> already does for SSH.
/// </summary>
public class WindowsRemoteCompositionTests
{
    private const string PasswordEnvVar = "SERVERSLEUTH_TEST_WINRM_PASSWORD_9F3A";

    private static WindowsRemoteScanOptions BuildOptions() => new()
    {
        Host = "192.0.2.1", // TEST-NET-1 (RFC 5737) — guaranteed non-routable, never really contacted
        Port = 1,
        Username = "tester",
        PasswordEnvironmentVariable = PasswordEnvVar
    };

    public WindowsRemoteCompositionTests() => Environment.SetEnvironmentVariable(PasswordEnvVar, "not-a-real-password");

    [Fact]
    public void Build_RemoteWindowsTarget_RegistersTheWinRmTransport_NeverTheLocalOne()
    {
        var options = new ScanOptions { WindowsRemote = BuildOptions() };
        using var provider = (ServiceProvider)CompositionRoot.Build(options);

        var transport = provider.GetRequiredService<ITargetTransport>();
        Assert.IsType<WindowsRemoteTargetTransport>(transport);
        Assert.Equal(TargetKind.Remote, transport.Target.Kind);
        Assert.Equal(TargetPlatform.Windows, transport.Target.Platform);
    }

    /// <summary>Items 1-6 of skill.md §25: none of the six Windows-domain local interfaces
    /// resolve to their LOCAL implementation for a remote Windows target.</summary>
    [Fact]
    public void Build_RemoteWindowsTarget_NeverResolvesAnyLocalWindowsInterfaceImplementation()
    {
        var options = new ScanOptions { WindowsRemote = BuildOptions() };
        using var provider = (ServiceProvider)CompositionRoot.Build(options);

        Assert.IsNotType<WindowsRegistryReader>(provider.GetRequiredService<IWindowsRegistryReader>());
        Assert.IsNotType<ProcessWmiProvider>(provider.GetRequiredService<IProcessWmiProvider>());
        Assert.IsNotType<ProcessEnumerator>(provider.GetRequiredService<IProcessEnumerator>());
        Assert.IsNotType<NetworkTableProvider>(provider.GetRequiredService<INetworkTableProvider>());
        Assert.IsNotType<IisConfigurationProvider>(provider.GetRequiredService<IIisConfigurationProvider>());
        Assert.IsNotType<ServiceEnumerator>(provider.GetRequiredService<IServiceEnumerator>());
        Assert.IsNotType<TaskSchedulerProvider>(provider.GetRequiredService<ITaskSchedulerProvider>());
        Assert.IsNotType<CertificateStoreProvider>(provider.GetRequiredService<ICertificateStoreProvider>());

        Assert.IsType<WinRmWindowsRegistryReader>(provider.GetRequiredService<IWindowsRegistryReader>());
        Assert.IsType<WinRmProcessWmiProvider>(provider.GetRequiredService<IProcessWmiProvider>());
        Assert.IsType<WinRmProcessEnumerator>(provider.GetRequiredService<IProcessEnumerator>());
        Assert.IsType<WinRmNetworkTableProvider>(provider.GetRequiredService<INetworkTableProvider>());
        Assert.IsType<WinRmIisConfigurationProvider>(provider.GetRequiredService<IIisConfigurationProvider>());
        Assert.IsType<WinRmServiceEnumerator>(provider.GetRequiredService<IServiceEnumerator>());
        Assert.IsType<WinRmTaskSchedulerProvider>(provider.GetRequiredService<ITaskSchedulerProvider>());
        Assert.IsType<WinRmCertificateStoreProvider>(provider.GetRequiredService<ICertificateStoreProvider>());
    }

    /// <summary>skill.md §21's excluded scanners (WindowsOsScanner/RuntimeDiscoveryScanner/
    /// WindowsConfigurationScanner/WindowsBinaryDiscoveryScanner) never register for a remote
    /// Windows target — the disclosed local-fallback-risk gap is an EXCLUSION, not a silent
    /// wrong answer.</summary>
    [Fact]
    public void Build_RemoteWindowsTarget_ExcludesTheDisclosedLocalFallbackRiskScanners()
    {
        var options = new ScanOptions { WindowsRemote = BuildOptions() };
        using var provider = (ServiceProvider)CompositionRoot.Build(options);

        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();
        var ids = registry.Scanners.Select(s => s.Id).ToList();

        Assert.DoesNotContain("windows-os-scanner", ids);
        Assert.DoesNotContain("runtime-discovery-scanner", ids);
        Assert.DoesNotContain("windows-configuration-scanner", ids);
        Assert.DoesNotContain("windows-binary-discovery-scanner", ids);
    }

    [Fact]
    public void Build_RemoteWindowsTarget_StillRegistersTheSafeWindowsScanners()
    {
        var options = new ScanOptions { WindowsRemote = BuildOptions() };
        using var provider = (ServiceProvider)CompositionRoot.Build(options);

        var registry = provider.GetRequiredService<IDiscoveryScannerRegistry>();
        var ids = registry.Scanners.Select(s => s.Id).ToList();

        Assert.Contains("windows-process-scanner", ids);
        Assert.Contains("windows-port-scanner", ids);
    }

    /// <summary>skill.md §25 item 25: never falls back to local — proven by comparing against a
    /// genuinely local composition's singletons, the same technique
    /// <see cref="RemoteCompositionTests.Build_RemoteTarget_NeverReusesTheLocalSingletons"/>
    /// already established for SSH.</summary>
    [Fact]
    public void Build_RemoteWindowsTarget_NeverReusesTheLocalSingletons()
    {
        using var localProvider = (ServiceProvider)CompositionRoot.Build(new ScanOptions());
        var localFileSystemReader = localProvider.GetRequiredService<IFileSystemReader>();
        var localProcessRunner = localProvider.GetRequiredService<IProcessRunner>();

        var options = new ScanOptions { WindowsRemote = BuildOptions() };
        using var remoteProvider = (ServiceProvider)CompositionRoot.Build(options);
        var remoteFileSystemReader = remoteProvider.GetRequiredService<IFileSystemReader>();
        var remoteProcessRunner = remoteProvider.GetRequiredService<IProcessRunner>();

        Assert.NotSame(localFileSystemReader, remoteFileSystemReader);
        Assert.NotSame(localProcessRunner, remoteProcessRunner);
        Assert.IsType<UnavailableRemoteFileSystemReader>(remoteFileSystemReader);
        Assert.IsType<UnavailableRemoteProcessRunner>(remoteProcessRunner);
    }

    /// <summary>Constructing the whole composition graph must never itself contact the network
    /// — only an explicit <c>Connect()</c> call (never made in this test) may.</summary>
    [Fact]
    public void Build_RemoteWindowsTarget_DoesNotConnect_CompositionAloneNeverTouchesTheNetwork()
    {
        var options = new ScanOptions { WindowsRemote = BuildOptions() };
        using var provider = (ServiceProvider)CompositionRoot.Build(options);

        var transport = (WindowsRemoteTargetTransport)provider.GetRequiredService<ITargetTransport>();
        Assert.NotNull(transport); // reaching this line without throwing/hanging is the assertion.
    }
}
#endif
