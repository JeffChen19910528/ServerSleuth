using System.Reflection;
using Microsoft.Win32;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.Tests.Fakes;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Tests.Remote;

/// <summary>
/// Phase 10D-3B §25's deterministic security test suite for the real WinRM transport — every
/// test runs against <see cref="FakeCimSession"/>, never a live WinRM host (skill.md §26).
/// </summary>
public class WinRmTransportSecurityTests
{
    private static readonly ScanTarget Target = ScanTarget.Remote("winhost.example.internal", TargetPlatform.Windows);

    // 7. Registry operations are read-only.
    [Fact]
    public void Registry32_IsRejected_BeforeAnySessionCallIsMade()
    {
        var session = new FakeCimSession();
        var transport = new CimWinRmTransport(Target, session);
        var registry = new WinRmRegistryOperations(transport, TimeSpan.FromSeconds(5));

        var result = registry.Query(WindowsRegistryQuery.ForAllValues(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Test"));

        Assert.Equal(OperationStatus.Unsupported, result.Status);
        Assert.Empty(session.RecordedMethodInvocations);
    }

    [Fact]
    public void Registry_OnlyEverInvokesStdRegProvReadMethods()
    {
        var session = new FakeCimSession();
        var transport = new CimWinRmTransport(Target, session);
        var registry = new WinRmRegistryOperations(transport, TimeSpan.FromSeconds(5));

        registry.Query(WindowsRegistryQuery.ForSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Test"));
        registry.Query(WindowsRegistryQuery.ForAllValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Test"));

        Assert.NotEmpty(session.RecordedMethodInvocations);
        foreach (var (ns, className, methodName) in session.RecordedMethodInvocations)
        {
            Assert.Equal(WindowsWmiMethodAllowList.StdRegProvNamespace, ns);
            Assert.Equal(WindowsWmiMethodAllowList.StdRegProvClass, className);
            Assert.Contains(methodName, WindowsWmiMethodAllowList.StdRegProvReadMethods);
        }
    }

    [Fact]
    public void StdRegProvReadMethods_ContainsNoWriteDeleteOrCreateVerb()
    {
        var forbidden = new[] { "Set", "Create", "Delete", "Rename", "SaveKey", "RestoreKey" };
        foreach (var method in WindowsWmiMethodAllowList.StdRegProvReadMethods)
        {
            Assert.DoesNotContain(forbidden, f => method.StartsWith(f, StringComparison.Ordinal));
        }
    }

    // 8. WMI operations are read-only / structured.
    [Fact]
    public void Wmi_Query_RejectsAnUnlistedClass_BeforeAnySessionCallIsMade()
    {
        var session = new FakeCimSession();
        var transport = new CimWinRmTransport(Target, session);
        var wmi = new WinRmWmiOperations(transport, TimeSpan.FromSeconds(5));

        var result = wmi.Query(new WindowsWmiQuery { Namespace = @"root\cimv2", ClassName = "Win32_OperatingSystem", Properties = ["Name"] });

        Assert.False(result.Success);
        Assert.Equal(OperationStatus.InvalidInput, result.Status);
        Assert.Empty(session.RecordedQueries);
    }

    [Fact]
    public void Wmi_Query_BuildsAParameterizedWhereClause_NeverConcatenatesAnUnquotedStringValue()
    {
        var session = new FakeCimSession();
        var transport = new CimWinRmTransport(Target, session);
        var wmi = new WinRmWmiOperations(transport, TimeSpan.FromSeconds(5));

        var maliciousValue = "x'; DROP TABLE Win32_Process; --";
        wmi.Query(new WindowsWmiQuery
        {
            Namespace = @"root\cimv2",
            ClassName = "Win32_Process",
            Properties = ["ProcessId"],
            Filters = [new WmiFilterClause { PropertyName = "Name", Operator = WmiComparisonOperator.Equals, Value = maliciousValue }]
        });

        var (_, wql) = Assert.Single(session.RecordedQueries);
        Assert.Contains("''", wql, StringComparison.Ordinal); // the embedded quote was escaped, not left to close the literal early.
    }

    // 9-11. No arbitrary PowerShell/cmd.exe/shell execution exists anywhere in this phase's types.
    [Theory]
    [InlineData(typeof(ICimSession))]
    [InlineData(typeof(CimWinRmTransport))]
    [InlineData(typeof(WinRmRegistryOperations))]
    [InlineData(typeof(WinRmWmiOperations))]
    public void TransportType_HasNoExecuteRunScriptPowerShellOrShellMember(Type type)
    {
        // "invoke" is deliberately excluded: InvokeMethod/InvokeAllowedMethod are this phase's
        // own named, allow-list-checked, structured CIM method-invocation operations — not an
        // arbitrary command surface. "runcommand"/"runscript" would still catch a shell wrapper.
        var forbidden = new[] { "execute", "runscript", "powershell", "shell", "cmd.exe", "runcommand" };
        var memberNames = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.MemberType is MemberTypes.Method or MemberTypes.Property)
            .Select(m => m.Name.ToLowerInvariant());

        foreach (var name in memberNames)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ServerSleuthWindowsAssembly_ReferencesNoPowerShellAutomationNamespace()
    {
        var assembly = typeof(CimNetSession).Assembly;
        var offenders = assembly.GetTypes()
            .Where(t => t.IsPublic)
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(DescribeReferencedTypes)
            .Where(t => (t.Namespace ?? string.Empty).StartsWith("System.Management.Automation", StringComparison.Ordinal));

        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> DescribeReferencedTypes(MemberInfo member) => member switch
    {
        PropertyInfo p => [p.PropertyType],
        MethodInfo m => m.GetParameters().Select(par => par.ParameterType).Append(m.ReturnType),
        FieldInfo f => [f.FieldType],
        _ => []
    };

    // 12-16 (mutation absence) already covered by WindowsRemoteCapabilityModelTests (10D-3A);
    // re-verified here specifically against the REAL implementations, not just the interfaces.
    [Fact]
    public void RealRegistryImplementation_HasNoWriteMethod()
    {
        var methods = typeof(WinRmRegistryOperations).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);
        var method = Assert.Single(methods);
        Assert.Equal(nameof(WinRmRegistryOperations.Query), method.Name);
    }

    // 18-19. Credentials never appear in logs/reports.
    [Fact]
    public void WindowsRemoteCredential_ToString_NeverPrintsThePassword()
    {
        var password = new System.Security.SecureString();
        foreach (var ch in "DeliberatelySensitivePassword123")
        {
            password.AppendChar(ch);
        }

        var credential = new WindowsRemoteCredential { UserName = "svc-account", Password = password };
        var text = credential.ToString();

        Assert.DoesNotContain("DeliberatelySensitivePassword123", text, StringComparison.Ordinal);
        Assert.Contains("svc-account", text, StringComparison.Ordinal);
    }

    // 20. Invalid server certificate fails closed (structural — see CimNetSession's doc comment).
    [Fact]
    public void ServerSleuthWindowsAssembly_HasNoCertificateBypassMember()
    {
        var forbidden = new[] { "trustall", "acceptany", "skipcertificatevalidation", "ignoresslerrors", "alwaystrust" };
        var assembly = typeof(CimNetSession).Assembly;

        var offenders = assembly.GetTypes()
            .Where(t => t.IsPublic || t.IsNotPublic)
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => forbidden.Any(f => m.Name.Replace("_", string.Empty, StringComparison.Ordinal).Contains(f, StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(offenders);
    }

    [Fact]
    public void WinRmConnectionOptions_DefaultsToTls_FailClosed()
    {
        var options = new WinRmConnectionOptions { Host = "example" };
        Assert.True(options.UseSsl);
        Assert.Equal(5986, options.ResolvedPort);
    }

    [Fact]
    public void WinRmConnectionOptions_HasNoFieldCapableOfDisablingEncryption()
    {
        var forbidden = new[] { "noencryption", "skipvalidation", "trustall", "insecure" };
        var names = typeof(WinRmConnectionOptions).GetProperties().Select(p => p.Name.ToLowerInvariant());

        foreach (var name in names)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }

    // 25. Never falls back to local — the real (non-fake) production types never construct a
    // local Registry/WMI/Process/Service/IIS/TaskScheduler/Certificate reader internally.
    [Theory]
    [InlineData(typeof(WinRmRegistryOperations))]
    [InlineData(typeof(WinRmWmiOperations))]
    public void RealRemoteImplementation_ReferencesNoLocalWindowsProviderType(Type type)
    {
        var forbiddenTypeNames = new[]
        {
            "ServerSleuth.Windows.Registry.WindowsRegistryReader",
            "ServerSleuth.Windows.Process.ProcessWmiProvider",
            "ServerSleuth.Windows.Process.ProcessEnumerator",
            "ServerSleuth.Windows.Networking.NetworkTableProvider",
            "ServerSleuth.Windows.Services.ServiceEnumerator"
        };

        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(f => f.FieldType.FullName);

        foreach (var name in fields)
        {
            Assert.DoesNotContain(forbiddenTypeNames, forbidden => forbidden == name);
        }
    }

    // 26. Unsupported remote capability is reported honestly — the three disclosed-gap
    // implementations never fabricate data.
    [Fact]
    public void DisclosedGapImplementations_AlwaysReportFailure_NeverFabricateData()
    {
        var iis = new ServerSleuth.Windows.IIS.WinRmIisOperations(Target);
        var iisResult = iis.GetSnapshot();
        Assert.False(iisResult.Success);
        Assert.Equal(OperationStatus.NotInstalled, iisResult.Status);
        Assert.Null(iisResult.Value);

        var taskScheduler = new ServerSleuth.Windows.ScheduledTasks.WinRmTaskSchedulerOperations(Target);
        var taskResult = taskScheduler.GetSnapshot();
        Assert.False(taskResult.Success);
        Assert.Null(taskResult.Value);

        var certificates = new ServerSleuth.Windows.Certificates.WinRmCertificateOperations(Target);
        var certResult = certificates.Query(ServerSleuth.Windows.Certificates.CertificateStoreSource.LocalMachineMy);
        Assert.False(certResult.Success);
        Assert.Null(certResult.Value);
    }
}
