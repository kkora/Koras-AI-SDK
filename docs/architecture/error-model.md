# Error Model

## Design

One exception type, **`AiException`**, with a closed taxonomy, **`AiErrorCode`** (ADR-0004).
Consumers write one catch block and switch on the code; providers map their wire errors into
the taxonomy at the adapter boundary. Nothing above a provider adapter ever sees a raw
`HttpRequestException` or provider JSON error.

```csharp
public enum AiErrorCode
{
    Unknown = 0,
    Authentication,      // 401/403 — invalid or missing credentials
    PermissionDenied,    // authenticated but not allowed (org, region, model access)
    ModelNotFound,       // unknown model/deployment (404 on model)
    InvalidRequest,      // 400 — malformed request, context length exceeded
    ContentFiltered,     // provider safety system blocked input or output
    RateLimited,         // 429 (and Anthropic 529) — RetryAfter populated when sent
    ProviderUnavailable, // 5xx, overloaded
    Network,             // DNS/connect/socket failures
    Timeout,             // per-attempt or overall timeout
    Canceled,            // caller's CancellationToken fired (paired with OperationCanceledException semantics, see below)
    InvalidResponse,     // unparseable success payload, structured-output deserialization failure
    NotSupported,        // capability not supported by this provider (e.g. Anthropic embeddings)
    ToolExecutionFailed, // a registered tool handler threw and policy = propagate
    Configuration        // invalid options detected at call time
}
```

### `AiException` shape

| Member | Meaning |
|---|---|
| `Code` | taxonomy above |
| `Provider` | provider name (`"openai"`, `"anthropic"`, …) or null pre-dispatch |
| `StatusCode` | HTTP status when applicable |
| `RetryAfter` | parsed `Retry-After`/provider hint |
| `IsTransient` | true for `RateLimited`, `ProviderUnavailable`, `Network`, `Timeout` — drives retry/fallback |
| `ProviderErrorBody` | raw provider error payload, truncated to 4 KB, secrets never present |
| `RequestId` | provider request id header when available |

## Rules

1. **Cancellation is not an error.** When the caller's token fires, `OperationCanceledException`
   propagates (standard .NET contract). `AiErrorCode.Canceled` exists only for wrapping in
   aggregate contexts (fallback exhaustion reports).
2. **Transient is a closed set.** Retry and fallback consult `IsTransient` only — never status
   codes — so custom providers integrate by mapping correctly.
3. **Original error preserved.** The triggering exception is always `InnerException`; provider
   response bodies are attached, truncated, and scrubbed.
4. **Fallback exhaustion** throws the *last* `AiException` with an `AggregateException` of all
   attempts as its inner exception.
5. **No secrets, ever.** Options types own the scrub list (API keys); the base provider client
   redacts known header names from any diagnostic string.

## Per-provider mapping (excerpt — full table in each provider guide)

| Wire condition | Code |
|---|---|
| OpenAI 401 `invalid_api_key` | Authentication |
| OpenAI 429 `insufficient_quota` | RateLimited (`IsTransient=false` — quota, not burst; documented nuance) |
| OpenAI 429 `rate_limit_exceeded` | RateLimited (transient) |
| Anthropic 529 `overloaded_error` | ProviderUnavailable |
| Anthropic `invalid_request_error` w/ max_tokens | InvalidRequest |
| Gemini 400 `API key not valid` | InvalidRequest (Gemini reports bad keys as HTTP 400; the message carries the detail) |
| Ollama connect refused | Network (message hints "is Ollama running?") |
| Any `stop_reason`/`finishReason` = safety | ContentFiltered (on response mapping) |

## Error lifecycle

```
provider wire error ──map──▶ AiException{Code,IsTransient}
        │                          │
        │                    Retry decorator: IsTransient? retry (≤N, honor RetryAfter) : rethrow
        │                          │
        │                    Fallback decorator: failover-eligible? next candidate : rethrow
        │                          │
        └──────────────▶ Telemetry decorator: span status=error, error.type=code ──▶ caller
```
