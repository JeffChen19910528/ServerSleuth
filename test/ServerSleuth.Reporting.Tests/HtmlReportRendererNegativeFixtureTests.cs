using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Negative fixtures for the HTML renderer — every edge case below (empty report, info/low/
/// medium/high/critical risk findings, orphan entities, server-level-only issues, shared
/// dependencies) must still render a valid document. None of these Risk/Migration severities are
/// rendered any more (the report shows deployment inventory, not risk status) — these tests
/// confirm that holds even for the fixtures that used to prove the opposite.
/// </summary>
public class HtmlReportRendererNegativeFixtureTests
{
    private static string Render(List<DiscoveryEntity> entities)
    {
        var (report, discovery, boundaries) = TestPipeline.RunWithInventory(entities);
        return new HtmlReportRenderer(discovery: discovery, boundaries: boundaries, externalDependencies: [])
            .Render(report).Content;
    }

    [Theory]
    [MemberData(nameof(EdgeCaseFixtures))]
    public void EdgeCase_RendersValidDocument_WithNoRiskOrMigrationStatus(List<DiscoveryEntity> entities)
    {
        var html = Render(entities);
        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("badge status-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("badge severity-", html, StringComparison.Ordinal);
        Assert.DoesNotContain("badge impact-", html, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> EdgeCaseFixtures()
    {
        yield return [new List<DiscoveryEntity>()];
        yield return [new List<DiscoveryEntity> { EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", "10.0.0") }];
        yield return [new List<DiscoveryEntity> { EntityFactory.Certificate("high.example.com", "HIGHCERT", validTo: DateTimeOffset.UtcNow.AddDays(10)) }];

        var service = EntityFactory.Service("NegSvc", @"D:\Neg\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Neg\svc.exe", notFound: true);
        yield return [new List<DiscoveryEntity> { service, missingExe }];
    }

    [Fact]
    public void SharedDependencyAcrossThreeAnchors_AllThreeStillAppearAsApplications()
    {
        var serviceA = EntityFactory.Service("NegA", @"D:\Neg\host.exe");
        var serviceB = EntityFactory.Service("NegB", @"D:\Neg\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Neg\NegC", @"D:\Neg\host.exe");
        var exe = EntityFactory.Dll(@"D:\Neg\host.exe");

        var html = Render([serviceA, serviceB, taskC, exe]);
        Assert.Contains(">NegA<", html, StringComparison.Ordinal);
        Assert.Contains(">NegB<", html, StringComparison.Ordinal);
        Assert.Contains("NegC", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerLevelOnlyFinding_NoApplicationBoundary_DoesNotAppearAsApplication()
    {
        // A lone Certificate with no owning Service/IIS Application/ScheduledTask anchors no
        // ApplicationBoundary — it simply doesn't appear in the Applications section (nothing
        // was "deployed" here in the sense this report cares about).
        var expiring = EntityFactory.Certificate("srvonly.example.com", "SRVONLY", validTo: DateTimeOffset.UtcNow.AddDays(3));
        var html = Render([expiring]);

        Assert.Contains("id=\"applications\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("RR5-CertificateExpiry", html, StringComparison.Ordinal);
    }
}
