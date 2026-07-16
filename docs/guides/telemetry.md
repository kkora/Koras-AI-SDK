# Telemetry Guide

The core SDK emits traces via `ActivitySource("Koras.AI")` and metrics via
`Meter("Koras.AI")` with **no OpenTelemetry dependency** — the signals exist whether or not
you export them. The `Koras.AI.OpenTelemetry` package wires them into an OTel pipeline.
Full signal reference: [observability](../architecture/observability.md).

## Wiring it up

```sh
dotnet add package Koras.AI.OpenTelemetry -v 0.1.0-preview.1
```

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddKorasAI()                      // subscribe to ActivitySource "Koras.AI"
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddKorasAI()                      // subscribe to Meter "Koras.AI"
        .AddOtlpExporter());
```

`AddKorasAI()` on the tracer builder is just `AddSource(KorasAiTelemetry.ActivitySourceName)`;
on the meter builder, `AddMeter(KorasAiTelemetry.MeterName)`. If you prefer not to reference
the package, `AddSource("Koras.AI")` / `AddMeter("Koras.AI")` are equivalent.

## Traces

One activity per operation, named per the OTel GenAI semantic conventions:

| Activity | When |
|---|---|
| `chat {model}` | `CompleteAsync` / `StreamAsync` (a streaming span ends when the stream completes) |
| `embeddings {model}` | `GenerateAsync` |
| `execute_tool {tool}` | each tool invocation inside the tool loop |

Span tags:

| Tag | Example |
|---|---|
| `gen_ai.operation.name` | `chat` |
| `gen_ai.system` | `openai` |
| `gen_ai.request.model` | `gpt-4o-mini` |
| `gen_ai.response.model` | `gpt-4o-mini-2024-07-18` |
| `gen_ai.usage.input_tokens` | `24` |
| `gen_ai.usage.output_tokens` | `57` |
| `gen_ai.response.finish_reasons` | `stop` |
| `error.type` | the `AiErrorCode`, lowercase (e.g. `rate_limited`), on failure |
| `koras.ai.client.name` | the registered client name (e.g. `interactive`) |

Fallback failovers add a span event `koras.ai.fallback` with source/target client names.
Tool-loop spans nest under the chat span, so a multi-tool conversation reads as a tree.

Message content is **never** attached to spans unless
`ai.ConfigureTelemetry(t => t.EnableSensitiveData = true)` — same switch and same warning as
in the [logging guide](logging.md).

## Metrics

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `koras.ai.client.operation.duration` | Histogram | s | operation, provider, model, client, error.type |
| `koras.ai.client.token.usage` | Counter | {token} | direction (`input`/`output`), provider, model, client |
| `koras.ai.client.fallbacks` | Counter | {fallback} | from, to, error.type |
| `koras.ai.client.retries` | Counter | {retry} | provider, model, client, error.type |

Four instruments cover the operational questions that matter:

- **Latency:** percentiles of `operation.duration`, split by provider/model.
- **Spend:** `token.usage` rated by your per-model pricing is a live cost proxy.
- **Resilience:** non-zero `retries` means provider pressure; non-zero `fallbacks` means
  you are running on a secondary.
- **Errors:** the `error.type` tag on `operation.duration` gives failure rates by code.

## .NET Aspire dashboard

Nothing extra is required. Aspire's service defaults already configure OTLP export; adding
`.AddKorasAI()` to the tracing and metrics builders in your `ServiceDefaults` project makes
chat spans, token counters, and retry activity appear in the Aspire dashboard alongside
HTTP and database telemetry — including the span tree for tool-calling conversations.

## Verifying locally

Quick check without an exporter — listen in-process:

```csharp
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "Koras.AI",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = a => Console.WriteLine($"{a.DisplayName}: {a.Duration.TotalMilliseconds:0}ms"),
};
System.Diagnostics.ActivitySource.AddActivityListener(listener);
```

Run a completion and you should see `chat llama3.2: 843ms`.
