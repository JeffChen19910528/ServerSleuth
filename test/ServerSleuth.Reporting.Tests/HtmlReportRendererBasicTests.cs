using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>Basic rendering contract, action/check grouping, and evidence display — see skill.md
/// (Phase 9B) §2, §13-15, §28.</summary>
public class HtmlReportRendererBasicTests
{
    [Fact]
    public void Format_IsHtml()
    {
        Assert.Equal(ReportFormat.Html, new HtmlReportRenderer().Format);
    }

    [Fact]
    public void Render_ReturnsHtmlFormat_WithUtf8Encoding()
    {
        var report = TestPipeline.Run([]);
        var result = new HtmlReportRenderer().Render(report);

        Assert.Equal(ReportFormat.Html, result.Format);
        Assert.Equal(System.Text.Encoding.UTF8, result.Encoding);
    }

    [Fact]
    public void Actions_AreSeparatedIntoPreMigrationPostMigrationAndReviewDocumentationSections()
    {
        var service = EntityFactory.Service("BasicSvc", @"D:\Basic\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Basic\svc.exe", notFound: true);
        var config = EntityFactory.Configuration(@"D:\Basic\web.config");
        config.SetMetadata("ParseStatus", "AccessDenied");

        var entities = new List<DiscoveryEntity> { service, missingExe, config };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.Contains("<h3>Pre-Migration (", html, StringComparison.Ordinal);
        Assert.Contains("<h3>Post-Migration (", html, StringComparison.Ordinal);
        Assert.Contains("<h3>Review / Documentation (", html, StringComparison.Ordinal);
        // RR3-AccessDenied maps to ReviewAccessDenied — must land in the Review/Documentation bucket.
        Assert.Contains("ReviewAccessDenied", html, StringComparison.Ordinal);
    }

    [Fact]
    public void VerificationChecks_AreRenderedUnderSeparatePrePostSections()
    {
        var expiring = EntityFactory.Certificate("basic.example.com", "BASICCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));
        var report = TestPipeline.Run([expiring]);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.Contains("<h2>Pre-Migration Verification Checks</h2>", html, StringComparison.Ordinal);
        Assert.Contains("<h2>Post-Migration Verification Checks</h2>", html, StringComparison.Ordinal);
        Assert.Contains("VerifyCertificate", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_DisplaysTypeLocationAndDetail()
    {
        var service = EntityFactory.Service("EvidSvc", @"D:\Evid\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Evid\svc.exe", notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        Assert.Contains("class=\"evidence-list\"", html, StringComparison.Ordinal);
        Assert.Contains("ServiceConfiguration", html, StringComparison.Ordinal);
        Assert.Contains("FileStatus=NotFound", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Issues_DisplayRuleIdSeverityConfidenceAndAffectedIds()
    {
        var service = EntityFactory.Service("IssueSvc", @"D:\Issue\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Issue\svc.exe", notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var html = new HtmlReportRenderer().Render(report).Content;

        // Phase 7A's MissingBinaryEntityId merge anchor collapses RR2 (fired against the missing
        // dll) and RR6 (fired against the service's Runs edge) into one finding; the merged
        // finding's RuleId is the ordinally-lower of the two tied-severity RuleIds, RR2-MissingBinary.
        Assert.Contains("RR2-MissingBinary", html, StringComparison.Ordinal);
        Assert.Contains("badge severity-critical", html, StringComparison.Ordinal);
        Assert.Contains("badge impact-blocking", html, StringComparison.Ordinal);
        Assert.Contains("service:IssueSvc", html, StringComparison.Ordinal);
    }
}
