# Competitive Analysis

## Existing NuGet alternatives

| Package | What it is | Strengths | Gaps Koras.AI addresses |
|---|---|---|---|
| `Microsoft.Extensions.AI` (+ `.Abstractions`) | Microsoft's provider-neutral AI abstraction | First-party, ecosystem gravity, middleware model | Providers ship separately with uneven coverage; resilience/fallback and provider-error normalization left to the consumer; GenAI telemetry partially DIY |
| `Microsoft.SemanticKernel` | Orchestration framework (agents, planners, plugins, memory) | Rich agent scenarios, first-party | Heavy dependency graph and concept count for teams that just need a client; API churn history; overkill for UC-01..UC-06 |
| `OpenAI` (official) | OpenAI's .NET SDK | Complete OpenAI surface | Single vendor; its types in your code *are* the lock-in |
| `Azure.AI.OpenAI` | Azure's OpenAI client | Entra ID auth, Azure ecosystem | Azure/OpenAI only |
| `Anthropic.SDK` (community) | Claude client | Covers Anthropic API | Single vendor, community maintenance risk |
| `LangChain` (.NET port) | Port of Python LangChain | Familiar to Python migrants | Port-lag, large surface, abstraction style foreign to idiomatic .NET |
| `OllamaSharp` | Ollama client | Good local-model coverage | Single backend |
| `Betalgo.OpenAI`, `LLMSharp`, etc. | Community clients | Various | Single vendor each, inconsistent quality |

## The honest question: why not just Microsoft.Extensions.AI?

`Microsoft.Extensions.AI` is the most serious alternative and the closest in philosophy. Koras.AI
competes on the *productized whole*, not the abstraction alone:

1. **One family, uniform quality.** Five provider packages maintained together, released
   together, tested against recorded wire fixtures together. With M.E.AI, providers come from
   different owners with different gaps.
2. **Resilience and fallback in the box.** Bounded retry honoring `Retry-After`, per-attempt
   timeouts, and a declarative provider fallback chain are first-class, not an exercise.
3. **Normalized error taxonomy.** One `AiException` with a documented `AiErrorCode` per failure
   class across all providers — the single feature that most reduces production incident time.
4. **No vendor SDKs underneath.** Adapters speak REST directly; the transitive closure stays
   small and auditable (Ingrid's requirement).
5. **Enterprise paperwork included.** Threat model, secure-configuration guide, health checks,
   deterministic signed builds, documented versioning policy.

**Risk acknowledged:** if M.E.AI's provider ecosystem matures to uniform quality, Koras.AI's
abstraction layer loses differentiation. Mitigation: keep `Koras.AI.Abstractions` small enough
that an M.E.AI *bridge package* (`Koras.AI.MicrosoftExtensionsAI`, roadmap v1.x) is cheap —
interoperate rather than fight.

## What would prevent adoption

- Distrust of a new publisher → countered by tests, docs, deterministic builds, CodeQL, and
  visible CI.
- "Yet another abstraction" fatigue → countered by the bridge-package strategy and a tiny
  abstractions package.
- Missing provider feature X → countered by `AdditionalProperties` escape hatches and raw
  response access.
- Breaking changes → countered by the public-API compatibility gate and semver policy.

## What we deliberately do not build

- Agent planners/orchestrators, vector stores, evaluation harnesses, prompt IDEs — adjacent
  markets with strong incumbents; entering them would dilute the core promise.
