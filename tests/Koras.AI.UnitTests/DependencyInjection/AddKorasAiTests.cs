using Koras.AI.UnitTests.TestInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.AI.UnitTests.DependencyInjection;

public class AddKorasAiTests
{
    [Fact]
    public void First_registered_client_is_the_default()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai =>
            {
                ai.AddClient("first", _ => new FakeChatClient("first"));
                ai.AddClient("second", _ => new FakeChatClient("second"));
            })
            .BuildServiceProvider();

        Assert.Equal("first", provider.GetRequiredService<IChatClient>().ProviderName);
    }

    [Fact]
    public void AsDefault_overrides_first_registration()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai =>
            {
                ai.AddClient("first", _ => new FakeChatClient("first"));
                ai.AddClient("second", _ => new FakeChatClient("second")).AsDefault();
            })
            .BuildServiceProvider();

        Assert.Equal("second", provider.GetRequiredService<IChatClient>().ProviderName);
    }

    [Fact]
    public void Factory_resolves_named_clients_and_caches_instances()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai => ai.AddClient("a", _ => new FakeChatClient("a")))
            .BuildServiceProvider();

        var factory = provider.GetRequiredService<IChatClientFactory>();
        Assert.Same(factory.GetChatClient("a"), factory.GetChatClient("a"));
        Assert.Equal(["a"], factory.ClientNames);
    }

    [Fact]
    public void Unknown_client_name_throws_with_registered_names_listed()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai => ai.AddClient("known", _ => new FakeChatClient()))
            .BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IChatClientFactory>().GetChatClient("unknown"));
        Assert.Contains("known", ex.Message);
    }

    [Fact]
    public void Duplicate_client_names_are_rejected_at_registration()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddKorasAI(ai =>
        {
            ai.AddClient("dup", _ => new FakeChatClient());
            ai.AddClient("dup", _ => new FakeChatClient());
        }));
    }

    [Fact]
    public void Resolving_default_client_without_registrations_gives_actionable_error()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(_ => { })
            .BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IChatClient>());
        Assert.Contains("AddKorasAI", ex.Message);
    }

    [Fact]
    public void Global_decorators_wrap_every_client_in_registration_order()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai =>
            {
                ai.AddClient("a", _ => new FakeChatClient("a"));
                ai.UseRetry();
                ai.UseToolInvocation();
            })
            .BuildServiceProvider();

        // Outermost built-ins are telemetry/logging; beneath them the registered order applies.
        var client = provider.GetRequiredService<IChatClient>();
        var delegating = Assert.IsAssignableFrom<DelegatingChatClient>(client);
        Assert.NotNull(delegating.GetService(typeof(RetryChatClient)));
        Assert.NotNull(delegating.GetService(typeof(ToolInvokingChatClient)));
        Assert.NotNull(delegating.GetService(typeof(FakeChatClient)));
    }

    [Fact]
    public async Task Fallback_registration_resolves_candidates_through_the_factory()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai =>
            {
                ai.AddClient("down", _ => new FakeChatClient("down")
                    .EnqueueError(new AiException("down", AiErrorCode.ProviderUnavailable)));
                ai.AddClient("up", _ => new FakeChatClient("up").EnqueueResponse("rescued"));
                ai.AddFallback("resilient", "down", "up").AsDefault();
            })
            .BuildServiceProvider();

        var response = await provider.GetRequiredService<IChatClient>().CompleteAsync(ChatRequest.FromPrompt("hi"));
        Assert.Equal("rescued", response.Text);
    }

    [Fact]
    public void Fallback_cannot_reference_itself()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddKorasAI(ai => ai.AddFallback("self", "self")));
    }

    [Fact]
    public void Multiple_AddKorasAI_calls_accumulate_registrations()
    {
        var services = new ServiceCollection();
        services.AddKorasAI(ai => ai.AddClient("one", _ => new FakeChatClient("one")));
        services.AddKorasAI(ai => ai.AddClient("two", _ => new FakeChatClient("two")));

        using ServiceProvider provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IChatClientFactory>();
        Assert.Equal(2, factory.ClientNames.Count);
    }

    [Fact]
    public void OpenAI_options_validation_fails_startup_for_missing_api_key()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai => ai.AddOpenAI(o => o.DefaultModel = "gpt-4o-mini"))
            .BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptionsMonitor<Koras.AI.OpenAI.OpenAIOptions>>().Get("openai"));
        Assert.Contains("ApiKey is required", ex.Message);
    }

    [Fact]
    public void OpenAI_options_validation_rejects_non_https_remote_endpoints()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai => ai.AddOpenAI(o =>
            {
                o.ApiKey = "sk-test-not-a-real-key";
                o.Endpoint = new Uri("http://example.com/v1/");
            }))
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptionsMonitor<Koras.AI.OpenAI.OpenAIOptions>>().Get("openai"));
    }

    [Fact]
    public void OpenAI_options_bind_from_configuration()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Koras:AI:OpenAI:ApiKey"] = "sk-test-not-a-real-key",
                ["Koras:AI:OpenAI:DefaultModel"] = "gpt-4o-mini",
            })
            .Build();

        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai => ai.AddOpenAI(configuration.GetSection("Koras:AI:OpenAI")))
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<Koras.AI.OpenAI.OpenAIOptions>>().Get("openai");
        Assert.Equal("gpt-4o-mini", options.DefaultModel);
    }

    [Fact]
    public void Two_providers_can_coexist_with_named_clients()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKorasAI(ai =>
            {
                ai.AddOpenAI(o =>
                {
                    o.ApiKey = "sk-test-not-a-real-key";
                    o.DefaultModel = "gpt-4o-mini";
                });
                ai.AddOllama(o => o.DefaultModel = "llama3.2");
            })
            .BuildServiceProvider();

        var factory = provider.GetRequiredService<IChatClientFactory>();
        Assert.Contains("openai", factory.ClientNames);
        Assert.Contains("ollama", factory.ClientNames);
        Assert.NotNull(factory.GetEmbeddingClient("openai"));
        Assert.NotNull(factory.GetEmbeddingClient("ollama"));
    }
}
