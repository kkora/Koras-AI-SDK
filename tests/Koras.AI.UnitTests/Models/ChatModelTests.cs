using System.Text.Json;

namespace Koras.AI.UnitTests.Models;

public class ChatModelTests
{
    [Fact]
    public void ChatMessage_factories_set_roles_and_content()
    {
        Assert.Equal(ChatRole.System, ChatMessage.System("s").Role);
        Assert.Equal(ChatRole.User, ChatMessage.User("u").Role);
        Assert.Equal(ChatRole.Assistant, ChatMessage.Assistant("a").Role);

        var toolResult = ChatMessage.ToolResult("call_1", "42");
        Assert.Equal(ChatRole.Tool, toolResult.Role);
        Assert.Equal("call_1", toolResult.ToolCallId);
        Assert.Equal("42", toolResult.Text);
    }

    [Fact]
    public void ChatMessage_factories_reject_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ChatMessage.User(null!));
        Assert.Throws<ArgumentNullException>(() => ChatMessage.ToolResult(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => ChatMessage.ToolResult("id", null!));
    }

    [Fact]
    public void ChatMessage_with_tool_calls_exposes_them()
    {
        var calls = new[] { new ToolCall { Id = "1", Name = "lookup", ArgumentsJson = "{}" } };
        var message = ChatMessage.Assistant(null, calls);

        Assert.Null(message.Text);
        Assert.Single(message.ToolCalls);
        Assert.Contains("1 tool call", message.ToString());
    }

    [Fact]
    public void ChatRequest_FromPrompt_builds_expected_messages()
    {
        var request = ChatRequest.FromPrompt("hello", "be brief");

        Assert.Equal(2, request.Messages.Count);
        Assert.Equal(ChatRole.System, request.Messages[0].Role);
        Assert.Equal("be brief", request.Messages[0].Text);
        Assert.Equal(ChatRole.User, request.Messages[1].Role);

        var withoutSystem = ChatRequest.FromPrompt("hello");
        Assert.Single(withoutSystem.Messages);
    }

    [Fact]
    public void ChatRequest_FromPrompt_rejects_blank_prompt()
        => Assert.Throws<ArgumentException>(() => ChatRequest.FromPrompt("  "));

    [Fact]
    public void TokenUsage_totals_and_addition_work()
    {
        var usage = new TokenUsage(10, 5);
        Assert.Equal(15, usage.TotalTokens);

        var sum = usage + new TokenUsage(1, 2);
        Assert.Equal(new TokenUsage(11, 7), sum);
    }

    [Fact]
    public void ChatRole_and_finish_reason_round_trip_through_json()
    {
        string roleJson = JsonSerializer.Serialize(ChatRole.Assistant);
        Assert.Equal("\"assistant\"", roleJson);
        Assert.Equal(ChatRole.Assistant, JsonSerializer.Deserialize<ChatRole>(roleJson));

        string reasonJson = JsonSerializer.Serialize(new ChatFinishReason("custom_reason"));
        Assert.Equal(new ChatFinishReason("custom_reason"), JsonSerializer.Deserialize<ChatFinishReason>(reasonJson));
    }

    [Fact]
    public void ChatMessage_round_trips_through_json_for_history_persistence()
    {
        var original = ChatMessage.Assistant("text", [new ToolCall { Id = "id1", Name = "tool", ArgumentsJson = """{"a":1}""" }]);

        string json = JsonSerializer.Serialize(original);
        ChatMessage? restored = JsonSerializer.Deserialize<ChatMessage>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Role, restored.Role);
        Assert.Equal(original.Text, restored.Text);
        Assert.Equal("tool", Assert.Single(restored.ToolCalls).Name);
    }

    [Fact]
    public void ChatResponse_Text_delegates_to_message()
    {
        var response = new ChatResponse { Message = ChatMessage.Assistant("hi"), Provider = "fake" };
        Assert.Equal("hi", response.Text);
        Assert.Equal(ChatFinishReason.Unknown, response.FinishReason);
    }

    [Fact]
    public void ToolChoice_tool_carries_name()
    {
        Assert.Equal("lookup", ToolChoice.Tool("lookup").RequiredToolName);
        Assert.Null(ToolChoice.Auto.RequiredToolName);
        Assert.Null(ToolChoice.Required.RequiredToolName);
    }

    [Fact]
    public void EmbeddingRequest_params_constructor_sets_values()
    {
        var request = new EmbeddingRequest("a", "b");
        Assert.Equal(2, request.Values.Count);
    }
}
