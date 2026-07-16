using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Koras.AI.Providers;

namespace Koras.AI.Gemini;

/// <summary>
/// <see cref="IChatClient"/> over the Google Gemini <c>generateContent</c> REST API,
/// including SSE streaming, tool calling, and schema-constrained output via
/// <c>responseSchema</c>. Thread-safe; intended for singleton use via <c>AddKorasAI</c>.
/// </summary>
public sealed class GeminiChatClient : ProviderChatClient, IProviderHealthProbe
{
    private readonly GeminiOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public GeminiChatClient(HttpClient httpClient, GeminiOptions options)
        : base(httpClient, providerName: "gemini")
    {
        _options = Guard.NotNull(options);
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateHttpRequest(request, stream: false);
        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        return GeminiWire.ParseGenerateContentResponse(document.RootElement, ProviderName);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateHttpRequest(request, stream: true);
        using HttpResponseMessage response = await SendForStreamAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await foreach (SseEvent sseEvent in SseReader.ReadEventsAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                using JsonDocument chunk = ParseChunk(sseEvent.Data);
                foreach (ChatStreamUpdate update in GeminiWire.ParseStreamChunk(chunk.RootElement))
                {
                    yield return update;
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildUri("models?pageSize=1"));
        ApplyAuthentication(httpRequest);
        using JsonDocument _ = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateHttpRequest(ChatRequest request, bool stream)
    {
        string model = request.Model
            ?? _options.DefaultModel
            ?? throw new AiException(
                $"No model specified: set {nameof(GeminiOptions)}.{nameof(GeminiOptions.DefaultModel)} or pass ChatRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

        string operation = stream
            ? $"models/{Uri.EscapeDataString(model)}:streamGenerateContent?alt=sse"
            : $"models/{Uri.EscapeDataString(model)}:generateContent";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri(operation))
        {
            Content = new StringContent(
                GeminiWire.BuildGenerateContentBody(request).ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
        ApplyAuthentication(httpRequest);
        return httpRequest;
    }

    private void ApplyAuthentication(HttpRequestMessage httpRequest)
        => httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);

    private Uri BuildUri(string relativePath)
    {
        string endpoint = _options.Endpoint.AbsoluteUri;
        if (!endpoint.EndsWith('/'))
        {
            endpoint += "/";
        }

        return new Uri(new Uri(endpoint), relativePath);
    }

    private JsonDocument ParseChunk(string data)
    {
        try
        {
            return JsonDocument.Parse(data);
        }
        catch (JsonException ex)
        {
            throw ProviderErrors.InvalidResponse(ProviderName, data, ex);
        }
    }
}
