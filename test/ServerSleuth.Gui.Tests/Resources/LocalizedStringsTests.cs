using ServerSleuth.Gui.Resources;
using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Tests.Resources;

/// <summary>GUI-7: <see cref="LocalizedStrings"/> is a single dictionary keyed once with both
/// languages as a tuple, so an English/Traditional-Chinese completeness gap is structurally
/// impossible for any key added going forward — these tests exist to make that guarantee
/// explicit and to catch an accidentally-empty translation (a key present but one side left as
/// an empty string) rather than just a missing key.</summary>
public class LocalizedStringsTests
{
    [Fact]
    public void EveryKey_HasANonEmptyEnglishValue()
    {
        foreach (var key in LocalizedStrings.Keys)
        {
            var value = LocalizedStrings.Get(key, GuiLanguage.English);
            Assert.False(string.IsNullOrWhiteSpace(value), $"Key '{key}' has no English value.");
        }
    }

    [Fact]
    public void EveryKey_HasANonEmptyTraditionalChineseValue()
    {
        foreach (var key in LocalizedStrings.Keys)
        {
            var value = LocalizedStrings.Get(key, GuiLanguage.TraditionalChinese);
            Assert.False(string.IsNullOrWhiteSpace(value), $"Key '{key}' has no Traditional Chinese value.");
        }
    }

    [Fact]
    public void AtLeastOneKeyExists()
    {
        // A guard against the table being accidentally emptied — every other test here would
        // otherwise pass vacuously on an empty dictionary.
        Assert.True(LocalizedStrings.Keys.Count > 50);
    }

    [Fact]
    public void UnknownKey_ReturnsTheKeyItself_NeverThrows()
    {
        const string unknownKey = "Some.Key.That.Does.Not.Exist";
        Assert.Equal(unknownKey, LocalizedStrings.Get(unknownKey, GuiLanguage.English));
        Assert.Equal(unknownKey, LocalizedStrings.Get(unknownKey, GuiLanguage.TraditionalChinese));
    }
}
