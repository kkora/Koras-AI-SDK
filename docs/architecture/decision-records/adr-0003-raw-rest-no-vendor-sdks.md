# ADR-0003: Providers speak raw REST — no vendor SDK dependencies

**Status:** Accepted · **Date:** 2026-07-16

## Context
Official SDKs (OpenAI, Azure.AI.OpenAI, community Anthropic/Gemini clients) would save adapter
code but: (a) drag large, fast-moving dependency trees; (b) risk version conflicts with
consumers using those SDKs directly; (c) tempt leaking vendor types into our API; (d) three of
five providers have no stable first-party .NET SDK anyway.

## Decision
Every provider adapter implements the provider's REST wire protocol directly over
`HttpClient` from `IHttpClientFactory`, with shared plumbing (`ProviderChatClient`, `SseReader`,
`JsonLinesReader`, error helpers) in `Koras.AI`'s `Koras.AI.Providers` namespace.
`Koras.AI.AzureOpenAI` reuses `Koras.AI.OpenAI`'s wire mapping (same protocol, different
endpoint/auth) — the single provider→provider reference allowed.

## Consequences
- Dependency closure stays tiny and auditable; no vendor types anywhere in the public API.
- We own wire-format tracking: mitigated by recorded-fixture contract tests per provider and a
  pinned, configurable API version per adapter.
- Provider control-plane features (files, fine-tunes) are simply not wrapped — in line with scope.
