using System.Net;
using System.Text;

namespace CookieMunch.Tests;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that never touches the network. It records the
/// last request (method, URI, headers, and body text) and returns a canned response
/// produced by the supplied factory.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string LastBody { get; private set; } = "";

    public MockHttpMessageHandler(Func<HttpRequestMessage, string, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>Convenience: always reply with the given status + JSON body.</summary>
    public static MockHttpMessageHandler Json(HttpStatusCode status, string json)
        => new((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        LastRequest = request;
        return _responder(request, LastBody);
    }
}
