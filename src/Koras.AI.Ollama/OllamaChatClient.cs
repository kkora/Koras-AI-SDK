using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koras.AI.Providers;

namespace Koras.AI.Ollama;

/// <summary>
/// <see cref="IChatClient"/> over the native Ollama <c>/api/chat</c> endpoint, including
/// JSON-lines streaming, tool calling (model-dependent), and schema-constrained output via
/// Ollama's <c>format</c> parameter. Thread-safe; intended for singleton use via
/// <c>AddKorasAI</c>.
/// </summary>
public sealed class OllamaChatClient : ProviderChatClient, IProviderHealthProbe
{
    private const string ConnectHint = "Is Ollama running? Start it with 'ollama serve' or install from https://ollama.com.";

    private readonly OllamaOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public OllamaChatClient(HttpClient httpClient, OllamaOptions options)
        : base(httpClient, providerName: "ollama")
    {
        _options = Guard.NotNull(options);
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateHttpRequest(request, stream: false);

        try
        {
            using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            return ParseChatResponse(document.RootElement);
        }
        catch (AiException ex) when (ex.Code == AiErrorCode.Network && !ex.Message.Contains(ConnectHint, StringComparison.Ordinal))
        {
            throw ProviderErrors.Network(ProviderName, ex.InnerException ?? ex, ConnectHint);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using HttpRequestMessage httpRequest = CreateHttpRequest(request, stream: true);

        HttpResponseMessage response;
        try
        {
            response = await SendForStreamAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (AiException ex) when (ex.Code == AiErrorCode.Network && !ex.Message.Contains(ConnectHint, StringComparison.Ordinal))
        {
            throw ProviderErrors.Network(ProviderName, ex.InnerException ?? ex, ConnectHint);
        }

        using (response)
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var toolCallIndex = 0;
                await foreach (string line in JsonLinesReader.ReadLinesAsync(stream, cancellationToken).ConfigureAwait(false))
                {
                    using JsonDocument document = ParseLine(line);
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("error", out JsonElement error))
                    {
                        throw new AiException(
                            $"The ollama stream reported an error: {error.GetString()}",
                            AiErrorCode.ProviderUnavailable)
                        { Provider = ProviderName };
                    }

                    if (root.TryGetProperty("message", out JsonElement message))
                    {
                        if (message.TryGetProperty("content", out JsonElement content)
                            && content.GetString() is { Length: > 0 } textDelta)
                        {
                            yield return new ChatStreamUpdate { TextDelta = textDelta };
                        }

                        foreach (ToolCall call in ParseToolCalls(message, startIndex: toolCallIndex))
                        {
                            yield return new ChatStreamUpdate
                            {
                                ToolCallDelta = new ToolCallDelta
                                {
                                    Index = toolCallIndex++,
                                    Id = call.Id,
                                    Name = call.Name,
                                    ArgumentsJsonDelta = call.ArgumentsJson,
                                },
                            };
                        }
                    }

                    if (root.TryGetProperty("done", out JsonElement done) && done.ValueKind == JsonValueKind.True)
                    {
                        yield return new ChatStreamUpdate
                        {
                            FinishReason = MapDoneReason(root),
                            Usage = ParseUsage(root),
                        };
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildUri("api/version"));
        using JsonDocument _ = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateHttpRequest(ChatRequest request, bool stream)
    {
        string model = request.Model
            ?? _options.DefaultModel
            ?? throw new AiException(
                $"No model specified: set {nameof(OllamaOptions)}.{nameof(OllamaOptions.DefaultModel)} or pass ChatRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

        return new HttpRequestMessage(HttpMethod.Post, BuildUri("api/chat"))
        {
            Content = new StringContent(
                BuildChatBody(request, model, stream).ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
    }

    private static JsonObject BuildChatBody(ChatRequest request, string model, bool stream)
    {
        var messages = new JsonArray();
        foreach (ChatMessage message in request.Messages)
        {
            var node = new JsonObject { ["role"] = message.Role.Value };
            node["content"] = message.Text ?? string.Empty;

            if (message.ToolCalls.Count > 0)
            {
                var toolCalls = new JsonArray();
                foreach (ToolCall call in message.ToolCalls)
                {
                    toolCalls.Add(new JsonObject
                    {
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = ParseNodeOrEmpty(call.ArgumentsJson),
                        },
                    });
                }

                node["tool_calls"] = toolCalls;
            }

            messages.Add(node);
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages,
            ["stream"] = stream,
        };

        ChatOptions? options = request.Options;
        if (options is not null)
        {
            var modelOptions = new JsonObject();
            if (options.Temperature is { } temperature)
            {
                modelOptions["temperature"] = temperature;
            }

            if (options.TopP is { } topP)
            {
                modelOptions["top_p"] = topP;
            }

            if (options.MaxOutputTokens is { } maxTokens)
            {
                modelOptions["num_predict"] = maxTokens;
            }

            if (options.StopSequences is { Count: > 0 } stops)
            {
                var stopArray = new JsonArray();
                foreach (string stop in stops)
                {
                    stopArray.Add((JsonNode)stop);
                }

                modelOptions["stop"] = stopArray;
            }

            if (modelOptions.Count > 0)
            {
                body["options"] = modelOptions;
            }

            if (options.Tools is { Count: > 0 } tools)
            {
                var toolsArray = new JsonArray();
                foreach (AiTool tool in tools)
                {
                    toolsArray.Add(new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = tool.Name,
                            ["description"] = tool.Description,
                            ["parameters"] = JsonNode.Parse(tool.ParametersSchema.GetRawText()),
                        },
                    });
                }

                body["tools"] = toolsArray;
            }

            switch (options.ResponseFormat)
            {
                case JsonChatResponseFormat:
                    body["format"] = "json";
                    break;

                case JsonSchemaChatResponseFormat schemaFormat:
                    body["format"] = JsonNode.Parse(schemaFormat.Schema.GetRawText());
                    break;

                default:
                    break;
            }

            if (options.AdditionalProperties is { Count: > 0 } additional)
            {
                foreach ((string key, object? value) in additional)
                {
                    body[key] = value is null ? null : JsonSerializer.SerializeToNode(value, OllamaJson.Options);
                }
            }
        }

        return body;
    }

    private ChatResponse ParseChatResponse(JsonElement root)
    {
        string? text = null;
        var toolCalls = new List<ToolCall>();

        if (root.TryGetProperty("message", out JsonElement message))
        {
            text = message.TryGetProperty("content", out JsonElement content) ? content.GetString() : null;
            toolCalls.AddRange(ParseToolCalls(message, startIndex: 0));
        }

        ChatMessage responseMessage = toolCalls.Count > 0
            ? ChatMessage.Assistant(string.IsNullOrEmpty(text) ? null : text, toolCalls)
            : ChatMessage.Assistant(text ?? string.Empty);

        return new ChatResponse
        {
            Message = responseMessage,
            Provider = ProviderName,
            Model = root.TryGetProperty("model", out JsonElement model) ? model.GetString() : null,
            FinishReason = toolCalls.Count > 0 ? ChatFinishReason.ToolCalls : MapDoneReason(root),
            Usage = ParseUsage(root),
            RawRepresentation = root.Clone(),
        };
    }

    private static IEnumerable<ToolCall> ParseToolCalls(JsonElement message, int startIndex)
    {
        if (!message.TryGetProperty("tool_calls", out JsonElement toolCalls) || toolCalls.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        int index = startIndex;
        foreach (JsonElement call in toolCalls.EnumerateArray())
        {
            if (!call.TryGetProperty("function", out JsonElement function))
            {
                continue;
            }

            string name = function.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            // Ollama does not assign tool-call ids; synthesize stable ones for the round-trip.
            yield return new ToolCall
            {
                Id = $"call_{index}_{name}",
                Name = name,
                ArgumentsJson = function.TryGetProperty("arguments", out JsonElement args) ? args.GetRawText() : "{}",
            };
            index++;
        }
    }

    private static ChatFinishReason MapDoneReason(JsonElement root)
    {
        string? reason = root.TryGetProperty("done_reason", out JsonElement done) ? done.GetString() : null;
        return reason switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            null or "" => ChatFinishReason.Stop,
            _ => new ChatFinishReason(reason),
        };
    }

    private static TokenUsage ParseUsage(JsonElement root)
        => new(
            root.TryGetProperty("prompt_eval_count", out JsonElement input) ? input.GetInt32() : 0,
            root.TryGetProperty("eval_count", out JsonElement output) ? output.GetInt32() : 0);

    private static JsonNode ParseNodeOrEmpty(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private Uri BuildUri(string relativePath)
    {
        string endpoint = _options.Endpoint.AbsoluteUri;
        if (!endpoint.EndsWith('/'))
        {
            endpoint += "/";
        }

        return new Uri(new Uri(endpoint), relativePath);
    }

    private JsonDocument ParseLine(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            throw ProviderErrors.InvalidResponse(ProviderName, line, ex);
        }
    }
}
