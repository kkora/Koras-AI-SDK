using System.Text.Json;
using System.Text.Json.Nodes;

namespace Koras.AI.Gemini;

/// <summary>Maps the provider-neutral model to and from the Gemini generateContent wire format.</summary>
internal static class GeminiWire
{
    public static JsonObject BuildGenerateContentBody(ChatRequest request)
    {
        ChatOptions? options = request.Options;

        var body = new JsonObject
        {
            ["contents"] = BuildContents(request.Messages),
        };

        string? system = BuildSystemInstruction(request.Messages);
        if (system is not null)
        {
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = system }),
            };
        }

        var generationConfig = new JsonObject();
        if (options is not null)
        {
            if (options.Temperature is { } temperature)
            {
                generationConfig["temperature"] = temperature;
            }

            if (options.TopP is { } topP)
            {
                generationConfig["topP"] = topP;
            }

            if (options.MaxOutputTokens is { } maxTokens)
            {
                generationConfig["maxOutputTokens"] = maxTokens;
            }

            if (options.StopSequences is { Count: > 0 } stops)
            {
                var stopArray = new JsonArray();
                foreach (string stop in stops)
                {
                    stopArray.Add((JsonNode)stop);
                }

                generationConfig["stopSequences"] = stopArray;
            }

            switch (options.ResponseFormat)
            {
                case JsonChatResponseFormat:
                    generationConfig["responseMimeType"] = "application/json";
                    break;

                case JsonSchemaChatResponseFormat schemaFormat:
                    generationConfig["responseMimeType"] = "application/json";
                    generationConfig["responseSchema"] = CleanSchemaForGemini(JsonNode.Parse(schemaFormat.Schema.GetRawText()));
                    break;

                default:
                    break;
            }

            if (options.Tools is { Count: > 0 } tools)
            {
                var declarations = new JsonArray();
                foreach (AiTool tool in tools)
                {
                    declarations.Add(new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = CleanSchemaForGemini(JsonNode.Parse(tool.ParametersSchema.GetRawText())),
                    });
                }

                body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });

                if (options.ToolChoice is { } choice)
                {
                    var config = new JsonObject();
                    if (choice.RequiredToolName is { } toolName)
                    {
                        config["mode"] = "ANY";
                        config["allowedFunctionNames"] = new JsonArray((JsonNode)toolName);
                    }
                    else
                    {
                        config["mode"] = choice.Value switch
                        {
                            "none" => "NONE",
                            "required" => "ANY",
                            _ => "AUTO",
                        };
                    }

                    body["toolConfig"] = new JsonObject { ["functionCallingConfig"] = config };
                }
            }

            if (options.AdditionalProperties is { Count: > 0 } additional)
            {
                foreach ((string key, object? value) in additional)
                {
                    body[key] = value is null ? null : JsonSerializer.SerializeToNode(value, GeminiJson.Options);
                }
            }
        }

        if (generationConfig.Count > 0)
        {
            body["generationConfig"] = generationConfig;
        }

        return body;
    }

    private static string? BuildSystemInstruction(IReadOnlyList<ChatMessage> messages)
    {
        var parts = new List<string>();
        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatRole.System && message.Text is { Length: > 0 })
            {
                parts.Add(message.Text);
            }
        }

        return parts.Count > 0 ? string.Join("\n\n", parts) : null;
    }

    private static JsonArray BuildContents(IReadOnlyList<ChatMessage> messages)
    {
        var contents = new JsonArray();
        foreach (ChatMessage message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                continue; // hoisted into systemInstruction
            }

            if (message.Role == ChatRole.Tool)
            {
                // Gemini has no tool-call ids; the id carries the function name (see ParseCandidate).
                contents.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = FunctionNameFromCallId(message.ToolCallId),
                            ["response"] = new JsonObject { ["result"] = message.Text },
                        },
                    }),
                });
                continue;
            }

            var parts = new JsonArray();
            if (message.Text is { Length: > 0 })
            {
                parts.Add(new JsonObject { ["text"] = message.Text });
            }

            foreach (ToolCall call in message.ToolCalls)
            {
                parts.Add(new JsonObject
                {
                    ["functionCall"] = new JsonObject
                    {
                        ["name"] = call.Name,
                        ["args"] = ParseNodeOrEmpty(call.ArgumentsJson),
                    },
                });
            }

            contents.Add(new JsonObject
            {
                ["role"] = message.Role == ChatRole.Assistant ? "model" : "user",
                ["parts"] = parts,
            });
        }

        return contents;
    }

    public static ChatResponse ParseGenerateContentResponse(JsonElement root, string providerName)
    {
        var textParts = new List<string>();
        var toolCalls = new List<ToolCall>();
        string? finishReasonRaw = null;

        if (root.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
        {
            JsonElement candidate = candidates[0];
            finishReasonRaw = candidate.TryGetProperty("finishReason", out JsonElement finish) ? finish.GetString() : null;

            if (candidate.TryGetProperty("content", out JsonElement content)
                && content.TryGetProperty("parts", out JsonElement parts))
            {
                var callIndex = 0;
                foreach (JsonElement part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out JsonElement text) && text.GetString() is { } textValue)
                    {
                        textParts.Add(textValue);
                    }

                    if (part.TryGetProperty("functionCall", out JsonElement functionCall))
                    {
                        string name = functionCall.TryGetProperty("name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? string.Empty
                            : string.Empty;
                        toolCalls.Add(new ToolCall
                        {
                            Id = MakeCallId(callIndex++, name),
                            Name = name,
                            ArgumentsJson = functionCall.TryGetProperty("args", out JsonElement args) ? args.GetRawText() : "{}",
                        });
                    }
                }
            }
        }

        string? combinedText = textParts.Count > 0 ? string.Concat(textParts) : null;
        ChatMessage message = toolCalls.Count > 0
            ? ChatMessage.Assistant(combinedText, toolCalls)
            : ChatMessage.Assistant(combinedText ?? string.Empty);

        return new ChatResponse
        {
            Message = message,
            Provider = providerName,
            Model = root.TryGetProperty("modelVersion", out JsonElement model) ? model.GetString() : null,
            FinishReason = toolCalls.Count > 0 ? ChatFinishReason.ToolCalls : MapFinishReason(finishReasonRaw),
            Usage = ParseUsage(root),
            ResponseId = root.TryGetProperty("responseId", out JsonElement id) ? id.GetString() : null,
            RawRepresentation = root.Clone(),
        };
    }

    public static IEnumerable<ChatStreamUpdate> ParseStreamChunk(JsonElement root)
    {
        string? responseId = root.TryGetProperty("responseId", out JsonElement id) ? id.GetString() : null;

        if (root.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
        {
            JsonElement candidate = candidates[0];

            if (candidate.TryGetProperty("content", out JsonElement content)
                && content.TryGetProperty("parts", out JsonElement parts))
            {
                var callIndex = 0;
                foreach (JsonElement part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out JsonElement text)
                        && text.GetString() is { Length: > 0 } textDelta)
                    {
                        yield return new ChatStreamUpdate { TextDelta = textDelta, ResponseId = responseId };
                    }

                    if (part.TryGetProperty("functionCall", out JsonElement functionCall))
                    {
                        string name = functionCall.TryGetProperty("name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? string.Empty
                            : string.Empty;
                        yield return new ChatStreamUpdate
                        {
                            ResponseId = responseId,
                            ToolCallDelta = new ToolCallDelta
                            {
                                Index = callIndex,
                                Id = MakeCallId(callIndex, name),
                                Name = name,
                                ArgumentsJsonDelta = functionCall.TryGetProperty("args", out JsonElement args) ? args.GetRawText() : "{}",
                            },
                        };
                        callIndex++;
                    }
                }
            }

            if (candidate.TryGetProperty("finishReason", out JsonElement finish)
                && finish.ValueKind == JsonValueKind.String)
            {
                yield return new ChatStreamUpdate
                {
                    FinishReason = MapFinishReason(finish.GetString()),
                    Usage = ParseUsage(root) is { TotalTokens: > 0 } usage ? usage : null,
                    ResponseId = responseId,
                };
            }
        }
    }

    public static TokenUsage ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out JsonElement usage))
        {
            return default;
        }

        return new TokenUsage(
            usage.TryGetProperty("promptTokenCount", out JsonElement input) ? input.GetInt32() : 0,
            usage.TryGetProperty("candidatesTokenCount", out JsonElement output) ? output.GetInt32() : 0);
    }

    public static ChatFinishReason MapFinishReason(string? reason) => reason switch
    {
        "STOP" => ChatFinishReason.Stop,
        "MAX_TOKENS" => ChatFinishReason.Length,
        "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" => ChatFinishReason.ContentFilter,
        null or "" => ChatFinishReason.Unknown,
        _ => new ChatFinishReason(reason.ToLowerInvariant()),
    };

    /// <summary>Gemini's schema dialect rejects several JSON Schema keywords; strip them recursively.</summary>
    public static JsonNode? CleanSchemaForGemini(JsonNode? node)
    {
        if (node is JsonObject schemaObject)
        {
            schemaObject.Remove("additionalProperties");
            schemaObject.Remove("$schema");
            schemaObject.Remove("$defs");

            foreach (KeyValuePair<string, JsonNode?> property in schemaObject.ToList())
            {
                CleanSchemaForGemini(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                CleanSchemaForGemini(item);
            }
        }

        return node;
    }

    internal static string MakeCallId(int index, string name) => $"call_{index}_{name}";

    private static string FunctionNameFromCallId(string? callId)
    {
        if (callId is null)
        {
            return string.Empty;
        }

        // "call_{index}_{name}" → name (names may contain underscores, so split on the 2nd '_').
        int first = callId.IndexOf('_', StringComparison.Ordinal);
        int second = first >= 0 ? callId.IndexOf('_', first + 1) : -1;
        return second >= 0 ? callId[(second + 1)..] : callId;
    }

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
}
