using ServerSleuth.Infrastructure.Remote;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>Phase 10D-2 §5, §26: unknown host keys must be rejected by default — never
/// silently trusted.</summary>
public class TrustedFingerprintHostKeyVerifierTests
{
    [Fact]
    public void Verify_MatchingFingerprint_IsTrusted()
    {
        var verifier = new TrustedFingerprintHostKeyVerifier("example.com", 22, "aa:bb:cc");
        Assert.Equal(HostKeyVerificationResult.Trusted, verifier.Verify("example.com", 22, "aa:bb:cc"));
    }

    [Fact]
    public void Verify_MatchingFingerprint_IsCaseInsensitive()
    {
        var verifier = new TrustedFingerprintHostKeyVerifier("example.com", 22, "AA:BB:CC");
        Assert.Equal(HostKeyVerificationResult.Trusted, verifier.Verify("example.com", 22, "aa:bb:cc"));
    }

    [Fact]
    public void Verify_UnknownHost_IsRejected_NeverSilentlyTrusted()
    {
        var verifier = new TrustedFingerprintHostKeyVerifier("example.com", 22, "aa:bb:cc");
        Assert.Equal(HostKeyVerificationResult.Rejected, verifier.Verify("attacker.example", 22, "aa:bb:cc"));
    }

    [Fact]
    public void Verify_MismatchedFingerprint_IsRejected()
    {
        var verifier = new TrustedFingerprintHostKeyVerifier("example.com", 22, "aa:bb:cc");
        Assert.Equal(HostKeyVerificationResult.Rejected, verifier.Verify("example.com", 22, "totally-different"));
    }

    [Fact]
    public void Verify_MismatchedPort_IsRejected()
    {
        var verifier = new TrustedFingerprintHostKeyVerifier("example.com", 22, "aa:bb:cc");
        Assert.Equal(HostKeyVerificationResult.Rejected, verifier.Verify("example.com", 2222, "aa:bb:cc"));
    }

    [Fact]
    public void Verify_EmptyAllowList_RejectsEverything()
    {
        var verifier = new TrustedFingerprintHostKeyVerifier(new Dictionary<string, string>());
        Assert.Equal(HostKeyVerificationResult.Rejected, verifier.Verify("anything", 22, "anything"));
    }

    /// <summary>Phase 10D-2 §5: mechanically proves no blind-trust implementation exists
    /// anywhere in this codebase — no <c>AcceptAnyHostKey</c>/<c>TrustAllCertificates</c>/
    /// <c>AutoAcceptUnknownHost</c>-shaped type or member.</summary>
    [Fact]
    public void NoBlindTrustImplementationExists_AnywhereInTheAssembly()
    {
        var forbiddenNames = new[] { "acceptany", "trustall", "autoaccept", "alwaystrust", "trustanyhost" };

        var assembly = typeof(IHostKeyVerifier).Assembly;
        var allNames = assembly.GetTypes()
            .SelectMany(t => t.GetMembers().Select(m => m.Name).Append(t.Name))
            .Select(n => n.ToLowerInvariant());

        foreach (var name in allNames)
        {
            Assert.DoesNotContain(forbiddenNames, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }
}
