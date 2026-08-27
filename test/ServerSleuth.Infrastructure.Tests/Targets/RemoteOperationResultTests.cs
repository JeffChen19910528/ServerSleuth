using System.Reflection;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Tests.Targets;

/// <summary>Phase 10D-1 §5: the result model must distinguish Success/NotInstalled/AccessDenied/
/// NotFound/Timeout/Cancelled/TransportUnavailable/ProtocolError/Failed — reusing the shared
/// <see cref="OperationStatus"/> enum rather than a second, duplicate status enum.</summary>
public class RemoteOperationResultTests
{
    [Fact]
    public void Ok_IsSuccessful_AndCarriesExitCodeAndOutput()
    {
        var result = RemoteOperationResult.Ok("stdout text", "stderr text", 0, TimeSpan.FromMilliseconds(50));

        Assert.True(result.Success);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("stdout text", result.StandardOutput);
        Assert.Equal("stderr text", result.StandardError);
    }

    [Theory]
    [InlineData(OperationStatus.AccessDenied)]
    [InlineData(OperationStatus.NotFound)]
    [InlineData(OperationStatus.ExecutionFailed)]
    public void Failure_IsNeverSuccessful_ForAnyNonSuccessStatus(OperationStatus status)
    {
        var result = RemoteOperationResult.Failure(status, "boom", TimeSpan.FromMilliseconds(10));
        Assert.False(result.Success);
        Assert.Equal(status, result.Status);
    }

    [Fact]
    public void TransportUnavailableResult_UsesTheTransportUnavailableStatus()
    {
        var result = RemoteOperationResult.TransportUnavailableResult("connection refused", TimeSpan.FromSeconds(1));
        Assert.Equal(OperationStatus.TransportUnavailable, result.Status);
        Assert.False(result.Success);
    }

    [Fact]
    public void ProtocolErrorResult_UsesTheProtocolErrorStatus()
    {
        var result = RemoteOperationResult.ProtocolErrorResult("malformed response", TimeSpan.FromSeconds(1));
        Assert.Equal(OperationStatus.ProtocolError, result.Status);
    }

    [Fact]
    public void NotInstalledResult_UsesTheNotInstalledStatus()
    {
        var result = RemoteOperationResult.NotInstalledResult(TimeSpan.Zero);
        Assert.Equal(OperationStatus.NotInstalled, result.Status);
    }

    [Fact]
    public void TimedOutResult_UsesTheTimeoutStatus()
    {
        var result = RemoteOperationResult.TimedOutResult(TimeSpan.FromSeconds(30));
        Assert.Equal(OperationStatus.Timeout, result.Status);
    }

    [Fact]
    public void CancelledResult_UsesTheCancelledStatus()
    {
        var result = RemoteOperationResult.CancelledResult(TimeSpan.FromMilliseconds(5));
        Assert.Equal(OperationStatus.Cancelled, result.Status);
    }

    [Fact]
    public void EveryDistinctFailureFactory_ProducesADifferentStatus()
    {
        var statuses = new[]
        {
            RemoteOperationResult.TransportUnavailableResult("x", TimeSpan.Zero).Status,
            RemoteOperationResult.ProtocolErrorResult("x", TimeSpan.Zero).Status,
            RemoteOperationResult.NotInstalledResult(TimeSpan.Zero).Status,
            RemoteOperationResult.TimedOutResult(TimeSpan.Zero).Status,
            RemoteOperationResult.CancelledResult(TimeSpan.Zero).Status
        };

        Assert.Equal(statuses.Length, statuses.Distinct().Count());
    }

    /// <summary>Phase 10D-1 §7: no property may look like a credential of any kind.</summary>
    [Fact]
    public void PublicProperties_NeverLookLikeACredential()
    {
        var forbiddenSubstrings = new[] { "password", "secret", "token", "credential", "apikey", "api_key", "privatekey", "private_key", "sshkey", "ssh_key" };

        var properties = typeof(RemoteOperationResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            var name = property.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbiddenSubstrings, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }
}
