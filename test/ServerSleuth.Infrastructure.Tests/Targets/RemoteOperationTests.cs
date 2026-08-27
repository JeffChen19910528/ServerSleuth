using System.Reflection;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Tests.Targets;

/// <summary>
/// Phase 10D-1 §3-4, §8, §17-18: <see cref="RemoteOperation"/> is the structured stand-in for
/// what would otherwise be an <c>Execute(string command)</c>/<c>RunShell(string)</c> API — these
/// tests verify that shape mechanically, not just by convention, and cover the secret-safe
/// logging representation §8 requires.
/// </summary>
public class RemoteOperationTests
{
    private static readonly ScanTarget RemoteTarget = ScanTarget.Remote("app-server-1", TargetPlatform.Linux);

    [Fact]
    public void ForProcessQuery_PreservesExecutableIdentityAndStructuredArguments_SeparateFromEachOther()
    {
        var operation = RemoteOperation.ForProcessQuery(RemoteTarget, "systemctl", ["show", "nginx.service"]);

        Assert.Equal(RemoteOperationKind.ProcessQuery, operation.Kind);
        Assert.Equal("systemctl", operation.Executable);
        Assert.Equal(["show", "nginx.service"], operation.Arguments);
        Assert.Null(operation.Path);
    }

    [Fact]
    public void ForFileRead_CarriesAPathClassification_NotAProcessInvocation()
    {
        var operation = RemoteOperation.ForFileRead(RemoteTarget, "/etc/os-release");

        Assert.Equal(RemoteOperationKind.FileRead, operation.Kind);
        Assert.Equal("/etc/os-release", operation.Path);
        Assert.Null(operation.Executable);
        Assert.Empty(operation.Arguments);
    }

    [Fact]
    public void ForDirectoryQuery_IsClassifiedDistinctlyFromFileRead()
    {
        var operation = RemoteOperation.ForDirectoryQuery(RemoteTarget, "/etc/systemd/system");

        Assert.Equal(RemoteOperationKind.DirectoryQuery, operation.Kind);
        Assert.NotEqual(RemoteOperationKind.FileRead, operation.Kind);
    }

    [Fact]
    public void EveryOperation_CarriesItsTarget()
    {
        var operation = RemoteOperation.ForProcessQuery(RemoteTarget, "dpkg", ["-l"]);
        Assert.Equal(RemoteTarget, operation.Target);
    }

    [Fact]
    public void Timeout_DefaultsToAFiniteValue_AndIsCallerOverridable()
    {
        var defaultTimeout = RemoteOperation.ForProcessQuery(RemoteTarget, "dpkg", ["-l"]);
        Assert.True(defaultTimeout.Timeout > TimeSpan.Zero);
        Assert.True(defaultTimeout.Timeout < TimeSpan.FromMinutes(5));

        var explicitTimeout = RemoteOperation.ForFileRead(RemoteTarget, "/etc/hosts", TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(5), explicitTimeout.Timeout);
    }

    /// <summary>Cancellation is deliberately NOT a stored field (a <see cref="CancellationToken"/>
    /// cannot be meaningfully persisted in an immutable record) — it is supplied only at the
    /// point of execution, matching <see cref="Process.IProcessRunner.RunAsync"/>'s own
    /// signature. This test proves that shape rather than merely asserting it in prose.</summary>
    [Fact]
    public void RemoteOperation_HasNoCancellationTokenProperty_OnlyExecutionTimeAcceptsOne()
    {
        var properties = typeof(RemoteOperation).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(properties, p => p.PropertyType == typeof(CancellationToken));
    }

    [Fact]
    public void DescribeForLogging_RedactsASecretShapedArgument()
    {
        var operation = RemoteOperation.ForProcessQuery(
            RemoteTarget, "mysql", ["--host=db1", "Password=hunter2"]);

        var description = operation.DescribeForLogging(new SecretRedactor());

        Assert.DoesNotContain("hunter2", description, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", description, StringComparison.Ordinal);
        Assert.Contains("mysql", description, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeForLogging_RedactsASecretShapedPath()
    {
        var operation = RemoteOperation.ForFileRead(RemoteTarget, "token=abcdef123456");

        var description = operation.DescribeForLogging(new SecretRedactor());

        Assert.DoesNotContain("abcdef123456", description, StringComparison.Ordinal);
    }

    /// <summary>Phase 10D-1 §17-18: mechanically proves no member of this type accepts a single
    /// raw command/shell string — the same style of proof Phase 10C's own
    /// <see cref="NoArbitraryShellExecutionTests"/> already applies to <see cref="ITargetTransport"/>.</summary>
    [Fact]
    public void RemoteOperation_HasNoMethodOrFactoryTakingASingleRawCommandString()
    {
        var forbiddenNames = new[] { "execute", "runcommand", "runshell", "shell", "invoke", "eval" };

        var members = typeof(RemoteOperation).GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Select(m => m.Name.ToLowerInvariant());

        foreach (var name in members)
        {
            Assert.DoesNotContain(forbiddenNames, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    /// <summary>Phase 10D-1 §7: no property may look like a credential of any kind.</summary>
    [Fact]
    public void PublicProperties_NeverLookLikeACredential()
    {
        var forbiddenSubstrings = new[] { "password", "secret", "token", "credential", "apikey", "api_key", "privatekey", "private_key", "sshkey", "ssh_key" };

        var properties = typeof(RemoteOperation).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var name = property.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbiddenSubstrings, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }
}
