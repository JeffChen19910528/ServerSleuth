using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Services;

/// <summary>GUI-7: exercises <see cref="LanguageService"/> directly — no WPF
/// <c>Application</c> is running in this test host, so <c>Application.Current</c> is always
/// null here, which is exactly the "no-op on the XAML side, still correct on the C# side"
/// path <see cref="LanguageService"/>'s own doc comment describes.</summary>
public class LanguageServiceTests
{
    [Fact]
    public void DefaultLanguage_IsEnglish()
    {
        var service = new LanguageService();
        Assert.Equal(GuiLanguage.English, service.CurrentLanguage);
    }

    [Fact]
    public void SetLanguage_ChangesCurrentLanguage()
    {
        var service = new LanguageService();
        service.SetLanguage(GuiLanguage.TraditionalChinese);
        Assert.Equal(GuiLanguage.TraditionalChinese, service.CurrentLanguage);
    }

    [Fact]
    public void T_ResolvesDifferentText_ForEachLanguage_ForTheSameKey()
    {
        var service = new LanguageService();

        var english = service.T("ScanConfig.Title");
        service.SetLanguage(GuiLanguage.TraditionalChinese);
        var chinese = service.T("ScanConfig.Title");
        service.SetLanguage(GuiLanguage.English);
        var englishAgain = service.T("ScanConfig.Title");

        Assert.NotEqual(english, chinese);
        Assert.Equal(english, englishAgain);
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChanged()
    {
        var service = new LanguageService();
        var raised = 0;
        service.LanguageChanged += (_, _) => raised++;

        service.SetLanguage(GuiLanguage.TraditionalChinese);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void T_OnUnknownKey_ReturnsTheKeyItself()
    {
        var service = new LanguageService();
        Assert.Equal("Not.A.Real.Key", service.T("Not.A.Real.Key"));
    }
}
