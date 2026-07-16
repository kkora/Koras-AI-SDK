using Koras.AI.Anthropic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.AI;

/// <summary>Registers the Anthropic provider with <see cref="KorasAiBuilder"/>.</summary>
public static class AnthropicKorasAiBuilderExtensions
{
    /// <summary>The default client name used by <c>AddAnthropic</c> overloads without an explicit name.</summary>
    public const string DefaultClientName = "anthropic";

    /// <summary>Adds an Anthropic (Claude) chat client named <c>"anthropic"</c>. Anthropic offers no embeddings API.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddAnthropic(this KorasAiBuilder ai, Action<AnthropicOptions> configure)
        => ai.AddAnthropic(DefaultClientName, configure);

    /// <summary>Adds an Anthropic chat client bound from configuration, named <c>"anthropic"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configuration">The configuration section to bind (for example <c>Configuration.GetSection("Koras:AI:Anthropic")</c>).</param>
    public static KorasAiClientBuilder AddAnthropic(this KorasAiBuilder ai, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ai.AddAnthropic(DefaultClientName, configuration.Bind);
    }

    /// <summary>Adds a named Anthropic chat client.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="name">The unique client name.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddAnthropic(this KorasAiBuilder ai, string name, Action<AnthropicOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        ai.Services.AddOptions<AnthropicOptions>(name)
            .Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                $"Koras.AI Anthropic client '{name}': ApiKey is required. Provide it via configuration or user secrets — never source code.")
            .Validate(o => o.Endpoint is not null,
                $"Koras.AI Anthropic client '{name}': Endpoint must not be null.")
            .Validate(o => o.Endpoint is null || o.Endpoint.Scheme == Uri.UriSchemeHttps || o.Endpoint.IsLoopback,
                $"Koras.AI Anthropic client '{name}': Endpoint must use HTTPS (HTTP is allowed only for loopback addresses).")
            .Validate(o => o.DefaultMaxOutputTokens > 0,
                $"Koras.AI Anthropic client '{name}': DefaultMaxOutputTokens must be positive (the Messages API requires max_tokens).")
            .ValidateOnStart();

        ai.Services.AddHttpClient($"Koras.AI.{name}");

        return ai.AddClient(name, sp => new AnthropicChatClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient($"Koras.AI.{name}"),
            sp.GetRequiredService<IOptionsMonitor<AnthropicOptions>>().Get(name)));
    }
}
