using ServerSleuth.Gui.Navigation;

namespace ServerSleuth.Gui.Tests.Navigation;

/// <summary>GUI-1 §6, §9: navigation is explicit and deterministic.</summary>
public class NavigationServiceTests
{
    [Fact]
    public void DefaultPage_IsDashboard()
    {
        var service = new NavigationService();
        Assert.Equal(NavigationPage.Dashboard, service.CurrentPage);
    }

    [Theory]
    [InlineData(NavigationPage.Dashboard)]
    [InlineData(NavigationPage.Scan)]
    [InlineData(NavigationPage.Results)]
    [InlineData(NavigationPage.Migration)]
    [InlineData(NavigationPage.Reports)]
    [InlineData(NavigationPage.Settings)]
    public void NavigateTo_EachRegisteredPage_UpdatesCurrentPage(NavigationPage page)
    {
        var service = new NavigationService();
        service.NavigateTo(page);
        Assert.Equal(page, service.CurrentPage);
    }

    [Fact]
    public void NavigateTo_RaisesCurrentPageChanged_WithTheNewPage()
    {
        var service = new NavigationService();
        NavigationPage? raised = null;
        service.CurrentPageChanged += (_, page) => raised = page;

        service.NavigateTo(NavigationPage.Scan);

        Assert.Equal(NavigationPage.Scan, raised);
    }

    [Fact]
    public void NavigateTo_TheCurrentPage_IsANoOp_NeverRaisesCurrentPageChanged()
    {
        var service = new NavigationService();
        var raiseCount = 0;
        service.CurrentPageChanged += (_, _) => raiseCount++;

        service.NavigateTo(NavigationPage.Dashboard); // already the default/current page

        Assert.Equal(0, raiseCount);
        Assert.Equal(NavigationPage.Dashboard, service.CurrentPage);
    }

    [Fact]
    public void RepeatedIdenticalNavigation_IsDeterministic()
    {
        var serviceA = new NavigationService();
        var serviceB = new NavigationService();

        serviceA.NavigateTo(NavigationPage.Reports);
        serviceA.NavigateTo(NavigationPage.Settings);
        serviceB.NavigateTo(NavigationPage.Reports);
        serviceB.NavigateTo(NavigationPage.Settings);

        Assert.Equal(serviceA.CurrentPage, serviceB.CurrentPage);
    }
}
