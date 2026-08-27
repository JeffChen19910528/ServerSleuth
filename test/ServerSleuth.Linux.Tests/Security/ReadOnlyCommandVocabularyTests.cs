namespace ServerSleuth.Linux.Tests.Security;

/// <summary>
/// Phase 10D-2 §12-17, §26 (items 8-11): static, source-level proof that every Linux
/// provider/scanner this phase makes remote-capable (by virtue of depending only on
/// <c>IProcessRunner</c>/<c>IFileSystemReader</c>) issues ONLY read-only commands. These
/// providers were not modified in this phase — this test locks in what was already true so a
/// future change cannot silently introduce a mutating command and have it "just work" over the
/// new SSH transport. Reads each provider's actual source file and asserts a mutating verb never
/// appears immediately after the relevant executable name.
/// </summary>
public class ReadOnlyCommandVocabularyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void SystemctlProvider_NeverIssuesAMutatingSystemctlVerb()
    {
        var source = ReadSource("Systemd/SystemctlProvider.cs");
        var forbidden = new[] { "\"start\"", "\"stop\"", "\"restart\"", "\"enable\"", "\"disable\"", "\"reload\"", "\"mask\"", "\"kill\"" };

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(verb, source, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Packages/DpkgPackageProvider.cs")]
    [InlineData("Packages/RpmPackageProvider.cs")]
    [InlineData("Packages/ApkPackageProvider.cs")]
    public void PackageProviders_NeverIssueAMutatingPackageManagerVerb(string relativePath)
    {
        var source = ReadSource(relativePath);
        var forbidden = new[] { "\"install\"", "\"remove\"", "\"upgrade\"", "\"add\"", "\"del\"", "\"purge\"", "\"erase\"" };

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(verb, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ContainerCliRuntimeProvider_NeverIssuesAMutatingDockerOrPodmanVerb()
    {
        var source = ReadSource("Containers/ContainerCliRuntimeProvider.cs");
        var forbidden = new[] { "\"exec\"", "\"run\"", "\"start\"", "\"stop\"", "\"rm\"", "\"pull\"", "\"push\"", "\"build\"", "\"kill\"", "\"restart\"" };

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(verb, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void KubectlKubernetesProvider_NeverIssuesAMutatingKubectlVerb()
    {
        var source = ReadSource("Kubernetes/KubectlKubernetesProvider.cs");
        var forbidden = new[] { "\"exec\"", "\"apply\"", "\"create\"", "\"delete\"", "\"patch\"", "\"edit\"", "\"rollout\"", "\"port-forward\"", "\"scale\"", "\"cordon\"" };

        foreach (var verb in forbidden)
        {
            Assert.DoesNotContain(verb, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LinuxScheduledTaskScanner_NeverExecutesADiscoveredCronCommand()
    {
        // Cron discovery must stay filesystem-read-only (skill.md §15) — this scanner has no
        // ProcessRequest/IProcessRunner dependency of any kind, only IFileSystemReader.
        var source = ReadSource("Cron/LinuxScheduledTaskScanner.cs");
        Assert.DoesNotContain("IProcessRunner", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LdconfigProvider_UsesOnlyTheApprovedReadOnlyQuery()
    {
        var source = ReadSource("Native/LdconfigProvider.cs");
        Assert.Contains("\"-p\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, "src", "ServerSleuth.Linux", relativePath));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ServerSleuth.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate the repository root (ServerSleuth.slnx) from the test output directory.");
    }
}
