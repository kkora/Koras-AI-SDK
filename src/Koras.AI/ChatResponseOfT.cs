namespace Koras.AI;

/// <summary>A structured-output chat result: the deserialized value plus the underlying response.</summary>
/// <typeparam name="T">The structured output contract type.</typeparam>
public sealed class ChatResponse<T>
{
    /// <summary>The model's output deserialized into <typeparamref name="T"/>.</summary>
    public required T Value { get; init; }

    /// <summary>The underlying provider response (usage, finish reason, raw payload).</summary>
    public required ChatResponse Raw { get; init; }
}
