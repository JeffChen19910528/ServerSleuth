using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Negative fixtures for the HTML renderer — see skill.md (Phase 9B) §25. Nothing
/// should disappear or fail to render across any of these edge cases.</summary>
public class HtmlReportRendererNegativeFixtureTests
{
    private static string Render(List<DiscoveryEntity> entities)
    {
        var report = TestPipeline.Run(entities);
        return new HtmlReportRenderer().Render(report).Content;
    }

    [Fact]
    public void EmptyReport_RendersValidDocument_ReadyStatus_NoApplications()
    {
        var html = Render([]);
        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("badge status-ready\"", html, StringComparison.Ordinal);
        Assert.Contains("No application boundaries with attributed findings.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NoFindings_RendersReady()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var html = Render([runtime]);
        Assert.Contains("badge status-ready\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void InfoOnlyFindings_RenderReady_WithServerLevelIssueVisible()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config", dependencyReferences: ["EnvVar: APP_HOME"]);
        var html = Render([config]);

        Assert.Contains("badge status-ready\"", html, StringComparison.Ordinal);
        Assert.Contains("badge impact-informational", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LowRiskFindings_RenderReady()
    {
        var config = EntityFactory.Configuration("/etc/app/app.conf", dependencyReferences: ["UnixSocket: /var/run/app.sock"]);
        var html = Render([config]);
        Assert.Contains("badge status-ready\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MediumRiskExternalDependency_RendersReadyWithConditions()
    {
        var config = EntityFactory.Configuration(@"D:\App\web.config");
        config.SetMetadata("Database0.Type", "SqlServer");
        config.SetMetadata("Database0.Host", "DB01");
        config.SetMetadata("Database0.Port", "1433");
        config.SetMetadata("Database0.Name", "AppDb");

        var html = Render([config]);
        Assert.Contains("badge status-ready-with-conditions", html, StringComparison.Ordinal);
        Assert.Contains("badge severity-medium", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HighRiskCertificateIssue_RendersNeedsRemediation()
    {
        var expiring = EntityFactory.Certificate("high.example.com", "HIGHCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var html = Render([expiring]);

        Assert.Contains("badge status-needs-remediation", html, StringComparison.Ordinal);
        Assert.Contains("badge severity-high", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalMissingBinary_RendersBlocked()
    {
        var service = EntityFactory.Service("NegSvc", @"D:\Neg\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg\svc.exe", notFound: true);
        var html = Render([service, missingExe]);

        Assert.Contains("badge status-blocked", html, StringComparison.Ordinal);
        Assert.Contains("badge severity-critical", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedDependencyAcrossThreeBoundaries_RendersOneLogicalDependency()
    {
        var serviceA = EntityFactory.Service("NegA", @"D:\Neg\host.exe");
        var serviceB = EntityFactory.Service("NegB", @"D:\Neg\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Neg\NegC", @"D:\Neg\host.exe");
        var exe = EntityFactory.Dll(@"D:\Neg\host.exe");

        var html = Render([serviceA, serviceB, taskC, exe]);
        var sectionStart = html.IndexOf("id=\"shared-infrastructure\"", StringComparison.Ordinal);
        var sectionEnd = html.IndexOf("<section", sectionStart + 1, StringComparison.Ordinal);
        var section = html[sectionStart..sectionEnd];

        var occurrences = System.Text.RegularExpressions.Regex.Matches(section, "dependency:SharedBinary:").Count;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void ServerLevelIssue_NoApplicationBoundary_StillRendersVisibly()
    {
        var expiring = EntityFactory.Certificate("srvonly.example.com", "SRVONLY", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var html = Render([expiring]);

        Assert.Contains("No application boundaries with attributed findings.", html, StringComparison.Ordinal);
        Assert.Contains("RR5-CertificateExpiry", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpectedOrphanRuntimeAndCertificate_RenderWithNoFalseFindings()
    {
        var runtime = EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0");
        var certificate = EntityFactory.Certificate("orphan.example.com", "ORPHANCERT", validTo: DateTimeOffset.UtcNow.AddYears(2));

        var html = Render([runtime, certificate]);
        Assert.Contains("No actions required.", html, StringComparison.Ordinal);
    }
}
