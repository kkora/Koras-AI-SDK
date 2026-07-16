using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.AI;

/// <summary>
/// The provider-neutral reason a chat completion stopped producing output. Unknown provider
/// values round-trip unchanged through <see cref="Value"/>.
/// </summary>
/// <param name="Value">The canonical, lower-case finish reason (for example <c>"stop"</c>).</param>
[JsonConverter(typeof(ChatFinishReasonJsonConverter))]
public readonly record struct ChatFinishReason(string Value)
{
    /// <summary>The model completed its answer naturally or hit a stop sequence.</summary>
    public static ChatFinishReason Stop { get; } = new("stop");

    /// <summary>Generation stopped because the output token limit was reached.</summary>
    public static ChatFinishReason Length { get; } = new("length");

    /// <summary>The model requested one or more tool invocations.</summary>
    public static ChatFinishReason ToolCalls { get; } = new("tool_calls");

    /// <summary>The provider's safety system truncated or blocked the output.</summary>
    public static ChatFinishReason ContentFilter { get; } = new("content_filter");

    /// <summary>The provider did not report a recognizable finish reason.</summary>
    public static ChatFinishReason Unknown { get; } = new("unknown");

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Serializes <see cref="ChatFinishReason"/> as its string value.</summary>
internal sealed class ChatFinishReasonJsonConverter : JsonConverter<ChatFinishReason>
{
    public override ChatFinishReason Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? ChatFinishReason.Unknown.Value);

    public override void Write(Utf8JsonWriter writer, ChatFinishReason value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
