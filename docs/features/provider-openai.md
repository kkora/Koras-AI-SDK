# OpenAI Provider

## Overview

`Koras.AI.OpenAI` implements `IChatClient` (`OpenAIChatClient`) and `IEmbeddingClient`
(`OpenAIEmbeddingClient`) over the OpenAI chat-completions REST API. Because the endpoint is
configurable, the same provider also works against OpenAI-compatible gateways and proxies.
Provider name: `"openai"`; default client name: `"openai"`.

## When to use it

Hosted OpenAI models (chat, streaming, tools, `json_schema` structured output, embeddings),
or any gateway speaking the OpenAI wire protocol. For Azure-hosted OpenAI use
[provider-azure-openai.md](provider-azure-openai.md).

## Required packages

- `Koras.AI.OpenAI` (depends on `Koras.AI`).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["Koras:AI:OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
        o.DefaultEmbeddingModel = "text-embedding-3-small";
    });
});
```

Configuration binding (conventional section `Koras:AI:OpenAI`):

```csharp
ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
```

Named registration for multiple accounts or gateways:

```csharp
ai.AddOpenAI("gateway", o =>
{
    o.ApiKey = cfg["Gateway:Key"];
    o.Endpoint = new Uri("https://llm-gateway.internal.example.com/v1/");
    o.DefaultModel = "gpt-4o-mini";
});
```

## Options (`Koras.AI.OpenAI.OpenAIOptions`)

| Option | Type | Default | Notes |
|---|---|---|---|
| `ApiKey` | `string?` | — | Required (validated at startup). Sent as `Authorization: Bearer`. |
| `Endpoint` | `Uri` | `https://api.openai.com/v1/` | Must be HTTPS except loopback. Point at OpenAI-compatible gateways. |
| `DefaultModel` | `string?` | — | Used when `ChatRequest.Model` is null (e.g. `"gpt-4o-mini"`). |
| `DefaultEmbeddingModel` | `string?` | — | Used when `EmbeddingRequest.Model` is null (e.g. `"text-embedding-3-small"`). |
| `Organization` | `string?` | — | Sent as `OpenAI-Organization` when set. |

## Capabilities

Chat ✅, streaming ✅ (SSE, `[DONE]` sentinel), tool calling ✅, structured output ✅
(`json_schema`, `strict` honored), embeddings ✅, health probe ✅, `Retry-After` surfaced ✅.

## Basic usage

```csharp
ChatResponse response = await chat.CompleteAsync(
    ChatRequest.FromPrompt("Give me three names for a coffee shop."), ct);
```

All shared features work unchanged: [streaming](streaming.md),
[structured output](structured-output.md), [tool calling](tool-calling.md),
[embeddings](embeddings.md).

## Dependency-injection usage

`AddOpenAI` registers both the chat and the embedding client under the same name plus a named
`HttpClient` (`"Koras.AI.openai"`). Inject `IChatClient`/`IEmbeddingClient` for the default,
or use `IChatClientFactory` for named clients — see [dependency injection](dependency-injection.md).

## Error mapping

| Wire condition | `AiErrorCode` |
|---|---|
| 401 (e.g. `invalid_api_key`) | `Authentication` |
| 403 | `PermissionDenied` |
| 404 (unknown model) | `ModelNotFound` |
| 400 / 405 / 409 / 413 / 415 / 422 | `InvalidRequest` |
| 408 | `Timeout` |
| 429 `rate_limit_exceeded` | `RateLimited` (transient; `RetryAfter` populated when sent) |
| 429 `insufficient_quota` | `RateLimited` with `IsTransient = false` — quota, not burst |
| 5xx | `ProviderUnavailable` |
| DNS/connect/socket failure | `Network` |
| `HttpClient.Timeout` elapsed | `Timeout` |
| unparseable success payload | `InvalidResponse` |

The quota nuance matters: [retry](resilience.md) and [fallback](provider-fallback.md) skip
non-transient failures, so an exhausted quota fails fast instead of burning retries.

## Health probe

`GET {Endpoint}models` with the Bearer key — used by
[health checks](health-checks.md); never a paid completion.

## Cancellation

Standard contract: `OperationCanceledException` on caller cancellation.

## Security considerations

Provide `ApiKey` via user secrets, environment variables, or a vault — registration fails at
startup if it is missing, and the endpoint must be HTTPS (loopback exempt). Keys never appear
in exceptions, logs, or telemetry.

## Thread safety

`OpenAIChatClient` and `OpenAIEmbeddingClient` are thread-safe singletons.

## Testing applications using this feature

Prefer faking `IChatClient` (see [chat completion](chat-completion.md)). To test the provider
mapping itself, construct `OpenAIChatClient` with an `HttpClient` over a stubbed
`HttpMessageHandler` returning canned OpenAI JSON — the unit tests in
`tests/Koras.AI.UnitTests/Providers/OpenAIChatClientTests.cs` follow this pattern.

## Common mistakes

- Retrying `insufficient_quota` 429s manually — check `IsTransient`, not just the code.
- Omitting the trailing `/v1/` (or equivalent) path on a gateway `Endpoint`.
- Setting `DefaultModel` only for chat and forgetting `DefaultEmbeddingModel` when using
  embeddings.
- Hardcoding the API key in source; validation cannot catch a leaked key, only a missing one.

## Related features

- [Chat completion](chat-completion.md) · [Streaming](streaming.md) ·
  [Structured output](structured-output.md) · [Tool calling](tool-calling.md) ·
  [Embeddings](embeddings.md)
- [Azure OpenAI provider](provider-azure-openai.md)
- [Error handling](error-handling.md)
