# Product Roadmap

## 0.1.0-preview.1 → 0.1.0 (MVP)

Everything in [mvp-scope](../features/mvp-scope.md): abstractions, core pipeline, five
providers (OpenAI, Azure OpenAI, Anthropic, Gemini, Ollama), chat, streaming, structured
output, tool calling, embeddings, retry/timeout, fallback, error normalization, DI/options,
logging, telemetry, usage tracking, health checks, prompt templates, samples, docs.

## 0.5.0 — Hardening / API freeze candidate

- Public API review against real-world feedback; last window for breaking changes.
- Performance pass with benchmarks (streaming allocation, serialization).
- Compatibility tests against recorded fixtures for all providers refreshed.
- `PublicAPI.Shipped.txt` baselines frozen.

## 1.0.0 — Stable

- Backward-compatibility promise begins (see [versioning policy](../migration/versioning-policy.md)).
- Supported: net8.0, net9.0, net10.0.

## 1.1

- **Multimodal content parts** (images in, provider-permitting) — the model is designed for it
  (`ChatMessage` content parts), the wire adapters gain the mappings.
- **Conversation memory** package (`Koras.AI.Memory`): pluggable history stores, token-budget
  trimming strategies.
- **Microsoft.Extensions.AI bridge** (`Koras.AI.MicrosoftExtensionsAI`): adapters both ways.
- Cost estimation hooks (pricing tables as data, per-request cost tags).

## 1.2

- **RAG abstractions** (`Koras.AI.Rag`): `IVectorStore`-agnostic retrieval contracts, chunking
  helpers; storage implementations remain third-party.
- **Semantic caching** (`Koras.AI.Caching`): embedding-similarity response cache with pluggable
  distance thresholds.
- Provider load balancing (weighted/round-robin across named clients).

## 2.0

- **Agent orchestration** (`Koras.AI.Agents`), only if 1.x adoption demonstrates demand —
  middleware pipeline generalization, moderation hooks, multi-step tool planning.
- Re-evaluate target frameworks (drop net8.0 when out of support).

## Experimental / parking lot

- Content moderation hooks, request signing, batch APIs, provider control-plane operations
  (files, fine-tunes), MAUI-focused samples.

## Out of scope (standing)

- Vector databases, prompt IDEs, evaluation platforms, model hosting.
