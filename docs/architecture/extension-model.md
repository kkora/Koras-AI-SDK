# Extension Model

Koras.AI has three sanctioned extension points. Everything else is composition of these.

## 1. Custom providers (the primary extension point)

Implement `IChatClient` and/or `IEmbeddingClient`. For HTTP providers, derive from the
published plumbing in `Koras.AI.Providers`:

```csharp
public sealed class AcmeChatClient : ProviderChatClient
{
    public AcmeChatClient(HttpClient http, AcmeOptions options)
        : base(http, providerName: "acme") { ... }

    protected override HttpRequestMessage CreateChatHttpRequest(ChatRequest request) { ... }
    protected override ChatResponse ParseChatResponse(JsonElement json) { ... }
    // Streaming: use SseReader/JsonLinesReader from Koras.AI.Providers
}
```

Register through the builder so decorators, telemetry, and the factory apply:

```csharp
services.AddKorasAI(ai => ai.AddClient("acme", sp => new AcmeChatClient(...)));
```

**Contract:** the shared provider contract-test suite (`Koras.AI.Testing` fixtures in the test
tree) documents the behavioral requirements — error normalization, cancellation, usage
reporting. First-party and custom providers pass the same suite.

## 2. Decorators (cross-cutting behavior)

Derive from `DelegatingChatClient` (mirrors `DelegatingHandler`):

```csharp
public sealed class AuditChatClient(IChatClient inner, IAuditSink sink) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        var response = await base.CompleteAsync(request, ct).ConfigureAwait(false);
        await sink.RecordAsync(response.Usage, ct).ConfigureAwait(false);
        return response;
    }
}
```

Attach globally (`ai.Use(inner => new AuditChatClient(inner, sink))`) or per named client.
Built-in retry, fallback, tool-invocation, logging, and telemetry are all implemented as
decorators — the extension point is the architecture, not an afterthought.

## 3. Options escape hatches (provider-specific features)

- `ChatOptions.AdditionalProperties` — provider-specific request fields serialized into the
  wire request (documented per provider). Keeps the abstraction honest: unsupported features
  are reachable without forking.
- `ChatResponse.RawRepresentation` — the provider's raw response `JsonElement` for consumers
  who need fields the model doesn't surface.

## Non-extension points (deliberate)

- The DI builder's registration model (sealed) — stability over flexibility.
- `AiErrorCode` is an enum extended only by the library (custom providers pick the closest
  code; `ProviderError` is the catch-all).
- No public middleware pipeline in 1.0 (ADR-0006): decorators cover known scenarios with a
  fraction of the API surface.
