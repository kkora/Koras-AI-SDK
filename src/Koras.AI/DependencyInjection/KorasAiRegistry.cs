namespace Koras.AI;

/// <summary>Mutable registration state shared between <see cref="KorasAiBuilder"/> instances and the factory.</summary>
internal sealed class KorasAiRegistry
{
    public Dictionary<string, Func<IServiceProvider, IChatClient>> ChatFactories { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Func<IServiceProvider, IEmbeddingClient>> EmbeddingFactories { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<Func<IServiceProvider, IChatClient, IChatClient>>> PerClientDecorators { get; } = new(StringComparer.Ordinal);

    public List<Func<IServiceProvider, IChatClient, IChatClient>> GlobalDecorators { get; } = [];

    public string? DefaultChatClientName { get; set; }

    public string? DefaultEmbeddingClientName { get; set; }

    public string? FirstChatClientName { get; set; }

    public string? FirstEmbeddingClientName { get; set; }

    public string ResolveDefaultChatClientName()
        => DefaultChatClientName
            ?? FirstChatClientName
            ?? throw new InvalidOperationException(
                "No chat clients are registered. Register a provider inside AddKorasAI, e.g. services.AddKorasAI(ai => ai.AddOpenAI(...)).");

    public string ResolveDefaultEmbeddingClientName()
        => DefaultEmbeddingClientName
            ?? FirstEmbeddingClientName
            ?? throw new InvalidOperationException(
                "No embedding clients are registered. Register a provider that supports embeddings inside AddKorasAI.");
}
