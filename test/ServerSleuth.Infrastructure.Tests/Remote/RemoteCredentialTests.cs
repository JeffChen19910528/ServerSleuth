using System.Reflection;
using ServerSleuth.Infrastructure.Remote;

namespace ServerSleuth.Infrastructure.Tests.Remote;

/// <summary>Phase 10D-2 §4, §26: no credential logging — a compiler-generated record
/// <c>ToString()</c> would otherwise print every field's raw value.</summary>
public class RemoteCredentialTests
{
    [Fact]
    public void ToString_NeverPrintsThePassword()
    {
        var credential = RemoteCredential.ForPassword("alice", "super-secret-password");
        Assert.DoesNotContain("super-secret-password", credential.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NeverPrintsThePrivateKeyBytesOrPassphrase()
    {
        var credential = RemoteCredential.ForPrivateKey("alice", "PRIVATE-KEY-MATERIAL"u8.ToArray(), "key-passphrase");
        var description = credential.ToString();

        Assert.DoesNotContain("PRIVATE-KEY-MATERIAL", description, StringComparison.Ordinal);
        Assert.DoesNotContain("key-passphrase", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_StillIdentifiesTheUsername_ForNonSecretDiagnostics()
    {
        var credential = RemoteCredential.ForPassword("alice", "secret");
        Assert.Contains("alice", credential.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ForPrivateKey_NeverSetsPassword()
    {
        var credential = RemoteCredential.ForPrivateKey("alice", [1, 2, 3]);
        Assert.Null(credential.Password);
    }

    [Fact]
    public void ForPassword_NeverSetsPrivateKeyFields()
    {
        var credential = RemoteCredential.ForPassword("alice", "secret");
        Assert.Null(credential.PrivateKeyBytes);
        Assert.Null(credential.PrivateKeyPassphrase);
    }
}
