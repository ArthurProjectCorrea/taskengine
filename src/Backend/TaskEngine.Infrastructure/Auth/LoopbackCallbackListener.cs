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
    /// <summary>
    /// Static confirmation page shown by the browser right after the provider redirects back here.
    /// No JS, no error handling (a denied/error callback never reaches this response - see
    /// <c>GitHubOAuthAuthenticator.AuthenticateAsync</c>, which reads the query string itself before
    /// this page is even rendered and surfaces the error in the app's own UI instead). Inline CSS
    /// only, since this is a plain string constant with nowhere to serve an external stylesheet
    /// from - kept simple on purpose, it is only ever on screen for a second or two.
    /// </summary>
    private const string CallbackResponseHtml = """
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
        <meta charset="utf-8" />
        <title>TaskEngine</title>
        <style>
            :root { color-scheme: light dark; }
            body {
                margin: 0;
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                background: #f4f2fb;
                font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
                color: #201a2b;
            }
            .card {
                text-align: center;
                padding: 40px 48px;
                border-radius: 12px;
                background: #ffffff;
                box-shadow: 0 8px 24px rgba(81, 43, 212, 0.15);
                max-width: 360px;
            }
            .check {
                width: 56px;
                height: 56px;
                margin: 0 auto 20px;
                border-radius: 50%;
                background: #512BD4;
                color: #ffffff;
                font-size: 30px;
                line-height: 56px;
            }
            h1 {
                margin: 0 0 8px;
                font-size: 20px;
                color: #512BD4;
            }
            p {
                margin: 0;
                font-size: 14px;
                color: #4b4458;
            }
        </style>
        </head>
        <body>
            <div class="card">
                <div class="check">&#10003;</div>
                <h1>Conectado ao TaskEngine</h1>
                <p>Pode fechar esta aba e voltar ao aplicativo.</p>
            </div>
        </body>
        </html>
        """;

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
