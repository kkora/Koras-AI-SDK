# Error Handling

Koras.AI throws exactly one exception type for AI operation failures: **`AiException`**,
carrying a provider-neutral **`AiErrorCode`**. Every provider maps its wire errors into this
taxonomy, so one catch block handles OpenAI, Anthropic, Gemini, Ollama, and Azure
identically. Caller cancellation is the deliberate exception: it surfaces as
`OperationCanceledException` ([cancellation](cancellation.md)).

Design rationale and provider mapping tables: [error model](../architecture/error-model.md).

## The error code taxonomy

| Code | Meaning | Transient |
|---|---|---|
| `Unknown` | The failure could not be classified | no |
| `Authentication` | Credentials missing, malformed, or rejected (401/403) | no |
| `PermissionDenied` | Authenticated but not allowed (org, region, model access) | no |
| `ModelNotFound` | Unknown model or deployment | no |
| `InvalidRequest` | Malformed request, context length exceeded (400) | no |
| `ContentFiltered` | Provider safety system blocked input or output | no |
| `RateLimited` | Throttled (429); `RetryAfter` populated when the provider sends it | **yes** |
| `ProviderUnavailable` | Server-side failure or overload (5xx) | **yes** |
| `Network` | DNS, connect, or socket failure | **yes** |
| `Timeout` | Per-attempt or overall timeout exceeded | **yes** |
| `Canceled` | Only inside aggregate reports; direct cancellation throws `OperationCanceledException` | no |
| `InvalidResponse` | Unparseable success payload or structured-output deserialization failure | no |
| `NotSupported` | Capability not offered by this provider (e.g. Anthropic embeddings) | no |
| `ToolExecutionFailed` | A tool handler threw and the policy propagates, or the tool loop did not converge | no |
| `Configuration` | Misconfiguration detected at call time rather than startup | no |

New codes may be added in minor releases — always default-case your switches.

## What AiException carries

```csharp
catch (AiException ex)
{
    ex.Code;                // the taxonomy above
    ex.Provider;            // "openai", "anthropic", … (null before dispatch)
    ex.StatusCode;          // HTTP status when applicable
    ex.RetryAfter;          // parsed Retry-After hint, when sent
    ex.IsTransient;         // whether retrying may succeed
    ex.ProviderErrorBody;   // raw provider error, ≤ 4 KB, credentials scrubbed
    ex.RequestId;           // provider request id, for support tickets
    ex.InnerException;      // the original triggering exception, always preserved
}
```

## IsTransient semantics

`IsTransient` defaults from the code (`RateLimited`, `ProviderUnavailable`, `Network`,
`Timeout` are transient) but can be overridden per instance — for example, OpenAI's
quota-exhausted 429 is mapped as `RateLimited` with `IsTransient = false`, because retrying
a spent quota is pointless. The retry and fallback decorators consult **only this flag**,
never status codes, so custom providers integrate by mapping correctly.

## Catch patterns

Handle the cases you can act on, let the rest bubble:

```csharp
try
{
    ChatResponse response = await chat.CompleteAsync(request, ct);
    return response.Text;
}
catch (AiException ex) when (ex.Code == AiErrorCode.RateLimited)
{
    // Surface the provider's wait hint to your caller.
    throw new MyServiceBusyException(ex.RetryAfter ?? TimeSpan.FromSeconds(30));
}
catch (AiException ex) when (ex.Code == AiErrorCode.ContentFiltered)
{
    return "That request was blocked by the provider's safety system.";
}
catch (AiException ex) when (ex.IsTransient)
{
    // Retries (if configured) are already exhausted when you see this.
    logger.LogError(ex, "AI temporarily unavailable ({Code})", ex.Code);
    return null;
}
```

In ASP.NET Core, map codes to HTTP statuses (429 / 422 / 503) — see the
[ASP.NET Core guide](../guides/aspnet-core.md). In background workers, log and skip the work
item rather than crashing the host — see the [worker service guide](../guides/worker-service.md).

Note the ordering: by the time your catch runs, `UseRetry` has already retried transient
failures. Catching `ex.IsTransient` means "retries exhausted", not "first failure".

## Fallback exhaustion

When a fallback client's candidates all fail, the **last** `AiException` is rethrown with an
`AggregateException` of every attempt as its inner exception:

```csharp
catch (AiException ex) when (ex.InnerException is AggregateException attempts)
{
    foreach (AiException attempt in attempts.InnerExceptions.OfType<AiException>())
    {
        logger.LogWarning("Candidate {Provider} failed: {Code}", attempt.Provider, attempt.Code);
    }
}
```

## Structured output failures

`CompleteAsync<T>` throws `AiException` with `AiErrorCode.InvalidResponse` when the model's
output cannot be deserialized as `T`; the offending text (truncated) is available in
`ProviderErrorBody` for debugging.

## Rules worth remembering

1. Cancellation is not an error — catch `OperationCanceledException` separately.
2. The original exception is always `InnerException`; nothing is swallowed.
3. Diagnostics never contain credentials — provider error bodies are scrubbed before they
   are attached.
