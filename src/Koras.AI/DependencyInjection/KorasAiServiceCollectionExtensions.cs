using Koras.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Koras.AI services in an <see cref="IServiceCollection"/>.</summary>
public static class KorasAiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Koras.AI and configures its clients. Registers the default
    /// <see cref="IChatClient"/> and <see cref="IEmbeddingClient"/> (singletons) plus
    /// <see cref="IChatClientFactory"/> for named-client lookup. Safe to call multiple times;
    /// registrations accumulate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures providers and cross-cutting behavior.</param>
    /// <example>
    /// <code>
    /// builder.Services.AddKorasAI(ai =>
    /// {
    ///     ai.AddOpenAI(o =>
    ///     {
    ///         o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
    ///         o.DefaultModel = "gpt-4o-mini";
    ///     });
    ///     ai.UseRetry();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKorasAI(this IServiceCollection services, Action<KorasAiBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        KorasAiRegistry registry = GetOrAddRegistry(services);

        services.AddOptions();
        services.TryAddSingleton<IChatClientFactory>(sp => new ChatClientFactory(sp, registry));
        services.TryAddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<IChatClientFactory>().GetChatClient(registry.ResolveDefaultChatClientName()));
        services.TryAddSingleton<IEmbeddingClient>(sp =>
            sp.GetRequiredService<IChatClientFactory>().GetEmbeddingClient(registry.ResolveDefaultEmbeddingClientName()));

        configure(new KorasAiBuilder(services, registry));
        return services;
    }

    private static KorasAiRegistry GetOrAddRegistry(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(KorasAiRegistry)
                && descriptor.ImplementationInstance is KorasAiRegistry existing)
            {
                return existing;
            }
        }

        var registry = new KorasAiRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
