using System.Net;
using Koras.AI.Anthropic;
using Koras.AI.AzureOpenAI;
using Koras.AI.Gemini;
using Koras.AI.Ollama;
using Koras.AI.OpenAI;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Providers;

/// <summary>
/// The shared behavioral contract every provider (first-party or custom) must satisfy:
/// normalized errors, standard cancellation, and a stable provider name. Custom provider
/// authors can mirror this suite (see docs/features/custom-providers.md).
/// </summary>
public class ProviderContractTests
{
    public static TheoryData<string> Providers { get; } = new(["openai", "azure_openai", "anthropic", "gemini", "ollama"]);

    private static IChatClient CreateClient(string provider, FakeHttpMessageHandler handler) => provider switch
    {
        "openai" => new OpenAIChatClient(handler.CreateClient(), new OpenAIOptions { ApiKey = "sk-test-x", DefaultModel = "m" }),
        "azure_openai" => new AzureOpenAIChatClient(handler.CreateClient(), new AzureOpenAIOptions
        {
            Endpoint = new Uri("https://r.openai.azure.com"),
            Deployment = "d",
            ApiKey = "test-x",
        }),
        "anthropic" => new AnthropicChatClient(handler.CreateClient(), new AnthropicOptions { ApiKey = "sk-ant-x", DefaultModel = "m" }),
        "gemini" => new GeminiChatClient(handler.CreateClient(), new GeminiOptions { ApiKey = "AIza-x", DefaultModel = "m" }),
        "ollama" => new OllamaChatClient(handler.CreateClient(), new OllamaOptions { DefaultModel = "m" }),
        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public void Provider_name_is_stable_and_lowercase(string provider)
    {
        var client = CreateClient(provider, FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "{}"));
        Assert.Equal(provider, client.ProviderName);
        Assert.Equal(client.ProviderName, client.ProviderName.ToLowerInvariant());
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Http_401_maps_to_Authentication_with_diagnostics(string provider)
    {
        var client = CreateClient(provider, FakeHttpMessageHandler.RespondingWith(
            HttpStatusCode.Unauthorized, """{"error":{"message":"bad key"}}"""));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));

        Assert.Equal(AiErrorCode.Authentication, ex.Code);
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal(provider, ex.Provider);
        Assert.False(ex.IsTransient);
        Assert.NotNull(ex.ProviderErrorBody);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Http_500_maps_to_transient_ProviderUnavailable(string provider)
    {
        var client = CreateClient(provider, FakeHttpMessageHandler.RespondingWith(
            HttpStatusCode.InternalServerError, """{"error":"server exploded"}"""));

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));

        Assert.Equal(AiErrorCode.ProviderUnavailable, ex.Code);
        Assert.True(ex.IsTransient);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Pre_canceled_token_surfaces_as_OperationCanceledException(string provider)
    {
        var handler = new FakeHttpMessageHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            throw new InvalidOperationException("should not get here");
        });
        var client = CreateClient(provider, handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteAsync(ChatRequest.FromPrompt("hi"), cts.Token));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Unparseable_success_payload_maps_to_InvalidResponse(string provider)
    {
        var client = CreateClient(provider, FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "<html>not json</html>", "text/html"));
        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("hi")));
        Assert.Equal(AiErrorCode.InvalidResponse, ex.Code);
    }
}
