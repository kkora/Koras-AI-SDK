# Custom Providers

## Overview

Any type implementing `IChatClient` (and optionally `IEmbeddingClient` and
`IProviderHealthProbe`) plugs into Koras.AI with full participation in retry, fallback,
logging, telemetry, and health checks. The `Koras.AI.Providers` namespace supplies the same
plumbing the first-party providers use: `ProviderChatClient` / `ProviderEmbeddingClient`
(HTTP send with normalized error handling), `SseReader` and `JsonLinesReader` for streaming
wire formats, and `ProviderErrors` for building taxonomy-correct `AiException`s.

## When to use it

Internal gateways, providers the SDK does not ship, OpenAI-incompatible proxies, or wrapping
an existing in-house client. (For OpenAI-compatible gateways, just point
`OpenAIOptions.Endpoint` at the gateway instead.)

## Required packages

- `Koras.AI` (brings `Koras.AI.Abstractions` and the `Koras.AI.Providers` plumbing).

## Basic usage

Derive from `ProviderChatClient` and implement the two operations:

```csharp
using System.Text;
using System.Text.Json;
using Koras.AI;
using Koras.AI.Providers;

public sealed class AcmeChatClient(HttpClient httpClient, Uri endpoint, string defaultModel)
    : ProviderChatClient(httpClient, providerName: "acme"), IProviderHealthProbe
{
    public override async Task<ChatResponse> CompleteAsync(
        ChatRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "v1/chat"))
        {
            Content = new StringContent(BuildBody(request), Encoding.UTF8, "application/json"),
        };

        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        return new ChatResponse
        {
            Message = ChatMessage.Assistant(document.RootElement.GetProperty("text").GetString() ?? string.Empty),
            Provider = ProviderName,
            Model = defaultModel,
            FinishReason = ChatFinishReason.Stop,
        };
    }

    public override IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request, CancellationToken cancellationToken = default)
        => throw ProviderErrors.NotSupported(ProviderName, "streaming");

    public Task ProbeAsync(CancellationToken cancellationToken = default)
        => CompleteAsync(ChatRequest.FromPrompt("ping"), cancellationToken); // or a cheap GET

    private static string BuildBody(ChatRequest request) => /* map messages to your wire format */ "{}";
}
```

`SendAndParseAsync` normalizes HTTP errors, network failures, timeouts, and unparseable
payloads into `AiException` for you; `SendForStreamAsync` returns a headers-read response for
`SseReader.ReadEventsAsync(stream, ct)` or `JsonLinesReader.ReadLinesAsync(stream, ct)`.

## Dependency-injection usage

Register with `AddClient` (and `AddEmbeddingClient` when applicable):

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.Services.AddHttpClient("Koras.AI.acme");
    ai.AddClient("acme", sp => new AcmeChatClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("Koras.AI.acme"),
            new Uri("https://ai.internal.example.com/"),
            "acme-large"))
        .AsDefault();
    ai.UseRetry();
});
```

The factory delegate is invoked once and the instance cached; global decorators, automatic
logging, and telemetry wrap your client exactly like the first-party ones. For cross-cutting
behavior rather than a transport, derive from `DelegatingChatClient` instead and attach it
with `ai.Use(...)`.

## The contract

- `ProviderName` — stable, lowercase.
- All failures throw `AiException` with an accurate `AiErrorCode`; map wire errors through
  `ProviderErrors.FromHttpResponse` / `Network` / `InvalidResponse` / `NotSupported` (or
  `ProviderErrors.MapStatusCode` for status-only mapping).
- **Transient is a closed set**: retry and fallback consult `AiException.IsTransient` only —
  correct mapping is how your provider integrates with [resilience](resilience.md).
- Caller cancellation propagates as `OperationCanceledException`, never `AiException`.
- Streaming opens the transport on first `MoveNextAsync`; disposing the enumerator releases
  the connection; the terminal update carries `FinishReason`.
- Unsupported capabilities throw `AiErrorCode.NotSupported` — never silently degrade.
- Scrub credentials from every diagnostic string (`ProviderErrorBody` must never contain keys).

## Error handling

Follow [../architecture/error-model.md](../architecture/error-model.md). The contract tests
in `tests/Koras.AI.UnitTests/Providers/ProviderContractTests.cs` show the exact expectations
the built-in providers satisfy; mirror them for your provider.

## Cancellation

Pass the token to every `HttpClient` call (the base-class helpers do this) and to any handler
work. Distinguish your own timeouts (`AiErrorCode.Timeout`) from caller cancellation.

## Telemetry

You inherit spans and metrics for free: the factory wraps registered clients in the telemetry
and logging decorators, tagged with your `ProviderName` and client name. Health checks pick up
`IProviderHealthProbe` automatically, including through decorator stacks.

## Security considerations

Use HTTPS endpoints (loopback exempt), keep credentials in options bound from secret sources,
and validate options with `AddOptions<T>().Validate(...).ValidateOnStart()` as the built-in
providers do.

## Thread safety

Your client must be thread-safe for singleton use — no mutable per-request state on the
instance.

## Testing applications using this feature

Test against a stubbed `HttpMessageHandler` so the full parse/error path runs without a
network. Verify at minimum: success parsing, HTTP error → correct `AiErrorCode`,
network failure → `Network`, cancellation → `OperationCanceledException`, and (if streaming)
that the enumerator honors early disposal.

## Common mistakes

- Throwing `HttpRequestException` or provider-specific exceptions instead of `AiException`.
- Marking everything transient (retry storms) or nothing transient (no resilience).
- Opening the streaming request eagerly in `StreamAsync` instead of on first enumeration.
- Forgetting `ConfigureAwait(false)` in library-style code paths.

## Related features

- [Dependency injection](dependency-injection.md)
- [Error handling](error-handling.md)
- [Resilience](resilience.md)
- [Health checks](health-checks.md)
