using ServerSleuth.Cli.ExitCodes;
using ServerSleuth.Cli.Tests.Fakes;
using ServerSleuth.Cli.Tests.Fixtures;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Results;
using ServerSleuth.Core.Targets;

namespace ServerSleuth.Cli.Tests;

/// <summary>
/// Phase 10E-1: proves a REMOTE <see cref="ScanTarget"/> (Linux/SSH-shaped and
/// Windows/WinRM-shaped) flows through the SAME, single, unmodified pipeline a local scan
/// already uses — Discovery → Correlation → Boundary → Expansion → Validation → Risk →
/// Aggregation → Migration Assessment → Migration Plan → Consolidation → Reporting → Export
/// (<c>ScanCommand</c>/<c>ScanPipelineRunner</c>, byte-for-byte the same code
/// <see cref="ScanCommandErpEndToEndTests"/> already exercises for a local target).
///
/// Uses <see cref="FakeRemoteTargetTransport"/> (never a real
/// <c>SshRemoteTargetTransport</c>/<c>WindowsRemoteTargetTransport</c>) — real SSH/WinRM connect
/// semantics are already exhaustively covered by Phase 10D-2's/10D-3B's own fake-session test
/// suites and are deliberately NOT duplicated here (skill.md Phase 10E-1 §14). What THIS suite
/// proves is integration: that whatever a remote transport's discovery stage produces reaches
/// every downstream stage identically to local, with correct target identity, no fabricated
/// data for a disclosed-gap scanner, and no credential leakage anywhere in the exported report.
/// </summary>
public class RemotePipelineIntegrationTests
{
    private static readonly ScanTarget RemoteLinuxTarget = ScanTarget.Remote("remote-linux-host.internal", TargetPlatform.Linux, 22);
    private static readonly ScanTarget RemoteWindowsTarget = ScanTarget.Remote("remote-windows-host.internal", TargetPlatform.Windows);

    private static readonly string[] SshArgs =
        ["--target", "remote-linux-host.internal", "--ssh-user", "tester", "--ssh-key", "/fake/never-read-key",
         "--ssh-host-fingerprint", "aa:bb:cc"];

    private static readonly string[] WinRmArgs =
        ["--target", "remote-windows-host.internal", "--winrm-user", "svc-account", "--winrm-password-env", "SERVERSLEUTH_TEST_UNUSED_VAR"];

