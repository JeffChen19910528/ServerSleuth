using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Containers;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Containers;

/// <summary>
/// Explicitly verifies the container runtime providers never invoke a mutating/execution
/// subcommand or a shell — skill.md (Phase 6C) §24. Every actual call the fake `IProcessRunner`
/// received is inspected after a full discovery run, not just the ones the test happened to
/// stub a result for.
/// </summary>
public class ContainerRuntimeNegativeSecurityTests
{
    private static readonly string[] ForbiddenSubcommands =
        ["exec", "run", "start", "stop", "restart", "rm", "pull", "push", "build", "commit", "kill", "pause", "unpause"];

    private static readonly string[] ForbiddenExecutables = ["sh", "bash", "/bin/sh", "/bin/bash"];

    private static FakeProcessRunner RunnerWithEverythingRegistered(string executable)
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(executable, ["ps", "-aq", "--no-trunc"], ProcessResult.Ok(0, "c1", "", TimeSpan.Zero));
        runner.SetResult(executable, ["inspect", "c1"], ProcessResult.Ok(0, """[{"Id":"c1","Name":"/x","State":{"Status":"running"}}]""", "", TimeSpan.Zero));
        runner.SetResult(executable, ["images", "--no-trunc", "--format", "{{json .}}"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));
        runner.SetResult(executable, ["volume", "ls", "--format", "{{json .}}"], ProcessResult.Ok(0, "", "", TimeSpan.Zero));
        runner.SetResult(executable, ["network", "ls", "-q", "--no-trunc"], ProcessResult.Ok(0, "net1", "", TimeSpan.Zero));
        runner.SetResult(executable, ["network", "inspect", "net1"], ProcessResult.Ok(0, """[{"Id":"net1","Name":"n"}]""", "", TimeSpan.Zero));
        return runner;
    }

    [Fact]
    public async Task Docker_FullDiscoveryRun_NeverInvokesAnyForbiddenSubcommand()
    {
        var runner = RunnerWithEverythingRegistered("docker");

        await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        AssertNoForbiddenInvocation(runner);
    }

    [Fact]
    public async Task Podman_FullDiscoveryRun_NeverInvokesAnyForbiddenSubcommand()
    {
        var runner = RunnerWithEverythingRegistered("podman");

        await new PodmanContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        AssertNoForbiddenInvocation(runner);
    }

    [Fact]
    public async Task NeitherProvider_EverInvokesAShellExecutable()
    {
        var dockerRunner = RunnerWithEverythingRegistered("docker");
        var podmanRunner = RunnerWithEverythingRegistered("podman");

        await new DockerContainerRuntimeProvider(dockerRunner).GetSnapshotAsync(CancellationToken.None);
        await new PodmanContainerRuntimeProvider(podmanRunner).GetSnapshotAsync(CancellationToken.None);

        foreach (var invocation in dockerRunner.Invocations.Concat(podmanRunner.Invocations))
        {
            Assert.DoesNotContain(invocation.Executable.ToLowerInvariant(), ForbiddenExecutables);
        }
    }

    [Fact]
    public async Task AllInvocations_UseOnlyTheRuntimeExecutableItself_NeverAnythingElse()
    {
        var runner = RunnerWithEverythingRegistered("docker");

        await new DockerContainerRuntimeProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.All(runner.Invocations, i => Assert.Equal("docker", i.Executable));
    }

    private static void AssertNoForbiddenInvocation(FakeProcessRunner runner)
    {
        Assert.NotEmpty(runner.Invocations); // sanity: the test actually exercised something

        foreach (var invocation in runner.Invocations)
        {
            Assert.DoesNotContain(invocation.Executable.ToLowerInvariant(), ForbiddenExecutables);

            foreach (var argument in invocation.Arguments)
            {
                Assert.DoesNotContain(argument.ToLowerInvariant(), ForbiddenSubcommands);
            }
        }
    }
}
