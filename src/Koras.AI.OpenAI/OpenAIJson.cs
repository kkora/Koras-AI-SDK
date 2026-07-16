using System.Text.Json;

namespace Koras.AI.OpenAI;

/// <summary>Serializer settings for OpenAI wire payloads.</summary>
internal static class OpenAIJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
