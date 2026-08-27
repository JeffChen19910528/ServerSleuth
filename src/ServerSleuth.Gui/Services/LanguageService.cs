using System.Windows;
using ServerSleuth.Gui.Resources;

namespace ServerSleuth.Gui.Services;

/// <summary>
/// The only <see cref="ILanguageService"/> implementation. GUI-7: rather than merging a second
/// <c>ResourceDictionary</c> XAML file per language (two files to hand-keep in sync — a
/// completeness gap between them would only surface as a blank/missing label at runtime), this
/// writes every key from <see cref="LocalizedStrings"/> straight into
/// <c>Application.Current.Resources</c> for whichever language is now current. Every XAML
/// <c>{DynamicResource SomeKey}</c> binding picks up the new value the moment this runs — no
/// window/page reload needed.
///
/// This project also sets <c>&lt;InvariantGlobalization&gt;true&lt;/InvariantGlobalization&gt;</c>
/// (see ServerSleuth.Gui.csproj) — the standard .resx + satellite-assembly +
/// <c>CultureInfo.CurrentUICulture</c> localization pipeline is not safe to rely on under that
/// setting, which is the other reason this uses plain resource-dictionary values instead.
///
/// <see cref="Application.Current"/> is <c>null</c> in a plain xUnit test host (no WPF
/// <c>Application</c> is ever started there) — every access below is guarded so this class
/// behaves identically whether or not a real WPF application is running; <see cref="T"/> always
/// works from <see cref="LocalizedStrings"/> directly regardless.
/// </summary>
public sealed class LanguageService : ILanguageService
{
    public GuiLanguage CurrentLanguage { get; private set; } = GuiLanguage.English;

    public event EventHandler? LanguageChanged;

    public LanguageService()
    {
        ApplyToApplicationResources();
    }

    public void SetLanguage(GuiLanguage language)
    {
        CurrentLanguage = language;
        ApplyToApplicationResources();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string T(string key) => LocalizedStrings.Get(key, CurrentLanguage);

    private void ApplyToApplicationResources()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        foreach (var key in LocalizedStrings.Keys)
        {
            app.Resources[key] = LocalizedStrings.Get(key, CurrentLanguage);
        }
    }
}
