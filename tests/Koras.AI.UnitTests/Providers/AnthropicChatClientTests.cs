using System.Net;
using System.Text;
using System.Text.Json;
using Koras.AI.Anthropic;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

public class AnthropicChatClientTests
{
    private const string MessageFixture = """
        {
          "id": "msg_01",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-5",
          "content": [{ "type": "text", "text": "Hello from Claude." }],
          "stop_reason": "end_turn",
          "usage": { "input_tokens": 15, "output_tokens": 6 }
        }
        """;

    private static AnthropicChatClient Client(FakeHttpMessageHandler handler, int defaultMaxTokens = 4096)
        => new(handler.CreateClient(), new AnthropicOptions
        {
            ApiKey = "sk-ant-test-not-a-real-key",
            DefaultModel = "claude-sonnet-4-5",
            DefaultMaxOutputTokens = defaultMaxTokens,
        });

    [Fact]
    public async Task Maps_request_with_hoisted_system_prompt_and_required_max_tokens()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, MessageFixture);
        var client = Client(handler, defaultMaxTokens: 1234);

        await client.CompleteAsync(new ChatRequest
        {
            Messages =
            [
                ChatMessage.System("be brief"),
                ChatMessage.System("be kind"),
                ChatMessage.User("hi"),
            ],
        });

        HttpRequestMessage sent = Assert.Single(handler.Requests);
        Assert.Equal("https://api.anthropic.com/v1/messages", sent.RequestUri!.ToString());
        Assert.Equal("sk-ant-test-not-a-real-key", sent.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", sent.Headers.GetValues("anthropic-version").Single());

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement root = body.RootElement;
        Assert.Equal("be brief\n\nbe kind", root.GetProperty("system").GetString());
        Assert.Equal(1234, root.GetProperty("max_tokens").GetInt32());
        Assert.Single(root.GetProperty("messages").EnumerateArray()); // system messages hoisted out
    }

    [Fact]
    public async Task Parses_text_response_with_usage()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, MessageFixture));
        var response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        Assert.Equal("Hello from Claude.", response.Text);
        Assert.Equal("anthropic", response.Provider);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(new TokenUsage(15, 6), response.Usage);
        Assert.Equal("msg_01", response.ResponseId);
    }

    [Fact]
    public async Task Maps_tools_with_input_schema_and_parses_tool_use_blocks()
    {
        const string toolFixture = """
            {
              "id": "msg_02",
              "model": "claude-sonnet-4-5",
              "content": [
                { "type": "text", "text": "Let me check." },
                { "type": "tool_use", "id": "toolu_1", "name": "get_weather", "input": {"city": "Oslo"} }
              ],
              "stop_reason": "tool_use",
              "usage": { "input_tokens": 30, "output_tokens": 12 }
            }
            """;
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, toolFixture);
        var client = Client(handler);

        var response = await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("weather?")],
            Options = new ChatOptions
            {
                Tools = [AiTool.Create("get_weather", "Gets weather", (string city) => city)],
                ToolChoice = ToolChoice.Required,
            },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement tool = body.RootElement.GetProperty("tools")[0];
        Assert.True(tool.TryGetProperty("input_schema", out _));
        Assert.Equal("any", body.RootElement.GetProperty("tool_choice").GetProperty("type").GetString());

        ToolCall call = Assert.Single(response.Message.ToolCalls);
        Assert.Equal("toolu_1", call.Id);
        Assert.Equal("""{"city": "Oslo"}""", call.ArgumentsJson);
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Equal("Let me check.", response.Text);
    }

    [Fact]
    public async Task Tool_results_merge_into_a_single_user_turn()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, MessageFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages =
            [
                ChatMessage.User("weather in two cities?"),
                ChatMessage.Assistant(null, [
                    new ToolCall { Id = "t1", Name = "w", ArgumentsJson = """{"city":"Oslo"}""" },
                    new ToolCall { Id = "t2", Name = "w", ArgumentsJson = """{"city":"Bergen"}""" },
                ]),
                ChatMessage.ToolResult("t1", "sunny"),
                ChatMessage.ToolResult("t2", "rainy"),
            ],
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement messages = body.RootElement.GetProperty("messages");
        Assert.Equal(3, messages.GetArrayLength());
        JsonElement toolResultTurn = messages[2];
        Assert.Equal("user", toolResultTurn.GetProperty("role").GetString());
        Assert.Equal(2, toolResultTurn.GetProperty("content").GetArrayLength());
    }

    [Fact]
    public async Task Structured_output_forces_the_synthetic_tool_and_surfaces_its_input_as_text()
    {
        const string structuredFixture = """
            {
              "id": "msg_03",
              "model": "claude-sonnet-4-5",
              "content": [{ "type": "tool_use", "id": "toolu_2", "name": "record_output", "input": {"number":"INV-1","total":5} }],
              "stop_reason": "tool_use",
              "usage": { "input_tokens": 10, "output_tokens": 9 }
            }
            """;
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, structuredFixture);
        var client = Client(handler);

        var response = await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("extract")],
            Options = new ChatOptions { ResponseFormat = ChatResponseFormat.JsonSchema("invoice", AiJsonSchema.FromType<int>()) },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal("record_output", body.RootElement.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.Equal("tool", body.RootElement.GetProperty("tool_choice").GetProperty("type").GetString());

        Assert.Empty(response.Message.ToolCalls);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Contains("INV-1", response.Text);
    }

    [Fact]
    public async Task Streams_anthropic_event_grammar()
    {
        var sse = new StringBuilder()
            .Append("event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"id\":\"msg_s\",\"usage\":{\"input_tokens\":25}}}\n\n")
            .Append("event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n")
            .Append("event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hi \"}}\n\n")
            .Append("event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"there\"}}\n\n")
            .Append("event: message_delta\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":7}}\n\n")
            .Append("event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n")
            .ToString();
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, sse, "text/event-stream"));

        var text = new StringBuilder();
        TokenUsage? usage = null;
        ChatFinishReason? finish = null;
        await foreach (ChatStreamUpdate update in client.StreamAsync(ChatRequest.FromPrompt("hi")))
        {
            text.Append(update.TextDelta);
            usage ??= update.Usage;
            finish ??= update.FinishReason;
        }

        Assert.Equal("Hi there", text.ToString());
        Assert.Equal(new TokenUsage(25, 7), usage);
        Assert.Equal(ChatFinishReason.Stop, finish);
    }

    [Fact]
    public async Task Stream_error_events_throw_normalized_exceptions()
    {
        const string sse = "event: error\ndata: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n";
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, sse, "text/event-stream"));

        var ex = await Assert.ThrowsAsync<AiException>(async () =>
        {
            await foreach (ChatStreamUpdate _ in client.StreamAsync(ChatRequest.FromPrompt("hi")))
            {
            }
        });

        Assert.Equal(AiErrorCode.ProviderUnavailable, ex.Code);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task Http_529_maps_to_ProviderUnavailable()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(
            (HttpStatusCode)529,
            """{"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}"""));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        Assert.Equal(AiErrorCode.ProviderUnavailable, ex.Code);
        Assert.Contains("Overloaded", ex.Message);
    }
}
