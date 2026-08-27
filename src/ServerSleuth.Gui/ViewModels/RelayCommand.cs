using System.Windows.Input;

namespace ServerSleuth.Gui.ViewModels;

/// <summary>A minimal <see cref="ICommand"/> — see <see cref="ObservableObject"/>'s doc comment
/// for why GUI-1 does not add a third-party MVVM toolkit package for this.</summary>
public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);
}
