using ServerSleuth.Infrastructure.Process;
using ServerSleuth.Linux.Systemd;
using ServerSleuth.Linux.Tests.Fixtures;

namespace ServerSleuth.Linux.Tests.Systemd;

public class SystemctlProviderTests
{
    private static readonly string[] ListArgs = ["list-units", "--type=service", "--all", "--no-legend", "--no-pager", "--output=json"];

    private static string ShowArgsFor(string unit) =>
        $"show {unit} --no-pager --property=Description,LoadState,ActiveState,SubState,UnitFileState,ExecStart,User,WorkingDirectory,FragmentPath";

    [Fact]
    public void GetSnapshot_SystemctlNotInstalled_ReturnsNotInstalled()
    {
        var runner = new FakeProcessRunner(); // systemctl not registered -> StartFailedResult

        var probe = new SystemctlProvider(runner).GetSnapshot();

        Assert.Equal(SystemdAvailability.NotInstalled, probe.Status);
    }

    [Fact]
    public void GetSnapshot_OneServiceListed_MergesShowPropertiesIntoOneRow()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("systemctl", ListArgs,
            ProcessResult.Ok(0, """[{"unit":"nginx.service","load":"loaded","active":"active","sub":"running","description":"Nginx"}]""", "", TimeSpan.Zero));
        runner.SetResult("systemctl", ["show", "nginx.service", "--no-pager", "--property=Description,LoadState,ActiveState,SubState,UnitFileState,ExecStart,User,WorkingDirectory,FragmentPath"],
            ProcessResult.Ok(0, "Description=Nginx\nLoadState=loaded\nActiveState=active\nSubState=running\nUnitFileState=enabled\nExecStart={ path=/usr/sbin/nginx ; }\nUser=www-data\nWorkingDirectory=/var/www\nFragmentPath=/lib/systemd/system/nginx.service\n", "", TimeSpan.Zero));

        var probe = new SystemctlProvider(runner).GetSnapshot();

        Assert.Equal(SystemdAvailability.Available, probe.Status);
        var unit = Assert.Single(probe.Units);
        Assert.Equal("nginx.service", unit.UnitName);
        Assert.Equal("enabled", unit.UnitFileState);
        Assert.Equal("www-data", unit.User);
        Assert.Equal("/lib/systemd/system/nginx.service", unit.FragmentPath);
    }

    [Fact]
    public void GetSnapshot_ShowFailsForOneUnit_RecordsPartialFailure_StillIncludesUnit()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("systemctl", ListArgs,
            ProcessResult.Ok(0, """[{"unit":"restricted.service","load":"loaded","active":"active","sub":"running","description":"Restricted"}]""", "", TimeSpan.Zero));
        // "show" intentionally not registered -> StartFailedResult, simulating a failure

        var probe = new SystemctlProvider(runner).GetSnapshot();

        Assert.Equal(SystemdAvailability.Available, probe.Status);
        var unit = Assert.Single(probe.Units);
        Assert.True(unit.DetailUnavailable);
        Assert.Single(probe.PartialFailures);
    }

    [Fact]
    public void GetSnapshot_EmptyUnitList_ReturnsAvailableWithNoUnits()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("systemctl", ListArgs, ProcessResult.Ok(0, "[]", "", TimeSpan.Zero));

        var probe = new SystemctlProvider(runner).GetSnapshot();

        Assert.Equal(SystemdAvailability.Available, probe.Status);
        Assert.Empty(probe.Units);
    }

    [Fact]
    public void GetSnapshot_MalformedJson_ReturnsFailed_NeverThrows()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult("systemctl", ListArgs, ProcessResult.Ok(0, "{not valid json", "", TimeSpan.Zero));

        var probe = new SystemctlProvider(runner).GetSnapshot();

        Assert.Equal(SystemdAvailability.Failed, probe.Status);
    }
}
