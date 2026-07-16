using Koras.AI.AzureOpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Koras.AI;

/// <summary>Registers the Azure OpenAI provider with <see cref="KorasAiBuilder"/>.</summary>
public static class AzureOpenAIKorasAiBuilderExtensions
{
    /// <summary>The default client name used by <c>AddAzureOpenAI</c> overloads without an explicit name.</summary>
    public const string DefaultClientName = "azure_openai";

    /// <summary>Adds an Azure OpenAI chat (and, when configured, embedding) client named <c>"azure_openai"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddAzureOpenAI(this KorasAiBuilder ai, Action<AzureOpenAIOptions> configure)
        => ai.AddAzureOpenAI(DefaultClientName, configure);

    /// <summary>Adds an Azure OpenAI client bound from configuration, named <c>"azure_openai"</c>.</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="configuration">The configuration section to bind (for example <c>Configuration.GetSection("Koras:AI:AzureOpenAI")</c>).</param>
    public static KorasAiClientBuilder AddAzureOpenAI(this KorasAiBuilder ai, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ai.AddAzureOpenAI(DefaultClientName, configuration.Bind);
    }

    /// <summary>Adds a named Azure OpenAI client (register several to talk to multiple resources or deployments).</summary>
    /// <param name="ai">The Koras.AI builder.</param>
    /// <param name="name">The unique client name.</param>
    /// <param name="configure">Configures the provider options.</param>
    public static KorasAiClientBuilder AddAzureOpenAI(this KorasAiBuilder ai, string name, Action<AzureOpenAIOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        ai.Services.AddOptions<AzureOpenAIOptions>(name)
            .Configure(configure)
            .Validate(o => o.Endpoint is not null,
                $"Koras.AI Azure OpenAI client '{name}': Endpoint is required (https://<resource>.openai.azure.com).")
            .Validate(o => o.Endpoint is null || o.Endpoint.Scheme == Uri.UriSchemeHttps || o.Endpoint.IsLoopback,
                $"Koras.AI Azure OpenAI client '{name}': Endpoint must use HTTPS.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                $"Koras.AI Azure OpenAI client '{name}': ApiKey is required. Provide it via configuration or Key Vault — never source code.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Deployment) || !string.IsNullOrWhiteSpace(o.EmbeddingDeployment),
                $"Koras.AI Azure OpenAI client '{name}': set Deployment (chat) and/or EmbeddingDeployment (embeddings).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiVersion),
                $"Koras.AI Azure OpenAI client '{name}': ApiVersion must not be empty.")
            .ValidateOnStart();

        ai.Services.AddHttpClient(OpenAIKorasAiBuilderExtensions.HttpClientName(name));

        ai.AddEmbeddingClient(name, sp => new AzureOpenAIEmbeddingClient(
            OpenAIKorasAiBuilderExtensions.CreateHttpClient(sp, name),
            GetOptions(sp, name)));

        return ai.AddClient(name, sp =>
        {
            AzureOpenAIOptions options = GetOptions(sp, name);
            if (string.IsNullOrWhiteSpace(options.Deployment))
            {
                throw new AiException(
                    $"Koras.AI Azure OpenAI client '{name}': Deployment is required for chat operations.",
                    AiErrorCode.Configuration)
                { Provider = "azure_openai" };
            }

            return new AzureOpenAIChatClient(OpenAIKorasAiBuilderExtensions.CreateHttpClient(sp, name), options);
        });
    }

    private static AzureOpenAIOptions GetOptions(IServiceProvider serviceProvider, string name)
        => serviceProvider.GetRequiredService<IOptionsMonitor<AzureOpenAIOptions>>().Get(name);
}
