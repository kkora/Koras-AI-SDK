using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.AI;

/// <summary>Shared serializer settings for the core package.</summary>
internal static class KorasJson
{
    public static JsonSerializerOptions DefaultOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
