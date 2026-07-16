using System.Text.Json;

namespace Koras.AI.Anthropic;

/// <summary>Serializer settings for Anthropic wire payloads.</summary>
internal static class AnthropicJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
