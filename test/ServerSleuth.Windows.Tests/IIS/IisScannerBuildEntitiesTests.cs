using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Models;
using ServerSleuth.Windows.IIS;
using CoreApplication = ServerSleuth.Core.Models.Application;
using CoreApplicationPool = ServerSleuth.Core.Models.ApplicationPool;
using CoreWebSite = ServerSleuth.Core.Models.WebSite;

namespace ServerSleuth.Windows.Tests.IIS;

public class IisScannerBuildEntitiesTests
{
    private static IisSnapshot MakeSnapshot() => new()
    {
        Sites =
        [
            new IisSiteRow
            {
                Name = "ERP",
                SiteId = 1,
                State = "Started",
                PhysicalPath = @"D:\Web\ERP",
                Bindings =
                [
                    new IisBindingRow { Protocol = "https", IpAddress = "0.0.0.0", Port = 443, HostName = "erp.company.com", BindingInformation = "0.0.0.0:443:erp.company.com", CertificateThumbprint = "AB12CD34", CertificateStoreName = "My" }
                ],
                Applications =
                [
                    new IisApplicationRow { VirtualPath = "/", PhysicalPath = @"D:\Web\ERP", ApplicationPoolName = "ERPAppPool" },
                    new IisApplicationRow { VirtualPath = "/api", PhysicalPath = @"D:\Web\ERP\api", ApplicationPoolName = "ERPAppPool" }
                ]
            }
        ],
        ApplicationPools =
        [
            new IisAppPoolRow
            {
                Name = "ERPAppPool",
                State = "Started",
                ManagedRuntimeVersion = "v4.0",
                ManagedPipelineMode = "Integrated",
                IdentityType = "ApplicationPoolIdentity",
                Enable32BitAppOnWin64 = false,
                StartMode = "AlwaysRunning"
            }
        ]
    };

    [Fact]
    public void BuildEntities_MapsWebSiteWithPrimaryBindingFields()
    {
        var entities = IisScanner.BuildEntities(MakeSnapshot());

        var site = Assert.Single(entities.OfType<CoreWebSite>());
        Assert.Equal(@"D:\Web\ERP", site.PhysicalPath);
        Assert.Equal("https", site.Protocol);
        Assert.Equal("erp.company.com", site.HostName);
        Assert.Equal(443, site.Port);
        Assert.Equal("AB12CD34", site.CertificateThumbprint);
        Assert.Equal(EntityStatus.Running, site.Status);
        Assert.Single(site.Bindings);
        Assert.Contains(site.Evidence, e => e.Type == EvidenceType.IisConfiguration);
    }

    [Fact]
    public void BuildEntities_RecordsCertificateThumbprintAsMetadataNotJustOnSite()
    {
        var entities = IisScanner.BuildEntities(MakeSnapshot());

        var site = Assert.Single(entities.OfType<CoreWebSite>());
        Assert.Equal("AB12CD34", site.Metadata["Binding0.CertificateThumbprint"]);
        Assert.Equal("My", site.Metadata["Binding0.CertificateStore"]);
    }

    [Fact]
    public void BuildEntities_MapsRootApplicationNameWithoutTrailingSlash()
    {
        var entities = IisScanner.BuildEntities(MakeSnapshot());

        var rootApp = entities.OfType<CoreApplication>().Single(a => a.Metadata["VirtualPath"] == "/");
        Assert.Equal("ERP", rootApp.Name);
        Assert.Equal(@"D:\Web\ERP", rootApp.Path);
    }

    [Fact]
    public void BuildEntities_MapsSubApplicationPreservingVirtualPath()
    {
        var entities = IisScanner.BuildEntities(MakeSnapshot());

        var subApp = entities.OfType<CoreApplication>().Single(a => a.Metadata["VirtualPath"] == "/api");
        Assert.Equal("ERP/api", subApp.Name);
        Assert.Equal(@"D:\Web\ERP\api", subApp.Path);
    }

