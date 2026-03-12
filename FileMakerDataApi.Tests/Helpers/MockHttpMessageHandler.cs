using System.Net;
using System.Text;

namespace FileMakerDataApi.Tests.Helpers;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that returns pre-queued responses
/// and captures every request it receives.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _queue = new();

    /// <summary>All requests sent through this handler, in order.</summary>
    public List<CapturedRequest> Requests { get; } = new();

    /// <summary>Enqueue a response to be returned for the next request.</summary>
    public void Enqueue(HttpResponseMessage response) => _queue.Enqueue(response);

    /// <summary>Enqueue a 200 OK response with the given JSON body.</summary>
    public void EnqueueJson(string json) =>
        Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Read the body eagerly before the request object may be disposed.
        string? body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null;

        Requests.Add(new CapturedRequest(request, body));

        if (_queue.TryDequeue(out var response))
            return response;

        throw new InvalidOperationException(
            $"MockHttpMessageHandler: no response queued for {request.Method} {request.RequestUri}");
    }

    protected override void Dispose(bool disposing) { /* handler owns nothing disposable */ }
}

/// <summary>A captured HTTP request with its body pre-read.</summary>
public sealed record CapturedRequest(HttpRequestMessage Message, string? Body)
{
    public HttpMethod Method  => Message.Method;
    public Uri?       Uri     => Message.RequestUri;
    /// <summary>
    /// Uses AbsoluteUri (not ToString()) so percent-encoded characters like %20 are preserved.
    /// Uri.ToString() may decode %20 back to a space, breaking URL-encoding assertions.
    /// </summary>
    public string     UriStr  => Message.RequestUri?.AbsoluteUri ?? string.Empty;
}
