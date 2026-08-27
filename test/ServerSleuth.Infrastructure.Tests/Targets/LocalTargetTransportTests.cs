using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.DependencyInjection;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Tests.Targets;

/// <summary>Phase 10C §3, §5, §7: local scanning is explicitly represented as a target, wrapping
/// the SAME already-registered local <see cref="IProcessRunner"/>/<see cref="IFileSystemReader"/>
/// singletons every scanner already receives via direct DI — registering
/// <see cref="ITargetTransport"/> changes nothing about how scanners are wired.</summary>
public class LocalTargetTransportTests
{
    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ProcessRunner>>(NullLogger<ProcessRunner>.Instance);
        return services;
    }

    [Fact]
    public void AddServerSleuthInfrastructure_RegistersALocalTargetTransport()
    {
        var provider = NewServices().AddServerSleuthInfrastructure().BuildServiceProvider();

        var transport = provider.GetRequiredService<ITargetTransport>();

        Assert.IsType<LocalTargetTransport>(transport);
        Assert.Equal(TargetKind.Local, transport.Target.Kind);
        Assert.Equal(ScanTarget.LocalTargetId, transport.Target.Id);
    }

    [Fact]
    public void AddServerSleuthInfrastructure_LocalTargetTransport_WrapsTheSameSingletonsScannersUse()
    {
        var provider = NewServices().AddServerSleuthInfrastructure().BuildServiceProvider();

        var transport = provider.GetRequiredService<ITargetTransport>();
        var processRunner = provider.GetRequiredService<IProcessRunner>();
        var fileSystemReader = provider.GetRequiredService<IFileSystemReader>();

        Assert.Same(processRunner, transport.ProcessRunner);
        Assert.Same(fileSystemReader, transport.FileSystemReader);
    }

    [Fact]
    public void AddServerSleuthInfrastructure_LocalTargetTransport_ResolvesARealOperatingSystemPlatform_NeverUnknownOnThisTestHost()
    {
        var provider = NewServices().AddServerSleuthInfrastructure().BuildServiceProvider();
        var transport = provider.GetRequiredService<ITargetTransport>();

        // This test always runs on either Windows or Linux — never a mystery third platform.
        Assert.True(transport.Target.Platform is TargetPlatform.Windows or TargetPlatform.Linux);
    }

    [Fact]
    public void LocalTargetTransport_ConstructedDirectly_ExposesExactlyWhatItWasGiven()
    {
        var target = ScanTarget.Local(TargetPlatform.Windows, "Test Machine");
        var processRunner = new FakeProcessRunner();
        var fileSystemReader = new FakeFileSystemReader();

        var transport = new LocalTargetTransport(target, processRunner, fileSystemReader);

        Assert.Equal(target, transport.Target);
        Assert.Same(processRunner, transport.ProcessRunner);
        Assert.Same(fileSystemReader, transport.FileSystemReader);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by this test.");
    }

    private sealed class FakeFileSystemReader : IFileSystemReader
    {
        public bool Exists(string path) => throw new NotSupportedException();
        public Task<FileSystemResult<string>> ReadTextAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileSystemResult<byte[]>> ReadBytesAsync(string path, CancellationToken cancellationToken) => throw new NotSupportedException();
        public FileSystemResult<FileEntryInfo> GetFileInfo(string path) => throw new NotSupportedException();
        public FileSystemResult<IReadOnlyList<string>> EnumerateFiles(string directoryPath, string searchPattern = "*", bool recursive = false) => throw new NotSupportedException();
        public FileSystemResult<IReadOnlyList<string>> EnumerateDirectories(string directoryPath, string searchPattern = "*", bool recursive = false) => throw new NotSupportedException();
        public FileSystemResult<string> ReadLinkTarget(string path) => throw new NotSupportedException();
    }
}
