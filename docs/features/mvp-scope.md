# MVP Scope (0.1.0)

## In

- **Abstractions:** `IChatClient`, `IEmbeddingClient`, message/request/response/usage models,
  tool models, response formats, `AiException`/`AiErrorCode`, `ChatStreamUpdate`.
- **Core:** decorator pipeline, retry+timeout, fallback client, structured-output extensions,
  tool-invocation loop, prompt templates, DI builder + options validation, logging, telemetry
  (ActivitySource + Meter), usage metrics, provider base classes (HTTP + SSE + error helpers).
- **Providers:** OpenAI, Azure OpenAI, Anthropic, Google Gemini, Ollama — chat, streaming,
  tools, structured output, embeddings per the capability matrix.
- **Integrations:** ASP.NET Core health checks; OpenTelemetry registration package.
- **Quality:** unit/integration/architecture/contract tests, recorded wire fixtures, CI, docs,
  four samples.

## Out (with reasons)

| Item | Reason |
|---|---|
| Multimodal (images/audio) | Model designed for it; wire mapping deferred to 1.1 to keep MVP testable |
| Conversation memory, RAG, semantic caching, agents | Separate packages; core must stabilize first |
| Cost estimation | Pricing data churns; needs a data-update story (1.1) |
| Load balancing | Fallback covers the availability need; balancing is optimization (1.2) |
| Provider control-plane APIs (files, fine-tunes, batches) | Different abstraction shape; out of MVP |
| netstandard2.0 | Requires giving up IAsyncEnumerable ergonomics, required members, STJ schema exporter |

## MVP acceptance gate

1. `dotnet build -c Release` warning-free; `dotnet test` green (all suites).
2. Shared provider contract tests pass for all five providers against fake servers.
3. All four samples run against Ollama or fake endpoints without code changes.
4. `dotnet pack` produces validated packages (README, icon, symbols, SourceLink).
5. Docs: quick start + all MVP feature guides complete.
