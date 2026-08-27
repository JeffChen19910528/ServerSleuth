using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Html;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>No-mutation — see skill.md (Phase 9B) §23.</summary>
public class HtmlReportRendererNoMutationTests
{
    [Fact]
    public void Render_NeverMutates_TheSourceReportOrAnyOfItsCollections()
    {
        var serviceA = EntityFactory.Service("MutA", @"D:\Mut\host.exe");
        var serviceB = EntityFactory.Service("MutB", @"D:\Mut\host.exe");
        var taskC = EntityFactory.ScheduledTask(@"\Mut\MutC", @"D:\Mut\host.exe");
        var exe = EntityFactory.Dll(@"D:\Mut\host.exe");
        var expiring = EntityFactory.Certificate("mut.example.com", "MUTCERT", validTo: DateTimeOffset.UtcNow.AddDays(10));

        var entities = new List<DiscoveryEntity> { serviceA, serviceB, taskC, exe, expiring };
        var report = TestPipeline.Run(entities);

        var appIdsBefore = report.ApplicationAssessments.Select(a => a.Assessment.ApplicationBoundaryId).ToList();
        var serverIssueIdsBefore = report.Assessment.Server.Issues.Select(i => i.IssueId).ToList();
        var dependencyIdsBefore = report.Assessment.Server.Dependencies.Select(d => d.DependencyId).ToList();
        var actionIdsBefore = report.Plan.Actions.Select(a => a.ActionId).ToList();
        var preCheckIdsBefore = report.Plan.PreMigrationChecks.Select(c => c.CheckId).ToList();
        var postCheckIdsBefore = report.Plan.PostMigrationChecks.Select(c => c.CheckId).ToList();
        var diagnosticsBefore = (report.Diagnostics.ApplicationsConsolidated, report.Diagnostics.ServerLevelIssueCount, report.Diagnostics.SharedInfrastructureDependencyCount);

        var renderer = new HtmlReportRenderer();
        renderer.Render(report);
        renderer.Render(report);
        renderer.Render(report);

        Assert.Equal(appIdsBefore, report.ApplicationAssessments.Select(a => a.Assessment.ApplicationBoundaryId));
        Assert.Equal(serverIssueIdsBefore, report.Assessment.Server.Issues.Select(i => i.IssueId));
        Assert.Equal(dependencyIdsBefore, report.Assessment.Server.Dependencies.Select(d => d.DependencyId));
        Assert.Equal(actionIdsBefore, report.Plan.Actions.Select(a => a.ActionId));
        Assert.Equal(preCheckIdsBefore, report.Plan.PreMigrationChecks.Select(c => c.CheckId));
        Assert.Equal(postCheckIdsBefore, report.Plan.PostMigrationChecks.Select(c => c.CheckId));
        Assert.Equal(diagnosticsBefore, (report.Diagnostics.ApplicationsConsolidated, report.Diagnostics.ServerLevelIssueCount, report.Diagnostics.SharedInfrastructureDependencyCount));
    }
}
