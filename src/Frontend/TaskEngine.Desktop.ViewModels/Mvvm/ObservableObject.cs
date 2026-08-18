using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskEngine.Desktop.Mvvm;

/// <summary>
/// Minimal hand-written MVVM base class (no <c>CommunityToolkit.Mvvm</c> — see
/// <c>CLAUDE.md</c>/persona do agente <c>maui-frontend</c>). View models derive from this to get
/// <see cref="INotifyPropertyChanged"/> plumbing without repeating the boilerplate per property.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets <paramref name="field"/> to <paramref name="value"/> and raises
    /// <see cref="PropertyChanged"/> only when the value actually changed.
    /// </summary>
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

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
