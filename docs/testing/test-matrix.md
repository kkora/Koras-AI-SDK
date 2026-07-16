# Test Matrix

Feature-by-feature map to the actual test classes in `tests/`. Unit classes live under
`tests/Koras.AI.UnitTests/`, architecture classes under `tests/Koras.AI.ArchitectureTests/`,
integration under `tests/Koras.AI.IntegrationTests/`.

## Core abstractions and pipeline

| Feature | Unit | Contract | Architecture | Integration (`EndToEndTests`) |
|---|---|---|---|---|
| Chat models & requests | `ChatModelTests` | — | `PublicApiSurfaceTests` | `Chat_completion_works_through_the_full_stack` |
| Error model (`AiException`) | `AiExceptionTests` | `ProviderContractTests` | — | `Authentication_failures_surface_with_the_normalized_code` |
| Client extensions (sugar APIs) | `ChatClientExtensionsTests` | — | — | — |
| Dependency injection / named clients | `AddKorasAiTests` | — | `DependencyRuleTests` | all (real container) |
| Options validation (HTTPS, ApiKey) | `AddKorasAiTests` | — | — | — |

## Resilience

| Feature | Unit | Integration |
|---|---|---|
| Retry (backoff, jitter, Retry-After, attempt timeout, streaming rule) | `RetryChatClientTests` (with `TestInfrastructure/ManualTimeProvider`) | `Retry_recovers_from_a_transient_429_with_retry_after` |
| Fallback / failover | `FallbackChatClientTests` | `Fallback_fails_over_from_a_broken_provider_to_ollama` |
| Cancellation | asserted across `RetryChatClientTests`, `ToolInvokingChatClientTests` | `Cancellation_mid_stream_releases_promptly` |

## Tools, schema, templates

| Feature | Unit |
|---|---|
| Tool definition & argument binding | `AiToolTests` |
| JSON schema generation | `AiJsonSchemaTests` |
| Bounded tool-invocation loop | `ToolInvokingChatClientTests` |
| Prompt templates (parse-once/render) | `PromptTemplateTests` |

## Providers

| Provider | Wire-mapping unit tests | Shared contract | Integration |
|---|---|---|---|
| OpenAI | `OpenAIChatClientTests`, `OpenAIEmbeddingClientTests` | `ProviderContractTests` | chat, streaming, retry, auth-failure tests |
| Azure OpenAI | `AzureOpenAIClientTests` | `ProviderContractTests` | — |
| Anthropic | `AnthropicChatClientTests` | `ProviderContractTests` | — |
| Gemini | `GeminiClientTests` | `ProviderContractTests` | — |
| Ollama | `OllamaClientTests` | `ProviderContractTests` | `Ollama_streaming_works_over_real_json_lines`, fallback target |
| SSE stream framing (shared) | `SseReaderTests` | — | `Streaming_aggregates_to_the_full_text_over_real_sse` |

## Diagnostics and operations

| Feature | Unit | Integration |
|---|---|---|
| Telemetry (spans, metrics via listeners) | `Diagnostics/TelemetryTests` | — |
| Health checks | `KorasAiHealthCheckTests` | `Health_checks_probe_the_real_endpoints` |

## Governance (architecture tests)

| Rule | Class |
|---|---|
| Dependency direction table ([dependency-rules.md](../architecture/dependency-rules.md)) | `DependencyRuleTests` |
| Public API snapshots for all 9 shipped assemblies (`PublicApi/*.approved.txt`) | `PublicApiSurfaceTests` |

## Test infrastructure (not tests)

| Helper | Purpose |
|---|---|
| `TestInfrastructure/FakeChatClient` | Scriptable `IChatClient` for decorator tests |
| `TestInfrastructure/FakeHttpMessageHandler` | Canned wire responses for provider unit tests |
| `TestInfrastructure/ManualTimeProvider` | Deterministic clock for retry/backoff tests |
| `IntegrationTests/FakeProviderServer` | In-process Kestrel speaking real provider wire formats |
