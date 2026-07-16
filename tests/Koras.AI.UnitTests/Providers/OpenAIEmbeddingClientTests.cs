using System.Net;
using System.Text.Json;
using Koras.AI.OpenAI;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

public class OpenAIEmbeddingClientTests
{
    private const string EmbeddingFixture = """
        {
          "object": "list",
          "data": [
            { "object": "embedding", "embedding": [0.1, 0.2, 0.3], "index": 0 },
            { "object": "embedding", "embedding": [0.4, 0.5, 0.6], "index": 1 }
          ],
          "model": "text-embedding-3-small",
          "usage": { "prompt_tokens": 8, "total_tokens": 8 }
        }
        """;

    private static OpenAIEmbeddingClient Client(FakeHttpMessageHandler handler)
        => new(handler.CreateClient(), new OpenAIOptions
        {
            ApiKey = "sk-test-not-a-real-key",
            DefaultEmbeddingModel = "text-embedding-3-small",
        });

    [Fact]
    public async Task Maps_batch_request_and_parses_vectors()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, EmbeddingFixture);
        var client = Client(handler);

        var response = await client.GenerateAsync(new EmbeddingRequest("first", "second") { Dimensions = 3 });

        Assert.Equal("https://api.openai.com/v1/embeddings", handler.Requests[0].RequestUri!.ToString());
        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal(2, body.RootElement.GetProperty("input").GetArrayLength());
        Assert.Equal(3, body.RootElement.GetProperty("dimensions").GetInt32());

        Assert.Equal(2, response.Embeddings.Count);
        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, response.Embeddings[0].Vector.ToArray());
        Assert.Equal(1, response.Embeddings[1].Index);
        Assert.Equal(new TokenUsage(8, 0), response.Usage);
        Assert.Equal("text-embedding-3-small", response.Model);
    }

    [Fact]
    public async Task Missing_embedding_model_is_a_configuration_error()
    {
        var client = new OpenAIEmbeddingClient(
            FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, EmbeddingFixture).CreateClient(),
            new OpenAIOptions { ApiKey = "sk-test-not-a-real-key" });

        var ex = await Assert.ThrowsAsync<AiException>(() => client.GenerateAsync(new EmbeddingRequest("x")));
        Assert.Equal(AiErrorCode.Configuration, ex.Code);
    }

    [Fact]
    public async Task Empty_input_is_rejected()
    {
        var client = Client(FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, EmbeddingFixture));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GenerateAsync(new EmbeddingRequest { Values = [] }));
    }
}
