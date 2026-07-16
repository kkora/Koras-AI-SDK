# Anthropic Provider

## Overview

`Koras.AI.Anthropic` implements `IChatClient` (`AnthropicChatClient`) over the Anthropic
Messages API (`POST {Endpoint}v1/messages`). Authentication uses the `x-api-key` header plus
the `anthropic-version` header. Two provider-specific facts shape usage: Anthropic **requires
`max_tokens` on every request** (the SDK supplies `DefaultMaxOutputTokens` when a request does
not set `ChatOptions.MaxOutputTokens`), and Anthropic **has no embeddings API**. Provider
name: `"anthropic"`; default client name: `"anthropic"`.

## When to use it

Claude models for chat, streaming, tool calling, and structured output. Pair with another
provider for [embeddings](embeddings.md).

## Required packages

- `Koras.AI.Anthropic` (depends on `Koras.AI`).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddAnthropic(o =>
    {
        o.ApiKey = builder.Configuration["Koras:AI:Anthropic:ApiKey"];
        o.DefaultModel = "claude-sonnet-4-5";
    });
});
```

Configuration binding (conventional section `Koras:AI:Anthropic`):

```csharp
ai.AddAnthropic(builder.Configuration.GetSection("Koras:AI:Anthropic"));
```

## Options (`Koras.AI.Anthropic.AnthropicOptions`)

| Option | Type | Default | Notes |
|---|---|---|---|
| `ApiKey` | `string?` | — | Required. Sent as `x-api-key`. |
| `Endpoint` | `Uri` | `https://api.anthropic.com/` | HTTPS enforced (loopback exempt). |
| `DefaultModel` | `string?` | — | e.g. `"claude-sonnet-4-5"`. |
| `DefaultMaxOutputTokens` | `int` | `4096` | Fills the mandatory `max_tokens` when a request sets none. Must be positive. |
| `AnthropicVersion` | `string` | `2023-06-01` | The `anthropic-version` header. |

## Structured output: the `record_output` mechanism

Anthropic has no native response-format parameter. When a request carries a
`JsonSchemaChatResponseFormat` (which `client.CompleteAsync<T>(...)` applies automatically),
the SDK appends a synthetic tool named `record_output` whose input schema is your output
schema, and forces the model to call it (`tool_choice: {"type":"tool","name":"record_output"}`).
On the way back, the tool call's input JSON is surfaced as the response **text**, with
`FinishReason` normalized to `Stop` — so `CompleteAsync<T>` deserializes it transparently:

```csharp
ChatResponse<Invoice> result = await chat.CompleteAsync<Invoice>(
    "Extract the invoice from:\n" + emailBody, ct);
```

Consequence: **prefer `CompleteAsync`/`CompleteAsync<T>` over `StreamAsync` for structured
output on Anthropic** — in a stream the payload arrives as `ToolCallDelta` fragments of the
`record_output` call, not as `TextDelta` text. See [structured output](structured-output.md).

## Embeddings: not supported

Anthropic offers no embeddings API; `AddAnthropic` registers no embedding client, and the
capability is classified `AiErrorCode.NotSupported`. Register OpenAI, Azure OpenAI, Gemini, or
Ollama alongside Anthropic and resolve the embedding client by name.

## Capabilities

Chat ✅, streaming ✅ (SSE event stream), tool calling ✅, structured output ✅ (via forced
tool), embeddings ❌ (`NotSupported`), health probe ✅, `Retry-After` surfaced ✅.

Wire details handled for you: system messages are hoisted into the top-level `system` field;
consecutive tool results merge into a single user turn; `stop_reason` maps to
`ChatFinishReason` (`end_turn`/`stop_sequence` → `Stop`, `max_tokens` → `Length`, `tool_use`
→ `ToolCalls`, `refusal` → `ContentFilter`).

## Dependency-injection usage

Standard: inject `IChatClient` or resolve `"anthropic"` via `IChatClientFactory`. See
[dependency injection](dependency-injection.md).

## Error mapping

| Wire condition | `AiErrorCode` |
|---|---|
| 401 / `authentication_error` | `Authentication` |
| 403 / `permission_error` | `PermissionDenied` |
| 404 / `not_found_error` | `ModelNotFound` |
| 400 / `invalid_request_error` (e.g. missing max_tokens) | `InvalidRequest` |
| 429 / `rate_limit_error` | `RateLimited` (`RetryAfter` surfaced) |
| 529 / `overloaded_error` | `ProviderUnavailable` |
| 5xx / `api_error` | `ProviderUnavailable` |
| mid-stream `error` event | mapped by its `type` as above |
| network failure / timeout | `Network` / `Timeout` |
| `stop_reason: refusal` | `ContentFilter` (finish reason, on response mapping) |

## Health probe

`GET {Endpoint}v1/models?limit=1` with the `x-api-key` and `anthropic-version` headers. Used
by [health checks](health-checks.md).

## Cancellation

Standard contract: `OperationCanceledException` on caller cancellation, mid-stream included.

## Security considerations

Keep the API key in user secrets/environment variables; startup validation rejects a missing
key. Diagnostics never include the `x-api-key` header.

## Thread safety

`AnthropicChatClient` is a thread-safe singleton.

## Testing applications using this feature

Fake `IChatClient` for app logic. For mapping tests, stub the `HttpMessageHandler` with
Messages-API JSON; `tests/Koras.AI.UnitTests/Providers/AnthropicChatClientTests.cs` includes
fixtures showing the `record_output` request body and response handling.

## Common mistakes

- Streaming schema-constrained requests and finding no `TextDelta` — the data is in
  `ToolCallDelta`s for `record_output`; use `CompleteAsync<T>`.
- Expecting embeddings; the capability is `NotSupported`.
- Assuming unlimited output: `max_tokens` is always set (request value or
  `DefaultMaxOutputTokens`), so watch for `FinishReason.Length`.
- Registering your own tool named `record_output`; the name is reserved for the structured
  output mechanism.

## Related features

- [Structured output](structured-output.md)
- [Tool calling](tool-calling.md)
- [Embeddings](embeddings.md) (use another provider)
- [Error handling](error-handling.md)