    // 1. Remote Linux discovery reaches the same pipeline, with disclosed-gap scanners honestly reported.
    [Fact]
    public async Task RemoteLinuxTarget_FlowsThroughTheSamePipeline_AsALocalScan_WithDisclosedGapsHonest()
    {
        using var temp = new TempDirectory();
        var entities = ErpFixture.BuildEntities();
        var discoveryResult = DiscoveryResultBuilder.Build(
            entities,
            new DiscoveryResult { ScannerId = "linux-container-scanner", Status = ScannerStatus.NotInstalled, Entities = [] },
            new DiscoveryResult { ScannerId = "linux-kubernetes-scanner", Status = ScannerStatus.NotInstalled, Entities = [] });

        var engine = new FakeDiscoveryEngine(discoveryResult);
        var transport = new FakeRemoteTargetTransport(RemoteLinuxTarget);

        var args = new List<string> { "scan", "--output", temp.Path, "--verbose" };
        args.AddRange(SshArgs);
        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync([.. args], engine, transport: transport);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Empty(stderr);
        Assert.Contains($"Target: {RemoteLinuxTarget.Id} (Linux)", stdout, StringComparison.Ordinal);

        // Same pipeline output shape a local ERP-fixture scan already produces.
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.html")));
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        Assert.Contains("\"OverallMigrationStatus\": \"Blocked\"", json, StringComparison.Ordinal);
        Assert.Contains("boundary:service:BatchA", json, StringComparison.Ordinal);

        // The disclosed-gap scanners' honest status is visible, never silently dropped or faked as success.
        Assert.Contains("linux-container-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("linux-kubernetes-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("NotInstalled", stdout, StringComparison.Ordinal);
    }

    // 1 (Windows). Remote Windows discovery reaches the same pipeline, with 10D-3B's disclosed gaps honestly reported.
    [Fact]
    public async Task RemoteWindowsTarget_FlowsThroughTheSamePipeline_AsALocalScan_WithDisclosedGapsHonest()
    {
        using var temp = new TempDirectory();
        var entities = ErpFixture.BuildEntities();
        var discoveryResult = DiscoveryResultBuilder.Build(
            entities,
            new DiscoveryResult { ScannerId = "windows-iis-scanner", Status = ScannerStatus.NotInstalled, Entities = [] },
            new DiscoveryResult { ScannerId = "windows-scheduled-task-scanner", Status = ScannerStatus.NotInstalled, Entities = [] },
            new DiscoveryResult { ScannerId = "windows-certificate-scanner", Status = ScannerStatus.NotInstalled, Entities = [] });

        var engine = new FakeDiscoveryEngine(discoveryResult);
        var transport = new FakeRemoteTargetTransport(RemoteWindowsTarget);

        var args = new List<string> { "scan", "--output", temp.Path, "--verbose" };
        args.AddRange(WinRmArgs);
        var (exitCode, stdout, stderr) = await CliTestRunner.RunAsync([.. args], engine, transport: transport);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Empty(stderr);
        Assert.Contains($"Target: {RemoteWindowsTarget.Id} (Windows)", stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.html")));

        Assert.Contains("windows-iis-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("windows-certificate-scanner", stdout, StringComparison.Ordinal);
        Assert.Contains("NotInstalled", stdout, StringComparison.Ordinal);
    }

    // 2. Local scan behavior is unaffected — same fixture, same exit code, same numbers.
    [Fact]
    public async Task LocalTarget_StillProducesTheSameErpResult_Unaffected()
    {
        using var temp = new TempDirectory();
        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));

        var (exitCode, stdout, _) = await CliTestRunner.RunAsync(["scan", "--output", temp.Path], engine);

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("Completed.", stdout, StringComparison.Ordinal);
    }

    // 9. Partial scanner failure over a remote target still yields PartialDiscovery, never a crash, never silent success.
    [Theory]
    [InlineData(ScannerStatus.AccessDenied)]
    [InlineData(ScannerStatus.Failed)]
    [InlineData(ScannerStatus.PartiallySupported)]
    public async Task RemoteLinuxTarget_PartialScannerFailure_YieldsPartialDiscovery_NeverCrashesOrFakesSuccess(ScannerStatus status)
    {
        using var temp = new TempDirectory();
        var entities = ErpFixture.BuildEntities();
        var discoveryResult = DiscoveryResultBuilder.Build(
            entities,
            new DiscoveryResult { ScannerId = "linux-package-scanner", Status = status, Entities = [] });

        var engine = new FakeDiscoveryEngine(discoveryResult);
        var transport = new FakeRemoteTargetTransport(RemoteLinuxTarget);

        var args = new List<string> { "scan", "--output", temp.Path };
        args.AddRange(SshArgs);
        var (exitCode, _, stderr) = await CliTestRunner.RunAsync([.. args], engine, transport: transport);

        Assert.Equal(CliExitCode.PartialDiscovery, exitCode);
        Assert.Empty(stderr);
        Assert.True(File.Exists(Path.Combine(temp.Path, "report.json"))); // still produces a full report, per the existing contract
    }

    // 10. Security: no credential-shaped text anywhere in the exported report for a remote scan.
    [Theory]
    [InlineData("Password=DeliberatelySensitiveRemote1")]
    [InlineData("aa:bb:cc:dd:ee:ff:00:11")] // an SSH-fingerprint-shaped string, never actually used to connect
    [InlineData("-----BEGIN PRIVATE KEY-----DeliberatelySensitiveRemote2")]
    public async Task RemoteScan_NeverLeaksCredentialShapedValues_IntoTheExportedReport(string sensitiveValue)
    {
        using var temp = new TempDirectory();
        var entities = ErpFixture.BuildEntities();
        var config = entities.OfType<ServerSleuth.Core.Models.Configuration>().First();
        config.SetMetadata("InjectedForTest", sensitiveValue);

        var engine = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(entities));
        var transport = new FakeRemoteTargetTransport(RemoteLinuxTarget);

        // --verbose deliberately included (Phase 10E-3 §D: "--verbose remains safe") — the
        // extra per-scanner/stage-duration detail verbose mode prints must not become a new
        // credential-leak surface.
        var args = new List<string> { "scan", "--output", temp.Path, "--verbose" };
        args.AddRange(SshArgs);
        var (exitCode, stdout, _) = await CliTestRunner.RunAsync([.. args], engine, transport: transport);

        Assert.Equal(CliExitCode.Success, exitCode);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.json"));
        var html = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.html"));

        Assert.DoesNotContain(sensitiveValue, json, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, html, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveValue, stdout, StringComparison.Ordinal);
    }

