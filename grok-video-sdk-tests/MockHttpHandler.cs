using System.Net;

namespace GrokVideoSdk.Tests;

/// <summary>
/// A testable HttpMessageHandler that returns preconfigured responses
/// based on request URL patterns.
/// </summary>
public class MockHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly List<HttpRequestMessage> _requests = new();

    public IReadOnlyList<HttpRequestMessage> SentRequests => _requests;

    public void EnqueueResponse(HttpStatusCode status, string json)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public void EnqueueResponse(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);

        if (cancellationToken.IsCancellationRequested)
            throw new TaskCanceledException();

        if (_responses.Count == 0)
            throw new InvalidOperationException(
                $"No mock response queued for {request.Method} {request.RequestUri}");

        return Task.FromResult(_responses.Dequeue());
    }
}
