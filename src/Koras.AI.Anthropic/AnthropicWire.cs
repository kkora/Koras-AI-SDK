using System.Text.Json;
using System.Text.Json.Nodes;

namespace Koras.AI.Anthropic;

/// <summary>Maps the provider-neutral model to and from the Anthropic Messages API wire format.</summary>
internal static class AnthropicWire
{
    /// <summary>The synthetic tool used to implement schema-constrained structured output.</summary>
    public const string StructuredOutputToolName = "record_output";

    public static JsonObject BuildMessagesBody(ChatRequest request, string model, int defaultMaxTokens, bool stream)
    {
        ChatOptions? options = request.Options;

        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = options?.MaxOutputTokens ?? defaultMaxTokens,
            ["messages"] = BuildMessages(request.Messages),
        };

        string? system = BuildSystemPrompt(request.Messages, options?.ResponseFormat);
        if (system is not null)
        {
            body["system"] = system;
        }

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

            if (options.StopSequences is { Count: > 0 } stops)
            {
                var stopArray = new JsonArray();
                foreach (string stop in stops)
                {
                    stopArray.Add((JsonNode)stop);
                }

                body["stop_sequences"] = stopArray;
            }
        }

        ApplyToolsAndResponseFormat(body, options);

        if (options?.AdditionalProperties is { Count: > 0 } additional)
        {
            foreach ((string key, object? value) in additional)
            {
                body[key] = value is null ? null : JsonSerializer.SerializeToNode(value, AnthropicJson.Options);
            }
        }

        if (stream)
        {
            body["stream"] = true;
        }

        return body;
    }

    private static void ApplyToolsAndResponseFormat(JsonObject body, ChatOptions? options)
    {
        var tools = new JsonArray();
        JsonNode? toolChoice = null;

        if (options?.Tools is { Count: > 0 } declaredTools)
        {
            foreach (AiTool tool in declaredTools)
            {
                tools.Add(new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["input_schema"] = JsonNode.Parse(tool.ParametersSchema.GetRawText()),
                });
            }

            toolChoice = options.ToolChoice is { } choice
                ? choice.RequiredToolName is { } toolName
                    ? new JsonObject { ["type"] = "tool", ["name"] = toolName }
                    : choice.Value switch
                    {
                        "none" => new JsonObject { ["type"] = "none" },
                        "required" => new JsonObject { ["type"] = "any" },
                        _ => new JsonObject { ["type"] = "auto" },
                    }
                : null;
        }

        // Structured output: Anthropic has no response_format — force a synthetic tool whose
        // input schema is the requested output schema, then surface its input as the text.
        if (options?.ResponseFormat is JsonSchemaChatResponseFormat schemaFormat)
        {
            tools.Add(new JsonObject
            {
                ["name"] = StructuredOutputToolName,
                ["description"] = "Record the final answer in the required structure. Always call this tool exactly once.",
                ["input_schema"] = JsonNode.Parse(schemaFormat.Schema.GetRawText()),
            });
            toolChoice = new JsonObject { ["type"] = "tool", ["name"] = StructuredOutputToolName };
        }

        if (tools.Count > 0)
        {
            body["tools"] = tools;
            if (toolChoice is not null)
            {
                body["tool_choice"] = toolChoice;
            }
        }
    }

    private static string? BuildSystemPrompt(IReadOnlyList<ChatMessage> messages, ChatResponseFormat? responseFormat)
    {
        var parts = new List<string>();
        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatRole.System && message.Text is { Length: > 0 })
            {
                parts.Add(message.Text);
            }
        }

        if (responseFormat is JsonChatResponseFormat)
        {
            parts.Add("Respond with valid JSON only, with no surrounding prose or code fences.");
        }

        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    private static JsonArray BuildMessages(IReadOnlyList<ChatMessage> messages)
    {
        var array = new JsonArray();
        JsonArray? pendingToolResults = null;

        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                continue; // hoisted into the top-level system field
            }

            if (message.Role == ChatRole.Tool)
            {
                // Consecutive tool results merge into a single user turn, as the API expects.
                pendingToolResults ??= [];
                pendingToolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = message.ToolCallId,
                    ["content"] = message.Text,
                });
                continue;
            }

            if (pendingToolResults is not null)
            {
                array.Add(new JsonObject { ["role"] = "user", ["content"] = pendingToolResults });
                pendingToolResults = null;
            }

            if (message.Role == ChatRole.Assistant && message.ToolCalls.Count > 0)
            {
                var content = new JsonArray();
                if (message.Text is { Length: > 0 })
                {
                    content.Add(new JsonObject { ["type"] = "text", ["text"] = message.Text });
                }

                foreach (ToolCall call in message.ToolCalls)
                {
                    content.Add(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = call.Id,
                        ["name"] = call.Name,
                        ["input"] = ParseArgumentsOrEmpty(call.ArgumentsJson),
                    });
                }

                array.Add(new JsonObject { ["role"] = "assistant", ["content"] = content });
            }
            else
            {
                array.Add(new JsonObject
                {
                    ["role"] = message.Role == ChatRole.Assistant ? "assistant" : "user",
                    ["content"] = message.Text ?? string.Empty,
                });
            }
        }

        if (pendingToolResults is not null)
        {
            array.Add(new JsonObject { ["role"] = "user", ["content"] = pendingToolResults });
        }

        return array;
    }

    private static JsonNode ParseArgumentsOrEmpty(string argumentsJson)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    public static ChatResponse ParseMessagesResponse(JsonElement root, string providerName, bool structuredOutputForced)
    {
        var textParts = new List<string>();
        var toolCalls = new List<ToolCall>();
        string? structuredOutputJson = null;

        if (root.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement block in content.EnumerateArray())
            {
                switch (block.TryGetProperty("type", out JsonElement type) ? type.GetString() : null)
                {
                    case "text":
                        if (block.TryGetProperty("text", out JsonElement text) && text.GetString() is { } textValue)
                        {
                            textParts.Add(textValue);
                        }

                        break;

                    case "tool_use":
                        string name = block.GetProperty("name").GetString() ?? string.Empty;
                        string arguments = block.TryGetProperty("input", out JsonElement input) ? input.GetRawText() : "{}";
                        if (structuredOutputForced && name == StructuredOutputToolName)
                        {
                            structuredOutputJson = arguments;
                        }
                        else
                        {
                            toolCalls.Add(new ToolCall
                            {
                                Id = block.GetProperty("id").GetString() ?? string.Empty,
                                Name = name,
                                ArgumentsJson = arguments,
                            });
                        }

                        break;

                    default:
                        break;
                }
            }
        }

        string? stopReason = root.TryGetProperty("stop_reason", out JsonElement stop) ? stop.GetString() : null;

        ChatMessage message;
        ChatFinishReason finishReason;
        if (structuredOutputJson is not null)
        {
            message = ChatMessage.Assistant(structuredOutputJson);
            finishReason = ChatFinishReason.Stop;
        }
        else
        {
            string? combinedText = textParts.Count > 0 ? string.Concat(textParts) : null;
            message = toolCalls.Count > 0
                ? ChatMessage.Assistant(combinedText, toolCalls)
                : ChatMessage.Assistant(combinedText ?? string.Empty);
            finishReason = MapStopReason(stopReason);
        }

        return new ChatResponse
        {
            Message = message,
            Provider = providerName,
            Model = root.TryGetProperty("model", out JsonElement model) ? model.GetString() : null,
            FinishReason = finishReason,
            Usage = ParseUsage(root),
            ResponseId = root.TryGetProperty("id", out JsonElement id) ? id.GetString() : null,
            RawRepresentation = root.Clone(),
        };
    }

    public static TokenUsage ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        return new TokenUsage(
            usage.TryGetProperty("input_tokens", out JsonElement input) ? input.GetInt32() : 0,
            usage.TryGetProperty("output_tokens", out JsonElement output) ? output.GetInt32() : 0);
    }

    public static ChatFinishReason MapStopReason(string? reason) => reason switch
    {
        "end_turn" or "stop_sequence" => ChatFinishReason.Stop,
        "max_tokens" => ChatFinishReason.Length,
        "tool_use" => ChatFinishReason.ToolCalls,
        "refusal" => ChatFinishReason.ContentFilter,
        null or "" => ChatFinishReason.Unknown,
        _ => new ChatFinishReason(reason),
    };
}
