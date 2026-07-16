# Resilience (Retry and Timeout)

## Overview

`ai.UseRetry()` wraps every registered chat client in a retry decorator. Only failures whose
`AiException.IsTransient` is true are retried — by default `RateLimited`,
`ProviderUnavailable`, `Network`, and `Timeout`. Backoff is exponential with full jitter,
capped by `MaxDelay`, and a provider-sent `Retry-After` hint overrides the computed delay when
`HonorRetryAfter` is enabled. Each attempt also gets its own timeout (`AttemptTimeout`), and
timeouts count as transient failures.

## When to use it

Enable retry in virtually every production configuration — transient 429s and 5xxs are a fact
of life with hosted models. Combine with [provider fallback](provider-fallback.md) for
failures that outlast the retry budget.

## When not to use it

Skip or shorten retry for latency-critical interactive paths where failing fast to a fallback
UI beats waiting out backoff, and in tests where you want failures to surface immediately.

## Required packages

- `Koras.AI` (the decorator is part of the core package).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
    ai.UseRetry(); // defaults: 3 attempts, 1s base, 30s cap, 100s per attempt
});
```

## Advanced configuration

```csharp
ai.UseRetry(r =>
{
    r.MaxAttempts = 5;                              // total attempts including the first
    r.BaseDelay = TimeSpan.FromMilliseconds(500);   // attempt n waits up to BaseDelay * 2^(n-1), jittered
    r.MaxDelay = TimeSpan.FromSeconds(20);          // cap on any computed delay
    r.AttemptTimeout = TimeSpan.FromSeconds(30);    // per-attempt timeout, surfaces as Timeout
    r.HonorRetryAfter = true;                       // provider Retry-After overrides backoff
});
```

`RetryOptions` validates its setters: `MaxAttempts >= 1`, non-negative delays, positive
`AttemptTimeout`.

## Public API

- `KorasAiBuilder.UseRetry(Action<RetryOptions>? configure = null)`
- `RetryOptions` — `MaxAttempts`, `BaseDelay`, `MaxDelay`, `AttemptTimeout`, `HonorRetryAfter`

## Execution lifecycle

Global decorators registered with `Use*` apply to every chat client in registration order,
innermost first — register `UseRetry()` before other `Use(...)` decorators you want outside
it. Retry consults only `AiException.IsTransient`, never raw status codes, so
[custom providers](custom-providers.md) integrate by mapping their errors correctly.

## Streaming behavior

A stream is retried only if it fails **before the first update is emitted**. After output has
started, a mid-stream failure propagates to the consumer; replaying would duplicate delivered
content. See [streaming](streaming.md).

## Error handling

When attempts are exhausted, the last `AiException` propagates unchanged. Non-transient
failures (`Authentication`, `InvalidRequest`, `ModelNotFound`, ...) are never retried. Note
the OpenAI nuance: a 429 caused by `insufficient_quota` is marked `IsTransient = false` —
retrying an exhausted quota cannot help. See [error handling](error-handling.md).

## Retry and timeout behavior

Two timeout layers exist: `RetryOptions.AttemptTimeout` (per attempt, retried) and
`HttpClient.Timeout` (the named `HttpClient` the provider uses; surfaces as
`AiErrorCode.Timeout`). Keep `AttemptTimeout` at or below the HTTP timeout so retries, not the
transport, own the deadline.

## Cancellation

The caller's `CancellationToken` is observed between and during attempts; cancellation
surfaces as `OperationCanceledException` immediately, never as another retry.

## Logging

Each scheduled retry logs a `Warning` with the attempt number, delay, and error code (logger
category `Koras.AI.*`). Terminal failures log `Error`.

## Telemetry

Every retry increments the `koras.ai.client.retries` counter (tags: provider, model, client,
`error.type`). Watch it to detect degraded providers before users do. See
[telemetry](telemetry.md).

## Security considerations

Retries re-send the full request, including prompt content, to the same provider. Nothing is
persisted by the decorator, and diagnostic payloads on exceptions remain scrubbed of
credentials.

## Performance considerations

Worst-case added latency is roughly the sum of the jittered delays plus attempts times
`AttemptTimeout`. With defaults that can exceed five minutes — tune `MaxAttempts` and
`AttemptTimeout` for interactive paths.

## Thread safety

The retry decorator is stateless per call and thread-safe; the same singleton client serves
concurrent requests.

## Testing applications using this feature

Inject a fake `TimeProvider` into DI (the decorator resolves `TimeProvider` from the
container) to make backoff instantaneous and deterministic. Then script a client that throws a
transient `AiException` a fixed number of times:

```csharp
services.AddSingleton<TimeProvider>(new Microsoft.Extensions.Time.Testing.FakeTimeProvider());
// fake client: throw new AiException("boom", AiErrorCode.ProviderUnavailable) on first call, succeed on second
```

Assert the operation succeeds after N failures and that non-transient codes are not retried.

## Common mistakes

- Retrying everything by expecting status-code checks — the decorator honors only
  `IsTransient`; a custom `AiException` with the wrong code will not be retried.
- Stacking aggressive `MaxAttempts` on top of a fallback chain, multiplying total attempts.
- Leaving default `AttemptTimeout` (100 s) on user-facing endpoints.
- Expecting mid-stream retry; streams are only retried before first output.

## Related features

- [Provider fallback](provider-fallback.md)
- [Error handling](error-handling.md)
- [Telemetry](telemetry.md)
- [Dependency injection](dependency-injection.md)
