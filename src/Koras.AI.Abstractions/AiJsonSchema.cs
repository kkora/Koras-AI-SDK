using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace Koras.AI;

/// <summary>
/// Generates JSON Schema documents from .NET types for structured output and tool parameter
/// declarations. Schemas honor <see cref="DescriptionAttribute"/> on types, properties, and
/// parameters, and are emitted in "strict" shape (every object lists all properties as
/// <c>required</c> and sets <c>additionalProperties: false</c>) so they are accepted by
/// providers with strict schema validation; providers with narrower schema dialects relax the
/// output themselves.
/// </summary>
public static class AiJsonSchema
{
    private static readonly JsonSerializerOptions SchemaSerializerOptions = CreateSerializerOptions();

    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = ApplyDescriptions,
    };

    /// <summary>Generates a JSON Schema for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The contract type to describe.</typeparam>
    /// <returns>The schema as a <see cref="JsonElement"/> rooted at a schema object.</returns>
    public static JsonElement FromType<T>() => FromType(typeof(T));

    /// <summary>Generates a JSON Schema for <paramref name="type"/>.</summary>
    /// <param name="type">The contract type to describe.</param>
    /// <returns>The schema as a <see cref="JsonElement"/> rooted at a schema object.</returns>
    public static JsonElement FromType(Type type)
    {
        Guard.NotNull(type);
        JsonNode node = SchemaSerializerOptions.GetJsonSchemaAsNode(type, ExporterOptions);
        MakeStrict(node);
        return ToElement(node);
    }

    /// <summary>
    /// Builds an object schema describing the parameters of a tool handler delegate. Parameters
    /// of type <see cref="CancellationToken"/> are excluded; parameters with default values are
    /// optional; <see cref="DescriptionAttribute"/> annotations become property descriptions.
    /// </summary>
    /// <param name="method">The handler method whose parameters describe the tool's arguments.</param>
    /// <returns>A JSON Schema object with one property per bindable parameter.</returns>
    public static JsonElement FromMethodParameters(MethodInfo method)
    {
        Guard.NotNull(method);

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            string name = parameter.Name ?? $"arg{parameter.Position}";
            JsonNode parameterSchema = SchemaSerializerOptions.GetJsonSchemaAsNode(parameter.ParameterType, ExporterOptions);
            MakeStrict(parameterSchema);

            if (parameter.GetCustomAttribute<DescriptionAttribute>() is { } description
                && parameterSchema is JsonObject parameterObject)
            {
                parameterObject["description"] = description.Description;
            }

            properties[name] = parameterSchema;
            if (!parameter.HasDefaultValue)
            {
                required.Add((JsonNode)name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };

        return ToElement(schema);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        options.MakeReadOnly();
        return options;
    }

    private static JsonNode ApplyDescriptions(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (schema is not JsonObject schemaObject || schemaObject.ContainsKey("description"))
        {
            return schema;
        }

        DescriptionAttribute? description = null;
        if (context.PropertyInfo?.AttributeProvider is { } propertyAttributes)
        {
            description = propertyAttributes
                .GetCustomAttributes(typeof(DescriptionAttribute), inherit: true)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault();
        }

        if (description is null && context.PropertyInfo is null)
        {
            description = context.TypeInfo.Type.GetCustomAttribute<DescriptionAttribute>();
        }

        if (description is not null)
        {
            schemaObject["description"] = description.Description;
        }

        return schemaObject;
    }

    /// <summary>Recursively enforces strict-object shape: required = all properties, additionalProperties = false.</summary>
    private static void MakeStrict(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode? item in array)
            {
                MakeStrict(item);
            }

            return;
        }

        if (node is not JsonObject schemaObject)
        {
            return;
        }

        if (schemaObject.TryGetPropertyValue("properties", out JsonNode? propertiesNode)
            && propertiesNode is JsonObject properties)
        {
            var required = new JsonArray();
            foreach (KeyValuePair<string, JsonNode?> property in properties)
            {
                required.Add((JsonNode)property.Key);
                MakeStrict(property.Value);
            }

            schemaObject["required"] = required;
            schemaObject["additionalProperties"] = false;
        }

        foreach (string key in new[] { "items", "anyOf", "allOf", "oneOf", "not" })
        {
            if (schemaObject.TryGetPropertyValue(key, out JsonNode? child))
            {
                MakeStrict(child);
            }
        }
    }

    private static JsonElement ToElement(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
