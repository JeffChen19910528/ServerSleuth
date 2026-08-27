using System.Reflection;
using ServerSleuth.Infrastructure.FileSystem;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Targets;

namespace ServerSleuth.Infrastructure.Tests.Targets;

/// <summary>
/// Phase 10C §6, §17: a future remote transport must never become an arbitrary shell interface.
/// Verified structurally, not just by convention — <see cref="IProcessRunner"/> (the operation
/// contract any transport, local or future-remote, must go through) exposes no method taking a
/// single raw command/shell string, and <see cref="ITargetTransport"/> itself defines no
/// <c>Execute</c>/<c>RunCommand</c>-shaped method of any kind — every operation it exposes is
/// already the existing, structured (Executable + Arguments, never a shell string)
/// <see cref="IProcessRunner"/>/<see cref="IFileSystemReader"/> contract.
/// </summary>
public class NoArbitraryShellExecutionTests
{
    [Fact]
    public void IProcessRunner_HasExactlyOneMethod_TakingAStructuredProcessRequest()
    {
        var methods = typeof(IProcessRunner).GetMethods();
        var method = Assert.Single(methods);

        Assert.Equal(nameof(IProcessRunner.RunAsync), method.Name);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(ProcessRequest), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public void ProcessRequest_SeparatesExecutableFromArguments_NeverACombinedCommandString()
    {
        var executableProperty = typeof(ProcessRequest).GetProperty(nameof(ProcessRequest.Executable));
        var argumentsProperty = typeof(ProcessRequest).GetProperty(nameof(ProcessRequest.Arguments));

        Assert.NotNull(executableProperty);
        Assert.Equal(typeof(string), executableProperty!.PropertyType);
        Assert.NotNull(argumentsProperty);
        Assert.Equal(typeof(IReadOnlyList<string>), argumentsProperty!.PropertyType);
    }

    [Fact]
    public void ITargetTransport_DefinesNoExecuteOrRunCommandMethod_OfAnyShape()
    {
        var forbiddenNames = new[] { "execute", "runcommand", "runshell", "shell", "invoke", "eval" };

        var members = typeof(ITargetTransport).GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.MemberType is MemberTypes.Method)
            .Select(m => m.Name.ToLowerInvariant());

        foreach (var name in members)
        {
            Assert.DoesNotContain(forbiddenNames, forbidden => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ITargetTransport_ExposesOnlyTheExistingStructuredContracts()
    {
        // ITargetTransport's entire operation surface is the property getters for
        // ScanTarget/IProcessRunner/IFileSystemReader — no method of its own that could take a
        // raw string and interpret it as a command.
        var declaredMethods = typeof(ITargetTransport).GetMethods()
            .Where(m => !m.IsSpecialName) // exclude property get_/set_ accessors
            .ToList();

        Assert.Empty(declaredMethods);
    }
}
