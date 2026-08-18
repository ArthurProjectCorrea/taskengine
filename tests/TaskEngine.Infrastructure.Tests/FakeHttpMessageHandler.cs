using System.Net;
using System.Text;

namespace TaskEngine.Infrastructure.Tests;

/// <summary>
/// Returns one canned JSON response per request, in order, and captures the request bodies for
/// assertions - no HTTP mocking library needed for this.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;

    public FakeHttpMessageHandler(params string[] jsonResponses)
    {
        _responses = new Queue<string>(jsonResponses);
    }

    public List<string> CapturedRequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        CapturedRequestBodies.Add(body);

        var json = _responses.Count > 0 ? _responses.Dequeue() : """{"data":null}""";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}
