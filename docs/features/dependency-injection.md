# Dependency Injection

## Overview

`services.AddKorasAI(ai => ...)` is the single entry point. Inside the callback,
`KorasAiBuilder` registers named clients (via provider extensions like `AddOpenAI`, or
`AddClient` for custom providers) and attaches cross-cutting behavior (`UseRetry`,
`UseToolInvocation`, `Use`, `ConfigureTelemetry`, `AddFallback`). `AddKorasAI` registers:

- the default `IChatClient` and `IEmbeddingClient` (singletons, resolved through the factory),
- `IChatClientFactory` (singleton) for named-client lookup,
- provider options with `ValidateOnStart` so misconfiguration fails at startup.

## When to use it

Always — DI registration is the supported way to construct clients. It is safe to call
`AddKorasAI` multiple times; registrations accumulate into the same registry.

## Required packages

- `Koras.AI` plus provider packages. The entry point lives in namespace
  `Microsoft.Extensions.DependencyInjection` for discoverability.

## Basic usage

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
    ai.UseRetry();
});
```

Consume the defaults anywhere:

```csharp
public sealed class AskService(IChatClient chat, IEmbeddingClient embeddings) { /* ... */ }
```

## Named clients and defaults

Every registration has a name (provider default names: `"openai"`, `"azure_openai"`,
`"anthropic"`, `"gemini"`, `"ollama"`). The first registered chat client is the default unless
another calls `.AsDefault()`:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI("fast", o => { o.ApiKey = key; o.DefaultModel = "gpt-4o-mini"; });
    ai.AddOpenAI("smart", o => { o.ApiKey = key; o.DefaultModel = "gpt-4o"; }).AsDefault();
});

public sealed class Router(IChatClientFactory factory)
{
    public IChatClient Pick(bool cheap) => factory.GetChatClient(cheap ? "fast" : "smart");
}
```

`IChatClientFactory` exposes `GetChatClient(name)`, `GetEmbeddingClient(name)`, and
`ClientNames`. Names must be unique; duplicate registration throws
`InvalidOperationException`. Instances are built once per name and cached.

## Configuration binding

Every provider has an `IConfiguration` overload that binds and validates. The conventional
section is `Koras:AI:<Provider>`:

```csharp
ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
```

```json
{
  "Koras": { "AI": { "OpenAI": { "DefaultModel": "gpt-4o-mini" } } }
}
```

Keep `ApiKey` out of appsettings — supply it via user secrets
(`dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "..."`) or environment variables
(`Koras__AI__OpenAI__ApiKey`). Validation runs at startup (`ValidateOnStart`).

## Decorators

- `ai.Use((sp, inner) => ...)` — global, wraps every chat client, applied in registration
  order (innermost first).
- `clientBuilder.Use((sp, inner) => ...)` — per client, applied before global decorators.
- Built-ins: `UseRetry`, `UseToolInvocation`. Logging and telemetry decorators are added
  automatically by the factory.

Write custom decorators by deriving from `DelegatingChatClient` and overriding
`CompleteAsync`/`StreamAsync`.

## ASP.NET Core usage

Registration in `Program.cs` as above; add [health checks](health-checks.md) with
`builder.Services.AddHealthChecks().AddKorasAI()` and OTel wiring per [telemetry](telemetry.md).

## Error handling

- Startup: invalid options (missing `ApiKey`, non-HTTPS endpoint) fail host start via
  `ValidateOnStart` with actionable messages.
- Resolution: unknown client names throw `InvalidOperationException` listing registered names.
- Call time: remaining misconfiguration (for example no model resolved) throws `AiException`
  with `AiErrorCode.Configuration`. See [error handling](error-handling.md).

## Cancellation

DI plays no role in cancellation; tokens are passed per call.

## HTTP client plumbing

Each provider registration adds a named `HttpClient` (`"Koras.AI.{clientName}"`) through
`IHttpClientFactory` — configure proxies, custom handlers, or `Timeout` there:

```csharp
builder.Services.AddHttpClient("Koras.AI.openai", http => http.Timeout = TimeSpan.FromSeconds(60));
```

## Security considerations

Never hardcode keys in `configure` callbacks; read them from configuration backed by a secret
source. Options validation messages remind you of this — they never echo key values.

## Thread safety

Everything registered is singleton-safe: clients, the factory, decorators. The factory caches
built clients in a `ConcurrentDictionary`.

## Testing applications using this feature

Build a real container with fakes registered as clients:

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddKorasAI(ai =>
{
    ai.AddClient("fake", _ => new FakeChatClient()).AsDefault();
    ai.UseRetry(r => r.MaxAttempts = 1);
});

using ServiceProvider sp = services.BuildServiceProvider();
IChatClient chat = sp.GetRequiredService<IChatClient>();
```

This exercises the same decorator pipeline (including automatic logging/telemetry wrappers)
as production.

## Common mistakes

- Registering two providers and assuming the second is the default — the first wins unless
  `.AsDefault()` is called.
- Resolving `IEmbeddingClient` when no registered provider supports embeddings (for example
  Anthropic-only setups).
- Registering the same provider twice without explicit names.
- Newing up provider clients manually and losing retry, logging, and telemetry decorators.

## Related features

- [Chat completion](chat-completion.md)
- [Custom providers](custom-providers.md)
- [Resilience](resilience.md)
- [Health checks](health-checks.md)
