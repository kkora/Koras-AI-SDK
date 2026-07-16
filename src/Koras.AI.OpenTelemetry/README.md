# Koras.AI.OpenTelemetry

OpenTelemetry integration for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk).
The SDK core already emits `ActivitySource`/`Meter` signals following the OpenTelemetry GenAI
semantic conventions; this package wires them into your OTel pipeline:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddKorasAI())
    .WithMetrics(m => m.AddKorasAI());
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/telemetry.md
