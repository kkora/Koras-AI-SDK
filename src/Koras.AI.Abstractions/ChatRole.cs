using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.AI;

/// <summary>
/// The role of a <see cref="ChatMessage"/> author. Behaves like an extensible enum: the
/// well-known roles are exposed as static properties, and unknown roles round-trip unchanged
/// so new provider vocabularies never require a package update.
/// </summary>
/// <param name="Value">The canonical, lower-case role value (for example <c>"user"</c>).</param>
[JsonConverter(typeof(ChatRoleJsonConverter))]
public readonly record struct ChatRole(string Value)
{
    /// <summary>Instructions that steer the model's behavior for the conversation.</summary>
    public static ChatRole System { get; } = new("system");

    /// <summary>Input authored by the end user or calling application.</summary>
    public static ChatRole User { get; } = new("user");

    /// <summary>Output authored by the model.</summary>
    public static ChatRole Assistant { get; } = new("assistant");

    /// <summary>The result of a tool invocation, returned to the model.</summary>
    public static ChatRole Tool { get; } = new("tool");

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Serializes <see cref="ChatRole"/> as its string value.</summary>
internal sealed class ChatRoleJsonConverter : JsonConverter<ChatRole>
{
    public override ChatRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, ChatRole value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
