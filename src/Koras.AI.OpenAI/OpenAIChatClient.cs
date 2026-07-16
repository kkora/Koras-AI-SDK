using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Koras.AI.Providers;

namespace Koras.AI.OpenAI;

/// <summary>
/// <see cref="IChatClient"/> over the OpenAI chat-completions REST API (and OpenAI-compatible
/// gateways). Thread-safe; intended for singleton use via <c>AddKorasAI</c>.
/// </summary>
public class OpenAIChatClient : ProviderChatClient, IProviderHealthProbe
{
    private readonly OpenAIOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public OpenAIChatClient(HttpClient httpClient, OpenAIOptions options)
        : this(httpClient, options, providerName: "openai")
    {
    }

    /// <summary>Initializes the client with a derived provider name (used by Azure OpenAI).</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    /// <param name="providerName">The provider identifier reported on responses and telemetry.</param>
    protected OpenAIChatClient(HttpClient httpClient, OpenAIOptions options, string providerName)
        : base(httpClient, providerName)
    {
        _options = Guard.NotNull(options);
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateChatHttpRequest(request, stream: false);

        try
        {
            using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return OpenAIWire.ParseChatResponse(document.RootElement, ProviderName);
        }
        catch (AiException ex)
        {
            throw Normalize(ex);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateChatHttpRequest(request, stream: true);

        HttpResponseMessage response;
        try
        {
            response = await SendForStreamAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (AiException ex)
        {
            throw Normalize(ex);
        }

        using (response)
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                await foreach (SseEvent sseEvent in SseReader.ReadEventsAsync(stream, cancellationToken).ConfigureAwait(false))
                {
                    if (sseEvent.Data is "[DONE]")
                    {
                        yield break;
                    }

                    using JsonDocument chunk = ParseChunk(sseEvent.Data);
                    foreach (ChatStreamUpdate update in OpenAIWire.ParseStreamChunk(chunk.RootElement))
                    {
                        yield return update;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildUri("models"));
        ApplyAuthentication(httpRequest);
        using JsonDocument _ = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the HTTP request for a chat completion. Overridden by Azure OpenAI for its URL and auth scheme.</summary>
    /// <param name="request">The provider-neutral request.</param>
    /// <param name="stream">Whether the request asks for a streamed response.</param>
    protected virtual HttpRequestMessage CreateChatHttpRequest(ChatRequest request, bool stream)
    {
        string model = ResolveModel(request.Model);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUri("chat/completions"))
        {
            Content = new StringContent(
                OpenAIWire.BuildChatBody(request, model, stream).ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
        ApplyAuthentication(httpRequest);
        return httpRequest;
    }

    /// <summary>Applies request authentication (Bearer key and optional organization header).</summary>
    /// <param name="httpRequest">The outgoing request.</param>
    protected virtual void ApplyAuthentication(HttpRequestMessage httpRequest)
    {
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (_options.Organization is { Length: > 0 } organization)
        {
            httpRequest.Headers.TryAddWithoutValidation("OpenAI-Organization", organization);
        }
    }

    /// <summary>Resolves the effective model for a request.</summary>
    /// <param name="requestModel">The per-request model override, if any.</param>
    /// <exception cref="AiException">Thrown with <see cref="AiErrorCode.Configuration"/> when no model is available.</exception>
    protected virtual string ResolveModel(string? requestModel)
        => requestModel
            ?? _options.DefaultModel
            ?? throw new AiException(
                $"No model specified: set {nameof(OpenAIOptions)}.{nameof(OpenAIOptions.DefaultModel)} or pass ChatRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

    /// <summary>Resolves an absolute API URI from a relative path.</summary>
    /// <param name="relativePath">The path relative to the configured endpoint.</param>
    protected Uri BuildUri(string relativePath)
        => OpenAIUris.Resolve(_options.Endpoint, relativePath);

    /// <summary>
    /// Post-processes normalized errors for OpenAI-specific semantics: a 429 caused by an
    /// exhausted quota (<c>insufficient_quota</c>) is not transient — retrying cannot succeed.
    /// </summary>
    /// <param name="exception">The normalized exception.</param>
    protected static AiException Normalize(AiException exception)
    {
        if (exception is { Code: AiErrorCode.RateLimited, ProviderErrorBody: { } body }
            && body.Contains("insufficient_quota", StringComparison.Ordinal))
        {
            return new AiException(exception.Message + " (quota exhausted; retrying will not help)", exception.Code, exception)
            {
                Provider = exception.Provider,
                StatusCode = exception.StatusCode,
                RetryAfter = exception.RetryAfter,
                ProviderErrorBody = exception.ProviderErrorBody,
                RequestId = exception.RequestId,
                IsTransient = false,
            };
        }

        return exception;
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

/// <summary>URI helpers shared by the OpenAI-protocol clients.</summary>
internal static class OpenAIUris
{
    public static Uri Resolve(Uri endpoint, string relativePath)
    {
        string baseUri = endpoint.AbsoluteUri;
        if (!baseUri.EndsWith('/'))
        {
            baseUri += "/";
        }

        return new Uri(new Uri(baseUri), relativePath);
    }
}
