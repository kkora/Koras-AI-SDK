using System.Net;
using System.Text;

namespace Koras.AI.UnitTests.TestInfrastructure;

/// <summary>An HttpMessageHandler driven by a delegate, capturing every request it sends.</summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> RequestBodies { get; } = [];

    public static FakeHttpMessageHandler RespondingWith(HttpStatusCode statusCode, string body, string mediaType = "application/json", Action<HttpResponseMessage>? customize = null)
        => new((_, _) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            };
            customize?.Invoke(response);
            return Task.FromResult(response);
        });

    public static FakeHttpMessageHandler Throwing(Exception exception)
        => new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    public HttpClient CreateClient() => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return await responder(request, cancellationToken);
    }
}
