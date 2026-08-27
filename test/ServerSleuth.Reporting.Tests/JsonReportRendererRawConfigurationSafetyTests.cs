using System.Reflection;
using ServerSleuth.Core.Models;
using ServerSleuth.Reporting.Json;
using ServerSleuth.Reporting.Json.Dto;
using ServerSleuth.Reporting.Tests.Fixtures;

namespace ServerSleuth.Reporting.Tests;

/// <summary>
/// Raw configuration non-disclosure — see skill.md (Phase 9A) §7. <c>ServerSleuth.Core.Models.Configuration</c>
/// never carries full file contents in the first place (see its own doc comment: "Never carries
/// full file contents, only detected sections/references and a secret-presence flag" — enforced
/// since Phase 4E-1/6E, not something Reporting re-implements); only structured facts already
/// present in <c>MigrationIssue</c>/<c>MigrationDependency</c>/etc. can ever reach this layer.
/// </summary>
public class JsonReportRendererRawConfigurationSafetyTests
{
    [Fact]
    public void RenderedJson_NeverContainsXmlConfigurationMarkup()
    {
        var app = EntityFactory.Application("XmlApp", "/", @"D:\XmlApp");
        var config = EntityFactory.Configuration(@"D:\XmlApp\web.config", ownerEntityId: app.Id,
            dependencyReferences: ["FileShare: \\\\FILESERVER\\Share"]);

        var entities = new List<DiscoveryEntity> { app, config };
        var report = TestPipeline.Run(entities);
        var json = new JsonReportRenderer().Render(report).Content;

        Assert.DoesNotContain("<configuration>", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<system.webServer>", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<connectionStrings>", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderedJson_NeverContainsSystemdOrDockerRawUnitContent()
    {
        var service = EntityFactory.Service("UnitSvc", @"D:\Unit\svc.exe");
        var missingExe = EntityFactory.Dll(@"D:\Unit\svc.exe", notFound: true);

        var entities = new List<DiscoveryEntity> { service, missingExe };
        var report = TestPipeline.Run(entities);
        var json = new JsonReportRenderer().Render(report).Content;

        Assert.DoesNotContain("[Unit]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("[Service]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecStart=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Image\":", json, StringComparison.Ordinal);
    }

    /// <summary>Structural proof, not just content-based: <c>Configuration</c> itself has no
    /// property capable of carrying raw file content, so no DTO mapping from it ever could
    /// either — verified directly against the domain model, not merely the DTO layer.</summary>
    [Fact]
    public void ConfigurationDomainModel_HasNoRawContentProperty()
    {
        var properties = typeof(Configuration).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();

        Assert.DoesNotContain(properties, name => name.Contains("Content", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("RawText", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("FileBody", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoDtoType_ExposesARawContentProperty()
    {
        var dtoTypes = typeof(ServerReportDto).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ServerReportDto).Namespace);

        foreach (var type in dtoTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.False(property.Name.Contains("RawContent", StringComparison.OrdinalIgnoreCase), $"{type.Name}.{property.Name}");
                Assert.False(property.Name.Contains("FileContent", StringComparison.OrdinalIgnoreCase), $"{type.Name}.{property.Name}");
            }
        }
    }
}
