# API Reference Overview

A map of the public surface, by package. Every public type and member ships with XML
documentation inside the NuGet packages — **IntelliSense is the primary reference**; this
page tells you where things live and which guide covers them. The contract-first definition
is in [public API design](../api/public-api-design.md).

Almost everything lives in the `Koras.AI` namespace regardless of package, so one `using
Koras.AI;` covers typical code. DI entry points live in
`Microsoft.Extensions.DependencyInjection` for discoverability.

## Koras.AI.Abstractions — the contracts

| Type | Description | Guide |
|---|---|---|
| `IChatClient` | Chat completion + streaming; `ProviderName` | [Chat completion](../features/chat-completion.md) |
| `IEmbeddingClient` | Batch embedding generation | [Embeddings](../features/embeddings.md) |
| `IProviderHealthProbe` | Optional cheap liveness probe capability | [Health checks](../features/health-checks.md) |
| `ChatMessage`, `ChatRole` | Immutable messages; `System`/`User`/`Assistant`/`ToolResult` factories | [Chat completion](../features/chat-completion.md) |
| `ChatRequest`, `ChatOptions` | Conversation + per-request options; `FromPrompt` helper | [Chat completion](../features/chat-completion.md) |
| `ChatResponse`, `ChatFinishReason`, `TokenUsage` | Result with provider, usage, `RawRepresentation` | [Chat completion](../features/chat-completion.md) |
| `ChatStreamUpdate` | Streaming delta (text/tool-call/finish/usage) | [Streaming](../features/streaming.md) |
| `AiTool`, `ToolCall`, `ToolCallDelta`, `ToolChoice` | Tool declaration (`Create`/`Declare`), calls, streaming deltas | [Tool calling](../features/tool-calling.md) |
| `ChatResponseFormat` (+ `Text`/`Json`/`JsonSchema`/`ForType<T>` factories) | Output-shape control | [Structured output](../features/structured-output.md) |
| `AiJsonSchema` | JSON-schema generation helpers | [Structured output](../features/structured-output.md) |
| `EmbeddingRequest`, `EmbeddingResponse`, `Embedding` | Embedding I/O models | [Embeddings](../features/embeddings.md) |
| `AiException`, `AiErrorCode` | The single exception type + closed error taxonomy | [Error handling](../features/error-handling.md) |

## Koras.AI — core engine

| Type | Description | Guide |
|---|---|---|
| `KorasAiServiceCollectionExtensions.AddKorasAI` (ns `Microsoft.Extensions.DependencyInjection`) | The DI entry point | [Dependency injection](../features/dependency-injection.md) |
| `KorasAiBuilder`, `KorasAiClientBuilder` | Registration builder: `AddClient`, `AddFallback`, `Use`, `UseRetry`, `UseToolInvocation`, `ConfigureTelemetry`, `AsDefault` | [Dependency injection](../features/dependency-injection.md) |
| `IChatClientFactory` | Named client lookup; `ClientNames` | [Dependency injection](../features/dependency-injection.md) |
| `ChatClientExtensions` | `CompleteAsync(string)`, `CompleteAsync<T>` structured output | [Structured output](../features/structured-output.md) |
| `ChatResponse<T>` | Typed value + raw response pair | [Structured output](../features/structured-output.md) |
| `DelegatingChatClient` | Base class for custom decorators | [Custom providers](../features/custom-providers.md) |
| `RetryOptions` | Retry decorator configuration | [Resilience](../features/resilience.md) |
| `FallbackChatClient` | Failover client (usually via `AddFallback`) | [Provider fallback](../features/provider-fallback.md) |
| `ToolInvocationOptions`, `ToolErrorBehavior` | Automatic tool-loop configuration | [Tool calling](../features/tool-calling.md) |
| `KorasAiTelemetryOptions`, `KorasAiTelemetry` | Sensitive-data switch; telemetry constants | [Telemetry](../features/telemetry.md) |
| `Koras.AI.Templates.PromptTemplate` | `{{placeholder}}` templates: parse once, render many | [Prompt templates](../features/prompt-templates.md) |
| `Koras.AI.Providers.ProviderChatClient` / `ProviderEmbeddingClient` | Base classes for provider authors (HTTP, error normalization) | [Custom providers](../features/custom-providers.md) |
| `Koras.AI.Providers.SseReader` / `JsonLinesReader` / `SseEvent` | Streaming wire-format readers | [Custom providers](../features/custom-providers.md) |
| `Koras.AI.Providers.ProviderErrors` | `AiException` factory helpers | [Custom providers](../features/custom-providers.md) |

## Provider packages

Each provider package contributes builder extensions (namespace `Koras.AI`) and an options
class (namespace `Koras.AI.<Provider>`). Defaults and validation:
[all options](../configuration/all-options.md).

| Package | Extensions (default client name) | Options | Guide |
|---|---|---|---|
| `Koras.AI.OpenAI` | `AddOpenAI` (`"openai"`) | `OpenAIOptions` | [OpenAI](../features/provider-openai.md) |
| `Koras.AI.AzureOpenAI` | `AddAzureOpenAI` (`"azure_openai"`) | `AzureOpenAIOptions` | [Azure OpenAI](../features/provider-azure-openai.md) |
| `Koras.AI.Anthropic` | `AddAnthropic` (`"anthropic"`) — chat only | `AnthropicOptions` | [Anthropic](../features/provider-anthropic.md) |
| `Koras.AI.Gemini` | `AddGemini` (`"gemini"`) | `GeminiOptions` | [Gemini](../features/provider-gemini.md) |
| `Koras.AI.Ollama` | `AddOllama` (`"ollama"`) | `OllamaOptions` | [Ollama](../features/provider-ollama.md) |

## Integration packages

| Package | Type | Description | Guide |
|---|---|---|---|
| `Koras.AI.AspNetCore` | `KorasAiHealthChecksBuilderExtensions.AddKorasAI` (ns `Microsoft.Extensions.DependencyInjection`) | Health-check registration probing a named client | [Health checks](../features/health-checks.md) |
| `Koras.AI.OpenTelemetry` | `AddKorasAI` on `TracerProviderBuilder` / `MeterProviderBuilder` | Wires `ActivitySource("Koras.AI")` and `Meter("Koras.AI")` into OTel | [Telemetry](../features/telemetry.md) |

## Conventions across the surface

- Async everywhere; `CancellationToken` is always the last parameter, defaulted.
- Clients and decorators are thread-safe and intended for singleton use.
- No third-party types in public signatures (BCL + `Microsoft.Extensions.*`; OTel builder
  types only in the OTel package).
- Extensible-enum record structs (`ChatRole`, `ChatFinishReason`, `ToolChoice`) instead of
  C# enums for wire vocabularies — compare against the static members.
- Failures are `AiException` only; caller cancellation is `OperationCanceledException`.

Compatibility guarantees for all of the above:
[backward compatibility](../api/backward-compatibility.md) ·
[versioning policy](../migration/versioning-policy.md).
