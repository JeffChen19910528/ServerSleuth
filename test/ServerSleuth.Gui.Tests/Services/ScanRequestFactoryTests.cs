using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.Models;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

public class ScanRequestFactoryTests
{
    private readonly ScanRequestFactory _factory = new();

    [Fact]
    public void Create_ForLocal_ProducesALocalScanTarget()
    {
        var config = ScanConfigurationState.Initial with { OutputDirectory = "./out" };
        var request = _factory.Create(config);

        Assert.Equal(TargetKind.Local, request.Target.Kind);
        Assert.Equal(ScanTarget.LocalTargetId, request.Target.Id);
        Assert.Null(request.TransportKind);
    }

    [Fact]
    public void Create_ForRemoteLinux_ProducesARemoteScanTarget_WithSshTransport()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "db-server-1", Platform = TargetPlatform.Linux,
            TransportKind = ScanTransportKind.Ssh, OutputDirectory = "./out"
        };

        var request = _factory.Create(config);

        Assert.Equal(TargetKind.Remote, request.Target.Kind);
        Assert.Equal("remote:db-server-1", request.Target.Id);
        Assert.Equal(TargetPlatform.Linux, request.Target.Platform);
        Assert.Equal(ScanTransportKind.Ssh, request.TransportKind);
    }

    [Fact]
    public void Create_CopiesOutputSettings_Unchanged()
    {
        var config = ScanConfigurationState.Initial with
        {
            OutputDirectory = "./my-report", OutputFormat = ScanOutputFormat.Json, OverwritePolicy = ScanOverwritePolicy.Overwrite, Verbose = true
        };

        var request = _factory.Create(config);

        Assert.Equal("./my-report", request.OutputDirectory);
        Assert.Equal(ScanOutputFormat.Json, request.OutputFormat);
        Assert.Equal(ScanOverwritePolicy.Overwrite, request.OverwritePolicy);
        Assert.True(request.Verbose);
    }

    [Fact]
    public void Create_RepeatedForIdenticalInput_ProducesEquivalentRequests()
    {
        var config = ScanConfigurationState.Initial with
        {
            TargetKind = TargetKind.Remote, RemoteHost = "host", Platform = TargetPlatform.Windows,
            TransportKind = ScanTransportKind.WinRm, OutputDirectory = "./out"
        };

        var first = _factory.Create(config);
        var second = _factory.Create(config);

        Assert.Equal(first.Target, second.Target);
        Assert.Equal(first.OutputDirectory, second.OutputDirectory);
        Assert.Equal(first.OutputFormat, second.OutputFormat);
        Assert.Equal(first.OverwritePolicy, second.OverwritePolicy);
        Assert.Equal(first.TransportKind, second.TransportKind);
    }

    [Fact]
    public void ScanRequestType_HasNoCredentialShapedProperty()
    {
        var forbidden = new[] { "password", "credential", "privatekey", "username", "secret", "token" };
        var names = typeof(ScanRequest).GetProperties().Select(p => p.Name.ToLowerInvariant());

        foreach (var name in names)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }
}
