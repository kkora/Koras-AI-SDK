# Telemetry

## Overview

Koras.AI emits traces, metrics, and structured logs out of the box, with zero OpenTelemetry
package dependency in the core. Traces come from `ActivitySource("Koras.AI")` following the
OTel GenAI semantic conventions; metrics from `Meter("Koras.AI")`. The optional
`Koras.AI.OpenTelemetry` package wires both into an OTel pipeline with one-line extensions.
Message content is never captured by default.

## When to use it

Always in production: the four instruments answer latency, cost (token spend), and resilience
questions per provider, model, and named client.

## Required packages

- `Koras.AI` (emission is built in).
- `Koras.AI.OpenTelemetry` for OTel wiring (`AddKorasAI()` on the tracer/meter builders).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o => { o.ApiKey = cfg["OpenAI:ApiKey"]; o.DefaultModel = "gpt-4o-mini"; });
});

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddKorasAI())
    .WithMetrics(m => m.AddKorasAI());
```

Without OpenTelemetry, any `ActivityListener`/`MeterListener` (for example `dotnet-counters`)
can observe the same sources by name: `Koras.AI`.

## Tracing

One activity per operation:

| Activity | When |
|---|---|
| `chat {model}` | `CompleteAsync` / `StreamAsync` (span ends when the stream completes) |
| `embeddings {model}` | `GenerateAsync` |
| `execute_tool {tool}` | each tool-loop invocation |

Tags: `gen_ai.operation.name`, `gen_ai.system` (provider), `gen_ai.request.model`,
`gen_ai.response.model`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`,
`gen_ai.response.finish_reasons`, `error.type` (the lowercase `AiErrorCode`), and
`koras.ai.client.name` (the registered client name). Fallback failovers add a
`koras.ai.fallback` activity event with source/target client names.

## Metrics

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `koras.ai.client.operation.duration` | Histogram | s | operation, provider, model, client, error.type |
| `koras.ai.client.token.usage` | Counter | {token} | direction (`input`/`output`), provider, model, client |
| `koras.ai.client.retries` | Counter | {retry} | provider, model, client, error.type |
| `koras.ai.client.fallbacks` | Counter | {fallback} | from, to, error.type |

## Logging

`ILogger` categories `Koras.AI.*`, source-generated and structured:

- `Information` — operation completed (provider, model, duration, tokens) / failover occurred.
- `Warning` — retry scheduled (attempt, delay, code), health probe degraded.
- `Error` — terminal failure (code, status, requestId — never content or keys).
- `Trace` — request/response content, only when sensitive data is enabled.

## Sensitive data

`KorasAiTelemetryOptions.EnableSensitiveData` (default `false`) gates content capture:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o => { /* ... */ });
    ai.ConfigureTelemetry(t => t.EnableSensitiveData = true); // development only
});
```

When enabled and the logger has `Trace` on, request and response content is logged for local
debugging. Never enable in production — prompts and completions frequently contain personal or
confidential data.

## Dependency-injection usage

Telemetry and logging decorators are applied automatically by the client factory to every
chat and embedding client — no opt-in needed. `ConfigureTelemetry` is the only knob on the
builder.

## Error handling

Failed operations set span status to error, tag `error.type` with the `AiErrorCode`, and land
in the duration histogram with the same tag — errors are first-class dimensions, not gaps.
See [error handling](error-handling.md).

## Cancellation

Cancelled operations end their activities; cancellation is not recorded as a provider error.

## Security considerations

Default emission contains no message content and no credentials: tags carry models, providers,
token counts, and error codes only. Review your OTel exporter pipeline for data residency
before shipping spans off-host, and treat `EnableSensitiveData` as a development-only switch.

## Performance considerations

Instruments are no-ops when nothing listens; overhead with listeners is a few allocations per
operation. Logging uses `LoggerMessage` source generation, so disabled levels cost near zero.

## Thread safety

The shared `ActivitySource` and `Meter` are readonly singletons; all recording paths are
thread-safe.

## Testing applications using this feature

Use `MetricCollector<T>` from `Microsoft.Extensions.Diagnostics.Testing` (or a manual
`MeterListener`) against the meter name `Koras.AI`, and an `ActivityListener` on source
`Koras.AI` to assert spans:

```csharp
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Koras.AI",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
};
System.Diagnostics.ActivitySource.AddActivityListener(listener);
```

## Common mistakes

- Forgetting `AddKorasAI()` on the tracer and meter builders and concluding the SDK emits
  nothing.
- Enabling `EnableSensitiveData` globally "temporarily" and shipping it — it also requires
  `Trace` level, but do not rely on that as a safety net.
- Aggregating `koras.ai.client.token.usage` without the `direction` tag and double-counting
  input plus output.
- Building dashboards on provider-specific span names; use the tags, which are stable.

## Related features

- [Error handling](error-handling.md)
- [Resilience](resilience.md)
- [Health checks](health-checks.md)
- [../architecture/observability.md](../architecture/observability.md)
