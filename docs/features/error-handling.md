# Error Handling

## Overview

Every AI operation failure in Koras.AI surfaces as a single exception type, `AiException`,
carrying a provider-neutral classification, `AiErrorCode`. Providers map their wire-level
errors into this taxonomy at the adapter boundary — application code never sees a raw
`HttpRequestException` or provider JSON error. The one deliberate exception: caller
cancellation surfaces as `OperationCanceledException`, per the standard .NET contract.

## When to use it

This model applies to every feature — chat, streaming, structured output, tools, embeddings.
Write one catch block and switch on `Code`.

## Required packages

- `Koras.AI.Abstractions` (comes with `Koras.AI`).

## The taxonomy

| `AiErrorCode` | Meaning | Transient |
|---|---|---|
| `Unknown` | unclassifiable | no |
| `Authentication` | credentials missing/rejected (401/403) | no |
| `PermissionDenied` | authenticated but not allowed | no |
| `ModelNotFound` | unknown model or deployment | no |
| `InvalidRequest` | malformed request, context length exceeded | no |
| `ContentFiltered` | safety system blocked input/output | no |
| `RateLimited` | throttled (429); `RetryAfter` populated when sent | yes* |
| `ProviderUnavailable` | 5xx / overloaded | yes |
| `Network` | DNS/connect/socket failure | yes |
| `Timeout` | attempt or overall timeout | yes |
| `Canceled` | used only inside aggregate reports | no |
| `InvalidResponse` | unparseable success payload or structured-output failure | no |
| `NotSupported` | capability absent (for example Anthropic embeddings) | no |
| `ToolExecutionFailed` | tool handler threw and policy propagates | no |
| `Configuration` | invalid options detected at call time | no |

\* OpenAI's `insufficient_quota` 429 is remapped to `IsTransient = false` — retrying an
exhausted quota cannot help. New codes may be added in minor releases — always default-case
your switches.

## Basic usage

```csharp
using Koras.AI;

try
{
    ChatResponse response = await chat.CompleteAsync(request, ct);
}
catch (OperationCanceledException)
{
    // caller cancelled — not an error
    throw;
}
catch (AiException ex)
{
    switch (ex.Code)
    {
        case AiErrorCode.RateLimited:
            logger.LogWarning("Throttled; retry after {Delay}", ex.RetryAfter);
            break;
        case AiErrorCode.ContentFiltered:
            // show a safe message to the user
            break;
        case AiErrorCode.Authentication or AiErrorCode.Configuration:
            // operator problem — alert, do not retry
            break;
        default:
            logger.LogError(ex, "AI call failed: {Code} ({Provider}, request {RequestId})",
                ex.Code, ex.Provider, ex.RequestId);
            break;
    }
}
```

## AiException shape

| Member | Meaning |
|---|---|
| `Code` | the taxonomy value |
| `Provider` | `"openai"`, `"anthropic"`, ... or null pre-dispatch |
| `StatusCode` | HTTP status when applicable |
| `RetryAfter` | parsed `Retry-After` / provider hint |
| `IsTransient` | drives [retry](resilience.md) and [fallback](provider-fallback.md); defaults from `Code`, overridable per instance |
| `ProviderErrorBody` | raw provider error payload, truncated to 4 KB, scrubbed of credentials |
| `RequestId` | provider request id for support tickets |

The triggering exception is always preserved as `InnerException`. Fallback exhaustion throws
the last `AiException` with an `AggregateException` of every attempt inside.

## Dependency-injection usage

Nothing to register — the model is intrinsic. Configuration errors are mostly caught earlier:
provider registrations validate options with `ValidateOnStart`, so a missing API key fails at
startup, while call-time issues (for example no model resolved) throw
`AiErrorCode.Configuration`.

## Cancellation

Rule 1 of the model: cancellation is not an error. Catch `OperationCanceledException`
separately and before `AiException`-based handling. `AiErrorCode.Canceled` appears only inside
aggregate reports (fallback exhaustion), never as a directly thrown code.

## Logging and telemetry

Terminal failures log at `Error` with code, status, and request id — never content or keys.
Failed operations tag spans with `error.type` (the lowercase `AiErrorCode`) and appear in the
`koras.ai.client.operation.duration` histogram with the same tag. See [telemetry](telemetry.md).

## Security considerations

`ProviderErrorBody` is scrubbed of credentials and truncated, but may still contain fragments
of your prompt echoed by the provider — log it internally, never render it to end users.

## Thread safety

`AiException` instances are immutable after construction; the model imposes no shared state.

## Testing applications using this feature

Throw `AiException` from fakes to drive your handling paths:

```csharp
throw new AiException("simulated throttle", AiErrorCode.RateLimited)
{
    Provider = "fake",
    StatusCode = 429,
    RetryAfter = TimeSpan.FromSeconds(2),
};
```

Assert that transient codes trigger your retry/fallback expectations and that
`OperationCanceledException` is not swallowed as a failure.

## Common mistakes

- Catching `Exception` and treating cancellation as failure — handle
  `OperationCanceledException` first.
- Switching on `StatusCode` instead of `Code`; status codes are provider-flavored diagnostics.
- Writing exhaustive switches without a default case; the enum grows in minor releases.
- Retrying on `Code == RateLimited` without consulting `IsTransient` (quota-exhausted 429s are
  not transient).

## Related features

- [Resilience](resilience.md)
- [Provider fallback](provider-fallback.md)
- [Telemetry](telemetry.md)
- [../architecture/error-model.md](../architecture/error-model.md)
