using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Koras.AI;

/// <summary>Builds and caches decorated client instances per registered name.</summary>
internal sealed class ChatClientFactory(IServiceProvider serviceProvider, KorasAiRegistry registry) : IChatClientFactory
{
    private readonly ConcurrentDictionary<string, IChatClient> _chatClients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IEmbeddingClient> _embeddingClients = new(StringComparer.Ordinal);

    public IReadOnlyList<string> ClientNames => [.. registry.ChatFactories.Keys];

    public IChatClient GetChatClient(string name)
    {
        Guard.NotNullOrWhiteSpace(name);
        return _chatClients.GetOrAdd(name, BuildChatClient);
    }

    public IEmbeddingClient GetEmbeddingClient(string name)
    {
        Guard.NotNullOrWhiteSpace(name);
        return _embeddingClients.GetOrAdd(name, BuildEmbeddingClient);
    }

    private IChatClient BuildChatClient(string name)
    {
        if (!registry.ChatFactories.TryGetValue(name, out Func<IServiceProvider, IChatClient>? factory))
        {
            throw new InvalidOperationException(
                $"No chat client is registered under the name '{name}'. " +
                $"Registered names: {(registry.ChatFactories.Count == 0 ? "(none)" : string.Join(", ", registry.ChatFactories.Keys))}.");
        }

        IChatClient client = factory(serviceProvider);

        if (registry.PerClientDecorators.TryGetValue(name, out List<Func<IServiceProvider, IChatClient, IChatClient>>? perClient))
        {
            foreach (Func<IServiceProvider, IChatClient, IChatClient> decorator in perClient)
            {
                client = decorator(serviceProvider, client);
            }
        }

        foreach (Func<IServiceProvider, IChatClient, IChatClient> decorator in registry.GlobalDecorators)
        {
            client = decorator(serviceProvider, client);
        }

        TimeProvider timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        KorasAiTelemetryOptions telemetryOptions =
            serviceProvider.GetService<IOptions<KorasAiTelemetryOptions>>()?.Value ?? new KorasAiTelemetryOptions();

        if (serviceProvider.GetService<ILoggerFactory>() is { } loggerFactory)
        {
            client = new LoggingChatClient(client, loggerFactory.CreateLogger("Koras.AI.ChatClient"), telemetryOptions, timeProvider);
        }

        return new TelemetryChatClient(client, name, timeProvider);
    }

    private IEmbeddingClient BuildEmbeddingClient(string name)
    {
        if (!registry.EmbeddingFactories.TryGetValue(name, out Func<IServiceProvider, IEmbeddingClient>? factory))
        {
            throw new InvalidOperationException(
                $"No embedding client is registered under the name '{name}'. " +
                $"Registered names: {(registry.EmbeddingFactories.Count == 0 ? "(none)" : string.Join(", ", registry.EmbeddingFactories.Keys))}.");
        }

        IEmbeddingClient client = factory(serviceProvider);
        TimeProvider timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        return new TelemetryEmbeddingClient(client, name, timeProvider);
    }
}
