using System.Text.Json;

namespace Koras.AI.Gemini;

/// <summary>Serializer settings for Gemini wire payloads.</summary>
internal static class GeminiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
