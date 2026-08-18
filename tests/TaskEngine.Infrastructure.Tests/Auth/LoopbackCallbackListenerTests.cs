using System.Net;
using TaskEngine.Infrastructure.Auth;

namespace TaskEngine.Infrastructure.Tests.Auth;

public class LoopbackCallbackListenerTests
{
    [Fact]
    public async Task WaitForCallbackAsync_ExtractsQueryStringFromARealHttpRequest()
    {
        using var listener = new LoopbackCallbackListener();
        using var httpClient = new HttpClient();

        var callbackUri = new Uri(listener.RedirectUri, "?code=abc123&state=xyz");
        var clientRequestTask = httpClient.GetAsync(callbackUri);

        var query = await listener.WaitForCallbackAsync(CancellationToken.None);
        var response = await clientRequestTask;
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("abc123", query["code"]);
        Assert.Equal("xyz", query["state"]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body));
        Assert.Contains("html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedirectUri_IsLoopbackWithACallbackPath()
    {
        using var listener = new LoopbackCallbackListener();

        Assert.Equal("127.0.0.1", listener.RedirectUri.Host);
        Assert.EndsWith("/callback/", listener.RedirectUri.AbsolutePath);
    }

    [Fact]
    public async Task WaitForCallbackAsync_ThrowsWhenCancelledBeforeAnyRequestArrives()
    {
        using var listener = new LoopbackCallbackListener();
        using var cts = new CancellationTokenSource();

        var waitTask = listener.WaitForCallbackAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }
}
