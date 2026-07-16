# User Personas

## Persona 1 — Priya, senior ASP.NET Core developer (primary)

- Builds line-of-business web APIs; AI features are one requirement among many.
- Wants: `AddKorasAI()` in `Program.cs`, options bound from `appsettings.json`, IntelliSense
  that answers questions before the docs do.
- Fears: vendor lock-in, fragile hand-rolled HTTP code, secrets leaking into logs.
- Success: streaming chat endpoint in production in a day, swap to Azure OpenAI later with a
  config change.

## Persona 2 — Marcus, SaaS founder / full-stack developer

- Small team, moves fast, cost-sensitive; prototypes on Ollama, ships on whichever hosted
  provider is cheapest this quarter.
- Wants: minimal ceremony, copy-paste-ready examples, one API for all providers.
- Fears: rewriting the integration every time pricing changes.
- Success: provider migration is an evening, not a sprint.

## Persona 3 — Ingrid, enterprise platform engineer

- Curates the "golden path" for 40 internal teams; reviews every package for security and
  operability before approving it.
- Wants: startup config validation, health checks, OTel-native metrics/tracing, a threat model
  document, signed deterministic builds, a clear support and versioning policy.
- Fears: packages with heavy transitive dependency trees, silent breaking changes, secrets in
  connection strings.
- Success: Koras.AI passes platform review and becomes the sanctioned AI client.

## Persona 4 — Tomás, AI engineer

- Deep in prompts, tools, and evaluation; pushes the SDK's advanced surface (tool loops,
  structured output, custom providers).
- Wants: full control of request parameters, escape hatches (`AdditionalProperties`, raw
  response access), a documented custom-provider base class.
- Fears: leaky abstractions that block provider-specific features.
- Success: implements an in-house provider against `Koras.AI.Abstractions` in an afternoon.

## Persona 5 — Dana, library author

- Publishes NuGet packages that need "a chat model" without dictating one.
- Wants: a tiny, stable abstractions package safe to depend on for years.
- Fears: abstraction churn forcing major-version bumps.
- Success: depends only on `Koras.AI.Abstractions` with a `>= 1.0.0` range.
