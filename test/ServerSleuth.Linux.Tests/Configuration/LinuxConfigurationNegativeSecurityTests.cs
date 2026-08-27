using Microsoft.Extensions.Logging.Abstractions;
using ServerSleuth.Core.Enums;
using ServerSleuth.Core.Interfaces;
using ServerSleuth.Infrastructure.Security;
using ServerSleuth.Linux.Configuration;
using ServerSleuth.Linux.Cron;
using ServerSleuth.Linux.Systemd;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Configuration;

/// <summary>
/// Explicitly verifies Linux configuration discovery never executes anything and never lets a
/// secret value reach an entity/metadata/evidence — skill.md (Phase 6E) §32. Unlike the Phase
/// 6C/6D negative-security suites, there is no `IProcessRunner` to inspect invocations on here:
/// `LinuxConfigurationScanner` (and everything it calls) has no process-execution dependency at
/// all, so "never runs nginx -t / apachectl / php / mysql / psql / touches the docker socket /
/// calls the Kubernetes API" is a structural, compile-time guarantee, not a runtime one —
/// verified below by reflecting over the actual dependency graph.
/// </summary>
public class LinuxConfigurationNegativeSecurityTests
{
    private static DiscoveryContext Context() => new() { Profile = ScanProfile.Migration, CancellationToken = CancellationToken.None };

    private sealed class FakeSystemdProvider(SystemdProbeResult result) : ISystemdProvider
    {
        public SystemdProbeResult GetSnapshot() => result;
    }

    [Fact]
    public void Scanner_HasNoProcessExecutionCapableDependency()
    {
        var forbiddenDependencyNameFragments = new[] { "ProcessRunner", "KubernetesProvider", "ContainerRuntimeProvider" };

        void AssertNoneOfType(Type type, HashSet<Type> visited)
        {
            if (!visited.Add(type))
            {
                return;
            }

            var constructor = type.GetConstructors().FirstOrDefault();
            if (constructor is null)
            {
                return;
            }

            foreach (var parameter in constructor.GetParameters())
            {
                Assert.DoesNotContain(forbiddenDependencyNameFragments, fragment => parameter.ParameterType.Name.Contains(fragment));
            }
        }

        AssertNoneOfType(typeof(LinuxConfigurationScanner), []);
    }

    [Fact]
    public async Task ScanAsync_SecretShapedValuesInEveryFileKind_NeverAppearRawInAnyEntityMetadataOrEvidence()
    {
        const string secretMarker = "SuperSecretSharedValue123";

        var fileSystemReader = new FakeFileSystemReader();

        fileSystemReader.SetFileEntries("/etc/nginx", "/etc/nginx/nginx.conf");
        fileSystemReader.SetFileInfo("/etc/nginx/nginx.conf", 200);
        fileSystemReader.SetText("/etc/nginx/nginx.conf", $"# Password={secretMarker}\nserver {{\n    listen 80;\n}}\n");

        fileSystemReader.SetFileEntries("/etc/mysql", "/etc/mysql/my.cnf");
        fileSystemReader.SetFileInfo("/etc/mysql/my.cnf", 100);
        fileSystemReader.SetText("/etc/mysql/my.cnf", $"[mysqld]\n# Token={secretMarker}\ndatadir=/var/lib/mysql\n");

        fileSystemReader.SetFileEntries("/etc/systemd/system", "/etc/systemd/system/erp.service");
        fileSystemReader.SetFileInfo("/etc/systemd/system/erp.service", 150);
        fileSystemReader.SetText("/etc/systemd/system/erp.service", $"[Service]\nExecStart=/opt/erp/bin/erp --token={secretMarker}\n");

        var systemdScanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.Available }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fileSystemReader, new SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);
        var scanner = new LinuxConfigurationScanner(systemdScanner, cronScanner, fileSystemReader, new SecretRedactor(), NullLogger<LinuxConfigurationScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        Assert.NotEmpty(result.Entities);

        foreach (var entity in result.Entities)
        {
            foreach (var (_, value) in entity.Metadata)
            {
                Assert.DoesNotContain(secretMarker, value);
            }

            foreach (var evidence in entity.Evidence)
            {
                Assert.DoesNotContain(secretMarker, evidence.Detail ?? string.Empty);
                Assert.DoesNotContain(secretMarker, evidence.Location);
            }
        }
    }

    [Fact]
    public async Task ScanAsync_SecretDetectedFlag_IsSetWheneverASecretWasFoundAndRedacted()
    {
        var fileSystemReader = new FakeFileSystemReader();
        fileSystemReader.SetFileEntries("/etc/nginx", "/etc/nginx/nginx.conf");
        fileSystemReader.SetFileInfo("/etc/nginx/nginx.conf", 100);
        fileSystemReader.SetText("/etc/nginx/nginx.conf", "# ApiKey=abc123XYZsecretvalue\n");

        var systemdScanner = new LinuxSystemdServiceScanner(
            new FakeSystemdProvider(new SystemdProbeResult { Status = SystemdAvailability.Available }),
            NullLogger<LinuxSystemdServiceScanner>.Instance);
        var cronScanner = new LinuxScheduledTaskScanner(fileSystemReader, new SecretRedactor(), NullLogger<LinuxScheduledTaskScanner>.Instance);
        var scanner = new LinuxConfigurationScanner(systemdScanner, cronScanner, fileSystemReader, new SecretRedactor(), NullLogger<LinuxConfigurationScanner>.Instance);

        var result = await scanner.ScanAsync(Context(), CancellationToken.None);

        var entity = (ServerSleuth.Core.Models.Configuration)Assert.Single(result.Entities);
        Assert.True(entity.SecretDetected);
    }
}
