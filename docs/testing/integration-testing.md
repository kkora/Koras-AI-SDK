# Integration Testing

How the end-to-end suite in `tests/Koras.AI.IntegrationTests` works, and why it is built on a
real HTTP server instead of mocked handlers.

## The `FakeProviderServer` approach

[`FakeProviderServer.cs`](../../tests/Koras.AI.IntegrationTests/FakeProviderServer.cs) is an
in-process **Kestrel** server (`WebApplication.CreateSlimBuilder`, bound to
`http://127.0.0.1:0` — a random loopback port, which is exactly why the SDK's HTTPS validation
exempts loopback). It implements the providers' real wire formats:

- **OpenAI-style** `POST /v1/chat/completions` — validates the `Authorization: Bearer` header,
  returns real chat-completion JSON, streams real SSE frames (`data: {...}\n\n` … `data:
  [DONE]`), and can be scripted to fail once with a 429 + `Retry-After` header
  (`FailOpenAIOnce`) while counting requests (`OpenAIChatRequests`).
- **Ollama-style** endpoints speaking newline-delimited JSON for streaming.
- Health/probe endpoints for the health-check tests.

Tests in [`EndToEndTests.cs`](../../tests/Koras.AI.IntegrationTests/EndToEndTests.cs) then
build a **real DI container** with `AddKorasAI().AddOpenAI(...)/.AddOllama(...)` pointed at
the fake server, and drive the SDK exactly as an application would — real
`IHttpClientFactory`, real sockets, real decorators.

## Why real Kestrel beats mocked handlers for streaming

A mocked `HttpMessageHandler` returns a pre-built `HttpContent` — which silently skips
everything that makes streaming hard:

| Only a real server exercises | Why it matters |
|---|---|
| Chunked transfer encoding & response buffering behavior | SSE bugs hide in "worked with a MemoryStream, breaks over a socket" |
| SSE/JSONL frames arriving as genuine network reads | Parser must handle frames split across reads, not one tidy string |
| Header timing (status/headers before body completes) | `HttpCompletionOption.ResponseHeadersRead` behavior is real, not simulated |
| Connection lifecycle & cancellation | `Cancellation_mid_stream_releases_promptly` proves an abandoned stream actually frees the connection |
| The `IHttpClientFactory` pipeline | Named-client wiring, handler pooling — the code production runs |

Unit tests still use `FakeHttpMessageHandler` for wire-mapping details (right JSON in, right
model out) — that's the correct altitude for those assertions. The integration suite exists
for the failure modes mocks cannot represent.

## How to run

```bash
dotnet test tests/Koras.AI.IntegrationTests
```

No external services, no API keys, no network egress — the server is in-process and starts in
milliseconds. The suite runs in CI on both ubuntu and windows as part of the ordinary
`dotnet test` invocation ([`test.yml`](../../.github/workflows/test.yml)).

## What's covered (8 tests)

| Test | Proves |
|---|---|
| `Chat_completion_works_through_the_full_stack` | DI → factory → client → HTTP → wire mapping, end to end |
| `Streaming_aggregates_to_the_full_text_over_real_sse` | SSE framing parsed correctly off a real socket |
| `Retry_recovers_from_a_transient_429_with_retry_after` | Retry decorator honors `Retry-After` against a real 429; request counter proves exactly one retry |
| `Fallback_fails_over_from_a_broken_provider_to_ollama` | Cross-provider failover through the real pipeline |
| `Authentication_failures_surface_with_the_normalized_code` | Wrong bearer token → `AiErrorCode.Authentication`, not a raw HTTP exception |
| `Ollama_streaming_works_over_real_json_lines` | JSONL streaming (the non-SSE format) end to end |
| `Health_checks_probe_the_real_endpoints` | `AddKorasAI` health checks against live endpoints |
| `Cancellation_mid_stream_releases_promptly` | Cancelling mid-stream releases resources without hangs |

## Extending

Adding a scenario means adding an endpoint (or scripted behavior flag) to `FakeProviderServer`
and a test to `EndToEndTests`. Keep fixtures wire-accurate — copy real provider responses, then
minimize. Per the [definition of done](../planning/definition-of-done.md), any feature that
touches a provider or the full pipeline needs coverage here or in the shared contract suite.
