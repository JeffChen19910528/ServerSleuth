using System.Reflection;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Core.Targets;

namespace ServerSleuth.Core.Tests.Targets;

/// <summary>Phase 10C §2, §15-17: target identity/equality/determinism, and the credential
/// boundary — <see cref="ScanTarget"/> must never carry a password/token/key-shaped field.</summary>
public class ScanTargetTests
{
    [Fact]
    public void Local_HasTheFixedDeterministicId()
    {
        var target = ScanTarget.Local(TargetPlatform.Windows);
        Assert.Equal("local", target.Id);
        Assert.Equal(TargetKind.Local, target.Kind);
    }

    [Fact]
    public void Local_TwoInstancesWithTheSameArguments_AreEqual()
    {
        var a = ScanTarget.Local(TargetPlatform.Windows, "Dev Box");
        var b = ScanTarget.Local(TargetPlatform.Windows, "Dev Box");

        Assert.Equal(a, b);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Local_NeverUsesARandomIdentifier_AcrossManyConstructions()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => ScanTarget.Local().Id).Distinct().ToList();
        Assert.Single(ids);
        Assert.Equal("local", ids[0]);
    }

    [Fact]
    public void Local_DefaultPlatform_IsUnknown_NeverGuessed()
    {
        var target = ScanTarget.Local();
        Assert.Equal(TargetPlatform.Unknown, target.Platform);
    }

    [Fact]
    public void Remote_ProducesDeterministicId_FromNormalizedHostName()
    {
        var a = ScanTarget.Remote("  Server1.Example.Com  ");
        var b = ScanTarget.Remote("server1.example.com");

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a, b);
        Assert.Equal(TargetKind.Remote, a.Kind);
        Assert.StartsWith("remote:", a.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Remote_DifferentHosts_ProduceDifferentIds()
    {
        var a = ScanTarget.Remote("server1");
        var b = ScanTarget.Remote("server2");
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void LocalAndRemote_AreNeverEqual_EvenWithSimilarData()
    {
        var local = ScanTarget.Local();
        var remote = ScanTarget.Remote("local");
        Assert.NotEqual(local, remote);
    }

    /// <summary>Phase 10D-1 §6: a remote target must validate a non-empty host — data-shape
    /// validation only, never a DNS lookup/connection attempt.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Remote_RejectsAnEmptyOrWhitespaceHostName(string? hostName)
    {
        Assert.Throws<ArgumentException>(() => ScanTarget.Remote(hostName!));
    }

    [Fact]
    public void Remote_AcceptsAnExplicitPlatform_ButNeverProbesToDiscoverOne()
    {
        var target = ScanTarget.Remote("server1", TargetPlatform.Linux);
        Assert.Equal(TargetPlatform.Linux, target.Platform);
    }

    [Fact]
    public void Remote_DefaultPlatform_IsUnknown_SinceNothingConnectsToDiscoverIt()
    {
        var target = ScanTarget.Remote("server1");
        Assert.Equal(TargetPlatform.Unknown, target.Platform);
    }

    /// <summary>Phase 10C §2, §17: mechanically proves the credential boundary rather than
    /// merely documenting it — no public property on <see cref="ScanTarget"/> may look like a
    /// password/token/key/secret/credential of any kind.</summary>
    [Fact]
    public void PublicProperties_NeverLookLikeACredential()
    {
        var forbiddenSubstrings = new[] { "password", "secret", "token", "credential", "apikey", "api_key", "privatekey", "private_key", "sshkey", "ssh_key" };

        var properties = typeof(ScanTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var name = property.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbiddenSubstrings, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void DiscoveryContext_DefaultTarget_IsLocalWithUnknownPlatform()
    {
        var context = new DiscoveryContext { Profile = ScanProfile.Quick, CancellationToken = CancellationToken.None };
        Assert.Equal(ScanTarget.Local(), context.Target);
    }
}
