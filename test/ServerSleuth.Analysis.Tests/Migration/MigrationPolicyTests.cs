using ServerSleuth.Analysis.Migration.Assessment;
using ServerSleuth.Analysis.Migration.Models;
using ServerSleuth.Analysis.Risk.Models;
using ServerSleuth.Core.Evidence;

namespace ServerSleuth.Analysis.Tests.Migration;

/// <summary>
/// Direct unit tests of <see cref="MigrationPolicy.Classify"/> — see skill.md (Phase 8A) §1,
/// §5-6. Every RuleId/Severity combination here is chosen to match what the corresponding real
/// Risk rule can actually produce (traced from `Risk/Rules/*.cs`), never an invented pairing.
/// </summary>
public class MigrationPolicyTests
{
    private static readonly MigrationPolicy Policy = new();

    private static RiskFinding MakeFinding(string ruleId, RiskSeverity severity, RiskCategory category = RiskCategory.Configuration) => new()
    {
        Id = RiskFinding.ComputeId(ruleId, "entity:1"),
        RuleId = ruleId,
        Category = category,
        Severity = severity,
        Confidence = Confidence.High(),
        Title = "Test finding",
        Description = "Test description",
        SourceEntityId = "entity:1",
        Evidence = [new EvidenceRecord { Type = ServerSleuth.Core.Enums.EvidenceType.ConfigurationFile, Location = "entity:1" }],
        Recommendation = "Test recommendation"
    };

    [Fact]
    public void InfoSeverity_AnyRuleId_IsAlwaysInformational()
    {
        var decision = Policy.Classify(MakeFinding("RR6-ServiceDependency", RiskSeverity.Info));
        Assert.Equal(MigrationStatusImpact.Informational, decision.Impact);
    }

    [Fact]
    public void UnknownRuleId_IsUnclassified()
    {
        var decision = Policy.Classify(MakeFinding("RR99-DoesNotExist", RiskSeverity.High));
        Assert.Equal(MigrationStatusImpact.Unclassified, decision.Impact);
        Assert.Contains("RR99-DoesNotExist", decision.Reason);
    }

    [Fact]
    public void RR1_MissingDependency_High_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR1-MissingDependency", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR2_MissingBinary_Critical_IsBlocking()
    {
        Assert.Equal(MigrationStatusImpact.Blocking, Policy.Classify(MakeFinding("RR2-MissingBinary", RiskSeverity.Critical)).Impact);
    }

    [Fact]
    public void RR2_MissingBinary_High_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR2-MissingBinary", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR3_AccessDenied_Medium_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR3-AccessDenied", RiskSeverity.Medium)).Impact);
    }

    [Fact]
    public void RR4_MissingRuntime_High_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR4-MissingRuntime", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR5_CertificateExpiry_Critical_IsRemediationRequired_NeverBlocking()
    {
        // skill.md (Phase 8A) §6's own explicit "do not blindly classify every Critical finding
        // as Blocked" example — an expired certificate is remediable, not a structural blocker.
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR5-CertificateExpiry", RiskSeverity.Critical)).Impact);
    }

    [Fact]
    public void RR5_CertificateExpiry_High_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR5-CertificateExpiry", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR5_CertificateExpiry_Medium_IsConditional()
    {
        Assert.Equal(MigrationStatusImpact.Conditional, Policy.Classify(MakeFinding("RR5-CertificateExpiry", RiskSeverity.Medium)).Impact);
    }

    [Fact]
    public void RR6_ServiceDependency_Critical_IsBlocking()
    {
        Assert.Equal(MigrationStatusImpact.Blocking, Policy.Classify(MakeFinding("RR6-ServiceDependency", RiskSeverity.Critical)).Impact);
    }

    [Fact]
    public void RR7_ScheduledTaskDependency_High_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR7-ScheduledTaskDependency", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR8_ComDependency_High_IsRemediationRequired()
    {
        Assert.Equal(MigrationStatusImpact.RemediationRequired, Policy.Classify(MakeFinding("RR8-ComDependency", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR9_ExternalDependency_Medium_IsConditional()
    {
        Assert.Equal(MigrationStatusImpact.Conditional, Policy.Classify(MakeFinding("RR9-ExternalDependency", RiskSeverity.Medium)).Impact);
    }

    [Fact]
    public void RR10_SharedInfrastructure_Medium_IsConditional_NeverEscalatedBySharing()
    {
        Assert.Equal(MigrationStatusImpact.Conditional, Policy.Classify(MakeFinding("RR10-SharedInfrastructure", RiskSeverity.Medium)).Impact);
    }

    [Fact]
    public void RR11_ConfigurationRisk_High_IsConditional()
    {
        // FileShare/NetworkStorage references — an explicit migration-sensitive path
        // dependency, not merely informational (skill.md §22).
        Assert.Equal(MigrationStatusImpact.Conditional, Policy.Classify(MakeFinding("RR11-ConfigurationRisk", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void RR11_ConfigurationRisk_Low_IsInformational()
    {
        Assert.Equal(MigrationStatusImpact.Informational, Policy.Classify(MakeFinding("RR11-ConfigurationRisk", RiskSeverity.Low)).Impact);
    }

    [Fact]
    public void RR12_GraphIntegrity_High_IsBlocking()
    {
        // GraphIntegrityRule's fixed severity is High (never Critical), but its own semantics
        // (only ever fires for an Error-severity GraphValidator finding) always warrant
        // Blocking regardless of the RiskSeverity label — skill.md §19.
        Assert.Equal(MigrationStatusImpact.Blocking, Policy.Classify(MakeFinding("RR12-GraphIntegrity", RiskSeverity.High)).Impact);
    }

    [Fact]
    public void Decision_AlwaysCarriesNonEmptyReasonAndRequiredAction()
    {
        foreach (var ruleId in new[]
                 {
                     "RR1-MissingDependency", "RR2-MissingBinary", "RR3-AccessDenied", "RR4-MissingRuntime",
                     "RR5-CertificateExpiry", "RR6-ServiceDependency", "RR7-ScheduledTaskDependency",
                     "RR8-ComDependency", "RR9-ExternalDependency", "RR10-SharedInfrastructure",
                     "RR11-ConfigurationRisk", "RR12-GraphIntegrity", "RR99-Unknown"
                 })
        {
            var decision = Policy.Classify(MakeFinding(ruleId, RiskSeverity.High));
            Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
            Assert.False(string.IsNullOrWhiteSpace(decision.RequiredAction));
        }
    }
}
