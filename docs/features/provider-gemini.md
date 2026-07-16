# Gemini Provider

## Overview

`Koras.AI.Gemini` implements `IChatClient` (`GeminiChatClient`) and `IEmbeddingClient`
(`GeminiEmbeddingClient`) over the Google Gemini REST API
(`models/{model}:generateContent`, streaming via `:streamGenerateContent?alt=sse`).
Authentication uses the `x-goog-api-key` **header — never the query string**, so the key
cannot leak into URLs, proxies, or access logs. Provider name: `"gemini"`; default client
name: `"gemini"`.

## When to use it

Google's Gemini models for chat, streaming, tool calling, structured output
(`responseSchema`), and embeddings.

## Required packages

- `Koras.AI.Gemini` (depends on `Koras.AI`).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddGemini(o =>
    {
        o.ApiKey = builder.Configuration["Koras:AI:Gemini:ApiKey"];
        o.DefaultModel = "gemini-2.0-flash";
        o.DefaultEmbeddingModel = "text-embedding-004";
    });
});
```

Configuration binding (conventional section `Koras:AI:Gemini`):

```csharp
ai.AddGemini(builder.Configuration.GetSection("Koras:AI:Gemini"));
```

## Options (`Koras.AI.Gemini.GeminiOptions`)

| Option | Type | Default | Notes |
|---|---|---|---|
| `ApiKey` | `string?` | — | Required. Sent as the `x-goog-api-key` header, never in the query string. |
| `Endpoint` | `Uri` | `https://generativelanguage.googleapis.com/v1beta/` | HTTPS enforced (loopback exempt). |
| `DefaultModel` | `string?` | — | e.g. `"gemini-2.0-flash"`. |
| `DefaultEmbeddingModel` | `string?` | — | e.g. `"text-embedding-004"`. |

## Schema-dialect cleaning

Gemini's `responseSchema`/tool-parameter dialect rejects several JSON Schema keywords. The SDK
recursively strips `additionalProperties`, `$schema`, and `$defs` from every schema it sends —
both structured output schemas (`ChatResponseFormat.ForType<T>()` /
`ChatResponseFormat.JsonSchema(...)`) and `AiTool.ParametersSchema`. Practical consequence:
schemas relying on `$defs` references or `additionalProperties: false` strictness lose those
constraints on Gemini; validate the deserialized value in code where it matters.

## Synthesized tool-call ids

The Gemini wire format has no tool-call ids; the SDK synthesizes stable ones as
`call_{index}_{name}` (for example `call_0_get_weather`). When you send
`ChatMessage.ToolResult(toolCallId, content)` back, the suffix after the second underscore is
mapped back to the function name for Gemini's `functionResponse`. Round-trip
`ToolCall.Id` verbatim — the automatic loop from `ai.UseToolInvocation()` does this for you.
See [tool calling](tool-calling.md).

## Capabilities

Chat ✅, streaming ✅ (SSE), tool calling ✅, structured output ✅ (`responseSchema` +
`responseMimeType: application/json`), embeddings ✅ (batch embed contents), health probe ✅,
`Retry-After` surfaced ✅. System messages are hoisted into `systemInstruction`; finish
reasons map `STOP` → `Stop`, `MAX_TOKENS` → `Length`, and `SAFETY`/`PROHIBITED_CONTENT`/
`BLOCKLIST` → `ContentFilter`.

## Dependency-injection usage

`AddGemini` registers chat + embedding clients under the same name plus a named `HttpClient`.
See [dependency injection](dependency-injection.md).

## Error mapping

| Wire condition | `AiErrorCode` |
|---|---|
| 401 | `Authentication` |
| 403 | `PermissionDenied` |
| 404 (unknown model) | `ModelNotFound` |
| 400 (including "API key not valid") | `InvalidRequest` — the message carries the API-key detail |
| 429 | `RateLimited` (`RetryAfter` surfaced) |
| 5xx | `ProviderUnavailable` |
| network failure / timeout | `Network` / `Timeout` |
| `finishReason: SAFETY` | `ContentFilter` (finish reason, on response mapping) |

Note the 400 nuance: Google reports an invalid API key as HTTP 400 `INVALID_ARGUMENT`, so it
classifies as `InvalidRequest` by status; read `AiException.Message` /`ProviderErrorBody` for
the "API key not valid" detail.

## Health probe

`GET {Endpoint}models?pageSize=1` with the `x-goog-api-key` header. Used by
[health checks](health-checks.md).

## Cancellation

Standard contract: `OperationCanceledException` on caller cancellation.

## Security considerations

The header-only key policy is deliberate — never move the key to a `?key=` query parameter
when fronting Gemini with a proxy. Keep the key in user secrets or environment variables;
startup validation rejects a missing key.

## Thread safety

Both clients are thread-safe singletons.

## Testing applications using this feature

Fake `IChatClient` for app logic. For provider tests, stub the `HttpMessageHandler` with
`generateContent` JSON; `tests/Koras.AI.UnitTests/Providers/GeminiClientTests.cs` shows the
synthesized `call_0_get_weather` ids and the tool-result round trip.

## Common mistakes

- Inventing your own tool-result ids instead of echoing `ToolCall.Id` — the function-name
  suffix is how results map back to Gemini functions.
- Relying on `additionalProperties: false` or `$defs` in structured output schemas; they are
  stripped for Gemini.
- Diagnosing a 400 as a malformed request when the real cause is a bad API key — check the
  message text.
- Passing a fully qualified `models/...` string as `ChatRequest.Model`; pass the bare model
  name (the SDK builds the route).

## Related features

- [Structured output](structured-output.md)
- [Tool calling](tool-calling.md)
- [Embeddings](embeddings.md)
- [Error handling](error-handling.md)
