namespace Koras.AI;

/// <summary>
/// A chat completion request: the conversation so far plus optional model selection and
/// generation options. Instances are immutable and safe to reuse and share.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>The conversation messages, oldest first.</summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>The model to use, or <see langword="null"/> to use the client's configured default model.</summary>
    public string? Model { get; init; }

    /// <summary>Generation parameters, or <see langword="null"/> for provider defaults.</summary>
    public ChatOptions? Options { get; init; }

    /// <summary>Creates a single-turn request from a user prompt with an optional system instruction.</summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="systemPrompt">An optional system instruction prepended to the conversation.</param>
    public static ChatRequest FromPrompt(string prompt, string? systemPrompt = null)
    {
        Guard.NotNullOrWhiteSpace(prompt);
        var messages = new List<ChatMessage>(2);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(ChatMessage.System(systemPrompt));
        }

        messages.Add(ChatMessage.User(prompt));
        return new ChatRequest { Messages = messages };
    }
}
