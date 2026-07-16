using System.Text.Json;

namespace Koras.AI.Providers;

/// <summary>
/// Base class for HTTP-based chat providers: send helpers with normalized error handling,
/// timeout classification, and streaming setup. First-party providers and custom providers
/// use the same plumbing. Implementations must be thread-safe for singleton use.
/// </summary>
public abstract class ProviderChatClient : IChatClient
{
    /// <summary>Initializes the base client.</summary>
    /// <param name="httpClient">The HTTP client used for provider calls (typically from <c>IHttpClientFactory</c>).</param>
    /// <param name="providerName">The stable, lower-case provider identifier.</param>
    protected ProviderChatClient(HttpClient httpClient, string providerName)
    {
        HttpClient = Guard.NotNull(httpClient);
        ProviderName = Guard.NotNullOrWhiteSpace(providerName);
    }

    /// <inheritdoc />
    public string ProviderName { get; }

    /// <summary>The HTTP client used for provider calls.</summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc />
    public abstract Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request and parses the successful response as JSON. HTTP errors, network
    /// failures, timeouts, and unparseable payloads all surface as <see cref="AiException"/>.
    /// </summary>
    /// <param name="request">The provider HTTP request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    protected Task<JsonDocument> SendAndParseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => ProviderHttp.SendAndParseAsync(HttpClient, Guard.NotNull(request), ProviderName, ExtractErrorMessage, cancellationToken);

    /// <summary>
    /// Sends a streaming request and returns the response with headers read, ready for
    /// <see cref="SseReader"/> or <see cref="JsonLinesReader"/>. Callers must dispose the response.
    /// </summary>
    /// <param name="request">The provider HTTP request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    protected Task<HttpResponseMessage> SendForStreamAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => ProviderHttp.SendForStreamAsync(HttpClient, Guard.NotNull(request), ProviderName, ExtractErrorMessage, cancellationToken);

    /// <summary>
    /// Extracts a human-readable message from a provider error body, or returns
    /// <see langword="null"/> to use a generic message. The default handles the common
    /// <c>{"error": {"message": "..."}}</c> and <c>{"error": "..."}</c> shapes.
    /// </summary>
    /// <param name="body">The error response body.</param>
    protected virtual string? ExtractErrorMessage(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("error", out JsonElement error))
        {
            return null;
        }

        return error.ValueKind switch
        {
            JsonValueKind.String => error.GetString(),
            JsonValueKind.Object when error.TryGetProperty("message", out JsonElement message) => message.GetString(),
            _ => null,
        };
    }
}

/// <summary>
/// Base class for HTTP-based embedding providers; see <see cref="ProviderChatClient"/> for the
/// shared behavior.
/// </summary>
public abstract class ProviderEmbeddingClient : IEmbeddingClient
{
    /// <summary>Initializes the base client.</summary>
    /// <param name="httpClient">The HTTP client used for provider calls.</param>
    /// <param name="providerName">The stable, lower-case provider identifier.</param>
    protected ProviderEmbeddingClient(HttpClient httpClient, string providerName)
    {
        HttpClient = Guard.NotNull(httpClient);
        ProviderName = Guard.NotNullOrWhiteSpace(providerName);
    }

    /// <inheritdoc />
    public string ProviderName { get; }

    /// <summary>The HTTP client used for provider calls.</summary>
    protected HttpClient HttpClient { get; }

    /// <inheritdoc />
    public abstract Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends a request and parses the successful response as JSON with normalized error handling.</summary>
    /// <param name="request">The provider HTTP request.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    protected Task<JsonDocument> SendAndParseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => ProviderHttp.SendAndParseAsync(HttpClient, Guard.NotNull(request), ProviderName, ExtractErrorMessage, cancellationToken);

    /// <inheritdoc cref="ProviderChatClient.ExtractErrorMessage"/>
    protected virtual string? ExtractErrorMessage(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("error", out JsonElement error))
        {
            return null;
        }

        return error.ValueKind switch
        {
            JsonValueKind.String => error.GetString(),
            JsonValueKind.Object when error.TryGetProperty("message", out JsonElement message) => message.GetString(),
            _ => null,
        };
    }
}
