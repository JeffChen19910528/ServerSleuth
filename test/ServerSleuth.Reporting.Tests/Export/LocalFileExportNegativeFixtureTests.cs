using ServerSleuth.Reporting.Export;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests.Export;

/// <summary>Negative fixtures for the export layer — see skill.md (Phase 9C) §20. Nothing
/// should fail to export across any of these edge cases.</summary>
public class LocalFileExportNegativeFixtureTests
{
    private static ReportBundleExportResult ExportEntities(TempDirectory temp, List<Core.Models.DiscoveryEntity> entities)
    {
        var report = TestPipeline.Run(entities);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        return new LocalFileReportExporter().ExportBundle(bundle, temp.Path);
    }

    [Fact]
    public void EmptyReport_ExportsSuccessfully()
    {
        using var temp = new TempDirectory();
        var result = ExportEntities(temp, []);

        Assert.True(result.Success);
        Assert.True(System.IO.File.Exists(result.Json.OutputPath));
        Assert.True(System.IO.File.Exists(result.Html.OutputPath));
    }

    [Fact]
    public void NoApplications_ExportsSuccessfully()
    {
        using var temp = new TempDirectory();
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var result = ExportEntities(temp, [runtime]);

        Assert.True(result.Success);
    }

    [Fact]
    public void CriticalMissingBinary_ExportsSuccessfully_WithBlockedStatus()
    {
        using var temp = new TempDirectory();
        var service = EntityFactory.Service("NegExportSvc", @"D:\Neg\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg\svc.exe", notFound: true);
        var result = ExportEntities(temp, [service, missingExe]);

        Assert.True(result.Success);
        var json = System.IO.File.ReadAllText(result.Json.OutputPath!);
        Assert.Contains("\"OverallMigrationStatus\": \"Blocked\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativeOutputDirectory_ExportsSuccessfully()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(temp.Path);
            var report = TestPipeline.Run([]);
            var bundle = ReportArtifactFactory.CreateBundle(report);

            var result = new LocalFileReportExporter().ExportBundle(bundle, "relative-output");

            Assert.True(result.Success);
            Assert.True(System.IO.File.Exists(Path.Combine(temp.Path, "relative-output", "report.json")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void AbsoluteOutputDirectory_ExportsSuccessfully()
    {
        using var temp = new TempDirectory();
        var absolute = Path.GetFullPath(temp.Path);

        var report = TestPipeline.Run([]);
        var bundle = ReportArtifactFactory.CreateBundle(report);
        var result = new LocalFileReportExporter().ExportBundle(bundle, absolute);

        Assert.True(result.Success);
        Assert.True(Path.IsPathRooted(result.Json.OutputPath));
    }
}
