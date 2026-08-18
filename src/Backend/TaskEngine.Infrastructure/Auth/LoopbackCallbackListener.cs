using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace TaskEngine.Infrastructure.Auth;

/// <summary>
/// A local (127.0.0.1-only) HTTP listener used to receive an OAuth redirect callback for the
/// Authorization Code + PKCE flow. Generic - not specific to any single provider. Handles exactly
/// one GET request, then stops itself.
/// </summary>
public sealed class LoopbackCallbackListener : IDisposable
{
    private const string CallbackResponseHtml =
        "<html><body><p>Pode fechar esta aba e voltar ao TaskEngine.</p></body></html>";

    private readonly HttpListener _listener;
    private bool _disposed;

    public LoopbackCallbackListener()
    {
        var port = GetFreeLoopbackPort();
        RedirectUri = new Uri($"http://127.0.0.1:{port}/callback/");

        _listener = new HttpListener();
        _listener.Prefixes.Add(RedirectUri.ToString());
        _listener.Start();
    }

    public Uri RedirectUri { get; }

    /// <summary>
    /// Waits for a single GET request, extracts its query string, responds with a simple HTML
    /// page, then stops the listener. Respects <paramref name="cancellationToken"/> by stopping
    /// the underlying listener, which aborts the pending accept.
    /// </summary>
    public async Task<NameValueCollection> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed - nothing to stop.
            }
        });

        HttpListenerContext context;
        try
        {
            context = await _listener.GetContextAsync();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);

        var buffer = Encoding.UTF8.GetBytes(CallbackResponseHtml);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        context.Response.OutputStream.Close();
        context.Response.Close();

        StopListener();

        return query;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopListener();
        _listener.Close();
        _disposed = true;
    }

    private void StopListener()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
    }

    /// <summary>
    /// Opens a <see cref="TcpListener"/> on an OS-assigned free port, then immediately stops it
    /// and reuses the port number for the <see cref="HttpListener"/>. There is a small race window
    /// between releasing the port here and <see cref="HttpListener"/> binding to it, but this is
    /// the standard technique for this scenario.
    /// </summary>
    private static int GetFreeLoopbackPort()
    {
        var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }
}
