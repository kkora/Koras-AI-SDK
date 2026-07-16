# Provider Fallback

## Overview

`ai.AddFallback(name, params string[] clientNames)` registers a named chat client that tries
an ordered list of previously registered clients and fails over when a candidate throws a
transient `AiException`. Terminal errors — `Authentication`, `InvalidRequest` — propagate
immediately. When every candidate fails, the last `AiException` is rethrown with an
`AggregateException` of all attempts as its inner exception. The decorator behind it is
`FallbackChatClient`, which you can also construct directly with a custom failover predicate.

## When to use it

Use fallback for availability: primary hosted provider with a secondary provider or a local
[Ollama](provider-ollama.md) model as the safety net, or two accounts/regions of the same
provider.

## When not to use it

Do not use fallback to paper over configuration errors (bad keys are terminal and will not
fail over) or when the candidates produce incompatible output quality for your use case.

## Required packages

- `Koras.AI` plus the provider packages for each candidate.

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o => { o.ApiKey = cfg["OpenAI:ApiKey"]; o.DefaultModel = "gpt-4o-mini"; });
    ai.AddAnthropic(o => { o.ApiKey = cfg["Anthropic:ApiKey"]; o.DefaultModel = "claude-sonnet-4-5"; });

    ai.AddFallback("resilient", "openai", "anthropic").AsDefault();
    ai.UseRetry();
});
```

`"resilient"` is a normal named client: `.AsDefault()` makes it the injected `IChatClient`,
and `factory.GetChatClient("resilient")` resolves it by name. A fallback client cannot list
itself as a candidate, and at least one candidate name is required.

## Advanced configuration

For a custom failover policy, construct `FallbackChatClient` yourself via `AddClient`:

```csharp
ai.AddClient("custom_fallback", sp =>
{
    var factory = sp.GetRequiredService<IChatClientFactory>();
    return new FallbackChatClient(
        [factory.GetChatClient("openai"), factory.GetChatClient("anthropic")],
        shouldFailover: ex => ex.IsTransient || ex.Code == AiErrorCode.ContentFiltered,
        sp.GetService<Microsoft.Extensions.Logging.ILogger<FallbackChatClient>>());
});
```

The default predicate is `ex => ex.IsTransient`.

## Dependency-injection usage

Inject the default `IChatClient` (when the fallback is `AsDefault()`) or resolve by name.
Because candidates are resolved through `IChatClientFactory`, each candidate keeps its own
per-client decorators, and global decorators wrap the fallback client itself.

## Execution lifecycle

`CompleteAsync`: candidates run in order; a failover is logged and recorded, then the next
candidate receives the identical request. `StreamAsync`: failover is possible only until the
first update has been emitted — once streaming output has started, failures propagate. Note
that `ChatRequest.Model` is passed to whichever candidate runs; leave it null so each
provider's own `DefaultModel` applies.

## Error handling

- Failover-eligible failure, more candidates left: try next candidate.
- Terminal failure (per predicate): rethrow immediately.
- All candidates failed: `AiException` with message
  `"All {n} fallback candidates failed. Last error: ..."`, the last error's `Code`,
  `Provider`, `StatusCode`, and `RetryAfter`, and an `AggregateException` of every attempt as
  `InnerException`.

See [error handling](error-handling.md).

## Retry and timeout behavior

Combine with `ai.UseRetry()` deliberately: as a global decorator, retry wraps the fallback
client (and each candidate resolved through the factory also gets globally decorated).
Multiplied attempts can add significant latency — keep `MaxAttempts` modest when chaining.

## Cancellation

The shared `CancellationToken` flows into every candidate; cancellation stops the chain with
`OperationCanceledException`.

## Logging

Each failover logs a `Warning` (event `KorasAiFallback`, id 3001):
`"Koras.AI fallback: provider {FromProvider} failing over to {ToProvider} after {ErrorCode}"`.

## Telemetry

Failovers increment `koras.ai.client.fallbacks` (tags: `from`, `to`, `error.type`) and add a
`koras.ai.fallback` activity event. Alert on this counter — steady failovers mean your primary
is unhealthy. See [telemetry](telemetry.md).

## Security considerations

A failover sends the full conversation to a different provider. Confirm that every candidate
is approved for the data being processed (compliance, data residency) before chaining them.

## Thread safety

`FallbackChatClient` is stateless per call and thread-safe for singleton use.

## Testing applications using this feature

Compose the client directly with fakes:

```csharp
var failing = new ThrowingClient(new AiException("down", AiErrorCode.ProviderUnavailable));
var healthy = new FakeChatClient("recovered");
var fallback = new FallbackChatClient([failing, healthy]);

ChatResponse response = await fallback.CompleteAsync(ChatRequest.FromPrompt("hi"));
Assert.Equal("recovered", response.Text);
```

Also test the exhaustion path and assert on the `AggregateException` contents.

## Common mistakes

- Listing candidates that were never registered — resolution fails at first use with an
  `InvalidOperationException` naming the registered clients.
- Setting an explicit `ChatRequest.Model` that only the primary provider knows; the secondary
  then fails with `ModelNotFound`.
- Expecting failover on `Authentication` errors; they are terminal by default.
- Assuming mid-stream failover; it only happens before the first streamed update.

## Related features

- [Resilience](resilience.md)
- [Error handling](error-handling.md)
- [Health checks](health-checks.md)
- [Dependency injection](dependency-injection.md)
