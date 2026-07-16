using System.Net;
using System.Text;
using System.Text.Json;
using Koras.AI.Ollama;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

public class OllamaClientTests
{
    private const string ChatFixture = """
        {
          "model": "llama3.2",
          "message": { "role": "assistant", "content": "Hello from llama." },
          "done": true,
          "done_reason": "stop",
          "prompt_eval_count": 9,
          "eval_count": 5
        }
        """;

    private static OllamaChatClient Client(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), new OllamaOptions { DefaultModel = "llama3.2" });

    [Fact]
    public async Task Maps_request_options_to_ollama_native_names()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("hi")],
            Options = new ChatOptions { Temperature = 0.5, MaxOutputTokens = 64, ResponseFormat = ChatResponseFormat.Json },
        });

        Assert.Equal("http://localhost:11434/api/chat", handler.Requests[0].RequestUri!.ToString());
        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        JsonElement root = body.RootElement;
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(0.5, root.GetProperty("options").GetProperty("temperature").GetDouble());
        Assert.Equal(64, root.GetProperty("options").GetProperty("num_predict").GetInt32());
        Assert.Equal("json", root.GetProperty("format").GetString());
    }

    [Fact]
    public async Task Json_schema_format_passes_the_schema_object()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatFixture);
        var client = Client(handler);

        await client.CompleteAsync(new ChatRequest
        {
            Messages = [ChatMessage.User("extract")],
            Options = new ChatOptions { ResponseFormat = ChatResponseFormat.ForType<int>() },
        });

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("format").ValueKind);
    }

    [Fact]
    public async Task Parses_response_with_usage_counts()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatFixture));
        var response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        Assert.Equal("Hello from llama.", response.Text);
        Assert.Equal("ollama", response.Provider);
        Assert.Equal(new TokenUsage(9, 5), response.Usage);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
    }

    [Fact]
    public async Task Synthesizes_tool_call_ids()
    {
        const string toolFixture = """
            {
              "model": "llama3.2",
              "message": {
                "role": "assistant",
                "content": "",
                "tool_calls": [{ "function": { "name": "get_weather", "arguments": {"city": "Oslo"} } }]
              },
              "done": true,
              "done_reason": "stop"
            }
            """;
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, toolFixture));
        var response = await client.CompleteAsync(ChatRequest.FromPrompt("weather?"));

        ToolCall call = Assert.Single(response.Message.ToolCalls);
        Assert.Equal("call_0_get_weather", call.Id);
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.Contains("Oslo", call.ArgumentsJson);
    }

    [Fact]
    public async Task Streams_json_lines_with_final_usage()
    {
        var jsonl = new StringBuilder()
            .AppendLine("""{"model":"llama3.2","message":{"role":"assistant","content":"Hel"},"done":false}""")
            .AppendLine("""{"model":"llama3.2","message":{"role":"assistant","content":"lo"},"done":false}""")
            .AppendLine("""{"model":"llama3.2","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":4,"eval_count":2}""")
            .ToString();
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, jsonl, "application/x-ndjson"));

        var text = new StringBuilder();
        TokenUsage? usage = null;
        await foreach (ChatStreamUpdate update in client.StreamAsync(ChatRequest.FromPrompt("hi")))
        {
            text.Append(update.TextDelta);
            usage ??= update.Usage;
        }

        Assert.Equal("Hello", text.ToString());
        Assert.Equal(new TokenUsage(4, 2), usage);
    }

    [Fact]
    public async Task Connection_refused_gets_the_is_ollama_running_hint()
    {
        var client = Client(FakeHttpMessageHandler.Throwing(
            new HttpRequestException("Connection refused", null, statusCode: null)));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        Assert.Equal(AiErrorCode.Network, ex.Code);
        Assert.Contains("Is Ollama running?", ex.Message);
    }

    [Fact]
    public async Task Embedding_client_maps_batch_embed()
    {
        const string embedFixture = """
            {
              "model": "nomic-embed-text",
              "embeddings": [[0.1, 0.2], [0.3, 0.4]],
              "prompt_eval_count": 6
            }
            """;
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, embedFixture);
        var client = new OllamaEmbeddingClient(handler.CreateClient(), new OllamaOptions { DefaultEmbeddingModel = "nomic-embed-text" });

        var response = await client.GenerateAsync(new EmbeddingRequest("a", "b"));

        Assert.Equal("http://localhost:11434/api/embed", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(2, response.Embeddings.Count);
        Assert.Equal(new[] { 0.1f, 0.2f }, response.Embeddings[0].Vector.ToArray());
        Assert.Equal(new TokenUsage(6, 0), response.Usage);
    }

    [Fact]
    public async Task Embedding_client_rejects_empty_input()
    {
        var client = new OllamaEmbeddingClient(
            FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "{}").CreateClient(),
            new OllamaOptions { DefaultEmbeddingModel = "m" });

        await Assert.ThrowsAsync<ArgumentException>(() => client.GenerateAsync(new EmbeddingRequest { Values = [] }));
    }
}
