using System.Diagnostics;
using TaskEngine.Desktop.ViewModels.Providers;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="IProviderLinkOpener"/> (RF-012/CA-012.1): hands the URL to
/// the shell via <see cref="Process.Start(ProcessStartInfo)"/> with <c>UseShellExecute = true</c>,
/// which resolves to the OS's configured default browser - the same technique
/// <c>GitHubOAuthAuthenticator</c> already uses to open the provider's login page for the OAuth
/// flow (<c>TaskEngine.Infrastructure.Providers.GitHub</c>), just implemented here instead since
/// this is a Desktop/presentation-only concern with no Application use case behind it.
/// </summary>
internal sealed class WindowsProviderLinkOpener : IProviderLinkOpener
{
    public void Open(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
