# Embeddings

## Overview

`IEmbeddingClient.GenerateAsync` turns texts into vectors. An `EmbeddingRequest` carries the
input `Values` (one vector is returned per value), an optional `Model`, and optional
`Dimensions` for models that support shortening. The `EmbeddingResponse` returns
`IReadOnlyList<Embedding>` ordered by input index, each exposing `Vector`
(`ReadOnlyMemory<float>`) and `Index`, plus `Provider`, `Model`, and `Usage`.

## When to use it

Semantic search, retrieval-augmented generation, clustering, deduplication, and
recommendation — anywhere text similarity is computed numerically.

## When not to use it

Anthropic offers no embeddings API: the capability throws `AiException` with
`AiErrorCode.NotSupported`. Use OpenAI, Azure OpenAI, Gemini, or Ollama for vectors (a mixed
setup with Anthropic for chat is normal).

## Required packages

- `Koras.AI` plus a provider package with embedding support: `Koras.AI.OpenAI`,
  `Koras.AI.AzureOpenAI`, `Koras.AI.Gemini`, or `Koras.AI.Ollama`.

## Basic usage

```csharp
using Koras.AI;

EmbeddingResponse response = await embeddings.GenerateAsync(
    new EmbeddingRequest("How do I reset my password?", "Billing questions"), ct);

foreach (Embedding embedding in response.Embeddings)
{
    ReadOnlyMemory<float> vector = embedding.Vector;
    Console.WriteLine($"[{embedding.Index}] {vector.Length} dimensions");
}
```

Object-initializer form when you need model or dimension control:

```csharp
var request = new EmbeddingRequest
{
    Values = documents,
    Model = "text-embedding-3-small",
    Dimensions = 256,
};
```

## Dependency-injection usage

Providers that support embeddings register an embedding client under the same name as their
chat client; the default `IEmbeddingClient` is injected as a singleton:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
        o.DefaultEmbeddingModel = "text-embedding-3-small";
    });
});

public sealed class Indexer(IEmbeddingClient embeddings) { /* ... */ }
```

Named lookup goes through the factory: `factory.GetEmbeddingClient("openai")`. Requesting a
name with no registered embedding client throws `InvalidOperationException` listing the
registered names.

## Provider support

| Provider | Embeddings | Default model option |
|---|---|---|
| OpenAI | Yes | `OpenAIOptions.DefaultEmbeddingModel` |
| Azure OpenAI | Yes | `AzureOpenAIOptions.EmbeddingDeployment` |
| Anthropic | No — `AiErrorCode.NotSupported` | — |
| Gemini | Yes (batch embed contents) | `GeminiOptions.DefaultEmbeddingModel` |
| Ollama | Yes | `OllamaOptions.DefaultEmbeddingModel` |

## Error handling

Failures throw `AiException` with the standard taxonomy: `Authentication`, `RateLimited`,
`InvalidRequest` (for example too-long inputs), `ProviderUnavailable`, `Network`, `Timeout`.
Missing model configuration surfaces as `AiErrorCode.Configuration`. See
[error handling](error-handling.md).

## Cancellation

`GenerateAsync` takes a `CancellationToken`; cancellation surfaces as
`OperationCanceledException`.

## Telemetry

Embedding operations produce `embeddings {model}` activities and record the same
`koras.ai.client.operation.duration` and `koras.ai.client.token.usage` instruments as chat.
See [telemetry](telemetry.md).

## Security considerations

Embedded text is sensitive data; vectors can leak information about their source text. Keep
API keys in secret stores, and treat vector databases with the same access control as the
source documents.

## Performance considerations

Batch multiple values into one `EmbeddingRequest` instead of one call per text — providers
price and rate-limit per request and per token. Use `Dimensions` to reduce storage when your
model supports shortening.

## Thread safety

`IEmbeddingClient` implementations are thread-safe singletons; requests and responses are
immutable.

## Testing applications using this feature

```csharp
private sealed class FakeEmbeddingClient : IEmbeddingClient
{
    public string ProviderName => "fake";

    public Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new EmbeddingResponse
        {
            Embeddings = [.. request.Values.Select((_, i) => new Embedding(new float[] { 0.1f, 0.2f }, i))],
            Provider = ProviderName,
        });
}
```

Register with `ai.AddEmbeddingClient("fake", _ => new FakeEmbeddingClient())`.

## Common mistakes

- Calling embeddings on an Anthropic-only setup — it is `NotSupported`; register a second
  provider for vectors.
- Comparing vectors produced by different models or different `Dimensions` values; they are
  not in the same space.
- Forgetting `DefaultEmbeddingModel` (or `EmbeddingDeployment` on Azure) and not setting
  `EmbeddingRequest.Model`.
- Relying on response order across providers instead of `Embedding.Index`.

## Related features

- [Chat completion](chat-completion.md)
- [Dependency injection](dependency-injection.md)
- [Provider guides](provider-openai.md)
