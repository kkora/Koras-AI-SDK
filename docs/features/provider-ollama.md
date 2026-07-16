# Ollama Provider

## Overview

`Koras.AI.Ollama` implements `IChatClient` (`OllamaChatClient`) and `IEmbeddingClient`
(`OllamaEmbeddingClient`) over the native Ollama API (`POST {Endpoint}api/chat`). It targets
local or self-hosted Ollama: the endpoint defaults to `http://localhost:11434/` and **no API
key is required** — the only validated option is a non-null endpoint. Provider name:
`"ollama"`; default client name: `"ollama"`.

## When to use it

Local development without API keys or per-token cost, offline/air-gapped scenarios,
self-hosted open models, and as a local safety net in a [fallback](provider-fallback.md)
chain.

## When not to use it

Hosted-quality models at scale; tool calling and structured-output fidelity are
model-dependent on Ollama.

## Required packages

- `Koras.AI.Ollama` (depends on `Koras.AI`). Plus a running Ollama daemon
  (`ollama serve`) with your model pulled (`ollama pull llama3.2`).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o =>
    {
        o.DefaultModel = "llama3.2";                 // Endpoint stays http://localhost:11434/
        o.DefaultEmbeddingModel = "nomic-embed-text";
    });
});
```

Configuration binding (conventional section `Koras:AI:Ollama`):

```csharp
ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
```

## Options (`Koras.AI.Ollama.OllamaOptions`)

| Option | Type | Default | Notes |
|---|---|---|---|
| `Endpoint` | `Uri` | `http://localhost:11434/` | Local daemon or self-hosted server. No HTTPS requirement (unlike hosted providers). |
| `DefaultModel` | `string?` | — | e.g. `"llama3.2"`. |
| `DefaultEmbeddingModel` | `string?` | — | e.g. `"nomic-embed-text"`. |

## JSON-lines streaming

Ollama streams newline-delimited JSON objects, not SSE. The SDK reads them with
`JsonLinesReader` and yields the same `ChatStreamUpdate`s as every other provider — your
`await foreach` code is unchanged. The terminal line (`"done": true`) yields the
`FinishReason` and `Usage` (from `prompt_eval_count`/`eval_count`). An in-band `error` field
mid-stream throws `AiException` with `AiErrorCode.ProviderUnavailable`.

## Synthesized tool-call ids

Ollama does not assign tool-call ids, so the SDK synthesizes stable ones as
`call_{index}_{name}` (for example `call_0_get_weather`) for the round trip. Echo
`ToolCall.Id` into `ChatMessage.ToolResult(...)` as usual; the automatic loop handles it.
Tool calling is **model-dependent** — the model must have been trained/templated for tools.

## The "Is Ollama running?" hint

A connection failure is the most common Ollama error, so the provider appends a remediation
hint to network errors:

> Is Ollama running? Start it with 'ollama serve' or install from https://ollama.com.

The exception is still a normal `AiException` with `AiErrorCode.Network` (transient, so
[retry](resilience.md)/[fallback](provider-fallback.md) apply).

## Capabilities

Chat ✅, streaming ✅ (JSON lines), tool calling ✅ (model-dependent), structured output ✅
(Ollama `format` parameter: `"json"` for JSON mode, or the full schema for
schema-constrained), embeddings ✅, health probe ✅, `Retry-After` n/a.
`ChatOptions` map to Ollama options: `MaxOutputTokens` → `num_predict`, `StopSequences` →
`stop`, `Temperature`/`TopP` pass through.

## Dependency-injection usage

`AddOllama` registers chat + embedding clients under the same name plus a named `HttpClient`.
See [dependency injection](dependency-injection.md).

## Error mapping

| Wire condition | `AiErrorCode` |
|---|---|
| connection refused / daemon not running | `Network` (message carries the "Is Ollama running?" hint) |
| 404 (model not pulled) | `ModelNotFound` |
| 400 | `InvalidRequest` |
| 5xx | `ProviderUnavailable` |
| in-band stream `error` field | `ProviderUnavailable` |
| `HttpClient.Timeout` elapsed | `Timeout` |
| unparseable line/payload | `InvalidResponse` |
| missing model configuration | `Configuration` |

## Health probe

`GET {Endpoint}api/version` — cheap and unauthenticated. Used by
[health checks](health-checks.md).

## Cancellation

Standard contract: `OperationCanceledException` on caller cancellation; local generation
stops when the connection is torn down.

## Security considerations

No API key exists, so network reachability is the access control: bind Ollama to localhost or
put a self-hosted instance behind your own authentication layer before exposing it. Prompts
stay on your infrastructure — often the point of choosing Ollama.

## Performance considerations

Local inference throughput depends on your hardware; first requests after model load are
slow. Keep `HttpClient.Timeout` (via the named client `"Koras.AI.ollama"`) generous for large
models.

## Thread safety

Both clients are thread-safe singletons; concurrency is bounded by the daemon itself.

## Testing applications using this feature

Ollama is itself a convenient integration-test backend (free, local, deterministic-ish with
temperature 0). For pure unit tests, stub the `HttpMessageHandler` with JSON-lines fixtures —
`tests/Koras.AI.UnitTests/Providers/OllamaClientTests.cs` covers the synthesized
`call_0_get_weather` ids and the connect-refused hint.

## Common mistakes

- Forgetting to start the daemon or pull the model — read the hint in the `Network` error,
  and expect `ModelNotFound` for unpulled models.
- Expecting tool calls from a model without tool support; you simply get text.
- Treating `Usage` as billing-grade; counts come from `prompt_eval_count`/`eval_count`.
- Pointing `Endpoint` at a remote Ollama over plain HTTP across untrusted networks.

## Related features

- [Streaming](streaming.md)
- [Tool calling](tool-calling.md)
- [Provider fallback](provider-fallback.md)
- [Health checks](health-checks.md)
