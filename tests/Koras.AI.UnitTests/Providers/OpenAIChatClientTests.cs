using System.Net;
using System.Text;
using System.Text.Json;
using Koras.AI.OpenAI;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

public class OpenAIChatClientTests
{
    private const string ChatCompletionFixture = """
        {
          "id": "chatcmpl-123",
          "object": "chat.completion",
          "model": "gpt-4o-mini-2024-07-18",
          "choices": [{
            "index": 0,
            "message": { "role": "assistant", "content": "Hello there!" },
            "finish_reason": "stop"
          }],
          "usage": { "prompt_tokens": 12, "completion_tokens": 4, "total_tokens": 16 }
        }
        """;

    private static OpenAIOptions Options() => new()
    {
        ApiKey = "sk-test-not-a-real-key",
        DefaultModel = "gpt-4o-mini",
        Organization = "org-test",
    };

    private static OpenAIChatClient Client(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), Options());

    [Fact]
    public async Task Maps_request_to_the_openai_wire_format()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture);
        var client = Client(handler);

        var request = new ChatRequest
        {
            Messages =
            [
                ChatMessage.System("be brief"),
                ChatMessage.User("hi"),
            ],
            Options = new ChatOptions
            {
                Temperature = 0.2,
                MaxOutputTokens = 100,
                StopSequences = ["END"],
                AdditionalProperties = new Dictionary<string, object?> { ["seed"] = 42 },
            },
        };

        await client.CompleteAsync(request);

        HttpRequestMessage sent = Assert.Single(handler.Requests);
        Assert.Equal("https://api.openai.com/v1/chat/completions", sent.RequestUri!.ToString());
        Assert.Equal("Bearer", sent.Headers.Authorization!.Scheme);
        Assert.Equal("org-test", sent.Headers.GetValues("OpenAI-Organization").Single());

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement root = body.RootElement;
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal(0.2, root.GetProperty("temperature").GetDouble());
        Assert.Equal(100, root.GetProperty("max_completion_tokens").GetInt32());
        Assert.Equal("END", root.GetProperty("stop")[0].GetString());
        Assert.Equal(42, root.GetProperty("seed").GetInt32());
        Assert.False(root.TryGetProperty("stream", out _));
    }

    [Fact]
    public async Task Parses_the_completion_response()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture));

        ChatResponse response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        Assert.Equal("Hello there!", response.Text);
        Assert.Equal("openai", response.Provider);
        Assert.Equal("gpt-4o-mini-2024-07-18", response.Model);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(new TokenUsage(12, 4), response.Usage);
        Assert.Equal("chatcmpl-123", response.ResponseId);
        Assert.Equal(JsonValueKind.Object, response.RawRepresentation.ValueKind);
    }

    [Fact]
    public async Task Maps_tools_and_parses_tool_calls()
    {
        const string toolCallFixture = """
            {
              "id": "chatcmpl-tc",
              "model": "gpt-4o-mini",
              "choices": [{
                "index": 0,
                "message": {
                  "role": "assistant",
                  "content": null,
                  "tool_calls": [{
                    "id": "call_abc",
                    "type": "function",
                    "function": { "name": "get_weather", "arguments": "{\"city\":\"Oslo\"}" }
                  }]
                },
                "finish_reason": "tool_calls"
              }],
              "usage": { "prompt_tokens": 20, "completion_tokens": 10 }
            }
            """;
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, toolCallFixture);
        var client = Client(handler);

        var tool = AiTool.Create("get_weather", "Gets weather", (string city) => city);
        var response = await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("weather in Oslo?")],
            Options = new ChatOptions { Tools = [tool], ToolChoice = ToolChoice.Auto },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement toolNode = body.RootElement.GetProperty("tools")[0];
        Assert.Equal("function", toolNode.GetProperty("type").GetString());
        Assert.Equal("get_weather", toolNode.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("auto", body.RootElement.GetProperty("tool_choice").GetString());

        ToolCall call = Assert.Single(response.Message.ToolCalls);
        Assert.Equal("call_abc", call.Id);
        Assert.Equal("get_weather", call.Name);
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
    }

    [Fact]
    public async Task Maps_forced_tool_choice_and_json_schema_response_format()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("extract")],
            Options = new ChatOptions
            {
                Tools = [AiTool.Create("t", null, (string a) => a)],
                ToolChoice = ToolChoice.Tool("t"),
                ResponseFormat = ChatResponseFormat.JsonSchema("out", AiJsonSchema.FromType<int>()),
            },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal("t", body.RootElement.GetProperty("tool_choice").GetProperty("function").GetProperty("name").GetString());
        JsonElement format = body.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.Equal("out", format.GetProperty("json_schema").GetProperty("name").GetString());
        Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task Streams_text_deltas_finish_reason_and_usage()
    {
        var sse = new StringBuilder()
            .Append("data: {\"id\":\"c1\",\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n")
            .Append("data: {\"id\":\"c1\",\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n")
            .Append("data: {\"id\":\"c1\",\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n")
            .Append("data: {\"id\":\"c1\",\"choices\":[],\"usage\":{\"prompt_tokens\":5,\"completion_tokens\":2}}\n\n")
            .Append("data: [DONE]\n\n")
            .ToString();
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, sse, "text/event-stream");
        var client = Client(handler);

        var text = new StringBuilder();
        ChatFinishReason? finish = null;
        TokenUsage? usage = null;
        await foreach (ChatStreamUpdate update in client.StreamAsync(ChatRequest.FromPrompt("hi")))
        {
            text.Append(update.TextDelta);
            finish ??= update.FinishReason;
            usage ??= update.Usage;
        }

        Assert.Equal("Hello", text.ToString());
        Assert.Equal(ChatFinishReason.Stop, finish);
        Assert.Equal(new TokenUsage(5, 2), usage);

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.True(body.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean());
    }

    [Fact]
    public async Task Maps_http_errors_to_the_taxonomy_with_provider_message()
    {
        const string errorBody = """{"error":{"message":"Incorrect API key provided.","type":"invalid_request_error","code":"invalid_api_key"}}""";
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized, errorBody));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));

        Assert.Equal(AiErrorCode.Authentication, ex.Code);
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("openai", ex.Provider);
        Assert.Contains("Incorrect API key", ex.Message);
        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task Rate_limit_carries_retry_after_and_is_transient()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(
            HttpStatusCode.TooManyRequests,
            """{"error":{"message":"Rate limit reached.","code":"rate_limit_exceeded"}}""",
            customize: r => r.Headers.TryAddWithoutValidation("Retry-After", "21"));
        var client = Client(handler);

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));

        Assert.Equal(AiErrorCode.RateLimited, ex.Code);
        Assert.True(ex.IsTransient);
        Assert.Equal(TimeSpan.FromSeconds(21), ex.RetryAfter);
    }

    [Fact]
    public async Task Exhausted_quota_is_rate_limited_but_not_transient()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(
            HttpStatusCode.TooManyRequests,
            """{"error":{"message":"You exceeded your current quota.","code":"insufficient_quota"}}"""));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));

        Assert.Equal(AiErrorCode.RateLimited, ex.Code);
        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task Network_failures_surface_as_Network_errors()
    {
        var client = Client(FakeHttpMessageHandler.Throwing(new HttpRequestException("connection refused")));
        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        Assert.Equal(AiErrorCode.Network, ex.Code);
        Assert.True(ex.IsTransient);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_as_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("unreachable");
        });
        var client = Client(handler);

        Task task = client.CompleteAsync(ChatRequest.FromPrompt("hi"), cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Missing_model_configuration_is_reported_clearly()
    {
        var client = new OpenAIChatClient(
            FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture).CreateClient(),
            new OpenAIOptions { ApiKey = "sk-test-not-a-real-key" });

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        Assert.Equal(AiErrorCode.Configuration, ex.Code);
        Assert.Contains("DefaultModel", ex.Message);
    }

    [Fact]
    public async Task Endpoint_without_trailing_slash_still_targets_the_full_path()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture);
        var client = new OpenAIChatClient(handler.CreateClient(), new OpenAIOptions
        {
            ApiKey = "sk-test-not-a-real-key",
            DefaultModel = "gpt-4o-mini",
            Endpoint = new Uri("https://gateway.example.com/openai/v1"),
        });

        await client.CompleteAsync(ChatRequest.FromPrompt("hi"));
        Assert.Equal("https://gateway.example.com/openai/v1/chat/completions", handler.Requests[0].RequestUri!.ToString());
    }
}
