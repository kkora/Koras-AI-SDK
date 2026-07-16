using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Koras.AI;

/// <summary>
/// Configures Koras.AI clients inside <c>services.AddKorasAI(ai =&gt; ...)</c>. Provider
/// packages add extension methods (for example <c>AddOpenAI</c>) that register named clients;
/// cross-cutting behavior is attached with the <c>Use*</c> methods and applies to every
/// registered chat client.
/// </summary>
public sealed class KorasAiBuilder
{
    private readonly KorasAiRegistry _registry;

    internal KorasAiBuilder(IServiceCollection services, KorasAiRegistry registry)
    {
        Services = services;
        _registry = registry;
    }

    /// <summary>The underlying service collection, for provider packages that need additional registrations.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers a named chat client. The first registered client becomes the default unless
    /// <see cref="KorasAiClientBuilder.AsDefault"/> selects another. Used directly for custom
    /// providers; provider packages call this internally.
    /// </summary>
    /// <param name="name">The unique client name.</param>
    /// <param name="factory">Creates the client instance (invoked once; the result is cached).</param>
    public KorasAiClientBuilder AddClient(string name, Func<IServiceProvider, IChatClient> factory)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(factory);

        if (!_registry.ChatFactories.TryAdd(name, factory))
        {
            throw new InvalidOperationException(
                $"A chat client named '{name}' is already registered. Client names must be unique; " +
                "pass an explicit name to register the same provider twice.");
        }

        _registry.FirstChatClientName ??= name;
        return new KorasAiClientBuilder(this, _registry, name);
    }

    /// <summary>Registers a named embedding client.</summary>
    /// <param name="name">The unique client name (conventionally the same as the chat client it accompanies).</param>
    /// <param name="factory">Creates the client instance (invoked once; the result is cached).</param>
    public KorasAiClientBuilder AddEmbeddingClient(string name, Func<IServiceProvider, IEmbeddingClient> factory)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(factory);

        if (!_registry.EmbeddingFactories.TryAdd(name, factory))
        {
            throw new InvalidOperationException($"An embedding client named '{name}' is already registered.");
        }

        _registry.FirstEmbeddingClientName ??= name;
        return new KorasAiClientBuilder(this, _registry, name);
    }

    /// <summary>
    /// Registers a named client that fails over across previously registered clients on
    /// transient failures (see <see cref="FallbackChatClient"/>).
    /// </summary>
    /// <param name="name">The unique name for the fallback client.</param>
    /// <param name="clientNames">The candidate client names in preference order.</param>
    public KorasAiClientBuilder AddFallback(string name, params string[] clientNames)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNull(clientNames);
        if (clientNames.Length == 0)
        {
            throw new ArgumentException("At least one candidate client name is required.", nameof(clientNames));
        }

        if (clientNames.Contains(name, StringComparer.Ordinal))
        {
            throw new ArgumentException("A fallback client cannot list itself as a candidate.", nameof(clientNames));
        }

        string[] candidates = [.. clientNames];
        return AddClient(name, sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            IReadOnlyList<IChatClient> clients = [.. candidates.Select(factory.GetChatClient)];
            return new FallbackChatClient(clients, shouldFailover: null, sp.GetService<ILogger<FallbackChatClient>>());
        });
    }

    /// <summary>Adds a decorator applied to every registered chat client (applied in registration order, innermost first).</summary>
    /// <param name="decorator">Receives the service provider and the client to wrap; returns the wrapped client.</param>
    public KorasAiBuilder Use(Func<IServiceProvider, IChatClient, IChatClient> decorator)
    {
        _registry.GlobalDecorators.Add(Guard.NotNull(decorator));
        return this;
    }

    /// <summary>Adds transient-failure retry to every chat client (see <see cref="RetryOptions"/> for defaults).</summary>
    /// <param name="configure">Optionally adjusts the retry behavior.</param>
    public KorasAiBuilder UseRetry(Action<RetryOptions>? configure = null)
    {
        var options = new RetryOptions();
        configure?.Invoke(options);
        return Use((sp, inner) => new RetryChatClient(
            inner,
            options,
            sp.GetService<TimeProvider>(),
            sp.GetService<ILogger<RetryChatClient>>()));
    }

    /// <summary>Adds the automatic tool-invocation loop to every chat client (see <see cref="ToolInvokingChatClient"/>).</summary>
    /// <param name="configure">Optionally adjusts loop behavior.</param>
    public KorasAiBuilder UseToolInvocation(Action<ToolInvocationOptions>? configure = null)
    {
        var options = new ToolInvocationOptions();
        configure?.Invoke(options);
        return Use((_, inner) => new ToolInvokingChatClient(inner, options));
    }

    /// <summary>Configures logging/tracing behavior (for example sensitive-data capture).</summary>
    /// <param name="configure">The configuration action.</param>
    public KorasAiBuilder ConfigureTelemetry(Action<KorasAiTelemetryOptions> configure)
    {
        Services.Configure(Guard.NotNull(configure));
        return this;
    }
}

/// <summary>Continues configuration of a single named client registration.</summary>
public sealed class KorasAiClientBuilder
{
    private readonly KorasAiRegistry _registry;

    internal KorasAiClientBuilder(KorasAiBuilder builder, KorasAiRegistry registry, string name)
    {
        Builder = builder;
        _registry = registry;
        Name = name;
    }

    /// <summary>The parent builder, for fluent chaining.</summary>
    public KorasAiBuilder Builder { get; }

    /// <summary>The client name this builder configures.</summary>
    public string Name { get; }

    /// <summary>
    /// Makes this client the default <see cref="IChatClient"/> (and
    /// <see cref="IEmbeddingClient"/>, when one is registered under the same name) resolved
    /// from the container.
    /// </summary>
    public KorasAiClientBuilder AsDefault()
    {
        if (_registry.ChatFactories.ContainsKey(Name))
        {
            _registry.DefaultChatClientName = Name;
        }

        if (_registry.EmbeddingFactories.ContainsKey(Name))
        {
            _registry.DefaultEmbeddingClientName = Name;
        }

        return this;
    }

    /// <summary>Adds a decorator applied only to this client (before any global decorators).</summary>
    /// <param name="decorator">Receives the service provider and the client to wrap; returns the wrapped client.</param>
    public KorasAiClientBuilder Use(Func<IServiceProvider, IChatClient, IChatClient> decorator)
    {
        Guard.NotNull(decorator);
        if (!_registry.PerClientDecorators.TryGetValue(Name, out List<Func<IServiceProvider, IChatClient, IChatClient>>? decorators))
        {
            decorators = [];
            _registry.PerClientDecorators[Name] = decorators;
        }

        decorators.Add(decorator);
        return this;
    }
}
