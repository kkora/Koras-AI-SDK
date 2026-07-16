using System.Text.Json;
using System.Text.Json.Nodes;

namespace Koras.AI.OpenAI;

/// <summary>Maps the provider-neutral model to and from the OpenAI chat-completions wire format.</summary>
internal static class OpenAIWire
{
    public static JsonObject BuildChatBody(ChatRequest request, string model, bool stream)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = BuildMessages(request.Messages),
        };

        ChatOptions? options = request.Options;
        if (options is not null)
        {
            if (options.Temperature is { } temperature)
            {
                body["temperature"] = temperature;
            }

            if (options.TopP is { } topP)
            {
                body["top_p"] = topP;
            }

            if (options.MaxOutputTokens is { } maxTokens)
            {
                body["max_completion_tokens"] = maxTokens;
            }

            if (options.StopSequences is { Count: > 0 } stops)
            {
                var stopArray = new JsonArray();
                foreach (string stop in stops)
                {
                    stopArray.Add((JsonNode)stop);
                }

                body["stop"] = stopArray;
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

                if (options.ToolChoice is { } choice)
                {
                    body["tool_choice"] = choice.RequiredToolName is { } toolName
                        ? new JsonObject
                        {
                            ["type"] = "function",
                            ["function"] = new JsonObject { ["name"] = toolName },
                        }
                        : choice.Value;
                }
            }

            switch (options.ResponseFormat)
            {
                case JsonChatResponseFormat:
                    body["response_format"] = new JsonObject { ["type"] = "json_object" };
                    break;

                case JsonSchemaChatResponseFormat schemaFormat:
                    body["response_format"] = new JsonObject
                    {
                        ["type"] = "json_schema",
                        ["json_schema"] = new JsonObject
                        {
                            ["name"] = schemaFormat.Name,
                            ["strict"] = schemaFormat.Strict,
                            ["schema"] = JsonNode.Parse(schemaFormat.Schema.GetRawText()),
                        },
                    };
                    break;

                default:
                    break; // Text / null → provider default
            }

            if (options.AdditionalProperties is { Count: > 0 } additional)
            {
                foreach ((string key, object? value) in additional)
                {
                    body[key] = value is null ? null : JsonSerializer.SerializeToNode(value, OpenAIJson.Options);
                }
            }
        }

        if (stream)
        {
            body["stream"] = true;
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        return body;
    }

    private static JsonArray BuildMessages(IReadOnlyList<ChatMessage> messages)
    {
        var array = new JsonArray();
        foreach (ChatMessage message in messages)
        {
            var node = new JsonObject { ["role"] = message.Role.Value };

            if (message.Role == ChatRole.Tool)
            {
                node["tool_call_id"] = message.ToolCallId;
                node["content"] = message.Text;
            }
            else
            {
                node["content"] = message.Text;
                if (message.ToolCalls.Count > 0)
                {
                    var toolCalls = new JsonArray();
                    foreach (ToolCall call in message.ToolCalls)
                    {
                        toolCalls.Add(new JsonObject
                        {
                            ["id"] = call.Id,
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = call.Name,
                                ["arguments"] = call.ArgumentsJson,
                            },
                        });
                    }

                    node["tool_calls"] = toolCalls;
                }
            }

            array.Add(node);
        }

        return array;
    }

    public static ChatResponse ParseChatResponse(JsonElement root, string providerName)
    {
        JsonElement choice = root.GetProperty("choices")[0];
        JsonElement messageElement = choice.GetProperty("message");

        string? text = messageElement.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.String
            ? content.GetString()
            : null;

        var toolCalls = new List<ToolCall>();
        if (messageElement.TryGetProperty("tool_calls", out JsonElement toolCallsElement) && toolCallsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement call in toolCallsElement.EnumerateArray())
            {
                JsonElement function = call.GetProperty("function");
                toolCalls.Add(new ToolCall
                {
                    Id = call.GetProperty("id").GetString() ?? string.Empty,
                    Name = function.GetProperty("name").GetString() ?? string.Empty,
                    ArgumentsJson = function.TryGetProperty("arguments", out JsonElement args) ? args.GetString() ?? "{}" : "{}",
                });
            }
        }

        ChatMessage message = toolCalls.Count > 0
            ? ChatMessage.Assistant(text, toolCalls)
            : ChatMessage.Assistant(text ?? string.Empty);

        return new ChatResponse
        {
            Message = message,
            Provider = providerName,
            Model = root.TryGetProperty("model", out JsonElement model) ? model.GetString() : null,
            FinishReason = MapFinishReason(choice.TryGetProperty("finish_reason", out JsonElement finish) ? finish.GetString() : null),
            Usage = ParseUsage(root),
            ResponseId = root.TryGetProperty("id", out JsonElement id) ? id.GetString() : null,
            RawRepresentation = root.Clone(),
        };
    }

    public static IEnumerable<ChatStreamUpdate> ParseStreamChunk(JsonElement root)
    {
        string? responseId = root.TryGetProperty("id", out JsonElement id) ? id.GetString() : null;

        if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
        {
            JsonElement choice = choices[0];

            if (choice.TryGetProperty("delta", out JsonElement delta))
            {
                if (delta.TryGetProperty("content", out JsonElement content)
                    && content.ValueKind == JsonValueKind.String
                    && content.GetString() is { Length: > 0 } textDelta)
                {
                    yield return new ChatStreamUpdate { TextDelta = textDelta, ResponseId = responseId };
                }

                if (delta.TryGetProperty("tool_calls", out JsonElement toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement call in toolCalls.EnumerateArray())
                    {
                        JsonElement function = default;
                        bool hasFunction = call.TryGetProperty("function", out function);
                        yield return new ChatStreamUpdate
                        {
                            ResponseId = responseId,
                            ToolCallDelta = new ToolCallDelta
                            {
                                Index = call.TryGetProperty("index", out JsonElement index) ? index.GetInt32() : 0,
                                Id = call.TryGetProperty("id", out JsonElement callId) ? callId.GetString() : null,
                                Name = hasFunction && function.TryGetProperty("name", out JsonElement name) ? name.GetString() : null,
                                ArgumentsJsonDelta = hasFunction && function.TryGetProperty("arguments", out JsonElement args) ? args.GetString() : null,
                            },
                        };
                    }
                }
            }

            if (choice.TryGetProperty("finish_reason", out JsonElement finish) && finish.ValueKind == JsonValueKind.String)
            {
                yield return new ChatStreamUpdate
                {
                    FinishReason = MapFinishReason(finish.GetString()),
                    ResponseId = responseId,
                };
            }
        }

        if (root.TryGetProperty("usage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object)
        {
            yield return new ChatStreamUpdate { Usage = ParseUsageElement(usage), ResponseId = responseId };
        }
    }

    public static TokenUsage ParseUsage(JsonElement root)
        => root.TryGetProperty("usage", out JsonElement usage) && usage.ValueKind == JsonValueKind.Object
            ? ParseUsageElement(usage)
            : default;

    private static TokenUsage ParseUsageElement(JsonElement usage)
        => new(
            usage.TryGetProperty("prompt_tokens", out JsonElement input) ? input.GetInt32() : 0,
            usage.TryGetProperty("completion_tokens", out JsonElement output) ? output.GetInt32() : 0);

    public static ChatFinishReason MapFinishReason(string? reason) => reason switch
    {
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "tool_calls" or "function_call" => ChatFinishReason.ToolCalls,
        "content_filter" => ChatFinishReason.ContentFilter,
        null or "" => ChatFinishReason.Unknown,
        _ => new ChatFinishReason(reason),
    };
}
