using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Models;

namespace ServerSleuth.Analysis.Tests.Diagnostics;

/// <summary>
/// Builds a synthetic entity set whose composition is loosely modeled on the real dev-machine
/// scan that motivated this investigation (skill.md Phase 10A-H §11): COM-heavy (many CLSIDs
/// legitimately pointing at a small number of shared system binaries — the same "hub" shape a
/// real Windows registry has), plus a proportional mix of Services/ScheduledTasks/Applications/
/// Configurations/Runtimes/Certificates, each producing real correlation edges (not an
/// artificially flat, disconnected entity list). No real configuration content or credentials
/// are used anywhere — every value is synthetic.
/// </summary>
internal static class RealisticScaleFixtureBuilder
{
    public static List<DiscoveryEntity> Build(int scale)
    {
        var entities = new List<DiscoveryEntity>();

        var binaryCount = Math.Max(10, scale * 40 / 100);
        var comCount = Math.Max(10, scale * 40 / 100);
        var serviceCount = Math.Max(2, scale * 5 / 100);
        var taskCount = Math.Max(1, scale * 2 / 100);
        var appCount = Math.Max(1, scale * 3 / 100);
        var runtimeCount = Math.Clamp(scale / 500, 1, 20);
        var certCount = Math.Clamp(scale / 200, 1, 50);

        // A small number of "hub" binaries that a large fraction of COM registrations and
        // Services legitimately reference — mirrors real Windows machines, where many CLSIDs
        // resolve to a handful of common system DLLs (ole32.dll, shell32.dll, etc.).
        var hubCount = Math.Max(5, binaryCount / 100);
        var hubBinaries = new List<Dll>();
        for (var i = 0; i < hubCount; i++)
        {
            hubBinaries.Add(EntityFactory.Dll($@"C:\Windows\System32\hub{i}.dll"));
        }
        entities.AddRange(hubBinaries);

        var perAppBinaries = new List<Dll>();
        for (var i = 0; i < binaryCount - hubCount; i++)
        {
            var appIndex = i % Math.Max(1, appCount);
            perAppBinaries.Add(EntityFactory.Dll($@"C:\App{appIndex}\bin\module{i}.dll"));
        }
        entities.AddRange(perAppBinaries);

        var allBinaries = hubBinaries.Concat(perAppBinaries).ToList();

        for (var i = 0; i < comCount; i++)
        {
            var target = allBinaries[i % allBinaries.Count];
            entities.Add(EntityFactory.Com($"{{{i:D8}-0000-0000-0000-000000000000}}", inprocServer32: target.Path));
        }

        for (var i = 0; i < serviceCount; i++)
        {
            var target = allBinaries[i % allBinaries.Count];
            entities.Add(EntityFactory.Service($"SvcScale{i}", target.Path));
        }

        for (var i = 0; i < taskCount; i++)
        {
            var target = allBinaries[i % allBinaries.Count];
            entities.Add(EntityFactory.ScheduledTask($@"\ScaleTasks\Task{i}", target.Path));
        }

        for (var i = 0; i < appCount; i++)
        {
            var site = EntityFactory.Site($"ScaleSite{i}");
            var pool = EntityFactory.ApplicationPool($"ScalePool{i}");
            var app = EntityFactory.Application($"ScaleSite{i}", "/", $@"C:\App{i}", poolId: pool.Id, siteId: site.Id);
            entities.Add(site);
            entities.Add(pool);
            entities.Add(app);

            var config = EntityFactory.Configuration($@"C:\App{i}\web.config", ownerEntityId: app.Id,
                dependencyReferences: ["RuntimeVersion: net8.0"]);
            entities.Add(config);
        }

        for (var i = 0; i < runtimeCount; i++)
        {
            entities.Add(EntityFactory.Runtime("DotNetRuntime", ".NET Runtime", $"{8 + i % 3}.0.{i}"));
        }

        for (var i = 0; i < certCount; i++)
        {
            entities.Add(EntityFactory.Certificate($"scale-cert-{i}.example.com", $"SCALETHUMB{i:D8}", validTo: DateTimeOffset.UtcNow.AddYears(1)));
        }

        return entities;
    }
}
