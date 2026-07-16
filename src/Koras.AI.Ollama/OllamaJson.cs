using System.Text.Json;

namespace Koras.AI.Ollama;

/// <summary>Serializer settings for Ollama wire payloads.</summary>
internal static class OllamaJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
