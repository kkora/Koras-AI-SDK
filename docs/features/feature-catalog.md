# Feature Catalog

Every feature has a stable ID. Detailed per-feature guides (usage + planning appendix) live in
`docs/features/<feature-name>.md`. Classification: **MVP**, **1.1**, **1.2**, **2.0**,
**Experimental**, **Out of scope**.

Common nonfunctional requirements applying to every MVP feature (stated once, not repeated):
async-first with `CancellationToken` on every I/O path; thread-safe for singleton use; no
secrets in logs or exceptions; XML-documented public API; unit + failure-path + cancellation
tests; a runnable example in docs and at least one sample app.

---

## F-001 Chat completion — MVP

- **User problem:** calling any chat model with one request/response model.
- **User story:** as a developer, I send a list of messages and options to `IChatClient` and get
  a typed response with content, finish reason, and token usage — identical across providers.
- **Business value:** the core primitive; everything else builds on it.
- **Functional requirements:** system/user/assistant/tool roles; model selection per request or
  per client default; temperature/top-p/max-tokens/stop; provider-specific passthrough via
  `AdditionalProperties`; response carries model, provider, finish reason, usage, raw response id.
- **Public API:** `IChatClient.CompleteAsync(ChatRequest, CancellationToken)` → `ChatResponse`;
  `ChatMessage`, `ChatRole`, `ChatOptions`, `TokenUsage`, `ChatFinishReason`.
- **Configuration:** per-provider default model; per-request `ChatOptions`.
- **Error conditions:** authentication, invalid request, model not found, rate limited, content
  filtered, network/timeout, provider error — all as `AiException` with `AiErrorCode`.
- **Security:** API keys only via options/DI; never serialized into logs or exceptions.
- **Performance:** single allocation-conscious serialization pass; pooled `HttpClient` via
  `IHttpClientFactory`.
- **Observability:** `chat` activity + duration histogram + token counters.
- **Tests:** request mapping per provider (unit), response mapping from recorded fixtures,
  cancellation, error mapping matrix. Integration: full stack against in-process fake server.
- **Acceptance:** the same `ChatRequest` runs unmodified against all five providers in tests.

## F-002 Streaming responses — MVP

- **User problem:** progressive token delivery for interactive UX.
- **User story:** I iterate `await foreach (var update in client.StreamAsync(request, ct))` and
  receive text deltas, tool-call deltas, and a final usage/finish update.
- **Functional requirements:** SSE parsing (OpenAI/Azure/Anthropic/Gemini) and JSON-lines
  (Ollama); mid-stream cancellation disposes the transport; mid-stream provider errors surface
  as `AiException`; final update carries usage where the provider emits it.
- **Public API:** `IChatClient.StreamAsync(ChatRequest, CancellationToken)` →
  `IAsyncEnumerable<ChatStreamUpdate>`.
- **Error conditions:** stream aborts, malformed events, HTTP error before first event.
- **Performance:** no buffering of the full response; `System.Net.ServerSentEvents`-style
  incremental parsing; deltas reuse strings without concatenation (consumer aggregates).
- **Tests:** SSE/JSONL parser units incl. split-across-chunks events; cancellation mid-stream;
  error event mapping; aggregation helper correctness.
- **Acceptance:** streamed concatenation equals non-streamed content for fixture conversations.

## F-003 Structured output — MVP

- **User problem:** getting typed C# objects instead of parsing free text.
- **User story:** `await client.CompleteAsync<Invoice>(request, ct)` returns a deserialized
  `Invoice` with the schema generated from the type.
- **Functional requirements:** JSON-schema generation from C# types (System.Text.Json
  `JsonSchemaExporter`); provider mapping (OpenAI `response_format: json_schema`, Anthropic
  tool-based structuring, Gemini `responseSchema`, Ollama `format`); deserialization failure →
  `AiErrorCode.InvalidResponse` with raw text attached.
- **Public API:** `ChatResponseFormat.Json`, `ChatResponseFormat.ForType<T>()`;
  `CompleteAsync<T>` extension returning `ChatResponse<T>`.
- **Security:** deserialization uses STJ with safe defaults; no polymorphic type resolution.
- **Tests:** schema generation snapshots; happy path; malformed-JSON failure path; nullable and
  enum round-trips.
- **Acceptance:** the same `[Description]`-annotated type produces working structured output on
  all providers that support it, and a documented `NotSupported` error otherwise.

## F-004 Tool and function calling — MVP

- **User problem:** letting models call application code safely.
- **User story:** I declare tools from delegates (`AiTool.Create("lookup_order", ..., handler)`),
  pass them in `ChatOptions.Tools`, and either handle `ToolCall`s myself or let the built-in
  tool loop execute them with a bounded iteration count.