    [Fact]
    public void BuildEntities_ApplicationComponentEntityIds_ReferenceSiteAndPool()
    {
        var entities = IisScanner.BuildEntities(MakeSnapshot());

        var rootApp = entities.OfType<CoreApplication>().Single(a => a.Metadata["VirtualPath"] == "/");
        Assert.Contains("iis-site:ERP", rootApp.ComponentEntityIds);
        Assert.Contains("iis-apppool:ERPAppPool", rootApp.ComponentEntityIds);
    }

    [Fact]
    public void BuildEntities_MapsApplicationPoolFields()
    {
        var entities = IisScanner.BuildEntities(MakeSnapshot());

        var pool = Assert.Single(entities.OfType<CoreApplicationPool>());
        Assert.Equal("v4.0", pool.ManagedRuntimeVersion);
        Assert.Equal("Integrated", pool.PipelineMode);
        Assert.Equal("ApplicationPoolIdentity", pool.Identity);
        Assert.Equal("AlwaysRunning", pool.StartMode);
        Assert.False(pool.Enable32BitAppOnWin64);
        Assert.Equal(EntityStatus.Running, pool.Status);
    }

    [Fact]
    public void BuildEntities_SpecificUserIdentity_RecordsUserNameAndMigrationRelevantTag()
    {
        var snapshot = MakeSnapshot() with
        {
            ApplicationPools =
            [
                new IisAppPoolRow
                {
                    Name = "ERPAppPool",
                    State = "Started",
                    IdentityType = "SpecificUser",
                    UserName = @"CONTOSO\svc-erp",
                    Enable32BitAppOnWin64 = true
                }
            ]
        };

        var pool = IisScanner.BuildEntities(snapshot).OfType<CoreApplicationPool>().Single();

        Assert.Equal(@"CONTOSO\svc-erp", pool.Identity);
        Assert.Contains("MigrationRelevant", pool.Tags);
        Assert.True(pool.Enable32BitAppOnWin64);
    }

    [Fact]
    public void BuildEntities_NoManagedCodePool_ReportsFriendlyRuntimeLabel()
    {
        var snapshot = MakeSnapshot() with
        {
            ApplicationPools =
            [
                new IisAppPoolRow { Name = "StaticPool", State = "Started", ManagedRuntimeVersion = "", IdentityType = "ApplicationPoolIdentity" }
            ]
        };

        var pool = IisScanner.BuildEntities(snapshot).OfType<CoreApplicationPool>().Single();

        Assert.Equal("No Managed Code", pool.ManagedRuntimeVersion);
    }

    [Fact]
    public void BuildEntities_MissingPhysicalPath_RecordsUnavailableNotEmptyString()
    {
        var snapshot = MakeSnapshot() with
        {
            Sites =
            [
                new IisSiteRow
                {
                    Name = "Broken",
                    SiteId = 2,
                    State = "Started",
                    PhysicalPath = null,
                    Applications = [new IisApplicationRow { VirtualPath = "/", PhysicalPath = null }]
                }
            ]
        };

        var site = IisScanner.BuildEntities(snapshot).OfType<CoreWebSite>().Single();

        Assert.Null(site.PhysicalPath);
        Assert.Equal("Unavailable", site.Metadata["PhysicalPathStatus"]);
    }

    [Fact]
    public void BuildEntities_StoppedSite_MapsToConfiguredNotRunning()
    {
        var snapshot = MakeSnapshot() with
        {
            Sites = [MakeSnapshot().Sites[0] with { State = "Stopped" }]
        };

        var site = IisScanner.BuildEntities(snapshot).OfType<CoreWebSite>().Single();

        Assert.Equal(EntityStatus.Configured, site.Status);
    }

    [Fact]
    public void BuildEntities_NoSitesOrPools_ReturnsEmptyList()
    {
        var entities = IisScanner.BuildEntities(new IisSnapshot());

        Assert.Empty(entities);
    }
}
