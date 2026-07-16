# Observability

Core emits logs, traces, and metrics with **zero OpenTelemetry package dependency** (ADR-0007).
`Koras.AI.OpenTelemetry` only wires the sources into an OTel pipeline.

## Tracing — `ActivitySource("Koras.AI")`

One activity per operation, named per OTel GenAI semantic conventions:

| Activity | When |
|---|---|
| `chat {model}` | CompleteAsync / StreamAsync (streaming span ends when the stream completes) |
| `embeddings {model}` | GenerateAsync |
| `execute_tool {tool}` | each tool-loop invocation |

Tags: `gen_ai.operation.name`, `gen_ai.system` (provider), `gen_ai.request.model`,
`gen_ai.response.model`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`,
`gen_ai.response.finish_reasons`, `error.type` (the `AiErrorCode`, lowercase), and
`koras.ai.client.name` (the registered client name). Fallback failovers add an activity event
`koras.ai.fallback` with source/target client names.

Message content is **never** put on spans by default. `KorasAiTelemetryOptions.
EnableSensitiveData` (default `false`) gates content capture for development.

## Metrics — `Meter("Koras.AI")`

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `koras.ai.client.operation.duration` | Histogram | s | operation, provider, model, client, error.type |
| `koras.ai.client.token.usage` | Counter | {token} | direction (`input`/`output`), provider, model, client |
| `koras.ai.client.fallbacks` | Counter | {fallback} | from, to, error.type |
| `koras.ai.client.retries` | Counter | {retry} | provider, model, client, error.type |

These four instruments answer the operations questions that matter: latency percentiles, token
spend (cost proxy), and resilience activity — per provider, model, and named client.

## Logging — `ILogger` categories `Koras.AI.*`

`LoggerMessage`-source-generated, high-performance, structured:

- `Information`: operation completed (provider, model, duration, tokens) / failover occurred.
- `Warning`: retry scheduled (attempt, delay, code), health probe degraded.
- `Error`: operation failed terminally (code, status, requestId — never content or keys).
- `Trace` + `EnableSensitiveData=true`: request/response content for local debugging only.

## OTel wiring (`Koras.AI.OpenTelemetry`)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddKorasAI())
    .WithMetrics(m => m.AddKorasAI());
```

## Health checks (`Koras.AI.AspNetCore`)

`builder.Services.AddHealthChecks().AddKorasAI()` probes registered providers through their
lightweight probe endpoints (models list / version — never a paid completion by default).
Failure maps to `Unhealthy` for the probed client, `Degraded` when only some named clients fail.
