# Package Boundaries

## Why multiple packages

A single package would force every consumer to carry every provider and integration dependency.
The split follows one rule: **a package boundary exists where a dependency boundary exists.**

- `Koras.AI.Abstractions` — the *contract*. Referenced by libraries and application layers that
  must not know about providers. Changes here are the most expensive; the package is kept
  minimal and additive-only after 1.0.
- `Koras.AI` — the *engine*. Depends on Microsoft.Extensions primitives that any host app
  already has. Contains no provider knowledge except the reusable `Koras.AI.Providers` plumbing
  (HTTP base client, SSE reader, error normalization helpers) published deliberately so
  third-party providers get the same quality of plumbing as first-party ones.
- `Koras.AI.{Provider}` — one per provider, so `dotnet add package Koras.AI.Anthropic` is the
  entire cost of Anthropic support. No provider references another (exception: AzureOpenAI →
  OpenAI, which shares a wire protocol — ADR-0003).
- `Koras.AI.AspNetCore` — carries the ASP.NET Core health-check dependency; console/worker apps
  never need it.
- `Koras.AI.OpenTelemetry` — carries the `OpenTelemetry.Api` dependency; core emits
  `ActivitySource`/`Meter` signals without it (ADR-0007).

## What belongs where (decision table)

| If a type… | It goes in |
|---|---|
| appears in application method signatures | Abstractions |
| is a decorator, builder, factory, or template engine | Koras.AI |
| mentions a provider name or wire format | that provider's package |
| references ASP.NET Core | Koras.AI.AspNetCore |
| references OpenTelemetry packages | Koras.AI.OpenTelemetry |

## Explicitly rejected packages

- `Koras.AI.DependencyInjection` — rejected. The DI abstractions are ~40 KB and every modern
  host has them; a separate package doubles install steps for zero benefit (ADR-0005).
- `Koras.AI.Core` as a PackageId — rejected in favor of plain `Koras.AI` (the "obvious install"
  should be the real package; `.Core` suffixes confuse).

## Versioning across the family

All packages version together (single `VersionPrefix` in `Directory.Build.props`) and release
together. A fix in one provider ships a patch release of the whole family — predictability over
minimal diffs.
