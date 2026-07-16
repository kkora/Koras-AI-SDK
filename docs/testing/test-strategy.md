# Test Strategy

The pyramid as actually implemented in `tests/` — three projects, three altitudes, all run by
`dotnet test` on every push (see [`test.yml`](../../.github/workflows/test.yml)).

## The pyramid

```
        8  integration tests   — in-process Kestrel, real wire formats
       16  architecture tests  — dependency rules + public API snapshots
      207  unit tests × 3 TFMs — behavior of every component in isolation
```

### Unit tests — `tests/Koras.AI.UnitTests` (207 tests, run on net8.0, net9.0, and net10.0)

Fast, deterministic, no I/O, no real clocks. Coverage by area:

| Area | What is exercised |
|---|---|
| Models | `ChatMessage`/`ChatRequest`/`ChatResponse` invariants, `AiException` shape and `IsTransient` semantics |
| Tools | `AiTool.Create` binding, `AiJsonSchema` generation, the bounded tool-invocation loop |
| Schema & templates | Schema output for records/nullability/collections; `PromptTemplate` parse/render, missing-value behavior |
| Retry | `RetryChatClient` against `ManualTimeProvider` (`TestInfrastructure/ManualTimeProvider.cs`) — backoff growth, jitter bounds, `Retry-After` precedence, per-attempt timeout, streaming first-update rule — no real delays, no flakiness |
| Fallback | Failover eligibility, exhaustion aggregation, last-exception semantics |
| Tool loop | Iteration bounding, tool-call round-trips, handler failure policy |
| DI | `AddKorasAI` registration, named clients, options validation (including HTTPS/ApiKey rules failing at startup) |
| Telemetry | Asserted via real `ActivityListener`/`MeterListener` subscriptions — spans, tags, and instruments observed, not mocked |
| Provider wire mapping | Per-provider request serialization and response/error mapping against recorded wire fixtures via `FakeHttpMessageHandler`; `SseReaderTests` for SSE framing edge cases |

### Shared provider contract suite

`Providers/ProviderContractTests.cs` runs one suite of behavioral assertions against **every**
provider adapter: error-code normalization, `Retry-After` surfacing, request-id capture,
secret-free diagnostics. A new provider gets the whole contract by being added to the theory
data — conformance is not re-invented per provider.

### Architecture tests — `tests/Koras.AI.ArchitectureTests` (16 tests)

- `DependencyRuleTests` — NetArchTest assertions of the
  [dependency direction table](../architecture/dependency-rules.md).
- `PublicApiSurfaceTests` — reflection-based snapshots of every shipped assembly's public
  surface under `PublicApi/*.approved.txt`; any change fails until deliberately regenerated
  with `UPDATE_PUBLIC_API=1` (see [compatibility-testing.md](compatibility-testing.md)).

### Integration tests — `tests/Koras.AI.IntegrationTests` (8 tests)

Full-stack: real DI container, real `HttpClientFactory`, real Kestrel server
(`FakeProviderServer`) speaking the providers' actual wire formats — real SSE framing for
OpenAI-style streaming, real JSON-lines for Ollama. Details in
[integration-testing.md](integration-testing.md).

## Required categories per feature

From the [definition of done](../planning/definition-of-done.md), every feature must ship
with:

- Unit tests for: happy path, invalid input, boundary, failure paths, cancellation — plus
  thread-safety where the type is documented singleton-safe.
- Integration/contract tests whenever the feature touches a provider or the full pipeline.
- Errors asserted to surface as `AiException` with the correct `AiErrorCode`, and secrets
  asserted absent from diagnostic output.

## Coverage philosophy

Coverage is collected in CI (Cobertura artifacts) but **no percentage gate exists, on
purpose**. The bar is behavioral: every branch that changes observable behavior — each
`AiErrorCode` mapping, each retry decision, each validation rule — has a test that would fail
if the behavior changed. A high line-coverage number that misses the `Retry-After`-exceeds-
`MaxDelay` branch is worth less than a low one that pins it. Review asks "which behavior is
untested?", not "what is the number?".

## See also

- [Test matrix](test-matrix.md) — feature × test-type map with class names
- [Integration testing](integration-testing.md) · [Compatibility testing](compatibility-testing.md) · [Performance testing](performance-testing.md)
