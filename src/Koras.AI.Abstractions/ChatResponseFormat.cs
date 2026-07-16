using System.Text.Json;

namespace Koras.AI;

/// <summary>
/// Describes the shape of output the model must produce. Use <see cref="Text"/> (the default),
/// <see cref="Json"/> for free-form JSON, or <see cref="JsonSchema"/>/<see cref="ForType{T}"/>
/// for schema-constrained structured output.
/// </summary>
public abstract class ChatResponseFormat
{
    private protected ChatResponseFormat()
    {
    }

    /// <summary>Plain text output (the default when no format is specified).</summary>
    public static ChatResponseFormat Text { get; } = new TextChatResponseFormat();

    /// <summary>Free-form JSON output ("JSON mode") without a schema constraint.</summary>
    public static ChatResponseFormat Json { get; } = new JsonChatResponseFormat();

    /// <summary>JSON output constrained by the supplied JSON Schema.</summary>
    /// <param name="name">A short identifier for the schema (letters, digits, underscores, dashes).</param>
    /// <param name="schema">The JSON Schema the output must satisfy.</param>
    /// <param name="strict">Whether the provider should enforce the schema strictly, when it supports the distinction.</param>
    public static ChatResponseFormat JsonSchema(string name, JsonElement schema, bool strict = true)
        => new JsonSchemaChatResponseFormat(Guard.NotNullOrWhiteSpace(name), schema, strict);

    /// <summary>
    /// JSON output constrained by a schema generated from <typeparamref name="T"/> via
    /// <see cref="AiJsonSchema"/>. Pair with
    /// <c>CompleteAsync&lt;T&gt;</c> to get a deserialized instance back.
    /// </summary>
    /// <typeparam name="T">The contract type describing the expected output.</typeparam>
    public static ChatResponseFormat ForType<T>()
    {
        string name = SchemaNameFor(typeof(T));
        return new JsonSchemaChatResponseFormat(name, AiJsonSchema.FromType<T>(), strict: true);
    }

    private static string SchemaNameFor(Type type)
    {
        string name = type.Name;
        int arity = name.IndexOf('`', StringComparison.Ordinal);
        if (arity >= 0)
        {
            name = name[..arity];
        }

        return name;
    }
}

/// <summary>Plain-text output format. Obtain via <see cref="ChatResponseFormat.Text"/>.</summary>
public sealed class TextChatResponseFormat : ChatResponseFormat
{
    internal TextChatResponseFormat()
    {
    }
}

/// <summary>Free-form JSON output format. Obtain via <see cref="ChatResponseFormat.Json"/>.</summary>
public sealed class JsonChatResponseFormat : ChatResponseFormat
{
    internal JsonChatResponseFormat()
    {
    }
}

/// <summary>Schema-constrained JSON output format. Obtain via <see cref="ChatResponseFormat.JsonSchema"/> or <see cref="ChatResponseFormat.ForType{T}"/>.</summary>
public sealed class JsonSchemaChatResponseFormat : ChatResponseFormat
{
    internal JsonSchemaChatResponseFormat(string name, JsonElement schema, bool strict)
    {
        Name = name;
        Schema = schema;
        Strict = strict;
    }

    /// <summary>The schema's short identifier.</summary>
    public string Name { get; }

    /// <summary>The JSON Schema the output must satisfy.</summary>
    public JsonElement Schema { get; }

    /// <summary>Whether the provider should enforce the schema strictly, when it supports the distinction.</summary>
    public bool Strict { get; }
}
