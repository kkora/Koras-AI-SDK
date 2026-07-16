using Koras.AI.Gemini;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.AI;

/// <summary>Registers the Google Gemini provider with <see cref="KorasAiBuilder"/>.</summary>
public static class GeminiKorasAiBuilderExtensions
{
    /// <summary>The default client name used by <c>AddGemini</c> overloads without an explicit name.</summary>
    public const string DefaultClientName = "gemini";

    /// <summary>Adds a Gemini chat + embedding client named <c>"gemini"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddGemini(this KorasAiBuilder ai, Action<GeminiOptions> configure)
        => ai.AddGemini(DefaultClientName, configure);

    /// <summary>Adds a Gemini client bound from configuration, named <c>"gemini"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configuration">The configuration section to bind (for example <c>Configuration.GetSection("Koras:AI:Gemini")</c>).</param>
    public static KorasAiClientBuilder AddGemini(this KorasAiBuilder ai, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ai.AddGemini(DefaultClientName, configuration.Bind);
    }

    /// <summary>Adds a named Gemini chat + embedding client.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="name">The unique client name.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddGemini(this KorasAiBuilder ai, string name, Action<GeminiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        ai.Services.AddOptions<GeminiOptions>(name)
            .Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                $"Koras.AI Gemini client '{name}': ApiKey is required. Provide it via configuration or user secrets — never source code.")
            .Validate(o => o.Endpoint is not null,
                $"Koras.AI Gemini client '{name}': Endpoint must not be null.")
            .Validate(o => o.Endpoint is null || o.Endpoint.Scheme == Uri.UriSchemeHttps || o.Endpoint.IsLoopback,
                $"Koras.AI Gemini client '{name}': Endpoint must use HTTPS (HTTP is allowed only for loopback addresses).")
            .ValidateOnStart();

        ai.Services.AddHttpClient($"Koras.AI.{name}");

        ai.AddEmbeddingClient(name, sp => new GeminiEmbeddingClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient($"Koras.AI.{name}"),
            sp.GetRequiredService<IOptionsMonitor<GeminiOptions>>().Get(name)));

        return ai.AddClient(name, sp => new GeminiChatClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient($"Koras.AI.{name}"),
            sp.GetRequiredService<IOptionsMonitor<GeminiOptions>>().Get(name)));
    }
}
