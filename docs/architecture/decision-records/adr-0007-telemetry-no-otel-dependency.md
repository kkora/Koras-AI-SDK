# ADR-0007: Telemetry via ActivitySource/Meter in core; OTel dependency only in `Koras.AI.OpenTelemetry`

**Status:** Accepted · **Date:** 2026-07-16

## Context
`System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` are the BCL-native
telemetry APIs that OpenTelemetry consumes. Depending on OpenTelemetry packages from core would
force exporter-ecosystem versioning onto every consumer.

## Decision
Core instruments with BCL `ActivitySource("Koras.AI")` and `Meter("Koras.AI")`, following the
OpenTelemetry GenAI semantic conventions for names/tags. `Koras.AI.OpenTelemetry` contains only
`TracerProviderBuilder.AddKorasAI()` / `MeterProviderBuilder.AddKorasAI()` sugar (one
`AddSource`/`AddMeter` call each) and depends on `OpenTelemetry.Api`.

## Consequences
- Zero telemetry dependency cost for consumers who don't use OTel; full fidelity for those who do.
- Sensitive content never recorded by default (`EnableSensitiveData` opt-in, default false).
