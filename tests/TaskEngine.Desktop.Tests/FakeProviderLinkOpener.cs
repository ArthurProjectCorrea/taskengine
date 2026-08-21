using TaskEngine.Desktop.ViewModels.Providers;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written fake for <see cref="IProviderLinkOpener"/>, no mocking library involved. Records the last opened URL instead of touching the OS shell.</summary>
public sealed class FakeProviderLinkOpener : IProviderLinkOpener
{
    public string? LastOpenedUrl { get; private set; }

    public void Open(string url) => LastOpenedUrl = url;
}
