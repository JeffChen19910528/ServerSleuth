using ServerSleuth.Analysis.Orchestration;
using ServerSleuth.Core.Boundaries;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Evidence;
using ServerSleuth.Core.Models;
using ServerSleuth.Gui.TestFixtures;
using ServerSleuth.Gui.ViewModels.Results;

namespace ServerSleuth.Gui.Tests.ViewModels.Results;

/// <summary>
/// GUI-8B: ApplicationComponentsViewModel resolves which discovered entities belong to each
/// application boundary and classifies them by C# type. Every test uses hand-built fixtures;
/// no scanner is run, no pipeline engine is called, no credentials appear anywhere.
///
/// Tests 1–10: entity type membership and correct classification.
/// Tests 11–14: multi-application and edge-case correctness.
/// Tests 15–20: invariants — risk unchanged, no credentials, no scan execution, no mutation,
/// deterministic ordering, real report fixture DLL acceptance.
/// </summary>
public class ApplicationComponentsViewModelTests
{
    // ── helpers ──────────────────────────────────────────────────────────────────────

    private static Dll MakeDll(string id, string name) =>
        new() { Id = id, Name = name, Type = "Dll", Source = "FileSystem" };

    private static Runtime MakeRuntime(string id, string name) =>
        new() { Id = id, Name = name, Type = "DotNetRuntime", Source = "Command" };

    private static Service MakeService(string id, string name) =>
        new() { Id = id, Name = name, Type = "Service", Source = "ServiceControlManager",
                Status = EntityStatus.Running, Confidence = Confidence.VeryHigh() };

    private static ComComponent MakeCom(string id, string name) =>
        new() { Id = id, Name = name, Type = "ComComponent", Source = "Registry",
                Clsid = $"{{{id}}}" };

    private static Configuration MakeConfig(string id, string name) =>
        new() { Id = id, Name = name, Type = "Configuration", Source = "FileSystem" };

    private static Certificate MakeCert(string id, string name) =>
        new() { Id = id, Name = name, Type = "Certificate", Source = "CertStore",
                Thumbprint = id };

    private static ScheduledTask MakeTask(string id, string name) =>
        new() { Id = id, Name = name, Type = "ScheduledTask", Source = "TaskScheduler" };

    private static Software MakeSoftware(string id, string name) =>
        new() { Id = id, Name = name, Type = "Software", Source = "Registry" };

    private static ExternalDependency MakeExternal(string id, string name, string kind = "Database") =>
        new() { Id = id, Name = name, Type = "ExternalDependency", Source = "Configuration",
                Kind = kind, Endpoint = $"server/{name}" };

    private static ApplicationBoundary MakeBoundary(string id, params string[] memberIds) =>
        new() { Id = id, Name = id, MemberEntityIds = memberIds,
                Confidence = Confidence.High(), Reason = "Test boundary" };

