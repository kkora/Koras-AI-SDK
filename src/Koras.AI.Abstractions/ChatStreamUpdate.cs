namespace Koras.AI;

/// <summary>
/// One incremental update from <see cref="IChatClient.StreamAsync"/>. Any combination of
/// members may be set; the terminal update carries <see cref="FinishReason"/> and, when the
/// provider reports it, <see cref="Usage"/>.
/// </summary>
public sealed class ChatStreamUpdate
{
    /// <summary>The next fragment of assistant text, when this update carries text.</summary>
    public string? TextDelta { get; init; }

    /// <summary>The next fragment of a tool call, when the model is streaming tool invocations.</summary>
    public ToolCallDelta? ToolCallDelta { get; init; }

    /// <summary>Why generation stopped; set only on the terminal update.</summary>
    public ChatFinishReason? FinishReason { get; init; }

    /// <summary>Token consumption for the whole request, when the provider reports it (typically on the terminal update).</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>The provider's response identifier, when available.</summary>
    public string? ResponseId { get; init; }
}