- **Functional requirements:** schema generation from delegate parameters; provider mapping of
  tool declarations and tool-call responses; manual mode (inspect `ChatResponse.Message.ToolCalls`)
  and automatic loop (`UseToolInvocation()` decorator, default max 8 iterations); tool handler
  exceptions returned to the model as error results, never crashing the request (configurable).
- **Public API:** `AiTool`, `AiTool.Create(...)`, `ToolCall`, `ChatMessage.ToolResult(...)`,
  `ChatOptions.Tools/ToolChoice`, `ToolInvocationOptions`.
- **Security:** tools execute only when explicitly registered; argument JSON validated against
  the schema types during binding; iteration bound prevents infinite loops.
- **Tests:** schema generation, argument binding (types, optionals, errors), loop termination,
  parallel tool calls, handler exception policy, cancellation inside handlers.
- **Acceptance:** tool round-trip fixture passes for all providers supporting tools.

## F-005 Embeddings — MVP

- **User story:** `IEmbeddingClient.GenerateAsync(new EmbeddingRequest(["a", "b"]))` returns
  vectors + usage, provider-neutral.
- **Functional requirements:** batch input; `ReadOnlyMemory<float>` vectors; dimensions
  reported; providers without embeddings (Anthropic) throw `AiErrorCode.NotSupported`.
- **Public API:** `IEmbeddingClient`, `EmbeddingRequest`, `EmbeddingResponse`, `Embedding`.
- **Tests:** mapping per provider, batching, usage, not-supported path.

## F-006 Prompt templates — MVP

- **User story:** `PromptTemplate.Parse("Summarize for {{audience}}: {{text}}")` renders with a
  dictionary or anonymous object; missing values throw with the placeholder name.
- **Functional requirements:** `{{name}}` placeholders, `{{{escaped}}}` literal braces via
  `{{`/`}}` doubling; culture-invariant formatting; parse-once render-many (thread-safe).
- **Public API:** `PromptTemplate`, `.Parse`, `.Render(IReadOnlyDictionary<string,object?>)`,
  `.Render(object)` (property bag).
