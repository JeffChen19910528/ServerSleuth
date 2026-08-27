using ServerSleuth.Windows.Software;

namespace ServerSleuth.Windows.Tests.Software;

public class SoftwareRegistryRowBuilderTests
{
    [Fact]
    public void TryBuild_ValidEntry_ReturnsTrueWithMappedFields()
    {
        var values = new Dictionary<string, object?>
        {
            ["DisplayName"] = "Contoso ERP Client",
            ["DisplayVersion"] = "19.3.0",
            ["Publisher"] = "Contoso",
            ["InstallLocation"] = @"C:\Program Files\Contoso ERP",
            ["InstallDate"] = "20240115",
            ["UninstallString"] = "msiexec /x {GUID}"
        };

        var built = SoftwareRegistryRowBuilder.TryBuild("{GUID}", values, out var row);

        Assert.True(built);
        Assert.Equal("Contoso ERP Client", row.DisplayName);
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), row.InstallDate);
    }

    [Fact]
    public void TryBuild_MissingDisplayName_ReturnsFalse()
    {
        var values = new Dictionary<string, object?> { ["DisplayVersion"] = "1.0" };

        var built = SoftwareRegistryRowBuilder.TryBuild("{GUID}", values, out _);

        Assert.False(built);
    }

    [Fact]
    public void TryBuild_SystemComponent_ReturnsFalse()
    {
        var values = new Dictionary<string, object?> { ["DisplayName"] = "Update KB123456", ["SystemComponent"] = 1 };

        var built = SoftwareRegistryRowBuilder.TryBuild("{GUID}", values, out _);

        Assert.False(built);
    }

    [Fact]
    public void TryBuild_UnparsableInstallDate_LeavesInstallDateNull()
    {
        var values = new Dictionary<string, object?> { ["DisplayName"] = "Some App", ["InstallDate"] = "not-a-date" };

        var built = SoftwareRegistryRowBuilder.TryBuild("{GUID}", values, out var row);

        Assert.True(built);
        Assert.Null(row.InstallDate);
    }

    [Fact]
    public void TryBuild_MissingOptionalFields_StillReturnsTrue()
    {
        var values = new Dictionary<string, object?> { ["DisplayName"] = "Minimal App" };

        var built = SoftwareRegistryRowBuilder.TryBuild("{GUID}", values, out var row);

        Assert.True(built);
        Assert.Null(row.DisplayVersion);
        Assert.Null(row.Publisher);
    }
}
