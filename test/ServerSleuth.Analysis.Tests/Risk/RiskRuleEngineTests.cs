using ServerSleuth.Analysis.Risk;
using ServerSleuth.Analysis.Risk.Engine;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Analysis.Risk.Rules;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Risk;

public class RiskRuleEngineTests
{
    private sealed class FakeRule(string id, RiskCategory category = RiskCategory.Configuration, Func<RiskAnalysisContext, IReadOnlyList<RiskFinding>>? evaluate = null, Exception? throwOnEvaluate = null) : IRiskRule
    {
        public string Id => id;
        public RiskCategory Category => category;
        public RiskSeverity DefaultSeverity => RiskSeverity.Medium;

        public IReadOnlyList<RiskFinding> Evaluate(RiskAnalysisContext context) =>
            throwOnEvaluate is not null ? throw throwOnEvaluate : evaluate?.Invoke(context) ?? [];
    }

    private static RiskFinding MakeFinding(string ruleId, string sourceId, RiskSeverity severity = RiskSeverity.Medium, IReadOnlyList<EvidenceRecord>? evidence = null, Dictionary<string, string>? metadata = null) => new()
    {
        Id = RiskFinding.ComputeId(ruleId, sourceId),
        RuleId = ruleId,
        Category = RiskCategory.Configuration,
        Severity = severity,
        Confidence = Confidence.High(),
        Title = "Test finding",
        Description = "Test description",
        SourceEntityId = sourceId,
        Evidence = evidence ?? [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.ConfigurationFile, Location = sourceId }],
        Recommendation = "Test recommendation",
        Metadata = metadata ?? new Dictionary<string, string>()
    };

    private static RiskAnalysisContext EmptyContext() => RiskPipeline.Run([]).Context;

    [Fact]
    public void Analyze_RulesEvaluated_InDeterministicIdOrder_RegardlessOfRegistrationOrder()
    {
        var order = new List<string>();
        var ruleZ = new FakeRule("RRZ", evaluate: c => { order.Add("RRZ"); return []; });
        var ruleA = new FakeRule("RRA", evaluate: c => { order.Add("RRA"); return []; });
        var ruleM = new FakeRule("RRM", evaluate: c => { order.Add("RRM"); return []; });

        new RiskRuleEngine([ruleZ, ruleA, ruleM]).Analyze(EmptyContext());

        Assert.Equal(["RRA", "RRM", "RRZ"], order);
    }

    [Fact]
    public void Analyze_OneRuleThrows_OtherRulesStillEvaluated_NeverAbortsWholeRun()
    {
        var healthyRan = false;
        var throwing = new FakeRule("RR1", throwOnEvaluate: new InvalidOperationException("boom"));
        var healthy = new FakeRule("RR2", evaluate: c => { healthyRan = true; return []; });

        var result = new RiskRuleEngine([throwing, healthy]).Analyze(EmptyContext());

        Assert.True(healthyRan);
        Assert.Single(result.Diagnostics.RuleFailures);
        Assert.Equal("RR1", result.Diagnostics.RuleFailures[0].RuleId);
    }

    [Fact]
    public void Analyze_NonInfoFindingWithNoEvidence_IsDropped_RecordedAsInvariantViolation()
    {
        var rule = new FakeRule("RR1", evaluate: c => [MakeFinding("RR1", "entity:1", RiskSeverity.High, evidence: [])]);

        var result = new RiskRuleEngine([rule]).Analyze(EmptyContext());

        Assert.Empty(result.Findings);
        Assert.Single(result.Diagnostics.EvidenceInvariantViolations);
    }

    [Fact]
    public void Analyze_InfoFindingWithNoEvidence_IsAllowed()
    {
        var finding = MakeFinding("RR1", "entity:1", RiskSeverity.Info, evidence: []);
        var rule = new FakeRule("RR1", evaluate: c => [finding]);

        var result = new RiskRuleEngine([rule]).Analyze(EmptyContext());

        Assert.Single(result.Findings);
        Assert.Empty(result.Diagnostics.EvidenceInvariantViolations);
    }

    [Fact]
    public void Analyze_FindingsSortedBySeverityDescendingThenIdOrdinal()
    {
        var rule = new FakeRule("RR1", evaluate: c =>
        [
            MakeFinding("RR1", "b-entity", RiskSeverity.Medium),
            MakeFinding("RR1", "a-entity", RiskSeverity.Critical),
            MakeFinding("RR1", "c-entity", RiskSeverity.Critical)
        ]);

        var result = new RiskRuleEngine([rule]).Analyze(EmptyContext());

        Assert.Equal(RiskSeverity.Critical, result.Findings[0].Severity);
        Assert.Equal(RiskSeverity.Critical, result.Findings[1].Severity);
        Assert.Equal(RiskSeverity.Medium, result.Findings[2].Severity);
        // Two Criticals tie-broken by Id ordinal:
        Assert.True(string.CompareOrdinal(result.Findings[0].Id, result.Findings[1].Id) <= 0);
    }

    [Fact]
    public void Analyze_TwoFindingsShareMissingBinaryAnchor_AreMergedIntoOne_PreservingBothRuleIds()
    {
        var ruleA = new FakeRule("RRA", evaluate: c => [MakeFinding("RRA", "dll:missing", RiskSeverity.High, metadata: new() { ["MissingBinaryEntityId"] = "dll:missing" })]);
        var ruleB = new FakeRule("RRB", evaluate: c => [MakeFinding("RRB", "service:erp", RiskSeverity.Critical, metadata: new() { ["MissingBinaryEntityId"] = "dll:missing" })]);

        var result = new RiskRuleEngine([ruleA, ruleB]).Analyze(EmptyContext());

        var merged = Assert.Single(result.Findings);
        Assert.Equal(RiskSeverity.Critical, merged.Severity); // max of the two
        Assert.Equal(1, result.Diagnostics.FindingsDeduplicated);
        var contributingRules = merged.Metadata["ContributingRules"].Split(',');
        Assert.Contains("RRA", contributingRules);
        Assert.Contains("RRB", contributingRules);
    }

    [Fact]
    public void Analyze_FindingsWithoutSharedAnchor_AreNeverMerged()
    {
        var ruleA = new FakeRule("RRA", evaluate: c => [MakeFinding("RRA", "entity:1")]);
        var ruleB = new FakeRule("RRB", evaluate: c => [MakeFinding("RRB", "entity:2")]);

        var result = new RiskRuleEngine([ruleA, ruleB]).Analyze(EmptyContext());

        Assert.Equal(2, result.Findings.Count);
        Assert.Equal(0, result.Diagnostics.FindingsDeduplicated);
    }

    [Fact]
    public void Analyze_RepeatedRuns_ProduceIdenticalResults_Deterministic()
    {
        var rule = new FakeRule("RR1", evaluate: c => [MakeFinding("RR1", "entity:1"), MakeFinding("RR1", "entity:2", RiskSeverity.High)]);
        var engine = new RiskRuleEngine([rule]);
        var context = EmptyContext();

        var resultA = engine.Analyze(context);
        var resultB = engine.Analyze(context);

        Assert.Equal(resultA.Findings.Select(f => f.Id), resultB.Findings.Select(f => f.Id));
        Assert.Equal(resultA.Diagnostics.RulesEvaluated, resultB.Diagnostics.RulesEvaluated);
    }

    /// <summary>
    /// Phase 10A-J regression. Real-machine reproduction shape (see PROGRESS.md/CHANGELOG.md):
    /// a single rule (in production, <c>MissingDependencyRule</c> reading GraphValidator's
    /// per-unresolved-import "UnresolvedBinary" findings) emits MORE THAN ONE
    /// <see cref="RiskFinding"/> for the SAME <c>SourceEntityId</c> with no
    /// <c>MissingBinaryEntityId</c> merge-anchor metadata set — since <c>RiskFinding.Id</c> is
    /// computed purely from RuleId+SourceEntityId (never a per-emission discriminator like which
    /// import name was missing), every such emission collides on the exact same deterministic
    /// Id. Deduplicate's own grouping (<c>"solo:{f.Id}"</c> when no anchor is present) then hands
    /// Merge a &gt;1-member group with no member carrying the anchor key at all — before the
    /// Phase 10A-J fix, <c>Merge</c> unconditionally read
    /// <c>primary.Metadata["MissingBinaryEntityId"]</c> and threw <c>KeyNotFoundException</c>.
    /// This shape is unreachable in every prior phase's small/ERP-scale fixtures (no binary in
    /// those fixtures had more than one unresolved import), which is exactly why it was never
    /// caught before a dense, real 34,000+-entity machine scan reached it for the first time.
    /// </summary>
    [Fact]
    public void Analyze_SameRuleEmitsMultipleFindingsWithIdenticalId_NeverCrashes_MergesIntoOne()
    {
        var evidenceA = new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.BinaryImport, Location = "dll:vendor.dll", Detail = "imports missing-a.dll" };
        var evidenceB = new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.BinaryImport, Location = "dll:vendor.dll", Detail = "imports missing-b.dll" };
        var evidenceC = new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.BinaryImport, Location = "dll:vendor.dll", Detail = "imports missing-c.dll" };

        // Three DISTINCT RiskFinding instances, same RuleId+SourceEntityId (and therefore the
        // exact same computed Id), no MissingBinaryEntityId metadata anywhere — mirrors
        // MissingDependencyRule emitting once per unresolved import name on one densely-imported
        // real DLL, without any cross-rule merge opt-in.
        var rule = new FakeRule("RR1", evaluate: c =>
        [
            MakeFinding("RR1", "dll:vendor.dll", RiskSeverity.High, evidence: [evidenceA]),
            MakeFinding("RR1", "dll:vendor.dll", RiskSeverity.High, evidence: [evidenceB]),
            MakeFinding("RR1", "dll:vendor.dll", RiskSeverity.High, evidence: [evidenceC])
        ]);

        var result = new RiskRuleEngine([rule]).Analyze(EmptyContext());

        var merged = Assert.Single(result.Findings);
        Assert.Equal("dll:vendor.dll", merged.SourceEntityId);
        Assert.Equal(RiskSeverity.High, merged.Severity);
        Assert.Equal(3, merged.Evidence.Count); // all three distinct import-name facts preserved, none silently dropped
        Assert.Contains(merged.Evidence, e => e.Detail == "imports missing-a.dll");
        Assert.Contains(merged.Evidence, e => e.Detail == "imports missing-b.dll");
        Assert.Contains(merged.Evidence, e => e.Detail == "imports missing-c.dll");
        Assert.Equal(2, result.Diagnostics.FindingsDeduplicated); // 3 members merged into 1 => 2 "merged away"
    }

    /// <summary>Same-Id-collision merging must remain deterministic across repeated runs — same
    /// merged Id, same evidence set, same severity/confidence, never dependent on Dictionary/
    /// GroupBy enumeration order.</summary>
    [Fact]
    public void Analyze_SameRuleEmitsMultipleFindingsWithIdenticalId_RepeatedRuns_AreDeterministic()
    {
        var rule = new FakeRule("RR1", evaluate: c =>
        [
            MakeFinding("RR1", "dll:vendor.dll", RiskSeverity.High, evidence: [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.BinaryImport, Location = "dll:vendor.dll", Detail = "imports missing-a.dll" }]),
            MakeFinding("RR1", "dll:vendor.dll", RiskSeverity.Critical, evidence: [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.BinaryImport, Location = "dll:vendor.dll", Detail = "imports missing-b.dll" }])
        ]);
        var engine = new RiskRuleEngine([rule]);
        var context = EmptyContext();

        var resultA = engine.Analyze(context);
        var resultB = engine.Analyze(context);

        Assert.Single(resultA.Findings);
        Assert.Equal(resultA.Findings.Select(f => f.Id), resultB.Findings.Select(f => f.Id));
        Assert.Equal(resultA.Findings[0].Severity, resultB.Findings[0].Severity);
        Assert.Equal(
            resultA.Findings[0].Evidence.Select(e => e.Detail).OrderBy(d => d, StringComparer.Ordinal),
            resultB.Findings[0].Evidence.Select(e => e.Detail).OrderBy(d => d, StringComparer.Ordinal));
    }

    /// <summary>The Phase 10A-I anchor-based merge (MissingBinaryRule/ServiceDependencyRule
    /// sharing an explicit MissingBinaryEntityId) must remain byte-for-byte unaffected by the
    /// Phase 10A-J fallback — this is the exact pre-existing scenario
    /// <see cref="Analyze_TwoFindingsShareMissingBinaryAnchor_AreMergedIntoOne_PreservingBothRuleIds"/>
    /// already covers; this test adds the specific regression-guard that the merged finding's
    /// SourceEntityId is still the EXPLICIT anchor value, never silently overridden by either
    /// contributing finding's own SourceEntityId (which legitimately differ across rules).</summary>
    [Fact]
    public void Analyze_AnchorMerge_MergedFindingSourceEntityId_IsTheExplicitAnchor_NeverAFallback()
    {
        var ruleA = new FakeRule("RRA", evaluate: c => [MakeFinding("RRA", "dll:missing", RiskSeverity.High, metadata: new() { ["MissingBinaryEntityId"] = "dll:missing" })]);
        var ruleB = new FakeRule("RRB", evaluate: c => [MakeFinding("RRB", "service:erp", RiskSeverity.Critical, metadata: new() { ["MissingBinaryEntityId"] = "dll:missing" })]);

        var result = new RiskRuleEngine([ruleA, ruleB]).Analyze(EmptyContext());

        var merged = Assert.Single(result.Findings);
        Assert.Equal("dll:missing", merged.SourceEntityId);
        Assert.Contains("service:erp", merged.RelatedEntityIds);
    }
}