- **Tests:** parser edge cases, missing-value errors, escaping, thread-safety smoke.
- **Non-goals:** logic/loops/conditionals (that's a template-engine dependency, out of scope).

## F-007..F-011 Providers: OpenAI, Azure OpenAI, Anthropic, Google Gemini, Ollama — MVP

- **User story:** I add one provider package, call `ai.AddOpenAI(...)` (etc.), and get
  `IChatClient`/`IEmbeddingClient` implementations honoring every abstraction feature the
  provider supports.
- **Functional requirements per provider:** chat, streaming, tools, structured output,
  embeddings (capability matrix in [feature-matrix](feature-matrix.md)); error normalization
  from provider wire errors; `Retry-After` extraction; configurable base endpoint (proxies,
  compatible gateways); options validation (key/endpoint/deployment as applicable).
- **Dependencies:** none beyond `Koras.AI` core (raw REST; no vendor SDKs — ADR-0003).
- **Security:** auth via headers, never query strings (Gemini uses `x-goog-api-key` header,
  not the `?key=` pattern); endpoints must be HTTPS by default (Ollama exempt, localhost).
- **Tests:** request/response/stream/error mapping against recorded wire fixtures per provider;
  options validation; integration against in-process fake servers speaking each wire format.
- **Acceptance:** contract-test suite (shared across providers) green per provider.

## F-012 Dependency injection & options — MVP

- **User story:** `services.AddKorasAI(ai => ai.AddOpenAI(o => ...))` registers named clients,
  a default client, options with `ValidateOnStart`, and `IChatClientFactory` for named lookup.
- **Functional requirements:** builder pattern; multiple named clients per provider;
  configuration binding (`ai.AddOpenAI(config.GetSection(...))`); explicit
  `.AsDefault()`; keyed access via `IChatClientFactory.GetChatClient(name)`; global pipeline
  decorators (`ai.UseRetry(...)`) applied to every registered client.
- **Error conditions:** duplicate names, missing default, invalid options → startup failure
  with actionable message naming the option path.
- **Tests:** registration shapes, validation failures, factory lookup, singleton lifetimes,
  config binding, two-providers-side-by-side.

## F-013 Retry and timeout — MVP

- **User story:** transient failures (429/5xx/network) retry with exponential backoff + jitter,
  honoring `Retry-After`, with a per-attempt timeout — on by default, configurable, removable.
- **Functional requirements:** default 3 attempts, base delay 1s, factor 2, max delay 30s,
  full jitter; per-attempt timeout default 100s; only `AiException.IsTransient` retries;
  streaming retries only before first token; `TimeProvider`-based for testability.
- **Public API:** `RetryOptions`, `ai.UseRetry(Action<RetryOptions>)`, `.WithoutRetry()`.
- **Tests:** backoff math with `FakeTimeProvider`, retry-after honored, non-transient
  short-circuit, exhaustion rethrows original, cancellation during delay.

## F-014 Provider fallback — MVP

- **User story:** `ai.AddFallback("resilient", "azure", "anthropic")` creates a named client
  that fails over on transient/unavailable errors and reports which provider served the call.
- **Functional requirements:** ordered candidates; failover on configurable error codes
  (default: transient + `NotSupported` excluded); logs + activity event on failover; terminal
  errors (auth, invalid request) do not fail over by default.
- **Tests:** failover matrix, exhaustion aggregates errors, streaming fallback before first
  token only, metrics tags.

## F-015 Error normalization & rate-limit handling — MVP

- **User story:** every failure from every provider is one exception type, `AiException`, with a
  documented `AiErrorCode`, HTTP status, provider name, `RetryAfter`, and `IsTransient`.
- **Functional requirements:** per-provider wire-error mapping tables; raw provider error body
  preserved (`ProviderErrorBody`, truncated) for diagnostics; secrets scrubbed.
- **Tests:** mapping matrix per provider (401/403/404/400/429/5xx/network/timeout/cancel).

## F-016 Logging — MVP

- **Functional requirements:** `ILogger` category per client type; `LoggerMessage`-generated
  high-performance messages; request start/stop/failure with provider+model, duration, tokens;
  never message content or keys at ≥ Information (opt-in content logging at Trace with explicit
  `EnableSensitiveDataLogging` flag, default off).
- **Tests:** log-output assertions, sensitive-data-off default verified.

## F-017 Telemetry & usage tracking — MVP

- **Functional requirements:** `ActivitySource("Koras.AI")` spans per operation with OTel GenAI
  semantic-convention tags (`gen_ai.system`, `gen_ai.request.model`,
  `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, error type); `Meter("Koras.AI")`
  instruments `koras.ai.client.operation.duration` (histogram),
  `koras.ai.client.token.usage` (counter, tagged input/output); zero OTel package dependency in
  core; `Koras.AI.OpenTelemetry` adds `WithKorasAI()` one-liners for `TracerProviderBuilder`/
  `MeterProviderBuilder`.
- **Tests:** activity/metric emission via `ActivityListener`/`MetricCollector`.

## F-018 Health checks & ASP.NET Core integration — MVP

- **Functional requirements:** `AddKorasAI()` health-check registration probing a named client
  (provider-implemented lightweight probe, e.g. model list / version endpoints — never a paid
  completion by default); degraded vs. unhealthy mapping; tags.
- **Public API:** `IHealthCheckBuilder.AddKorasAI(name?, ...)` in `Koras.AI.AspNetCore`.
- **Tests:** healthy/unhealthy/degraded mapping with fake probes.

## F-019 Custom provider extensibility — MVP (docs + base classes)

- **User story:** I implement `IChatClient` (optionally via `Koras.AI.Providers` base classes:
  HTTP plumbing, SSE reader, error helper) and register it with `ai.AddClient("mine", sp => ...)`;
  every decorator (retry, fallback, telemetry) works unchanged.
- **Tests:** custom fake provider registered through the builder passes the shared contract tests.

---

## Deferred features (summary — full detail in [future-roadmap](future-roadmap.md))

| ID | Feature | Release | Notes |
|---|---|---|---|
| F-020 | Multimodal content parts (images) | 1.1 | Model shaped for it now; wire mappings later |
| F-021 | Conversation memory | 1.1 | `Koras.AI.Memory`; stores + token-budget trimming |
| F-022 | Microsoft.Extensions.AI bridge | 1.1 | Two-way adapters |
| F-023 | Cost estimation | 1.1 | Pricing tables as data; per-request cost tags |
| F-024 | RAG abstractions | 1.2 | Contracts only, no storage impls |
| F-025 | Semantic caching | 1.2 | Embedding-similarity cache |
| F-026 | Provider load balancing | 1.2 | Weighted/round-robin named-client groups |
| F-027 | Agent orchestration | 2.0 | Only on demonstrated demand |
| F-028 | Content moderation hooks | 2.0 | Pipeline stage before/after model |
| F-029 | Middleware pipeline (public) | 2.0 | Decorators are the internal model already; public middleware API only if decorators prove insufficient |
| — | Vector stores, prompt IDE, eval harness, model hosting | Out of scope | Standing decision |
