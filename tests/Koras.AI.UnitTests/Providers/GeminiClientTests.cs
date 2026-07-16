using System.Net;
using System.Text;
using System.Text.Json;
using Koras.AI.Gemini;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

public class GeminiClientTests
{
    private const string GenerateContentFixture = """
        {
          "candidates": [{
            "content": { "parts": [{ "text": "Hello from Gemini." }], "role": "model" },
            "finishReason": "STOP"
          }],
          "usageMetadata": { "promptTokenCount": 8, "candidatesTokenCount": 4 },
          "modelVersion": "gemini-2.0-flash",
          "responseId": "resp-1"
        }
        """;

    private static GeminiChatClient Client(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), new GeminiOptions
        {
            ApiKey = "AIza-test-not-a-real-key",
            DefaultModel = "gemini-2.0-flash",
        });

    [Fact]
    public async Task Maps_request_with_system_instruction_and_header_auth()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, GenerateContentFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.System("be brief"), ChatMessage.User("hi"), ChatMessage.Assistant("hello"), ChatMessage.User("more")],
            Options = new ChatOptions { Temperature = 0.1, MaxOutputTokens = 50 },
        });

        HttpRequestMessage sent = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
            sent.RequestUri!.ToString());
        Assert.Equal("AIza-test-not-a-real-key", sent.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain("key=", sent.RequestUri.Query); // never in the query string

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement root = body.RootElement;
        Assert.Equal("be brief", root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(3, root.GetProperty("contents").GetArrayLength());
        Assert.Equal("model", root.GetProperty("contents")[1].GetProperty("role").GetString());
        Assert.Equal(50, root.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
    }

    [Fact]
    public async Task Parses_response_with_usage()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, GenerateContentFixture));
        var response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        Assert.Equal("Hello from Gemini.", response.Text);
        Assert.Equal("gemini", response.Provider);
        Assert.Equal(new TokenUsage(8, 4), response.Usage);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal("resp-1", response.ResponseId);
    }

    [Fact]
    public async Task Maps_tools_to_function_declarations_with_cleaned_schemas()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, GenerateContentFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("weather?")],
            Options = new ChatOptions
            {
                Tools = [AiTool.Create("get_weather", "Gets weather", (string city) => city)],
                ToolChoice = ToolChoice.Tool("get_weather"),
            },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement declaration = body.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations")[0];
        Assert.Equal("get_weather", declaration.GetProperty("name").GetString());
        Assert.False(declaration.GetProperty("parameters").TryGetProperty("additionalProperties", out _)); // Gemini dialect

        JsonElement config = body.RootElement.GetProperty("toolConfig").GetProperty("functionCallingConfig");
        Assert.Equal("ANY", config.GetProperty("mode").GetString());
        Assert.Equal("get_weather", config.GetProperty("allowedFunctionNames")[0].GetString());
    }

    [Fact]
    public async Task Parses_function_calls_with_synthesized_ids()
    {
        const string functionCallFixture = """
            {
              "candidates": [{
                "content": { "parts": [{ "functionCall": { "name": "get_weather", "args": {"city": "Oslo"} } }], "role": "model" },
                "finishReason": "STOP"
              }],
              "usageMetadata": { "promptTokenCount": 5, "candidatesTokenCount": 3 }
            }
            """;
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, functionCallFixture));
        var response = await client.CompleteAsync(ChatRequest.FromPrompt("weather?"));

        ToolCall call = Assert.Single(response.Message.ToolCalls);
        Assert.Equal("call_0_get_weather", call.Id);
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
    }

    [Fact]
    public async Task Tool_results_map_back_to_function_responses()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, GenerateContentFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages =
            [
                ChatMessage.User("weather?"),
                ChatMessage.Assistant(null, [new ToolCall { Id = "call_0_get_weather", Name = "get_weather", ArgumentsJson = "{}" }]),
                ChatMessage.ToolResult("call_0_get_weather", "sunny"),
            ],
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement functionResponse = body.RootElement.GetProperty("contents")[2].GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.Equal("get_weather", functionResponse.GetProperty("name").GetString());
        Assert.Equal("sunny", functionResponse.GetProperty("response").GetProperty("result").GetString());
    }

    [Fact]
    public async Task Response_schema_is_applied_for_structured_output()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, GenerateContentFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("extract")],
            Options = new ChatOptions { ResponseFormat = ChatResponseFormat.ForType<int>() },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement config = body.RootElement.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.True(config.TryGetProperty("responseSchema", out _));
    }

    [Fact]
    public async Task Streams_sse_chunks()
    {
        var sse = new StringBuilder()
            .Append("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hel\"}],\"role\":\"model\"}}]}\n\n")
            .Append("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"lo\"}],\"role\":\"model\"},\"finishReason\":\"STOP\"}],\"usageMetadata\":{\"promptTokenCount\":3,\"candidatesTokenCount\":2}}\n\n")
            .ToString();
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, sse, "text/event-stream");
        var client = Client(handler);

        var text = new StringBuilder();
        TokenUsage? usage = null;
        await foreach (ChatStreamUpdate update in client.StreamAsync(ChatRequest.FromPrompt("hi")))
        {
            text.Append(update.TextDelta);
            usage ??= update.Usage;
        }

        Assert.Contains("alt=sse", handler.Requests[0].RequestUri!.Query);
        Assert.Equal("Hello", text.ToString());
        Assert.Equal(new TokenUsage(3, 2), usage);
    }

    [Fact]
    public async Task Safety_block_maps_to_ContentFiltered()
    {
        const string safetyFixture = """
            {
              "candidates": [{
                "content": { "parts": [{ "text": "" }], "role": "model" },
                "finishReason": "SAFETY"
              }]
            }
            """;
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, safetyFixture));
        var response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));
        Assert.Equal(ChatFinishReason.ContentFilter, response.FinishReason);
    }

    [Fact]
    public async Task Api_key_error_maps_to_Authentication()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(
            HttpStatusCode.BadRequest,
            """{"error":{"code":400,"message":"API key not valid. Please pass a valid API key.","status":"INVALID_ARGUMENT"}}"""));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        // 400 with API-key text still maps InvalidRequest by status; the message carries the detail.
        Assert.Equal(AiErrorCode.InvalidRequest, ex.Code);
        Assert.Contains("API key not valid", ex.Message);
    }

    [Fact]
    public async Task Embedding_client_uses_batch_embed_contents()
    {
        const string embedFixture = """{"embeddings":[{"values":[0.5,0.6]},{"values":[0.7,0.8]}]}""";
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, embedFixture);
        var client = new GeminiEmbeddingClient(handler.CreateClient(), new GeminiOptions
        {
            ApiKey = "AIza-test-not-a-real-key",
            DefaultEmbeddingModel = "text-embedding-004",
        });

        var response = await client.GenerateAsync(new EmbeddingRequest("a", "b"));

        Assert.Contains(":batchEmbedContents", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(2, response.Embeddings.Count);
        Assert.Equal(new[] { 0.7f, 0.8f }, response.Embeddings[1].Vector.ToArray());
    }
}
