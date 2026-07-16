using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Koras.AI.IntegrationTests;

/// <summary>
/// Full-stack tests: real DI container, real HttpClientFactory, real Kestrel server speaking
/// each provider's wire format — covering the composed pipeline (retry, fallback, telemetry,
/// health checks) end to end.
/// </summary>
public class EndToEndTests : IAsyncLifetime
{
    private FakeProviderServer _server = null!;

    public async Task InitializeAsync() => _server = await FakeProviderServer.StartAsync();

    public async Task DisposeAsync() => await _server.DisposeAsync();

    private ServiceProvider BuildServices(Action<KorasAiBuilder>? extra = null, string apiKey = "sk-test-integration")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKorasAI(ai =>
        {
            ai.AddOpenAI(o =>
            {
                o.ApiKey = apiKey;
                o.DefaultModel = "gpt-test";
                o.Endpoint = new Uri(_server.BaseAddress, "/v1/");
            });
            ai.AddOllama(o =>
            {
                o.DefaultModel = "llama-test";
                o.Endpoint = _server.BaseAddress;
            });
            extra?.Invoke(ai);
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Chat_completion_works_through_the_full_stack()
    {
        await using ServiceProvider provider = BuildServices();
        var client = provider.GetRequiredService<IChatClient>();

        ChatResponse response = await client.CompleteAsync("hello?");

        Assert.Equal("integration says hello", response.Text);
        Assert.Equal("openai", response.Provider);
        Assert.Equal(new TokenUsage(3, 4), response.Usage);
    }

    [Fact]
    public async Task Streaming_aggregates_to_the_full_text_over_real_sse()
    {
        await using ServiceProvider provider = BuildServices();
        var client = provider.GetRequiredService<IChatClient>();

        var text = new StringBuilder();
        TokenUsage? usage = null;
        await foreach (ChatStreamUpdate update in client.StreamAsync(ChatRequest.FromPrompt("hi")))
        {
            text.Append(update.TextDelta);
            usage ??= update.Usage;
        }

        Assert.Equal("integration", text.ToString());
        Assert.Equal(new TokenUsage(3, 2), usage);
    }

    [Fact]
    public async Task Retry_recovers_from_a_transient_429_with_retry_after()
    {
        await using ServiceProvider provider = BuildServices(ai => ai.UseRetry(r => r.MaxAttempts = 3));
        var client = provider.GetRequiredService<IChatClient>();

        _server.FailOpenAIOnce = true;
        ChatResponse response = await client.CompleteAsync("hello?");

        Assert.Equal("integration says hello", response.Text);
        Assert.Equal(2, _server.OpenAIChatRequests);
    }

    [Fact]
    public async Task Fallback_fails_over_from_a_broken_provider_to_ollama()
    {
        await using ServiceProvider provider = BuildServices(
            ai => ai.AddFallback("resilient", "openai", "ollama").AsDefault(),
            apiKey: "sk-test-integration"); // auth ok, but we break the call another way

        _server.FailOpenAIOnce = true; // 429 → transient → failover
        var client = provider.GetRequiredService<IChatClient>();

        ChatResponse response = await client.CompleteAsync("hello?");

        Assert.Equal("ollama fallback answer", response.Text);
        Assert.Equal("ollama", response.Provider);
    }

    [Fact]
    public async Task Authentication_failures_surface_with_the_normalized_code()
    {
        await using ServiceProvider provider = BuildServices(apiKey: "sk-test-wrong");
        var client = provider.GetRequiredService<IChatClient>();

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync("hello?"));

        Assert.Equal(AiErrorCode.Authentication, ex.Code);
        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("Incorrect API key", ex.Message);
    }

    [Fact]
    public async Task Ollama_streaming_works_over_real_json_lines()
    {
        await using ServiceProvider provider = BuildServices();
        var client = provider.GetRequiredService<IChatClientFactory>().GetChatClient("ollama");

        var text = new StringBuilder();
        await foreach (ChatStreamUpdate update in client.StreamAsync(ChatRequest.FromPrompt("hi")))
        {
            text.Append(update.TextDelta);
        }

        Assert.Equal("ollama", text.ToString());
    }

    [Fact]
    public async Task Health_checks_probe_the_real_endpoints()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKorasAI(ai =>
        {
            ai.AddOpenAI(o =>
            {
                o.ApiKey = "sk-test-integration";
                o.DefaultModel = "gpt-test";
                o.Endpoint = new Uri(_server.BaseAddress, "/v1/");
            });
            ai.AddOllama(o => o.Endpoint = _server.BaseAddress);
        });
        services.AddHealthChecks()
            .AddKorasAI("openai")
            .AddKorasAI("ollama");

        await using ServiceProvider provider = services.BuildServiceProvider();
        HealthReport report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal(2, report.Entries.Count);
    }

    [Fact]
    public async Task Cancellation_mid_stream_releases_promptly()
    {
        await using ServiceProvider provider = BuildServices();
        var client = provider.GetRequiredService<IChatClient>();

        using var cts = new CancellationTokenSource();
        var received = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (ChatStreamUpdate _ in client.StreamAsync(ChatRequest.FromPrompt("hi"), cts.Token))
            {
                received++;
                await cts.CancelAsync();
            }
        });

        Assert.Equal(1, received);
    }
}
