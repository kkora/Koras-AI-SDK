using Koras.AI.Ollama;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.AI;

/// <summary>Registers the Ollama provider with <see cref="KorasAiBuilder"/>.</summary>
public static class OllamaKorasAiBuilderExtensions
{
    /// <summary>The default client name used by <c>AddOllama</c> overloads without an explicit name.</summary>
    public const string DefaultClientName = "ollama";

    /// <summary>Adds an Ollama chat + embedding client named <c>"ollama"</c> (defaults to <c>http://localhost:11434</c>).</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddOllama(this KorasAiBuilder ai, Action<OllamaOptions> configure)
        => ai.AddOllama(DefaultClientName, configure);

    /// <summary>Adds an Ollama client bound from configuration, named <c>"ollama"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configuration">The configuration section to bind (for example <c>Configuration.GetSection("Koras:AI:Ollama")</c>).</param>
    public static KorasAiClientBuilder AddOllama(this KorasAiBuilder ai, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ai.AddOllama(DefaultClientName, configuration.Bind);
    }

    /// <summary>Adds a named Ollama chat + embedding client.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="name">The unique client name.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddOllama(this KorasAiBuilder ai, string name, Action<OllamaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        ai.Services.AddOptions<OllamaOptions>(name)
            .Configure(configure)
            .Validate(o => o.Endpoint is not null,
                $"Koras.AI Ollama client '{name}': Endpoint must not be null.")
            .ValidateOnStart();

        ai.Services.AddHttpClient($"Koras.AI.{name}");

        ai.AddEmbeddingClient(name, sp => new OllamaEmbeddingClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient($"Koras.AI.{name}"),
            sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().Get(name)));

        return ai.AddClient(name, sp => new OllamaChatClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient($"Koras.AI.{name}"),
            sp.GetRequiredService<IOptionsMonitor<OllamaOptions>>().Get(name)));
    }
}
