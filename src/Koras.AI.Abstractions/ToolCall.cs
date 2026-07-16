using System.Text.Json;

namespace Koras.AI;

/// <summary>
/// A tool invocation requested by the model. Inspect <see cref="Name"/> and
/// <see cref="ArgumentsJson"/> (or bind them with <see cref="ParseArguments{T}"/>), execute the
/// corresponding operation, and return the result to the model with
/// <see cref="ChatMessage.ToolResult(string, string)"/>.
/// </summary>
public sealed class ToolCall
{
    /// <summary>The provider-assigned identifier correlating this call with its result message.</summary>
    public required string Id { get; init; }

    /// <summary>The name of the tool the model wants to invoke.</summary>
    public required string Name { get; init; }

    /// <summary>The invocation arguments as a JSON object string (may be <c>"{}"</c>).</summary>
    public required string ArgumentsJson { get; init; }

    /// <summary>
    /// Deserializes <see cref="ArgumentsJson"/> into <typeparamref name="T"/> using
    /// case-insensitive property matching.
    /// </summary>
    /// <typeparam name="T">The argument contract type.</typeparam>
    /// <returns>The bound arguments, or <see langword="null"/> for a JSON <c>null</c> payload.</returns>
    /// <exception cref="AiException">
    /// Thrown with <see cref="AiErrorCode.InvalidResponse"/> when the arguments cannot be
    /// deserialized into <typeparamref name="T"/>.
    /// </exception>
    public T? ParseArguments<T>()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(ArgumentsJson, AiJson.DefaultOptions);
        }
        catch (JsonException ex)
        {
            throw new AiException(
                $"The model's arguments for tool '{Name}' could not be parsed as {typeof(T).Name}.",
                AiErrorCode.InvalidResponse,
                ex)
            {
                ProviderErrorBody = ArgumentsJson,
            };
        }
    }
}

/// <summary>
/// An incremental piece of a tool call emitted while streaming. Deltas with the same
/// <see cref="Index"/> belong to the same tool call; concatenate
/// <see cref="ArgumentsJsonDelta"/> fragments in order to reconstruct the arguments.
/// </summary>
public sealed class ToolCallDelta
{
    /// <summary>The position of the tool call within the response (stable across its deltas).</summary>
    public required int Index { get; init; }

    /// <summary>The tool-call identifier, present on the first delta for a call.</summary>
    public string? Id { get; init; }

    /// <summary>The tool name, present on the first delta for a call.</summary>
    public string? Name { get; init; }

    /// <summary>The next fragment of the arguments JSON, when present.</summary>
    public string? ArgumentsJsonDelta { get; init; }
}
