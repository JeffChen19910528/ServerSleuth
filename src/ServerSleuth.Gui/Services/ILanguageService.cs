namespace ServerSleuth.Gui.Services;

/// <summary>Owns the GUI's current display language and lets any part of the presentation
/// layer look up a localized string for the current language. See <see cref="LanguageService"/>
/// for how a change here reaches XAML <c>{DynamicResource}</c> bindings.</summary>
public interface ILanguageService
{
    GuiLanguage CurrentLanguage { get; }

    /// <summary>Raised after <see cref="CurrentLanguage"/> has changed and (in the real WPF
    /// app) after <c>Application.Current.Resources</c> has already been updated — a ViewModel
    /// reacting to this can safely re-read any localized value immediately.</summary>
    event EventHandler? LanguageChanged;

    void SetLanguage(GuiLanguage language);

    /// <summary>Looks up <paramref name="key"/> for <see cref="CurrentLanguage"/>. Never
    /// throws — an unknown key returns the key itself.</summary>
    string T(string key);
}
