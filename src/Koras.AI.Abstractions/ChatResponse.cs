using System.Text.Json;

namespace Koras.AI;

/// <summary>The result of a chat completion, normalized across providers.</summary>
public sealed class ChatResponse
{
    /// <summary>The assistant message produced by the model (text and/or tool calls).</summary>
    public required ChatMessage Message { get; init; }

    /// <summary>The provider that served the request (for example <c>"openai"</c>).</summary>
    public required string Provider { get; init; }

    /// <summary>The model that produced the response, as reported by the provider.</summary>
    public string? Model { get; init; }

    /// <summary>Why generation stopped.</summary>
    public ChatFinishReason FinishReason { get; init; } = ChatFinishReason.Unknown;

    /// <summary>Token consumption for this request, when the provider reports it.</summary>
    public TokenUsage Usage { get; init; }

    /// <summary>The provider's response identifier, when available.</summary>
    public string? ResponseId { get; init; }

    /// <summary>
    /// The provider's raw response payload, for fields the normalized model does not surface.
    /// May be an undefined element when the producing client does not retain raw payloads.
    /// </summary>
    public JsonElement RawRepresentation { get; init; }

    /// <summary>Convenience accessor for <see cref="ChatMessage.Text"/> on <see cref="Message"/>.</summary>
    public string? Text => Message.Text;
}
