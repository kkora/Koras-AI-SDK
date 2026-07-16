using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Koras.AI.Providers;

namespace Koras.AI.Anthropic;

/// <summary>
/// <see cref="IChatClient"/> over the Anthropic Messages API. Structured output is implemented
/// with a forced synthetic tool (Anthropic has no native response-format parameter), so
/// schema-constrained requests should use <c>CompleteAsync</c> rather than streaming.
/// Thread-safe; intended for singleton use via <c>AddKorasAI</c>.
/// </summary>
public sealed class AnthropicChatClient : ProviderChatClient, IProviderHealthProbe
{
    private readonly AnthropicOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public AnthropicChatClient(HttpClient httpClient, AnthropicOptions options)
        : base(httpClient, providerName: "anthropic")
    {
        _options = Guard.NotNull(options);
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        bool structuredOutput = request.Options?.ResponseFormat is JsonSchemaChatResponseFormat;
        using HttpRequestMessage httpRequest = CreateHttpRequest(request, stream: false);
        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        return AnthropicWire.ParseMessagesResponse(document.RootElement, ProviderName, structuredOutput);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateHttpRequest(request, stream: true);
        using HttpResponseMessage response = await SendForStreamAsync(httpRequest, cancellationToken).ConfigureAwait(false);

        var inputTokens = 0;
        string? responseId = null;

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            await foreach (SseEvent sseEvent in SseReader.ReadEventsAsync(stream, cancellationToken).ConfigureAwait(false))
            {
                using JsonDocument document = ParseEvent(sseEvent.Data);
                JsonElement root = document.RootElement;
                string? eventType = sseEvent.EventType
                    ?? (root.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() : null);

                switch (eventType)
                {
                    case "message_start":
                        if (root.TryGetProperty("message", out JsonElement message))
                        {
                            responseId = message.TryGetProperty("id", out JsonElement id) ? id.GetString() : null;
                            inputTokens = AnthropicWire.ParseUsage(message).InputTokens;
                        }

                        break;

                    case "content_block_start":
                        if (root.TryGetProperty("content_block", out JsonElement block)
                            && block.TryGetProperty("type", out JsonElement blockType)
                            && blockType.GetString() == "tool_use")
                        {
                            yield return new ChatStreamUpdate
                            {
                                ResponseId = responseId,
                                ToolCallDelta = new ToolCallDelta
                                {
                                    Index = root.TryGetProperty("index", out JsonElement index) ? index.GetInt32() : 0,
                                    Id = block.TryGetProperty("id", out JsonElement blockId) ? blockId.GetString() : null,
                                    Name = block.TryGetProperty("name", out JsonElement name) ? name.GetString() : null,
                                },
                            };
                        }

                        break;

                    case "content_block_delta":
                        if (root.TryGetProperty("delta", out JsonElement delta)
                            && delta.TryGetProperty("type", out JsonElement deltaType))
                        {
                            switch (deltaType.GetString())
                            {
                                case "text_delta":
                                    if (delta.TryGetProperty("text", out JsonElement text)
                                        && text.GetString() is { Length: > 0 } textDelta)
                                    {
                                        yield return new ChatStreamUpdate { TextDelta = textDelta, ResponseId = responseId };
                                    }

                                    break;

                                case "input_json_delta":
                                    if (delta.TryGetProperty("partial_json", out JsonElement partial)
                                        && partial.GetString() is { Length: > 0 } partialJson)
                                    {
                                        yield return new ChatStreamUpdate
                                        {
                                            ResponseId = responseId,
                                            ToolCallDelta = new ToolCallDelta
                                            {
                                                Index = root.TryGetProperty("index", out JsonElement deltaIndex) ? deltaIndex.GetInt32() : 0,
                                                ArgumentsJsonDelta = partialJson,
                                            },
                                        };
                                    }

                                    break;

                                default:
                                    break;
                            }
                        }

                        break;

                    case "message_delta":
                        ChatFinishReason? finishReason = null;
                        if (root.TryGetProperty("delta", out JsonElement messageDelta)
                            && messageDelta.TryGetProperty("stop_reason", out JsonElement stopReason)
                            && stopReason.ValueKind == JsonValueKind.String)
                        {
                            finishReason = AnthropicWire.MapStopReason(stopReason.GetString());
                        }

                        var outputTokens = 0;
                        if (root.TryGetProperty("usage", out JsonElement usage)
                            && usage.TryGetProperty("output_tokens", out JsonElement output))
                        {
                            outputTokens = output.GetInt32();
                        }

                        yield return new ChatStreamUpdate
                        {
                            FinishReason = finishReason,
                            Usage = new TokenUsage(inputTokens, outputTokens),
                            ResponseId = responseId,
                        };
                        break;

                    case "error":
                        throw CreateStreamError(root);

                    default:
                        break; // ping, content_block_stop, message_stop
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        var uri = new Uri(EndpointBase() + "v1/models?limit=1");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyHeaders(httpRequest);
        using JsonDocument _ = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateHttpRequest(ChatRequest request, bool stream)
    {
        string model = request.Model
            ?? _options.DefaultModel
            ?? throw new AiException(
                $"No model specified: set {nameof(AnthropicOptions)}.{nameof(AnthropicOptions.DefaultModel)} or pass ChatRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(EndpointBase() + "v1/messages"))
        {
            Content = new StringContent(
                AnthropicWire.BuildMessagesBody(request, model, _options.DefaultMaxOutputTokens, stream).ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
        ApplyHeaders(httpRequest);
        return httpRequest;
    }

    private void ApplyHeaders(HttpRequestMessage httpRequest)
    {
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);
    }

    private string EndpointBase()
    {
        string endpoint = _options.Endpoint.AbsoluteUri;
        return endpoint.EndsWith('/') ? endpoint : endpoint + "/";
    }

    private AiException CreateStreamError(JsonElement root)
    {
        string? errorType = null;
        string? message = null;
        if (root.TryGetProperty("error", out JsonElement error))
        {
            errorType = error.TryGetProperty("type", out JsonElement type) ? type.GetString() : null;
            message = error.TryGetProperty("message", out JsonElement messageElement) ? messageElement.GetString() : null;
        }

        AiErrorCode code = errorType switch
        {
            "overloaded_error" => AiErrorCode.ProviderUnavailable,
            "rate_limit_error" => AiErrorCode.RateLimited,
            "authentication_error" => AiErrorCode.Authentication,
            "permission_error" => AiErrorCode.PermissionDenied,
            "invalid_request_error" => AiErrorCode.InvalidRequest,
            "not_found_error" => AiErrorCode.ModelNotFound,
            "api_error" => AiErrorCode.ProviderUnavailable,
            _ => AiErrorCode.Unknown,
        };

        return new AiException(
            $"The anthropic stream reported an error: {message ?? errorType ?? "unknown"}",
            code)
        {
            Provider = ProviderName,
            ProviderErrorBody = ProviderErrors.Truncate(root.GetRawText()),
        };
    }

    private JsonDocument ParseEvent(string data)
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