    /// <summary>Builds a minimal ScanPipelineResult with given entities and boundaries, using
    /// ScanResultFixtureFactory for the report/risk scaffolding — never touches a real scanner.</summary>
    private static ScanPipelineResult BuildPipeline(
        IReadOnlyList<DiscoveryEntity> entities,
        IReadOnlyList<ApplicationBoundary> boundaries,
        IReadOnlyList<ExternalDependency>? externalDeps = null)
    {
        return ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 0,
            DiscoveryEntities = entities,
            Boundaries = boundaries,
            ExternalDependencies = externalDeps ?? []
        });
    }

    // ── Test 1: Boundary member resolution ───────────────────────────────────────────

    [Fact]
    public void BoundaryMemberResolution_OnlyIncludesEntitiesInThatBoundary()
    {
        var dll1 = MakeDll("dll:app-a", "App.dll");
        var dll2 = MakeDll("dll:app-b", "Other.dll");
        var boundary = MakeBoundary("boundary:a", "dll:app-a");
        var pipeline = BuildPipeline([dll1, dll2], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:a", boundaryIndex, entityIndex);

        Assert.Single(vm.DllBinaries);
        Assert.Equal("App.dll", vm.DllBinaries[0].Name);
    }

    // ── Test 2: DLL / Binary ─────────────────────────────────────────────────────────

    [Fact]
    public void DllBinaries_ContainsMemberDlls_NotOtherApplicationsDlls()
    {
        var dapper = MakeDll("dll:Dapper", "Dapper.dll");
        var epplus = MakeDll("dll:EPPlus", "EPPlus.dll");
        var unrelated = MakeDll("dll:Other", "Unrelated.dll");
        var boundary = MakeBoundary("boundary:test", "dll:Dapper", "dll:EPPlus");
        var pipeline = BuildPipeline([dapper, epplus, unrelated], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:test", boundaryIndex, entityIndex);

        Assert.Equal(2, vm.DllBinaries.Count);
        Assert.Contains(vm.DllBinaries, d => d.Name == "Dapper.dll");
        Assert.Contains(vm.DllBinaries, d => d.Name == "EPPlus.dll");
        Assert.DoesNotContain(vm.DllBinaries, d => d.Name == "Unrelated.dll");
    }

    // ── Test 3: Runtime entities ─────────────────────────────────────────────────────

    [Fact]
    public void Runtimes_ContainsMemberRuntimeEntities()
    {
        var rt = MakeRuntime("rt:dotnet8", ".NET 8.0");
        var boundary = MakeBoundary("boundary:app", "rt:dotnet8");
        var pipeline = BuildPipeline([rt], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.Runtimes);
        Assert.Equal(".NET 8.0", vm.Runtimes[0].Name);
        Assert.True(vm.HasRuntimes);
        Assert.False(vm.HasDllBinaries);
    }

    // ── Test 4: Services ─────────────────────────────────────────────────────────────

    [Fact]
    public void Services_ContainsMemberServiceEntities()
    {
        var svc = MakeService("service:Worker", "WorkerService");
        var boundary = MakeBoundary("boundary:app", "service:Worker");
        var pipeline = BuildPipeline([svc], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.Services);
        Assert.Equal("WorkerService", vm.Services[0].Name);
        Assert.True(vm.HasServices);
    }

    // ── Test 5: COM Components ───────────────────────────────────────────────────────

    [Fact]
    public void ComComponents_ContainsMemberComComponentEntities()
    {
        var com = MakeCom("com:{1234}", "MyActiveX");
        var boundary = MakeBoundary("boundary:app", "com:{1234}");
        var pipeline = BuildPipeline([com], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.ComComponents);
        Assert.Equal("MyActiveX", vm.ComComponents[0].Name);
        Assert.True(vm.HasComComponents);
    }

    // ── Test 6: Configuration ────────────────────────────────────────────────────────

    [Fact]
    public void Configurations_ContainsMemberConfigurationEntities()
    {
        var cfg = MakeConfig("cfg:web.config", "web.config");
        var boundary = MakeBoundary("boundary:app", "cfg:web.config");
        var pipeline = BuildPipeline([cfg], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.Configurations);
        Assert.Equal("web.config", vm.Configurations[0].Name);
        Assert.True(vm.HasConfigurations);
    }

    // ── Test 7: Certificates ─────────────────────────────────────────────────────────

    [Fact]
    public void Certificates_ContainsMemberCertificateEntities()
    {
        var cert = MakeCert("cert:AABBCC", "app.example.com");
        var boundary = MakeBoundary("boundary:app", "cert:AABBCC");
        var pipeline = BuildPipeline([cert], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.Certificates);
        Assert.Equal("app.example.com", vm.Certificates[0].Name);
        Assert.Equal("cert:AABBCC", vm.Certificates[0].Thumbprint); // MakeCert sets Thumbprint = id
        Assert.True(vm.HasCertificates);
    }

    // ── Test 8: Scheduled Tasks ──────────────────────────────────────────────────────

    [Fact]
    public void ScheduledTasks_ContainsMemberScheduledTaskEntities()
    {
        var task = MakeTask("task:NightlyJob", "NightlyJob");
        var boundary = MakeBoundary("boundary:app", "task:NightlyJob");
        var pipeline = BuildPipeline([task], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.ScheduledTasks);
        Assert.Equal("NightlyJob", vm.ScheduledTasks[0].Name);
        Assert.True(vm.HasScheduledTasks);
    }

    // ── Test 9: Software ─────────────────────────────────────────────────────────────

    [Fact]
    public void Software_ContainsMemberSoftwareEntities()
    {
        var sw = MakeSoftware("sw:Crystal", "Crystal Reports Runtime");
        var boundary = MakeBoundary("boundary:app", "sw:Crystal");
        var pipeline = BuildPipeline([sw], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.Software);
        Assert.Equal("Crystal Reports Runtime", vm.Software[0].Name);
        Assert.True(vm.HasSoftware);
    }

    // ── Test 10: External Dependencies where attributable ────────────────────────────

    [Fact]
    public void ExternalConnections_AppearsWhenExternalDependencyIdIsInMemberEntityIds()
    {
        var ext = MakeExternal("ext:db-1", "ProductionDB", "Database");
        var boundary = MakeBoundary("boundary:app", "ext:db-1");
        var pipeline = BuildPipeline([], [boundary], [ext]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Single(vm.ExternalConnections);
        Assert.Equal("ProductionDB", vm.ExternalConnections[0].Name);
        Assert.Equal("Database", vm.ExternalConnections[0].Kind);
        Assert.True(vm.HasExternalConnections);
    }

    // ── Test 11: Shared entities appear in all owning applications ───────────────────

    [Fact]
    public void SharedEntities_AppearInAllOwningApplications_NotJustFirst()
    {
        var sharedDll = MakeDll("dll:shared", "Shared.dll");
        var dllA = MakeDll("dll:a-only", "AppA.dll");
        var dllB = MakeDll("dll:b-only", "AppB.dll");
        var boundaryA = MakeBoundary("boundary:a", "dll:shared", "dll:a-only");
        var boundaryB = MakeBoundary("boundary:b", "dll:shared", "dll:b-only");
        var pipeline = BuildPipeline([sharedDll, dllA, dllB], [boundaryA, boundaryB]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vmA = ApplicationRowViewModel.ResolveComponents("boundary:a", boundaryIndex, entityIndex);
        var vmB = ApplicationRowViewModel.ResolveComponents("boundary:b", boundaryIndex, entityIndex);

        // Shared.dll appears in BOTH applications
        Assert.Contains(vmA.DllBinaries, d => d.Name == "Shared.dll");
        Assert.Contains(vmB.DllBinaries, d => d.Name == "Shared.dll");
        // Application-specific DLLs appear only in their own application
        Assert.Contains(vmA.DllBinaries, d => d.Name == "AppA.dll");
        Assert.DoesNotContain(vmA.DllBinaries, d => d.Name == "AppB.dll");
        Assert.Contains(vmB.DllBinaries, d => d.Name == "AppB.dll");
        Assert.DoesNotContain(vmB.DllBinaries, d => d.Name == "AppA.dll");
    }

    // ── Test 12: Missing attribution does not fabricate an application ───────────────

    [Fact]
    public void NoBoundary_ProducesEmptyComponents_NotFabricatedData()
    {
        var dll = MakeDll("dll:unassigned", "Orphan.dll");
        var pipeline = BuildPipeline([dll], []);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:nonexistent", boundaryIndex, entityIndex);

        Assert.False(vm.HasAnyComponents);
        Assert.Empty(vm.DllBinaries);
        Assert.Empty(vm.Services);
        Assert.Empty(vm.Runtimes);
    }

    // ── Test 13: Empty categories remain empty ───────────────────────────────────────

    [Fact]
    public void EmptyCategories_HaveFalseFlags_AndEmptyLists()
    {
        var dll = MakeDll("dll:one", "One.dll");
        var boundary = MakeBoundary("boundary:app", "dll:one");
        var pipeline = BuildPipeline([dll], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.True(vm.HasDllBinaries);
        Assert.False(vm.HasRuntimes);
        Assert.False(vm.HasServices);
        Assert.False(vm.HasComComponents);
        Assert.False(vm.HasConfigurations);
        Assert.False(vm.HasCertificates);
        Assert.False(vm.HasScheduledTasks);
        Assert.False(vm.HasSoftware);
        Assert.False(vm.HasExternalConnections);

        Assert.Empty(vm.Runtimes);
        Assert.Empty(vm.Services);
        Assert.Empty(vm.ComComponents);
        Assert.Empty(vm.Configurations);
        Assert.Empty(vm.Certificates);
        Assert.Empty(vm.ScheduledTasks);
        Assert.Empty(vm.Software);
        Assert.Empty(vm.ExternalConnections);
    }

    // ── Test 14: Migration preparation counts match actual entities ──────────────────

    [Fact]
    public void MigrationPrepCounts_MatchActualEntityCounts()
    {
        var dlls = new[] { MakeDll("dll:1", "A.dll"), MakeDll("dll:2", "B.dll"), MakeDll("dll:3", "C.dll") };
        var services = new[] { MakeService("svc:1", "SvcA"), MakeService("svc:2", "SvcB") };
        var certs = new[] { MakeCert("cert:1", "cert.example.com") };
        var boundary = MakeBoundary("boundary:app",
            "dll:1", "dll:2", "dll:3", "svc:1", "svc:2", "cert:1");
        var entities = dlls.Cast<DiscoveryEntity>()
            .Concat(services).Concat(certs).ToList();
        var pipeline = BuildPipeline(entities, [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        Assert.Equal(3, vm.DllBinaryCount);
        Assert.Equal(2, vm.ServiceCount);
        Assert.Equal(1, vm.CertificateCount);
        Assert.Equal(0, vm.RuntimeCount);
        Assert.Equal(0, vm.ComComponentCount);
        Assert.Equal(0, vm.ConfigurationCount);
        Assert.Equal(0, vm.ScheduledTaskCount);
        Assert.Equal(0, vm.SoftwareCount);
        Assert.Equal(0, vm.ExternalConnectionCount);

        // Counts match actual list lengths
        Assert.Equal(vm.DllBinaryCount, vm.DllBinaries.Count);
        Assert.Equal(vm.ServiceCount, vm.Services.Count);
        Assert.Equal(vm.CertificateCount, vm.Certificates.Count);
    }

    // ── Test 15: Risk assessment remains unchanged ───────────────────────────────────

    [Fact]
    public void RiskAssessment_RemainsUnchangedAfterAddingComponents()
    {
        var pipeline = ScanResultFixtureFactory.Build(new ScanResultFixtureFactory.Options
        {
            ApplicationCount = 2,
            FindingsPerApplication = 3,
            DiscoveryEntities = [MakeDll("dll:x", "X.dll")],
            Boundaries = [MakeBoundary("boundary-00000", "dll:x")]
        });
        var state = ScanResultFixtureFactory.BuildCompletedState() with { PipelineResult = pipeline };
        var vm = new ResultsDashboardViewModel(state);

        var detail = vm.Applications[0].Detail;

        // Components is populated for boundary-00000 (has DLL)
        // Risk and migration data must be unchanged from the pipeline record
        Assert.Equal(3, detail.FindingCount);
        Assert.Equal(3, detail.AllFindings.Count);
        Assert.Equal(3, detail.Issues.Count);
        // Components field exists alongside risk — neither replaces the other
        Assert.NotNull(detail.Components);
    }

    // ── Test 16: No credential-shaped state ──────────────────────────────────────────

    [Fact]
    public void ApplicationComponentsViewModel_HasNoCredentialShapedProperty()
    {
        var credentialNames = new[] { "Password", "Secret", "Credential", "Token", "Key", "Hash", "Pin", "Passphrase" };
        var props = typeof(ApplicationComponentsViewModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var name in credentialNames)
        {
            Assert.DoesNotContain(props, p => p.Contains(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── Test 17: No scan execution ───────────────────────────────────────────────────

    [Fact]
    public void Components_AreBuiltFromExistingPipelineData_NoScanExecutorReferenced()
    {
        // ApplicationComponentsViewModel has no constructor that takes IGuiScanExecutor.
        // Verified structurally: if this test compiles and runs, no scan executor was involved.
        var vm = new ApplicationComponentsViewModel(
            [MakeDll("dll:verify", "Verify.dll")],
            []);

        Assert.Single(vm.DllBinaries);
        Assert.False(vm.HasRuntimes); // confirms only what was passed in is present
    }

    // ── Test 18: No mutation (lists are read-only after construction) ─────────────────

    [Fact]
    public void Components_Lists_AreReadOnly_NotDirectlyMutable()
    {
        var vm = new ApplicationComponentsViewModel(
            [MakeDll("dll:a", "A.dll"), MakeService("svc:a", "SvcA")],
            []);

        // IReadOnlyList<T> — the property type itself enforces read-only access.
        // Cannot call Add/Remove without a cast, and the underlying List<T> is private.
        Assert.IsAssignableFrom<IReadOnlyList<Dll>>(vm.DllBinaries);
        Assert.IsAssignableFrom<IReadOnlyList<Service>>(vm.Services);

        // Counts are stable — multiple reads return the same value
        var countBefore = vm.DllBinaries.Count;
        _ = vm.DllBinaries.Count; // second read
        Assert.Equal(countBefore, vm.DllBinaries.Count);
    }

    // ── Test 19: Deterministic ordering ──────────────────────────────────────────────

    [Fact]
    public void Components_OrderedDeterministically_NameCaseInsensitiveOrdinalThenId()
    {
        var dlls = new[]
        {
            MakeDll("dll:zzz", "Zebra.dll"),
            MakeDll("dll:aaa", "Apple.dll"),
            MakeDll("dll:mmm", "mango.dll"), // lowercase — should sort between Apple and Zebra
        };
        var boundary = MakeBoundary("boundary:app", "dll:zzz", "dll:aaa", "dll:mmm");
        var pipeline = BuildPipeline(dlls, [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm1 = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);
        var vm2 = ApplicationRowViewModel.ResolveComponents("boundary:app", boundaryIndex, entityIndex);

        // Both runs produce identical order
        Assert.Equal(vm1.DllBinaries.Select(d => d.Id), vm2.DllBinaries.Select(d => d.Id));
        // Name-ordinal order: Apple < mango < Zebra (case-insensitive)
        Assert.Equal("Apple.dll", vm1.DllBinaries[0].Name);
        Assert.Equal("mango.dll", vm1.DllBinaries[1].Name);
        Assert.Equal("Zebra.dll", vm1.DllBinaries[2].Name);
    }

    // ── Test 20: Real report fixture — Default Web Site/TEST DLL relationship ────────

    [Fact]
    public void RealReportFixture_DefaultWebSiteTest_HasExpectedDlls_WhenInBoundaryMembership()
    {
        // This fixture mirrors the entity IDs that appear in the real report.json for the
        // "Default Web Site/TEST" boundary (boundary:iis-application:Default Web Site:/TEST).
        // The DLLs are in C:\QINV\QINV_WEB_NOURM\Bin\ — the same IDs that appear in
        // risk finding AffectedEntityIds and in the real boundary's MemberEntityIds.
        const string boundaryId = "boundary:iis-application:Default Web Site:/TEST";
        const string dapperId = "dll:C:\\QINV\\QINV_WEB_NOURM\\Bin\\Dapper.dll";
        const string epplusId = "dll:C:\\QINV\\QINV_WEB_NOURM\\Bin\\EPPlus.dll";
        const string genQuesId = "dll:C:\\QINV\\QINV_WEB_NOURM\\Bin\\GenQues.dll";
        const string bcohId = "dll:C:\\QINV\\QINV_WEB_NOURM\\Bin\\BCOH.Business.dll";

        var dapper = new Dll { Id = dapperId, Name = "Dapper.dll", Type = "Dll", Source = "FileSystem",
                               Path = @"C:\QINV\QINV_WEB_NOURM\Bin\Dapper.dll", Version = "2.0.123" };
        var epplus = new Dll { Id = epplusId, Name = "EPPlus.dll", Type = "Dll", Source = "FileSystem",
                               Path = @"C:\QINV\QINV_WEB_NOURM\Bin\EPPlus.dll" };
        var genQues = new Dll { Id = genQuesId, Name = "GenQues.dll", Type = "Dll", Source = "FileSystem",
                                Path = @"C:\QINV\QINV_WEB_NOURM\Bin\GenQues.dll" };
        var bcoh = new Dll { Id = bcohId, Name = "BCOH.Business.dll", Type = "Dll", Source = "FileSystem",
                             Path = @"C:\QINV\QINV_WEB_NOURM\Bin\BCOH.Business.dll" };

        var boundary = new ApplicationBoundary
        {
            Id = boundaryId,
            Name = "Default Web Site/TEST",
            MemberEntityIds = [dapperId, epplusId, genQuesId, bcohId],
            Confidence = Confidence.VeryHigh(),
            Reason = "IIS Application PhysicalPath root"
        };

        var pipeline = BuildPipeline([dapper, epplus, genQues, bcoh], [boundary]);

        var entityIndex = ApplicationRowViewModel.BuildEntityIndex(pipeline);
        var boundaryIndex = pipeline.Boundaries.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var vm = ApplicationRowViewModel.ResolveComponents(boundaryId, boundaryIndex, entityIndex);

        // All four DLLs must appear as Application Components — not only as Risk Findings
        Assert.Equal(4, vm.DllBinaryCount);
        Assert.True(vm.HasDllBinaries);
        Assert.True(vm.HasAnyComponents);

        var names = vm.DllBinaries.Select(d => d.Name).ToList();
        Assert.Contains("Dapper.dll", names);
        Assert.Contains("EPPlus.dll", names);
        Assert.Contains("GenQues.dll", names);
        Assert.Contains("BCOH.Business.dll", names);

        // Migration prep count matches DLL list count
        Assert.Equal(vm.DllBinaries.Count, vm.DllBinaryCount);

        // No risk-only fabrication — the DLLs have their real entity data intact
        Assert.Equal(@"C:\QINV\QINV_WEB_NOURM\Bin\Dapper.dll",
            vm.DllBinaries.First(d => d.Name == "Dapper.dll").Path);
        Assert.Equal("2.0.123",
            vm.DllBinaries.First(d => d.Name == "Dapper.dll").Version);
    }
}
