using System.Net;
using System.Text.Json;
using Koras.AI.AzureOpenAI;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

public class AzureOpenAIClientTests
{
    private const string ChatCompletionFixture = """
        {
          "id": "chatcmpl-az",
          "model": "gpt-4o-mini",
          "choices": [{
            "index": 0,
            "message": { "role": "assistant", "content": "Hello from Azure." },
            "finish_reason": "stop"
          }],
          "usage": { "prompt_tokens": 7, "completion_tokens": 3 }
        }
        """;

    private static AzureOpenAIOptions Options() => new()
    {
        Endpoint = new Uri("https://my-resource.openai.azure.com"),
        Deployment = "gpt4o-mini-deploy",
        EmbeddingDeployment = "embed-deploy",
        ApiKey = "azure-test-not-a-real-key",
    };

    [Fact]
    public async Task Targets_the_deployment_url_with_api_key_header()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture);
        var client = new AzureOpenAIChatClient(handler.CreateClient(), Options());

        var response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        HttpRequestMessage sent = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://my-resource.openai.azure.com/openai/deployments/gpt4o-mini-deploy/chat/completions?api-version=2024-10-21",
            sent.RequestUri!.ToString());
        Assert.Equal("azure-test-not-a-real-key", sent.Headers.GetValues("api-key").Single());
        Assert.Null(sent.Headers.Authorization); // no Bearer header on Azure

        Assert.Equal("azure_openai", response.Provider);
        Assert.Equal("Hello from Azure.", response.Text);
    }

    [Fact]
    public async Task Uses_deployment_as_model_when_request_has_none()
    {
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture);
        var client = new AzureOpenAIChatClient(handler.CreateClient(), Options());

        await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        using JsonDocument body = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.Equal("gpt4o-mini-deploy", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task Custom_api_version_is_respected()
    {
        var options = Options();
        options.ApiVersion = "2025-01-01-preview";
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, ChatCompletionFixture);
        var client = new AzureOpenAIChatClient(handler.CreateClient(), options);

        await client.CompleteAsync(ChatRequest.FromPrompt("hi"));
        Assert.Contains("api-version=2025-01-01-preview", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task Embedding_client_targets_the_embedding_deployment()
    {
        const string embedFixture = """{"data":[{"embedding":[0.1,0.2],"index":0}],"model":"text-embedding-3-small","usage":{"prompt_tokens":2}}""";
        var handler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, embedFixture);
        var client = new AzureOpenAIEmbeddingClient(handler.CreateClient(), Options());

        var response = await client.GenerateAsync(new EmbeddingRequest("hello"));

        Assert.StartsWith(
            "https://my-resource.openai.azure.com/openai/deployments/embed-deploy/embeddings",
            handler.Requests[0].RequestUri!.ToString());
        Assert.Single(response.Embeddings);
        Assert.Equal("azure_openai", response.Provider);
    }

    [Fact]
    public async Task Azure_error_shape_maps_through_the_taxonomy()
    {
        var client = new AzureOpenAIChatClient(
            FakeHttpMessageHandler.RespondingWith(
                HttpStatusCode.NotFound,
                """{"error":{"code":"DeploymentNotFound","message":"The API deployment for this resource does not exist."}}""").CreateClient(),
            Options());

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        Assert.Equal(AiErrorCode.ModelNotFound, ex.Code);
        Assert.Contains("deployment", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
