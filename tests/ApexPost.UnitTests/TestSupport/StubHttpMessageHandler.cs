using System.Net;
using System.Text;

namespace ApexPost.UnitTests.TestSupport;

/// <summary>
/// Fake <see cref="HttpMessageHandler"/>: answers with a canned response (or throws) and records every request.
/// No socket is ever opened, so tests using it cannot reach the network.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = [];

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public static StubHttpMessageHandler Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, mediaType)
        }));

    public static StubHttpMessageHandler Throwing(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri, body));
        return await _responder(request, cancellationToken);
    }
}
