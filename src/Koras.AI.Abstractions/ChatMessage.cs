using System.Text.Json.Serialization;

namespace Koras.AI;

/// <summary>
/// A single message in a chat conversation. Create instances through the static factory
/// methods (<see cref="System"/>, <see cref="User(string)"/>, <see cref="Assistant(string)"/>,
/// <see cref="ToolResult"/>); instances are immutable and safe to share.
/// </summary>
public sealed class ChatMessage
{
    private static readonly IReadOnlyList<ToolCall> EmptyToolCalls = [];

    /// <summary>Initializes a message with a role and optional text.</summary>
    /// <param name="role">The author role.</param>
    /// <param name="text">The message text, or <see langword="null"/> for tool-call-only messages.</param>
    public ChatMessage(ChatRole role, string? text)
        : this(role, text, toolCalls: null, toolCallId: null)
    {
    }

    /// <summary>Initializes a message with all components. Used for deserializing persisted history.</summary>
    /// <param name="role">The author role.</param>
    /// <param name="text">The message text, if any.</param>
    /// <param name="toolCalls">Tool calls requested by an assistant message, if any.</param>
    /// <param name="toolCallId">For tool-result messages, the id of the call being answered.</param>
    [JsonConstructor]
    public ChatMessage(ChatRole role, string? text, IReadOnlyList<ToolCall>? toolCalls, string? toolCallId)
    {
        Role = role;
        Text = text;
        ToolCalls = toolCalls ?? EmptyToolCalls;
        ToolCallId = toolCallId;
    }

    /// <summary>The author role of this message.</summary>
    public ChatRole Role { get; }

    /// <summary>The textual content, or <see langword="null"/> when the message carries only tool calls.</summary>
    public string? Text { get; }

    /// <summary>Tool invocations requested by the model (assistant messages only; empty otherwise).</summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; }

    /// <summary>For <see cref="ChatRole.Tool"/> messages, the id of the <see cref="ToolCall"/> this result answers.</summary>
    public string? ToolCallId { get; }

    /// <summary>Creates a system (instruction) message.</summary>
    /// <param name="text">The instruction text.</param>
    public static ChatMessage System(string text)
        => new(ChatRole.System, Guard.NotNull(text));

    /// <summary>Creates a user message.</summary>
    /// <param name="text">The user's input text.</param>
    public static ChatMessage User(string text)
        => new(ChatRole.User, Guard.NotNull(text));

    /// <summary>Creates an assistant (model output) message, e.g. when replaying history.</summary>
    /// <param name="text">The assistant's text.</param>
    public static ChatMessage Assistant(string text)
        => new(ChatRole.Assistant, Guard.NotNull(text));

    /// <summary>Creates an assistant message that carries tool calls (and optionally text).</summary>
    /// <param name="text">The assistant's text, if any.</param>
    /// <param name="toolCalls">The tool calls requested by the model.</param>
    public static ChatMessage Assistant(string? text, IReadOnlyList<ToolCall> toolCalls)
        => new(ChatRole.Assistant, text, Guard.NotNull(toolCalls), toolCallId: null);

    /// <summary>Creates a tool-result message answering a specific <see cref="ToolCall"/>.</summary>
    /// <param name="toolCallId">The <see cref="ToolCall.Id"/> being answered.</param>
    /// <param name="content">The tool's result, serialized as text (commonly JSON).</param>
    public static ChatMessage ToolResult(string toolCallId, string content)
        => new(ChatRole.Tool, Guard.NotNull(content), toolCalls: null, toolCallId: Guard.NotNull(toolCallId));

    /// <inheritdoc />
    public override string ToString() => $"{Role}: {Text ?? $"[{ToolCalls.Count} tool call(s)]"}";
}
