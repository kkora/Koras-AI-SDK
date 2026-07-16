# Feature Future Roadmap

Expanded records for deferred features (classification: 1.1 / 1.2 / 2.0 / Experimental).

## F-020 Multimodal content parts — 1.1

`ChatMessage` gains `IReadOnlyList<AiContent>` (text, image-url, image-bytes) alongside the
`Text` convenience property (which concatenates text parts). Wire mappings per provider.
Binary content size limits and content-type validation are the security focus. Requires no
breaking change: `Text`-only messages remain the 90% path.

## F-021 Conversation memory — 1.1 (`Koras.AI.Memory`)

`IChatHistoryStore` (in-memory, distributed-cache implementations), token-budget trimming
strategies (`SlidingWindow`, `Summarizing` — the latter uses a model call, explicitly opt-in),
`WithMemory(conversationId)` decorator. Risks: unbounded storage growth → retention options
mandatory.

## F-022 Microsoft.Extensions.AI bridge — 1.1 (`Koras.AI.MicrosoftExtensionsAI`)

`AsMicrosoftChatClient()` / `AsKorasChatClient()` adapters. Keeps Koras.AI relevant regardless
of which abstraction a consumer's other dependencies pick. Contract-test both directions.

## F-023 Cost estimation — 1.1

Pricing table as embedded + user-overridable data (`CostOptions.PricePerMillionTokens`), cost
tags on activities/metrics. Never presented as billing truth — estimation only.

## F-024 RAG abstractions — 1.2 (`Koras.AI.Rag`)

`IRetriever`, `TextChunker`, citation models. No vector-store implementations — integration
guides for Qdrant/pgvector/Azure AI Search instead.

## F-025 Semantic caching — 1.2 (`Koras.AI.Caching`)

Embedding-similarity lookup with threshold + TTL; explicit opt-in per client; cache key
excludes/normalizes volatile fields. Privacy note: cached completions are data at rest →
document encryption expectations.

## F-026 Provider load balancing — 1.2

`ai.AddLoadBalanced("pool", strategy, "a", "b")` with round-robin/weighted/least-latency
strategies; composes with fallback.

## F-027 Agent orchestration — 2.0 (`Koras.AI.Agents`)

Multi-step planning over the tool loop. Gate: demonstrated 1.x demand; otherwise remains out.

## F-028 Content moderation hooks — 2.0

Pre/post pipeline stages with pluggable moderators; ships with abstain-by-default behavior.

## F-029 Public middleware pipeline — 2.0

Only if the decorator model (`DelegatingChatClient`) proves insufficient for consumers; today
decorators + DI cover the known cases with less API surface.

## Experimental parking lot

Request signing, idempotency keys, batch APIs, offline queueing, MAUI sample, source-generated
tool schemas (AOT), provider control-plane operations.
