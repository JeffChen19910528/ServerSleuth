using Microsoft.Extensions.DependencyInjection;
using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Orchestration;
using ServerSleuth.Core.Targets;
using ServerSleuth.Gui.ExecutionHost.Tests.Fakes;
using ServerSleuth.Gui.ExecutionHost.Tests.Fixtures;
using ServerSleuth.Gui.Models;
using ServerSleuth.Reporting.Export;

namespace ServerSleuth.Gui.ExecutionHost.Tests;

/// <summary>
/// GUI-3 §Step14: proves the GUI execution path and a CLI-shaped pipeline invocation produce an
/// EQUIVALENT report for the SAME discovery fixture. This does not duplicate pipeline code to
/// make the comparison pass — both sides literally call
/// <see cref="ScanPipelineRunner"/>/<see cref="ReportArtifactFactory"/>, the exact same types
/// <c>ServerSleuth.Cli.Commands.ScanCommand</c> itself calls (see that type and
/// <see cref="ScanPipelineRunner"/>'s own doc comment — moved into
/// <c>ServerSleuth.Analysis</c> in this same phase specifically so both callers share one
/// implementation). The "CLI side" here is a direct <see cref="ScanPipelineRunner"/> invocation
/// rather than literally shelling out to <c>serversleuth.exe</c> — sufficient to prove pipeline-
/// semantic equivalence, since <c>ScanCommand</c> itself does nothing more than that same call
/// plus console output.
/// </summary>
public sealed class CliGuiEquivalenceTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "serversleuth-equivalence-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GuiExecution_AndDirectPipelineInvocation_ProduceEquivalentReports_ForTheSameFixture()
    {
        var fixture = MinimalFixture.Build();

        // "CLI-shaped" side: call the shared pipeline runner directly, exactly as ScanCommand does.
        var cliDiscoveryEngine = new FakeDiscoveryEngine(fixture);
        var cliPipelineRunner = new ScanPipelineRunner(cliDiscoveryEngine);
        var cliDiscovery = await cliPipelineRunner.DiscoverAsync(
            new DiscoveryContext { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None, Target = ScanTarget.Local(TargetPlatform.Windows) },
            CancellationToken.None);
        var cliPipelineResult = cliPipelineRunner.Analyze(cliDiscovery, CancellationToken.None);
        var cliBundle = ReportArtifactFactory.CreateBundle(cliPipelineResult);

        // GUI side: the real GuiScanExecutor, fakes swapped in only at the transport/discovery-engine seam.
        var guiDiscoveryEngine = new FakeDiscoveryEngine(fixture);
        var guiExecutor = new GuiScanExecutor((request, _) =>
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDiscoveryEngine>(guiDiscoveryEngine);
            return new GuiScanComposition { Transport = new FakeTargetTransport(request.Target), Services = services.BuildServiceProvider() };
        });

        var request = new ScanRequest
        {
            Target = ScanTarget.Local(TargetPlatform.Windows),
            OutputDirectory = _outputDirectory,
            OutputFormat = ScanOutputFormat.Both,
            OverwritePolicy = ScanOverwritePolicy.Overwrite,
            Verbose = false
        };

        var completion = await guiExecutor.ExecuteAsync(
            request, ScanCredentialInput.Empty, new SynchronousProgress<ScanProgressState>(_ => { }), CancellationToken.None);

        var writtenJson = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, ReportArtifactFactory.DefaultJsonFileName));
        var writtenHtml = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, ReportArtifactFactory.DefaultHtmlFileName));

        // Semantic content must match — normalize the one field genuinely allowed to differ
        // (generation timestamp) rather than fabricating a byte-for-byte match.
        Assert.Equal(NormalizeTimestamps(cliBundle.Json.Content), NormalizeTimestamps(writtenJson));
        Assert.Equal(NormalizeTimestamps(cliBundle.Html.Content), NormalizeTimestamps(writtenHtml));

        Assert.Equal(ScanExecutionStatus.Partial, completion.Status);
        Assert.Equal(cliDiscovery.Entities.Count, completion.EntityCount);
    }

    private static string NormalizeTimestamps(string content) =>
        System.Text.RegularExpressions.Regex.Replace(content, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?", "<timestamp>");
}
