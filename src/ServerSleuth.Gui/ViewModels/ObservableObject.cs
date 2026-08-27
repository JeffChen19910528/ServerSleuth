using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>
/// A minimal <see cref="INotifyPropertyChanged"/> base — GUI-1 deliberately does not add a
/// third-party MVVM toolkit package (e.g. CommunityToolkit.Mvvm) for the handful of properties
/// this phase's placeholder ViewModels need; the manual `SetProperty` pattern below is the
/// entire requirement. A future phase may reconsider this once real ViewModels grow enough
/// properties/commands to justify the dependency (skill.md GUI-1 §3: "do not add third-party
/// NuGet packages unless genuinely required").
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
