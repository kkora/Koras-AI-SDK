# Primary Use Cases

## UC-01 — AI chat in a web application

An ASP.NET Core application exposes a chat endpoint. The handler injects `IChatClient`, streams
tokens to the browser via SSE or SignalR, and never references a vendor SDK.

**Key requirements:** streaming (`IAsyncEnumerable`), cancellation when the browser disconnects,
per-request token usage for billing.

## UC-02 — Structured extraction

A SaaS backend extracts typed data (invoices, tickets, résumés) from free text. The developer
calls `CompleteAsync<InvoiceData>(...)` and receives a validated, deserialized object, with the
JSON-schema plumbing handled by the SDK.

**Key requirements:** structured output via JSON schema, schema generation from C# types,
deserialization failures surfaced as normalized errors.

## UC-03 — Tool calling / function execution

A support copilot lets a model call `LookupOrder(orderId)` and `RefundOrder(orderId, amount)`.
Tools are declared from C# delegates; the SDK runs the model↔tool loop with a bounded iteration
count.

**Key requirements:** tool schema generation, tool-call round-trip loop, guardrails (max
iterations, argument validation), cancellation.

## UC-04 — Embeddings and RAG ingestion

A document pipeline generates embeddings for chunks and stores them in a vector store. The
pipeline depends only on `IEmbeddingClient`, so the embedding model can change without touching
ingestion code.

**Key requirements:** batch embedding generation, usage tracking, provider-specific dimensions
surfaced in the response.

## UC-05 — Provider fallback for availability

A regulated SaaS must keep its AI feature available through provider outages. Configuration
declares a fallback chain (Azure OpenAI → Anthropic); a rate-limit or outage on the primary
transparently fails over.

**Key requirements:** error classification (transient vs. terminal), fallback decorator, metrics
distinguishing which provider served each request.

## UC-06 — Local development, hosted production

Developers run Ollama locally; staging uses OpenAI; production uses Azure OpenAI with managed
identity-style secrets handling. Only `appsettings.{Environment}.json` differs.

**Key requirements:** uniform options pattern, named clients, configuration binding, startup
validation with actionable error messages.

## UC-07 — Enterprise observability

Operations dashboards show tokens/minute, cost proxies, latency percentiles, and error rates per
provider and model, sourced from the SDK's built-in `Meter` and `ActivitySource` via the
existing OpenTelemetry pipeline.

**Key requirements:** OTel GenAI semantic conventions, no OTel dependency in core, one-line
registration in `Koras.AI.OpenTelemetry`.

## UC-08 — Library authors accepting "any model"

A NuGet library implements text summarization and accepts an `IChatClient` in its constructor.
It works with every provider Koras.AI supports — including a consumer's custom provider —
without referencing any of them.

**Key requirements:** stable, minimal abstractions package; documented custom-provider
extension point.
