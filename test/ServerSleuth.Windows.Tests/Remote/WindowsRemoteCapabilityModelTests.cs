using Microsoft.Win32;
using ServerSleuth.Core.Targets;
using ServerSleuth.Infrastructure.Common;
using ServerSleuth.Windows.Certificates;
using ServerSleuth.Windows.Registry;
using ServerSleuth.Windows.Remote;
using ServerSleuth.Windows.Tests.Fakes;
using ServerSleuth.Windows.Wmi;

namespace ServerSleuth.Windows.Tests.Remote;

/// <summary>
/// Phase 10D-3A §19: the security/behavioral test suite for the Windows remote capability
/// model. Every test runs entirely against <see cref="FakeWindowsRemoteCapabilities"/> — no
/// live Windows remote host, no WinRM connection, no real registry/WMI/IIS/Task Scheduler/
/// certificate-store access (skill.md §18 — model-level tests never require a live server, the
/// same guarantee Phase 10D-2's SSH suite already established for Linux).
/// </summary>
public class WindowsRemoteCapabilityModelTests
{
    private static readonly ScanTarget RemoteWindowsTarget = ScanTarget.Remote("winhost.example.internal", TargetPlatform.Windows);

    // 1. RegistryQuery is read-only.
    [Fact]
    public void WindowsRegistryQuery_HasNoWriteDeleteRenameOrAclMember()
    {
        var forbidden = new[] { "write", "set", "delete", "remove", "rename", "acl", "import", "export", "create" };
        var memberNames = typeof(WindowsRegistryQuery).GetProperties().Select(p => p.Name.ToLowerInvariant());

        foreach (var name in memberNames)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void FakeCapabilities_RegistryQuery_RecordsExactlyTheStructuredRequest()
    {
        var fake = new FakeWindowsRemoteCapabilities(RemoteWindowsTarget);
        var query = WindowsRegistryQuery.ForAllValues(RegistryHive.LocalMachine, RegistryView.Registry64, @"SYSTEM\CurrentControlSet\Services\W3SVC");

        var result = fake.Registry.Query(query);

        Assert.True(result.Success);
        var recorded = Assert.Single(fake.RecordedRegistryQueries);
        Assert.Equal(RegistryHive.LocalMachine, recorded.Hive);
        Assert.Equal(RegistryView.Registry64, recorded.View);
        Assert.Equal(@"SYSTEM\CurrentControlSet\Services\W3SVC", recorded.KeyPath);
        Assert.True(recorded.IncludeValues);
        Assert.False(recorded.IncludeSubKeys);
    }

    // 2. WmiQuery is structured.
    [Fact]
    public void WindowsWmiQuery_HasNoRawWqlStringField()
    {
        var properties = typeof(WindowsWmiQuery).GetProperties();

        Assert.DoesNotContain(properties, p => p.PropertyType == typeof(string) &&
            (p.Name.Contains("wql", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("query", StringComparison.OrdinalIgnoreCase)));

        // The only string-typed members are Namespace/ClassName — identifiers, not statements.
        var stringProperties = properties.Where(p => p.PropertyType == typeof(string)).Select(p => p.Name);
        Assert.Equal(new[] { nameof(WindowsWmiQuery.Namespace), nameof(WindowsWmiQuery.ClassName) }, stringProperties);
    }

    [Fact]
    public void WmiFilterClause_UsesATypedComparisonOperator_NeverARawOperatorString()
    {
        var operatorProperty = typeof(WmiFilterClause).GetProperty(nameof(WmiFilterClause.Operator));

        Assert.NotNull(operatorProperty);
        Assert.True(operatorProperty!.PropertyType.IsEnum);
    }

    [Fact]
    public void FakeCapabilities_WmiQuery_RecordsExactlyTheStructuredRequest()
    {
        var fake = new FakeWindowsRemoteCapabilities(RemoteWindowsTarget);
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.StandardCimv2Namespace,
            ClassName = "MSFT_NetTCPConnection",
            Properties = ["LocalAddress", "LocalPort", "OwningProcess"],
            Filters = [new WmiFilterClause { PropertyName = "State", Operator = WmiComparisonOperator.Equals, Value = "2" }]
        };

        var result = fake.Wmi.Query(query);

        Assert.True(result.Success);
        var recorded = Assert.Single(fake.RecordedWmiQueries);
        Assert.Equal("MSFT_NetTCPConnection", recorded.ClassName);
        var filter = Assert.Single(recorded.Filters);
        Assert.Equal("State", filter.PropertyName);
        Assert.Equal(WmiComparisonOperator.Equals, filter.Operator);
    }

    // 3 & 4. No arbitrary PowerShell/shell operation exists anywhere in the model.
    [Theory]
    [InlineData(typeof(IWindowsRemoteCapabilities))]
    [InlineData(typeof(WindowsRemoteOperationKind))]
    [InlineData(typeof(WindowsRegistryQuery))]
    [InlineData(typeof(WindowsWmiQuery))]
    public void CapabilityModelType_HasNoExecuteRunScriptPowerShellOrShellMember(Type type)
    {
        var forbidden = new[] { "execute", "runscript", "powershell", "shell", "invoke", "runcommand" };

        var memberNames = type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
            .Where(m => m.MemberType is System.Reflection.MemberTypes.Method or System.Reflection.MemberTypes.Property)
            .Select(m => m.Name.ToLowerInvariant());

        foreach (var name in memberNames)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }

    // 5. No registry mutation capability exists.
    [Fact]
    public void IWindowsRemoteRegistryOperations_ExposesExactlyOneReadOnlyQueryMethod()
    {
        var methods = typeof(IWindowsRemoteRegistryOperations).GetMethods()
            .Where(m => !m.IsSpecialName)
            .ToList();

        var method = Assert.Single(methods);
        Assert.Equal(nameof(IWindowsRemoteRegistryOperations.Query), method.Name);
    }

    // 6. No WMI method execution capability exists.
    [Fact]
    public void WindowsWmiQuery_HasNoMethodNameOrMethodArgumentsField()
    {
        var names = typeof(WindowsWmiQuery).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain(names, n => n.Contains("method", StringComparison.Ordinal));
    }

    [Fact]
    public void IWindowsRemoteWmiOperations_ExposesExactlyOneQueryMethod_NoInvokeMethodMember()
    {
        var methods = typeof(IWindowsRemoteWmiOperations).GetMethods().Where(m => !m.IsSpecialName).ToList();
        var method = Assert.Single(methods);
        Assert.Equal(nameof(IWindowsRemoteWmiOperations.Query), method.Name);
    }

    // 7. No service mutation capability exists (Start/Stop/Restart/Pause/Continue/Delete/Create/ChangeConfiguration).
    [Fact]
    public void NoCapabilityModelInterface_ExposesAServiceMutationMethod()
    {
        var forbidden = new[] { "start", "stop", "restart", "pause", "continue", "delete", "create", "changeconfiguration", "enable", "disable" };
        var interfaces = new[]
        {
            typeof(IWindowsRemoteCapabilities),
            typeof(IWindowsRemoteRegistryOperations),
            typeof(IWindowsRemoteWmiOperations),
            typeof(ServerSleuth.Windows.IIS.IWindowsRemoteIisOperations),
            typeof(ServerSleuth.Windows.ScheduledTasks.IWindowsRemoteTaskSchedulerOperations),
            typeof(IWindowsRemoteCertificateOperations)
        };

        var offenders = new List<string>();
        foreach (var type in interfaces)
        {
            foreach (var method in type.GetMethods().Where(m => !m.IsSpecialName))
            {
                var lowerName = method.Name.ToLowerInvariant();
                if (forbidden.Any(f => lowerName.Contains(f, StringComparison.Ordinal)))
                {
                    offenders.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    // 8. No scheduled-task mutation capability exists.
    [Fact]
    public void IWindowsRemoteTaskSchedulerOperations_ExposesOnlyGetSnapshot()
    {
        var methods = typeof(ServerSleuth.Windows.ScheduledTasks.IWindowsRemoteTaskSchedulerOperations).GetMethods()
            .Where(m => !m.IsSpecialName)
            .ToList();

        var method = Assert.Single(methods);
        Assert.Equal("GetSnapshot", method.Name);
        Assert.Empty(method.GetParameters());
    }

    // 9. No IIS mutation capability exists.
    [Fact]
    public void IWindowsRemoteIisOperations_ExposesOnlyGetSnapshot()
    {
        var methods = typeof(ServerSleuth.Windows.IIS.IWindowsRemoteIisOperations).GetMethods()
            .Where(m => !m.IsSpecialName)
            .ToList();

        var method = Assert.Single(methods);
        Assert.Equal("GetSnapshot", method.Name);
        Assert.Empty(method.GetParameters());
    }

    // 10. No certificate private-key export capability exists.
    [Fact]
    public void IWindowsRemoteCertificateOperations_HasNoExportOrPrivateKeyByteMember()
    {
        var forbidden = new[] { "export", "privatekeybytes", "privatekey", "key" };
        var iface = typeof(IWindowsRemoteCertificateOperations);
        var certificateRow = typeof(CertificateRow);

        foreach (var member in iface.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            var name = member.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }

        // CertificateRow.HasPrivateKey is a boolean FLAG only — no byte[]/string key-material member anywhere.
        Assert.DoesNotContain(certificateRow.GetProperties(), p => p.Name.Contains("Bytes", StringComparison.Ordinal) || p.PropertyType == typeof(byte[]));
        var hasPrivateKey = certificateRow.GetProperty(nameof(CertificateRow.HasPrivateKey));
        Assert.NotNull(hasPrivateKey);
        Assert.Equal(typeof(bool), hasPrivateKey!.PropertyType);
    }

    // 11. No COM activation capability exists (COM is registry-only in this model).
    [Fact]
    public void CapabilityModel_HasNoComActivationOrDllLoadingMember()
    {
        var forbidden = new[] { "activate", "createinstance", "loadlibrary", "cocreate", "instantiate" };
        var assembly = typeof(IWindowsRemoteCapabilities).Assembly;

        var offenders = assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace is not null && t.Namespace.StartsWith("ServerSleuth.Windows", StringComparison.Ordinal))
            .SelectMany(t => t.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
            .Where(m => forbidden.Any(f => m.Name.Contains(f, StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(offenders);
    }

    [Fact]
    public void WindowsRemoteOperationKind_HasNoDedicatedComMember_ComIsRepresentedAsARegistryQuery()
    {
        // Documents the folding decision from skill.md §5/§12: COM registration reads entirely
        // through the registry today (ComClsidReader), so no separate ComRegistryQuery member exists.
        Assert.DoesNotContain(Enum.GetNames<WindowsRemoteOperationKind>(), n => n.Contains("Com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindowsRemoteOperationKind_HasNoDedicatedServiceMember_ServiceIsRepresentedAsAWmiQuery()
    {
        // Documents the folding decision from skill.md §5/§8: a future service query is
        // representable as a Win32_Service WmiQuery, so no separate ServiceQuery member exists.
        Assert.DoesNotContain(Enum.GetNames<WindowsRemoteOperationKind>(), n => n.Contains("Service", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindowsRemoteOperationKind_HasExactlyFiveMembers()
    {
        Assert.Equal(5, Enum.GetValues<WindowsRemoteOperationKind>().Length);
    }

    // 12. ScanTarget contains no credential property.
    [Fact]
    public void ScanTarget_HasNoCredentialProperty()
    {
        var forbidden = new[] { "password", "credential", "privatekey", "token", "secret", "passphrase" };
        var names = typeof(ScanTarget).GetProperties().Select(p => p.Name.ToLowerInvariant());

        foreach (var name in names)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }

    // 13. Remote operation results contain no credential property.
    [Fact]
    public void WindowsRemoteOperationResult_HasNoCredentialProperty()
    {
        var forbidden = new[] { "password", "credential", "privatekey", "token", "secret", "passphrase" };
        var names = typeof(WindowsRemoteOperationResult<object>).GetProperties().Select(p => p.Name.ToLowerInvariant());

        foreach (var name in names)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void QueryTypes_HaveNoCredentialProperty()
    {
        var forbidden = new[] { "password", "credential", "privatekey", "token", "secret", "passphrase" };
        var types = new[]
        {
            typeof(WindowsRegistryQuery), typeof(WindowsRegistryQueryResult), typeof(WindowsWmiQuery),
            typeof(WmiFilterClause), typeof(CertificateStoreSource)
        };

        foreach (var type in types)
        {
            var names = type.GetProperties().Select(p => p.Name.ToLowerInvariant());
            foreach (var name in names)
            {
                Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.Ordinal));
            }
        }
    }

    // 14. Windows remote capability cannot silently fall back to local execution.
    [Fact]
    public void WindowsRemoteCapabilityFactory_AlwaysThrows_NeverReturnsALocalImplementation()
    {
        var ex = Assert.Throws<NotSupportedException>(() => WindowsRemoteCapabilityFactory.Create(RemoteWindowsTarget));
        Assert.Contains("WinRM", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsRemoteCapabilityFactory_RejectsALocalTarget()
    {
        var localTarget = ScanTarget.Local(TargetPlatform.Windows);
        Assert.Throws<InvalidOperationException>(() => WindowsRemoteCapabilityFactory.Create(localTarget));
    }

    [Fact]
    public void WindowsRemoteCapabilityFactory_RejectsALinuxTarget()
    {
        var linuxTarget = ScanTarget.Remote("linux-host", TargetPlatform.Linux);
        Assert.Throws<NotSupportedException>(() => WindowsRemoteCapabilityFactory.Create(linuxTarget));
    }

    [Fact]
    public void ExactlyOneConcreteImplementationOfIWindowsRemoteCapabilities_ExistsInTheProductionAssembly_AndItIsWinRmBacked()
    {
        // Phase 10D-3B fills the seam Phase 10D-3A deliberately left empty — but there must
        // still be exactly ONE real implementation (WinRmWindowsRemoteCapabilities), never a
        // second "local" one that could silently satisfy a remote request by quietly running
        // against the local machine instead (skill.md §21/§25's "never falls back to local").
        var assembly = typeof(IWindowsRemoteCapabilities).Assembly;
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IWindowsRemoteCapabilities).IsAssignableFrom(t))
            .ToList();

        var implementation = Assert.Single(implementations);
        Assert.Equal(typeof(WinRmWindowsRemoteCapabilities), implementation);
        Assert.DoesNotContain("Local", implementation.Name, StringComparison.Ordinal);
    }

    // 15. No network calls occur during model construction.
    [Fact]
    public void ConstructingEveryQueryType_MakesNoNetworkCall_PureDataConstruction()
    {
        // If construction did anything beyond assigning fields, a remote host with no listener
        // at all would cause one of these to throw/hang. They must all complete instantly.
        _ = WindowsRegistryQuery.ForSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Classes\CLSID");
        _ = new WindowsWmiQuery { Namespace = "root\\cimv2", ClassName = "Win32_Process", Properties = ["ProcessId"] };
        _ = CertificateStoreSource.LocalMachineMy;
        _ = new FakeWindowsRemoteCapabilities();

        Assert.True(true); // reaching this line without throwing/hanging is the assertion.
    }

    // 16. Operation serialization contains no credential material.
    [Fact]
    public void SerializedWindowsRegistryQuery_NeverContainsCredentialLookingText()
    {
        var query = WindowsRegistryQuery.ForOneValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"SYSTEM\CurrentControlSet\Services\W3SVC", "ImagePath");
        var json = System.Text.Json.JsonSerializer.Serialize(query);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("privatekey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializedWindowsWmiQuery_NeverContainsCredentialLookingText()
    {
        var query = new WindowsWmiQuery
        {
            Namespace = WindowsWmiQuery.Cimv2Namespace,
            ClassName = "Win32_Process",
            Properties = ["ProcessId", "ExecutablePath"]
        };
        var json = System.Text.Json.JsonSerializer.Serialize(query);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationStatus_IsTheSharedStatusEnum_NoSecondWindowsOnlyStatusEnumWasIntroduced()
    {
        var statusProperty = typeof(WindowsRemoteOperationResult<object>).GetProperty(nameof(WindowsRemoteOperationResult<object>.Status));
        Assert.Equal(typeof(OperationStatus), statusProperty!.PropertyType);
    }
}
