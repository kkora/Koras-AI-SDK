namespace Koras.AI;

/// <summary>
/// Base class for chat client decorators, mirroring <see cref="System.Net.Http.DelegatingHandler"/>.
/// Override the members you need and delegate the rest; built-in cross-cutting features
/// (retry, fallback, telemetry, tool invocation) are implemented this way.
/// </summary>
/// <param name="innerClient">The client this decorator wraps.</param>
public abstract class DelegatingChatClient(IChatClient innerClient) : IChatClient
{
    /// <summary>The wrapped client.</summary>
    protected IChatClient InnerClient { get; } = Guard.NotNull(innerClient);

    /// <inheritdoc />
    public virtual string ProviderName => InnerClient.ProviderName;

    /// <inheritdoc />
    public virtual Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => InnerClient.CompleteAsync(request, cancellationToken);

    /// <inheritdoc />
    public virtual IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => InnerClient.StreamAsync(request, cancellationToken);

    /// <summary>
    /// Walks the decorator chain and returns the first client assignable to
    /// <paramref name="serviceType"/>, or <see langword="null"/>. Lets integrations discover
    /// optional capabilities (for example <see cref="IProviderHealthProbe"/>) through any
    /// decorator stack.
    /// </summary>
    /// <param name="serviceType">The capability or client type to find.</param>
    public virtual object? GetService(Type serviceType)
    {
        Guard.NotNull(serviceType);
        if (serviceType.IsInstanceOfType(this))
        {
            return this;
        }

        if (InnerClient is DelegatingChatClient delegating)
        {
            return delegating.GetService(serviceType);
        }

        return serviceType.IsInstanceOfType(InnerClient) ? InnerClient : null;
    }
}
