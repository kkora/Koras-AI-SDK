# Positioning, Naming, and Adoption Strategy

## Package positioning

"**The provider-neutral AI client for .NET** — one API for OpenAI, Azure OpenAI, Anthropic,
Gemini, and Ollama, with enterprise-grade resilience and observability built in."

Positioned *below* Semantic Kernel (no orchestration) and *beside/above*
`Microsoft.Extensions.AI` (same layer, more batteries included).

## Naming assessment

- **Package name `Koras.AI`** — short, brandable, category-obvious. Verified no conflicting
  `Koras.*` packages of note on NuGet. Risk: two-letter TLD-style suffix ".AI" is generic; the
  `Koras.` prefix (reserved-prefix application recommended on NuGet.org) provides the identity.
- **Root namespace `Koras.AI`** — matches package IDs 1:1; no clash with `System.*`,
  `Microsoft.*`. DI extension methods live in `Microsoft.Extensions.DependencyInjection`
  by ecosystem convention (see ADR-0005).
- **NuGet IDs** — `Koras.AI.Abstractions`, `Koras.AI` (core), `Koras.AI.{Provider}`,
  `Koras.AI.AspNetCore`, `Koras.AI.OpenTelemetry`. Suffix pattern mirrors
  `Microsoft.Extensions.*`, instantly legible to .NET developers.
- **Action item:** register the `Koras.` reserved prefix on NuGet.org under the Koras
  Technologies account before 1.0.

## Adoption strategy

1. **Five-minute success**: README quick start that runs against Ollama (no API key needed).
2. **Meet developers where they are**: samples for Console, Web API, Minimal API, Worker
   Service; docs answers phrased for search ("switch openai to anthropic .net").
3. **Trust artifacts first**: CI badges, coverage, CodeQL, threat model, deterministic builds —
   the platform-engineer checklist is the marketing page.
4. **Escape-hatch honesty**: document exactly what the abstraction cannot express and how
   `AdditionalProperties` covers it — this defuses the "leaky abstraction" objection.
5. **Interoperate, don't fight**: the planned Microsoft.Extensions.AI bridge makes trying
   Koras.AI a low-commitment decision.

## Open-source strategy

- MIT license; development in the open at `github.com/korastechnologies/koras-ai-sdk`.
- All planning documents (this docs tree) public — architecture decisions are auditable.
- Semantic versioning with a published support policy; security disclosures via SECURITY.md.

## Community contribution strategy

- `CONTRIBUTING.md` with a tiered on-ramp: docs fixes → provider fixtures → new providers.
- Provider contributions are the ideal community shape: the `Koras.AI.Providers` base classes
  and the provider test-fixture pattern make a new provider a well-bounded PR.
- Issue templates route bugs vs. feature requests vs. docs; ROADMAP.md sets expectations.
- Maintainer review gate: public API changes require an ADR and a `PublicAPI.Unshipped.txt` diff.

## Monetization possibilities

None planned for the SDK itself (MIT, free). If ever pursued: paid support contracts or a hosted
usage/cost analytics service that consumes the SDK's OTel output. The package never phones home.

## What would create unnecessary complexity (standing guardrails)

- Supporting `netstandard2.0` (drags polyfills, kills `IAsyncEnumerable`/`required`/STJ features).
- Wrapping vendor SDKs instead of REST (dependency weight, version conflicts, leaky types).
- An orchestration DSL, planners, or YAML pipelines.
- Premature abstraction of provider control-plane APIs (files, fine-tuning, batches).
