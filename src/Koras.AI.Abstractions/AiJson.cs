using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koras.AI;

/// <summary>Shared serializer settings used across the SDK.</summary>
internal static class AiJson
{
    /// <summary>
    /// Safe defaults for user-facing (de)serialization: camelCase, case-insensitive reads,
    /// enums as camelCase strings, no payload-driven polymorphism.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
