# ADR-0005: DI registration lives in the core package; extension methods in `Microsoft.Extensions.DependencyInjection`

**Status:** Accepted · **Date:** 2026-07-16

## Context
The original module recommendation included a separate `Koras.AI.DependencyInjection` package.
`Microsoft.Extensions.DependencyInjection.Abstractions` + `Options` + `Http` are lightweight and
present in every modern host; a separate package would double the install steps for the 100%
case (every consumer uses DI).

## Decision
`AddKorasAI` and the `KorasAiBuilder` ship in `Koras.AI`. `IServiceCollection` extension methods
are declared in the `Microsoft.Extensions.DependencyInjection` namespace (ecosystem convention:
discoverable without a `using`). Builder extension methods (`AddOpenAI`, `UseRetry`) are
declared in `Koras.AI` so provider packages light up via their own namespace.

## Consequences
- One fewer package; simpler install; matches how Polly, OTel, HealthChecks register.
- Core takes Microsoft.Extensions.* dependencies — acceptable, they are the platform.
