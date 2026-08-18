using System.Windows.Input;

namespace TaskEngine.Desktop.Mvvm;

/// <summary>
/// Minimal hand-written synchronous <see cref="ICommand"/> (no <c>CommunityToolkit.Mvvm</c>).
/// For asynchronous work (e.g. calling into <c>TaskEngine.Application</c> ports), use
/// <see cref="AsyncRelayCommand"/> instead — awaiting inside <see cref="Execute"/> here would
/// swallow exceptions and block the UI thread.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
