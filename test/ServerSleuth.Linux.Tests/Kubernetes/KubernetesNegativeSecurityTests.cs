using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Kubernetes;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Kubernetes;

/// <summary>
/// Explicitly verifies the Kubernetes provider/scanner never invokes a mutating/execution
/// kubectl subcommand or a shell — skill.md (Phase 6D) §25 — and that Secret values never reach
/// any entity/metadata/evidence. Every actual call the fake `IProcessRunner` received is
/// inspected after a full discovery run.
/// </summary>
public class KubernetesNegativeSecurityTests
{
    private static readonly string[] ForbiddenSubcommands =
        ["exec", "cp", "apply", "delete", "create", "patch", "edit", "rollout", "port-forward", "attach", "replace"];

    private static readonly string[] ForbiddenExecutables = ["sh", "bash", "/bin/sh", "/bin/bash"];

    private static FakeProcessRunner RunnerWithEverythingRegistered()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("kubectl", ["version", "-o", "json"], ProcessResult.Ok(0, """{"serverVersion":{"gitVersion":"v1.29.2"}}""", "", TimeSpan.Zero));
        runner.SetResult("kubectl", ["config", "current-context"], ProcessResult.Ok(0, "ctx\n", "", TimeSpan.Zero));

        foreach (var (resource, allNamespaces) in new (string, bool)[]
        {
            ("namespaces", false), ("nodes", false), ("pods", true), ("deployments", true), ("statefulsets", true),
            ("daemonsets", true), ("services", true), ("ingress", true), ("configmaps", true), ("secrets", true),
            ("pvc", true), ("pv", false)
        })
        {
            var args = allNamespaces
                ? new[] { "get", resource, "--all-namespaces", "-o", "json" }
                : new[] { "get", resource, "-o", "json" };
            runner.SetResult("kubectl", args, ProcessResult.Ok(0, """{"items":[{"metadata":{"name":"x","namespace":"ns","uid":"u"},"data":{"K":"Password=SECRETVALUE123"}}]}""", "", TimeSpan.Zero));
        }

        return runner;
    }

    [Fact]
    public async Task FullDiscoveryRun_NeverInvokesAnyForbiddenSubcommand()
    {
        var runner = RunnerWithEverythingRegistered();

        await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.NotEmpty(runner.Invocations);
        foreach (var invocation in runner.Invocations)
        {
            Assert.DoesNotContain(invocation.Executable.ToLowerInvariant(), ForbiddenExecutables);
            foreach (var argument in invocation.Arguments)
            {
                Assert.DoesNotContain(argument.ToLowerInvariant(), ForbiddenSubcommands);
            }
        }
    }

    [Fact]
    public async Task FullDiscoveryRun_NeverInvokesAShellExecutable()
    {
        var runner = RunnerWithEverythingRegistered();

        await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        Assert.All(runner.Invocations, i => Assert.Equal("kubectl", i.Executable));
    }

    [Fact]
    public async Task FullDiscoveryRun_UsesOnlyBulkQueries_NeverOnePerObject()
    {
        var runner = RunnerWithEverythingRegistered();

        await new KubectlKubernetesProvider(runner).GetSnapshotAsync(CancellationToken.None);

        // Exactly one invocation per resource kind (plus version + current-context) —
        // never a second "get pods <name>"-shaped call per discovered object.
        var podInvocations = runner.Invocations.Count(i => i.Arguments.Contains("pods"));
        Assert.Equal(1, podInvocations);
    }

    [Fact]
    public async Task SecretValues_NeverAppearInAnyEntityMetadataOrEvidence_AcrossTheWholeScan()
    {
        var runner = RunnerWithEverythingRegistered();
        var provider = new KubectlKubernetesProvider(runner);
        var scanner = new LinuxKubernetesScanner(provider, new SecretRedactor(), NullLogger<LinuxKubernetesScanner>.Instance);

        var result = await scanner.ScanAsync(
            new ServerSleuth.Core.Interfaces.DiscoveryContext { Profile = ServerSleuth.Core.Enums.ScanProfile.Migration, CancellationToken = CancellationToken.None },
            CancellationToken.None);

        foreach (var entity in result.Entities)
        {
            foreach (var (_, value) in entity.Metadata)
            {
                Assert.DoesNotContain("SECRETVALUE123", value);
            }

            foreach (var evidence in entity.Evidence)
            {
                Assert.DoesNotContain("SECRETVALUE123", evidence.Detail ?? string.Empty);
                Assert.DoesNotContain("SECRETVALUE123", evidence.Location);
            }
        }

        var secretEntity = Assert.Single(result.Entities, e => e.Type == "KubernetesSecret");
        Assert.Empty(secretEntity.Metadata); // Secret entity carries zero metadata — keys only, on the model itself
    }
}
