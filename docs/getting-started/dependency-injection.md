# Dependency Injection Basics

Everything starts with one call: `services.AddKorasAI(ai => ...)`. Inside the callback the
`KorasAiBuilder` registers providers and cross-cutting behavior. This page covers the shapes
you will use daily; the [advanced DI guide](../guides/dependency-injection.md) covers custom
decorators.

## The default client

Registering a single provider gives you an injectable default `IChatClient` (and
`IEmbeddingClient`, for providers that support embeddings):

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
});
```

```csharp
public sealed class SummaryService(IChatClient chat)
{
    public async Task<string?> SummarizeAsync(string text, CancellationToken ct)
        => (await chat.CompleteAsync($"Summarize:\n{text}", ct)).Text;
}
```

Both interfaces are registered as **singletons** — all clients are thread-safe
([thread safety](../concepts/thread-safety.md)).

## Named clients and IChatClientFactory

Every registration has a name (`AddOpenAI` defaults to `"openai"`, `AddOllama` to
`"ollama"`, and so on). Register several and resolve by name through `IChatClientFactory`:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o => { o.ApiKey = key; o.DefaultModel = "gpt-4o-mini"; });
    ai.AddOllama(o => o.DefaultModel = "llama3.2");
});
```

```csharp
public sealed class RoutingService(IChatClientFactory clients)
{
    public Task<ChatResponse> AskLocalAsync(string prompt, CancellationToken ct)
        => clients.GetChatClient("ollama").CompleteAsync(prompt, ct);
}
```

`IChatClientFactory.ClientNames` lists all registered names — handy for diagnostics
endpoints. Registering the same provider twice requires explicit names:
`ai.AddOpenAI("openai-eu", o => ...)`.

## Choosing the default: AsDefault

The **first registered client is the default** unless another calls `.AsDefault()`:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o => o.DefaultModel = "llama3.2");        // default so far

    if (!string.IsNullOrEmpty(openAiKey))
    {
        ai.AddOpenAI(o => { o.ApiKey = openAiKey; o.DefaultModel = "gpt-4o-mini"; })
          .AsDefault();                                    // now OpenAI is the default
    }
});
```

This pattern (from `samples/Console.Sample`) gives you keyless local development that
upgrades itself when a key appears.

## Fallback across providers

`AddFallback` registers a named client that tries candidates in order, failing over on
transient errors (`AiException.IsTransient`):

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
    ai.AddAnthropic(builder.Configuration.GetSection("Koras:AI:Anthropic"));
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    ai.AddFallback("resilient", "openai", "anthropic", "ollama").AsDefault();
});
```

Terminal errors (bad credentials, invalid request) propagate immediately; only transient
failures move to the next candidate. If every candidate fails, the last `AiException` is
rethrown with an `AggregateException` of all attempts as its inner exception
([error handling](../concepts/error-handling.md)).

## Global decorators

`Use*` methods on the builder wrap **every** registered chat client:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o => { o.ApiKey = key; o.DefaultModel = "gpt-4o-mini"; });

    ai.UseRetry(r =>
    {
        r.MaxAttempts = 4;                          // total attempts, incl. the first
        r.AttemptTimeout = TimeSpan.FromSeconds(60);
    });

    ai.UseToolInvocation(t => t.MaxIterations = 8); // auto-execute AiTool.Create handlers

    ai.Use((sp, inner) => new MyAuditingClient(inner)); // your own decorator
});
```

- `UseRetry` — retries transient failures with exponential backoff and jitter, honoring
  `Retry-After` hints.
- `UseToolInvocation` — runs the model↔tool round-trip loop automatically for tools created
  with `AiTool.Create`.
- `Use` — any `Func<IServiceProvider, IChatClient, IChatClient>`; typically returns a
  subclass of `DelegatingChatClient`.

Decorators apply in registration order, innermost first; logging and telemetry are always
added outermost by the SDK. The resulting pipeline is described in
[architecture](../concepts/architecture.md). Per-client decorators exist too — see the
[advanced DI guide](../guides/dependency-injection.md).

## Next steps

- [Configuration](configuration.md) — options binding and startup validation.
- [Advanced DI guide](../guides/dependency-injection.md) — per-client decorators, custom
  `DelegatingChatClient`, testing overrides.
