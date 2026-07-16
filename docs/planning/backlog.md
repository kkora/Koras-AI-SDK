# Implementation Backlog

Epics → stories/tasks. Every task lists: files, dependencies, public API, tests, docs, risks,
completion criteria. Conventions: `src/<project>/…`; "std DoD" = compiles warning-free, unit
tests incl. failure+cancellation paths, XML docs, CHANGELOG entry, feature guide updated.

## EPIC-0 Repository foundation (Milestone 0)

- **T-000 Repo scaffolding** — Files: `Koras.AI.sln`, `Directory.Build.props`,
  `Directory.Packages.props`, `global.json`, `.editorconfig`, `NuGet.Config`, `.gitattributes`,
  community files, `.github/**`. Deps: none. Tests: CI builds empty solution. Risk: SDK
  version drift → pin `global.json` rollForward latestFeature. Done: CI green on empty solution.

## EPIC-1 Abstractions (Milestone 1)

- **T-101 Chat models** — Files: `Koras.AI.Abstractions/{ChatMessage,ChatRole,ChatRequest,ChatOptions,ChatResponse,ChatFinishReason,TokenUsage,ChatStreamUpdate}.cs`.
  API: per [public-api-design](../api/public-api-design.md). Tests: construction, factories,
  STJ round-trip, usage addition. Done: std DoD.
- **T-102 Error model** — Files: `AiException.cs`, `AiErrorCode.cs`. Tests: transient
  classification table, message formatting, no secrets in ToString. Done: std DoD.
- **T-103 Client contracts** — Files: `IChatClient.cs`, `IEmbeddingClient.cs`,
  `IProviderHealthProbe.cs`; core: `DelegatingChatClient.cs`. Tests: delegation pass-through.
- **T-104 Tool models** — Files: `AiTool.cs`, `ToolCall.cs`, `ToolChoice.cs`,
  `ToolCallDelta.cs`; schema generation internal (`AiJsonSchema.cs`). Deps: T-101. Tests:
  schema snapshots (primitives, nullable, enums, records, `[Description]`), argument binding
  errors, async/sync handlers. Risks: STJ exporter fidelity → post-process step. Done: std DoD.
- **T-105 Response formats** — `ChatResponseFormat.cs` (+`ForType<T>`). Deps: T-104 schema gen.
- **T-106 Embedding models** — `EmbeddingRequest/Response/Embedding.cs`. Tests: ctor validation.

## EPIC-2 Core engine (Milestone 2)

- **T-201 Provider plumbing** — Files: `Koras.AI/Providers/{ProviderChatClient,ProviderEmbeddingClient,SseReader,JsonLinesReader,ProviderErrors}.cs`.
  Tests: SSE parser (split chunks, comments, multiline data, CRLF), JSONL parser, HTTP error →
  AiException mapping, Retry-After parsing (delta + http-date). Risks: parser edge cases —
  highest test density here. Done: std DoD.
- **T-202 Retry decorator** — `RetryChatClient.cs`, `RetryOptions.cs`. Deps: T-102.
  Tests: FakeTimeProvider backoff/jitter bounds, RetryAfter honored, terminal short-circuit,
  exhaustion rethrow, stream-after-first-token no-retry, cancellation during delay.
- **T-203 Structured output** — `ChatClientExtensions.cs`, `ChatResponse{T}.cs`. Deps: T-105.
  Tests: happy path, malformed JSON → InvalidResponse with body attached, custom serializer opts.
- **T-204 Tool loop** — `ToolInvokingChatClient.cs`, `ToolInvocationOptions.cs`. Deps: T-104.
  Tests: loop termination, max iterations → ToolExecutionFailed?/returns last, parallel calls,
  handler exception policies, cancellation propagation into handlers.
- **T-205 Prompt templates** — `Templates/PromptTemplate.cs`. Tests: parse/render matrix,
  escaping, missing values, invariant formatting, ParameterNames.
- **T-206 Telemetry + logging decorators** — `Diagnostics/{KorasAiDiagnostics,TelemetryChatClient,LoggingChatClient,KorasAiTelemetryOptions}.cs`.
  Tests: ActivityListener assertions, MetricCollector assertions, sensitive-data default off.

## EPIC-3 Composition (Milestone 3)

- **T-301 Builder + factory** — `KorasAiBuilder.cs`, `KorasAiClientBuilder.cs`,
  `ChatClientRegistration.cs`, `ChatClientFactory.cs`, `KorasAiServiceCollectionExtensions.cs`.
  Tests: registration shapes, default selection (explicit > first), duplicate-name throw,
  decorator ordering, singleton caching, keyed lookup.
- **T-302 Fallback client** — `FallbackChatClient.cs` + builder `AddFallback`. Tests:
  failover matrix, exhaustion aggregate, non-transient no-failover, stream failover pre-token.
- **T-303 Options infrastructure** — validation helpers, `ValidateOnStart` wiring, config
  binding overloads. Tests: invalid options fail startup with option-path message.

## EPIC-4 Providers (Milestone 4) — pattern per provider

Files: `Koras.AI.{P}/{P}Options.cs`, `{P}ChatClient.cs`, `{P}EmbeddingClient.cs` (if
supported), `{P}KorasAiBuilderExtensions.cs`, internal wire DTO mappers; fixtures under
`tests/…/Fixtures/{p}/*.json|.sse`. Tests per provider: request-mapping (roles, options,
tools, response format, AdditionalProperties), response-mapping (content, tool calls, usage,
finish reasons), streaming (fixture replay), error matrix, options validation + shared
contract suite. Done: contract suite green.

- **T-401 OpenAI** (reference implementation) → **T-402 AzureOpenAI** (endpoint/auth/deployment
  variant over T-401's mapper) → **T-403 Anthropic** (system prompt extraction, content blocks,
  input_schema tools, SSE event grammar, max_tokens required, no embeddings) → **T-404 Ollama**
  (JSONL streaming, /api/embed, localhost default) → **T-405 Gemini** (contents/parts,
  functionDeclarations, systemInstruction, responseSchema, SSE `alt=sse`).

## EPIC-5 Integrations (Milestones 5–6)

- **T-501 Health checks** — `Koras.AI.AspNetCore`: `KorasAiHealthCheck.cs` + builder ext.
  Tests: healthy/degraded/unhealthy with fake probes; no probe → skipped client.
- **T-601 OTel package** — `Koras.AI.OpenTelemetry`: two extension classes. Tests: sources
  registered.

## EPIC-6 Quality & release (Milestones 7–9)

- **T-701 Samples** ×4 (+READMEs). **T-702 Docs tree** complete. **T-703 Root README**.
- **T-801 Integration tests** — in-process Kestrel fake providers (OpenAI SSE, Anthropic SSE,
  Ollama JSONL), full-stack scenarios incl. fallback and cancellation.
- **T-802 Architecture tests** — NetArchTest dependency rules. **T-803 Benchmarks** project.
- **T-804 Security checklist pass** — secrets scrubbing tests, docs.
- **T-901 Pack metadata + icon + package README** per project; **T-902 Package validation +
  consumer smoke test**; **T-903 Release workflows + docs**.
