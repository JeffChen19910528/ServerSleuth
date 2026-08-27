using ServerSleuth.Analysis.Correlation;
using ServerSleuth.Analysis.Correlation.Rules;
using ServerSleuth.Analysis.Tests.Fixtures;
using ServerSleuth.Core.Enums;

namespace ServerSleuth.Analysis.Tests.Correlation.Rules;

public class BinaryImportsBinaryRuleTests
{
    [Fact]
    public void Evaluate_ImportResolvesToBinaryInSameDirectory_ProducesImportsCandidate()
    {
        var importer = EntityFactory.Dll(@"D:\ERP\ERP.Web.dll", importsCsv: "VendorNative.dll");
        var imported = EntityFactory.Dll(@"D:\ERP\VendorNative.dll");
        var context = new CorrelationContext([importer, imported]);

        var candidates = new BinaryImportsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(importer.Id, candidate.SourceEntityId);
        Assert.Equal(imported.Id, candidate.TargetEntityId);
        Assert.Equal(DependencyEdgeType.Imports, candidate.Type);
    }

    [Fact]
    public void Evaluate_SameFilenameDifferentPath_NeverCrossLinksToUnrelatedApp()
    {
        // Negative fixture (skill.md §23): two DLLs share a filename in different apps.
        // AppA's binary imports "Vendor.dll" — it must resolve to AppA's own copy only.
        var importerA = EntityFactory.Dll(@"C:\AppA\Importer.dll", importsCsv: "Vendor.dll");
        var vendorInA = EntityFactory.Dll(@"C:\AppA\Vendor.dll");
        var vendorInB = EntityFactory.Dll(@"C:\AppB\Vendor.dll");
        var context = new CorrelationContext([importerA, vendorInA, vendorInB]);

        var candidates = new BinaryImportsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Equal(vendorInA.Id, candidate.TargetEntityId);
        Assert.NotEqual(vendorInB.Id, candidate.TargetEntityId);
    }

    [Fact]
    public void Evaluate_UnresolvedImport_PreservesImportNameAsUnresolvedEvidence_NeverInventsTarget()
    {
        var importer = EntityFactory.Dll(@"D:\ERP\ERP.Web.dll", importsCsv: "KERNEL32.dll");
        var context = new CorrelationContext([importer]);

        var candidates = new BinaryImportsBinaryRule().Evaluate(context);

        var candidate = Assert.Single(candidates);
        Assert.Null(candidate.TargetEntityId);
        Assert.Contains("KERNEL32.dll", candidate.UnresolvedReason);
    }

    [Fact]
    public void Evaluate_NoImportsMetadata_ProducesNoCandidate()
    {
        var dll = EntityFactory.Dll(@"D:\ERP\ERP.Web.dll");
        var context = new CorrelationContext([dll]);

        var candidates = new BinaryImportsBinaryRule().Evaluate(context);

        Assert.Empty(candidates);
    }
}
