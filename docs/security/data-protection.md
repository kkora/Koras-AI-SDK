# Data Protection

What data the SDK touches, where it goes, and what it keeps. Short version: your data goes to
the provider you configured and nowhere else, and the SDK stores nothing.

## Data flows

| Data | Destination | Notes |
|---|---|---|
| Prompts, messages, tool results, embedding inputs | The **configured provider endpoint only** (OpenAI, Azure OpenAI, Anthropic, Gemini, or your Ollama host) | Sent over HTTPS (loopback exempt). Endpoint is explicit configuration — nothing is inferred. |
| Completions, stream updates, embeddings | Returned to your application | Parsed defensively; never forwarded anywhere. |
| API keys | Authentication header of the provider request only | Never query strings, logs, exceptions, or telemetry. |
| Telemetry (logs, traces, metrics) | Your `ILogger` / OpenTelemetry pipeline | Metadata only by default — see below. |

**Nothing phones home.** The SDK contains no telemetry endpoint of its own, no usage
reporting, no update checks. The only outbound connections it ever makes are to the provider
endpoints you configure.

## What the SDK persists

**Nothing.** No conversation history, no caches on disk, no temp files, no cookies. Chat
history is whatever `IReadOnlyList<ChatMessage>` your application passes on each request;
retention is entirely in your hands. (A memory/history package is on the roadmap as an
explicit opt-in — see [memory-management.md](../performance/memory-management.md).)

## Logging and telemetry content policy

By default the SDK's diagnostics are **metadata only**:

- Logs: provider, model, duration, token counts, finish reason, `AiErrorCode`, HTTP status,
  provider `RequestId`. Never message content, never keys.
- Traces: OTel GenAI semantic-convention tags (operation, system, model, token usage,
  `error.type`) — content is never put on spans by default.
- Metrics: durations, token counters, retry/fallback counters.

Two deliberate exceptions:

1. **Error bodies** — provider error payloads are attached to `AiException.ProviderErrorBody`
   truncated to 4 KB, for diagnosis of failed calls. They are provider-authored error JSON
   (which can echo fragments of an invalid request); treat exception sinks accordingly.
2. **`EnableSensitiveData=true` + Trace level** — prompt/response content is logged for local
   debugging only. Both switches are required; see
   [secure-configuration.md](secure-configuration.md).

`RequestId` (the provider's request identifier header) appears in logs and exceptions by
design: it is the correlation handle for provider support tickets and contains no content.

## PII guidance

- **Treat prompts and completions as PII.** Users paste names, addresses, medical details,
  and source code into prompts; assume every request body is personal data under GDPR/CCPA.
- **Retention is the provider's.** Once a request leaves the SDK, storage and training-use
  policies are governed by your agreement with the provider. Review and link the relevant
  DPA/terms for each provider you enable:
  - OpenAI — API data usage policies and DPA (platform.openai.com/docs → Enterprise privacy)
  - Azure OpenAI — Microsoft Products and Services DPA; Azure OpenAI data, privacy & security docs
  - Anthropic — Commercial Terms and DPA (anthropic.com/legal)
  - Google Gemini API — Gemini API additional terms; Google Cloud DPA when using Vertex
  - Ollama — self-hosted: data stays on the host **you** operate; you are the processor.
- Put your provider choice and DPA in your own record of processing; the SDK's role is
  transport, not storage.
- If you must scrub PII before sending, do it in your application before building the
  `ChatRequest` — the SDK does not modify message content.

## Encryption

- **In transit:** TLS for every remote endpoint, enforced at startup (HTTP allowed only for
  loopback, e.g., local Ollama). Certificate validation is standard `HttpClient` behavior —
  the SDK never disables it.
- **At rest:** not applicable — the SDK writes nothing to disk. Anything *you* persist
  (histories, embeddings in a vector store, logs with `EnableSensitiveData`) needs your own
  at-rest encryption story.

## See also

- [Threat model](threat-model.md) — assets and trust boundaries
- [Secure configuration](secure-configuration.md) — keys, HTTPS, multi-tenancy
- [Observability](../architecture/observability.md) — the full telemetry surface
