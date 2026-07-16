namespace Koras.AI;

/// <summary>
/// Resolves named clients registered through <c>AddKorasAI</c>. The default (unnamed)
/// <see cref="IChatClient"/> and <see cref="IEmbeddingClient"/> are also registered directly
/// in the container; use the factory when an application configures several named clients.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>The names of all registered chat clients.</summary>
    IReadOnlyList<string> ClientNames { get; }

    /// <summary>Returns the chat client registered under <paramref name="name"/> (with all decorators applied).</summary>
    /// <param name="name">The client name used at registration.</param>
    /// <exception cref="InvalidOperationException">No client is registered under that name.</exception>
    IChatClient GetChatClient(string name);

    /// <summary>Returns the embedding client registered under <paramref name="name"/>.</summary>
    /// <param name="name">The client name used at registration.</param>
    /// <exception cref="InvalidOperationException">No embedding client is registered under that name.</exception>
    IEmbeddingClient GetEmbeddingClient(string name);
}
