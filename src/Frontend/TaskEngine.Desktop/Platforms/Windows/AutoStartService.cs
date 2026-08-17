using Microsoft.Win32;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Registra o executável para iniciar junto com o Windows, gravando uma entrada em
/// <c>HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run</c> via
/// <see cref="Microsoft.Win32.Registry"/> (BCL nativa do .NET, sem pacote NuGet). Idempotente:
/// só grava quando o valor ainda não existe ou está desatualizado.
/// </summary>
internal static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TaskEngine";

    public static void EnsureRegistered()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        var expectedCommand = $"\"{exePath}\"";

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        var currentCommand = key.GetValue(ValueName) as string;

        if (!string.Equals(currentCommand, expectedCommand, StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue(ValueName, expectedCommand, RegistryValueKind.String);
        }
    }
}