    // 11. Determinism: identical remote-origin input produces byte-identical JSON/HTML across two runs.
    [Fact]
    public async Task RemoteLinuxTarget_IdenticalInput_ProducesByteIdenticalReports_AcrossTwoRuns()
    {
        using var tempA = new TempDirectory();
        using var tempB = new TempDirectory();

        var engineA = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var engineB = new FakeDiscoveryEngine(DiscoveryResultBuilder.Build(ErpFixture.BuildEntities()));
        var transportA = new FakeRemoteTargetTransport(RemoteLinuxTarget);
        var transportB = new FakeRemoteTargetTransport(RemoteLinuxTarget);

        var argsA = new List<string> { "scan", "--output", tempA.Path };
        argsA.AddRange(SshArgs);
        var argsB = new List<string> { "scan", "--output", tempB.Path };
        argsB.AddRange(SshArgs);

        await CliTestRunner.RunAsync([.. argsA], engineA, transport: transportA);
        await CliTestRunner.RunAsync([.. argsB], engineB, transport: transportB);

        var jsonA = await File.ReadAllTextAsync(Path.Combine(tempA.Path, "report.json"));
        var jsonB = await File.ReadAllTextAsync(Path.Combine(tempB.Path, "report.json"));
        var htmlA = await File.ReadAllTextAsync(Path.Combine(tempA.Path, "report.html"));
        var htmlB = await File.ReadAllTextAsync(Path.Combine(tempB.Path, "report.html"));

        // Everything the pipeline itself computes (IDs, ordering, correlation/boundary/risk/
        // migration results) must be byte-identical — the only legitimate difference between two
        // runs is a wall-clock "generated at" timestamp, which is normalized out before comparing.
        Assert.Equal(NormalizeTimestamps(jsonA), NormalizeTimestamps(jsonB));
        Assert.Equal(NormalizeTimestamps(htmlA), NormalizeTimestamps(htmlB));
    }

    // 3. Exactly one pipeline exists — no parallel Remote* engine of any kind was created anywhere in the solution.
    [Fact]
    public void NoSeparateRemotePipelineType_ExistsAnywhereInTheLoadedAssemblies()
    {
        var forbidden = new[]
        {
            "RemoteDiscoveryEngine", "RemoteScanPipeline", "RemoteAnalysisEngine", "RemoteRiskEngine",
            "RemoteMigrationEngine", "RemoteReportEngine", "RemoteCorrelationEngine", "RemoteBoundaryEngine",
            "RemoteExpansionEngine", "RemoteValidationEngine", "RemoteAggregationEngine"
        };

        var relevantAssemblies = new[]
        {
            typeof(ServerSleuth.Core.Orchestration.IDiscoveryEngine).Assembly,
            typeof(ServerSleuth.Analysis.Correlation.CorrelationEngine).Assembly,
            typeof(ServerSleuth.Analysis.Orchestration.ScanPipelineRunner).Assembly
        };

        var offenders = relevantAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => forbidden.Contains(t.Name))
            .ToList();

        Assert.Empty(offenders);
    }

    // 5. Analysis remains platform-agnostic — no reference to Windows/Linux/SSH/WinRM/CIM/network types.
    [Fact]
    public void AnalysisAssembly_ReferencesNoPlatformOrTransportSpecificType()
    {
        var assembly = typeof(ServerSleuth.Analysis.Correlation.CorrelationEngine).Assembly;
        var referencedAssemblyNames = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        var forbiddenPrefixes = new[]
        {
            "ServerSleuth.Windows", "ServerSleuth.Linux", "Renci.SshNet", "Microsoft.Management.Infrastructure",
            "System.Net.Sockets", "System.Net.Http"
        };

        foreach (var name in referencedAssemblyNames)
        {
            Assert.DoesNotContain(forbiddenPrefixes, forbidden => name.StartsWith(forbidden, StringComparison.Ordinal));
        }
    }

    private static readonly System.Text.RegularExpressions.Regex TimestampPattern = new(
        @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+(\+|\\u002[Bb]|&\#x2[Bb];)\d{2}:\d{2}", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string NormalizeTimestamps(string text) => TimestampPattern.Replace(text, "<TIMESTAMP>");
}
