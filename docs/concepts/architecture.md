# Architecture

This is the short, consumer-facing view. The authoritative deep dive lives in
[architecture/overview.md](../architecture/overview.md), with
[diagrams](../architecture/diagrams.md), [package boundaries](../architecture/package-boundaries.md),
[dependency rules](../architecture/dependency-rules.md), and the
[decision records](../architecture/decision-records/) behind each choice.

## Packages

Dependencies point inward: everything depends on `Koras.AI.Abstractions`, which depends on
nothing beyond the BCL. Providers never reference each other (Azure OpenAI reusing the
OpenAI wire protocol is the single sanctioned exception).

| Layer | Packages |
|---|---|
| Contracts | `Koras.AI.Abstractions` |
| Core | `Koras.AI` |
| Providers | `Koras.AI.OpenAI`, `.AzureOpenAI`, `.Anthropic`, `.Gemini`, `.Ollama` |
| Integrations | `Koras.AI.AspNetCore`, `Koras.AI.OpenTelemetry` |

## The decorator pipeline

The `IChatClient` you resolve from DI is not a bare provider client — it is a chain of
decorators built by the client factory. Each decorator does one job and delegates the rest
(the `DelegatingHandler` pattern, applied to chat):

```
caller
  │
  ▼
Telemetry        spans + metrics (always outermost, added by the SDK)
  ▼
Logging          start/complete/fail log records (added by the SDK)
  ▼
ToolLoop         auto-executes tool calls           (opt-in: UseToolInvocation)
  ▼
Retry            transient-failure retry + backoff  (opt-in: UseRetry)
  ▼
Fallback         provider failover                  (opt-in: AddFallback)
  ▼
Provider client  HTTP ↔ OpenAI / Anthropic / Gemini / Ollama / Azure
```

Why this order matters:

- **Telemetry and logging observe everything**, including every retry and failover, because
  they sit outside the resilience decorators.
- **Retry wraps the provider call**, so each attempt is an independent HTTP request with its
  own per-attempt timeout.
- **The tool loop sits above retry**, so each model round-trip in the loop benefits from
  retry individually.
- **Fallback is innermost of the resilience pair** (it is itself a named client composed of
  fully built candidates), so a failover target gets its own retry budget.

Opt-in decorators you register with `Use*` are applied in registration order, innermost
first; per-client decorators (`KorasAiClientBuilder.Use`) sit inside global ones. Custom
decorators are ordinary `DelegatingChatClient` subclasses — see the
[advanced DI guide](../guides/dependency-injection.md).

## Streaming through the pipeline

Streaming flows through the same chain. Decorators that cannot re-enter a live stream —
retry and fallback — act only in the window **before the first update is emitted**; once
data has been observed, replaying could duplicate output, so mid-stream failures propagate.
The telemetry span for a streamed call ends when the stream completes.

## What the factory adds for you

When `IChatClientFactory` builds a named client it always wraps the result in the logging
and telemetry decorators (when an `ILoggerFactory` is available) and caches the finished
chain, so every resolution of the same name returns the same singleton instance — details in
[lifecycle](lifecycle.md).
