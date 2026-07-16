using Koras.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.AI;

/// <summary>Registers the OpenAI provider with <see cref="KorasAiBuilder"/>.</summary>
public static class OpenAIKorasAiBuilderExtensions
{
    /// <summary>The default client name used by <c>AddOpenAI</c> overloads without an explicit name.</summary>
    public const string DefaultClientName = "openai";

    /// <summary>Adds an OpenAI chat + embedding client named <c>"openai"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddOpenAI(this KorasAiBuilder ai, Action<OpenAIOptions> configure)
        => ai.AddOpenAI(DefaultClientName, configure);

    /// <summary>Adds an OpenAI chat + embedding client bound from configuration, named <c>"openai"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configuration">The configuration section to bind (for example <c>Configuration.GetSection("Koras:AI:OpenAI")</c>).</param>
    public static KorasAiClientBuilder AddOpenAI(this KorasAiBuilder ai, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ai.AddOpenAI(DefaultClientName, configuration.Bind);
    }

    /// <summary>Adds a named OpenAI chat + embedding client (register several to talk to multiple accounts or gateways).</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="name">The unique client name.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddOpenAI(this KorasAiBuilder ai, string name, Action<OpenAIOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        ai.Services.AddOptions<OpenAIOptions>(name)
            .Configure(configure)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                $"Koras.AI OpenAI client '{name}': ApiKey is required. Provide it via configuration or user secrets — never source code.")
            .Validate(o => o.Endpoint is not null,
                $"Koras.AI OpenAI client '{name}': Endpoint must not be null.")
            .Validate(o => o.Endpoint is null || o.Endpoint.Scheme == Uri.UriSchemeHttps || o.Endpoint.IsLoopback,
                $"Koras.AI OpenAI client '{name}': Endpoint must use HTTPS (HTTP is allowed only for loopback addresses).")
            .ValidateOnStart();

        ai.Services.AddHttpClient(HttpClientName(name));

        ai.AddEmbeddingClient(name, sp => new OpenAIEmbeddingClient(
            CreateHttpClient(sp, name),
            GetOptions(sp, name)));

        return ai.AddClient(name, sp => new OpenAIChatClient(
            CreateHttpClient(sp, name),
            GetOptions(sp, name)));
    }

    internal static string HttpClientName(string clientName) => $"Koras.AI.{clientName}";

    internal static HttpClient CreateHttpClient(IServiceProvider serviceProvider, string clientName)
        => serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName(clientName));

    private static OpenAIOptions GetOptions(IServiceProvider serviceProvider, string name)
        => serviceProvider.GetRequiredService<IOptionsMonitor<OpenAIOptions>>().Get(name);
}
