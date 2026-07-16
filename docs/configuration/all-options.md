# Configuration: All Options Reference

Every builder method and options class, with defaults and validation rules. Configuration
sections follow the `Koras:AI:<Provider>` convention — see
[appsettings](appsettings.md) and [environment variables](environment-variables.md).
Validation runs at startup via `ValidateOnStart` — see [validation](validation.md).

## `KorasAiBuilder` (inside `services.AddKorasAI(ai => ...)`)

| Method | Purpose |
|---|---|
| `AddClient(name, factory)` | Registers a named `IChatClient` (custom providers). First registration becomes the default. Duplicate names throw. |
| `AddEmbeddingClient(name, factory)` | Registers a named `IEmbeddingClient`. |
| `AddFallback(name, params clientNames)` | Registers a client that fails over across the named candidates on transient errors. At least one candidate; cannot list itself. |
| `Use(decorator)` | Global decorator applied to every chat client (registration order, innermost first). |
| `UseRetry(configure?)` | Adds the retry decorator (`RetryOptions` below). |
| `UseToolInvocation(configure?)` | Adds the automatic tool loop (`ToolInvocationOptions` below). |
| `ConfigureTelemetry(configure)` | Configures `KorasAiTelemetryOptions`. |

Per-client, `KorasAiClientBuilder` adds `AsDefault()` (make this client the default
`IChatClient`/`IEmbeddingClient`) and `Use(decorator)` (decorator for this client only,
applied before global decorators).

Provider packages add `AddOpenAI` / `AddAzureOpenAI` / `AddAnthropic` / `AddGemini` /
`AddOllama`, each with three overloads: `(Action<TOptions>)`, `(IConfiguration)`, and
`(string name, Action<TOptions>)`. Default client names: `"openai"`, `"azure_openai"`,
`"anthropic"`, `"gemini"`, `"ollama"`.

## `RetryOptions` (`ai.UseRetry(r => ...)`)

| Property | Type | Default | Description | Validation |
|---|---|---|---|---|
| `MaxAttempts` | `int` | `3` | Total attempts including the first. | ≥ 1 (setter throws) |
| `BaseDelay` | `TimeSpan` | 1 s | Backoff base; attempt *n* waits up to `BaseDelay × 2ⁿ⁻¹`, full jitter. | ≥ 0 (setter throws) |
| `MaxDelay` | `TimeSpan` | 30 s | Upper bound for any computed delay. | ≥ 0 (setter throws) |
| `AttemptTimeout` | `TimeSpan` | 100 s | Per-attempt timeout; timeouts count as transient. | > 0 (setter throws) |
| `HonorRetryAfter` | `bool` | `true` | Provider `Retry-After` hints override the computed backoff. | — |

Only `AiException.IsTransient` failures retry; streams retry only before the first update.

## `ToolInvocationOptions` (`ai.UseToolInvocation(t => ...)`)

| Property | Type | Default | Description | Validation |
|---|---|---|---|---|
| `MaxIterations` | `int` | `8` | Max model round-trips per request; exceeding throws `AiErrorCode.ToolExecutionFailed`. | ≥ 1 (setter throws) |
| `ErrorBehavior` | `ToolErrorBehavior` | `ReturnToModel` | `ReturnToModel` feeds handler failures back to the model; `Throw` raises `AiException(ToolExecutionFailed)`. | — |

## `KorasAiTelemetryOptions` (`ai.ConfigureTelemetry(t => ...)`)

| Property | Type | Default | Description |
|---|---|---|---|
| `EnableSensitiveData` | `bool` | `false` | Allows message content at `Trace` log level. Local debugging only — never enable in production. |

## `OpenAIOptions` — section `Koras:AI:OpenAI`

| Property | Type | Default | Description | Startup validation |
|---|---|---|---|---|
| `ApiKey` | `string?` | — | OpenAI API key (`Authorization: Bearer`). | Required (non-blank) |
| `Endpoint` | `Uri` | `https://api.openai.com/v1/` | Base endpoint; point at OpenAI-compatible gateways. | Not null; HTTPS unless loopback |
| `DefaultModel` | `string?` | — | Model when `ChatRequest.Model` is null (e.g. `gpt-4o-mini`). | — (missing at call time → `Configuration` error) |
| `DefaultEmbeddingModel` | `string?` | — | Embedding model (e.g. `text-embedding-3-small`). | — |
| `Organization` | `string?` | — | Sent as `OpenAI-Organization` when set. | — |

## `AzureOpenAIOptions` — section `Koras:AI:AzureOpenAI`

| Property | Type | Default | Description | Startup validation |
|---|---|---|---|---|
| `Endpoint` | `Uri?` | — | Resource endpoint, e.g. `https://my-resource.openai.azure.com`. | Required; HTTPS unless loopback |
| `Deployment` | `string?` | — | Chat deployment name. | `Deployment` and/or `EmbeddingDeployment` required; chat calls without `Deployment` throw `Configuration` |
| `EmbeddingDeployment` | `string?` | — | Embedding deployment name. | (see above) |
| `ApiKey` | `string?` | — | Sent as the `api-key` header. | Required (non-blank) |
| `ApiVersion` | `string` | `2024-10-21` | Data-plane API version. | Non-empty |

## `AnthropicOptions` — section `Koras:AI:Anthropic`

| Property | Type | Default | Description | Startup validation |
|---|---|---|---|---|
| `ApiKey` | `string?` | — | Sent as the `x-api-key` header. | Required (non-blank) |
| `Endpoint` | `Uri` | `https://api.anthropic.com/` | Base endpoint. | Not null; HTTPS unless loopback |
| `DefaultModel` | `string?` | — | Model when the request has none (e.g. `claude-sonnet-4-5`). | — |
| `DefaultMaxOutputTokens` | `int` | `4096` | `max_tokens` when `ChatOptions.MaxOutputTokens` is unset (the Messages API requires it). | > 0 |
| `AnthropicVersion` | `string` | `2023-06-01` | `anthropic-version` header. | — |

Anthropic has no embeddings API — `AddAnthropic` registers a chat client only.

## `GeminiOptions` — section `Koras:AI:Gemini`

| Property | Type | Default | Description | Startup validation |
|---|---|---|---|---|
| `ApiKey` | `string?` | — | Sent as `x-goog-api-key` (never in the query string). | Required (non-blank) |
| `Endpoint` | `Uri` | `https://generativelanguage.googleapis.com/v1beta/` | Base endpoint. | Not null; HTTPS unless loopback |
| `DefaultModel` | `string?` | — | e.g. `gemini-2.0-flash`. | — |
| `DefaultEmbeddingModel` | `string?` | — | e.g. `text-embedding-004`. | — |

## `OllamaOptions` — section `Koras:AI:Ollama`

| Property | Type | Default | Description | Startup validation |
|---|---|---|---|---|
| `Endpoint` | `Uri` | `http://localhost:11434/` | Local or self-hosted Ollama. No API key. | Not null (HTTP allowed) |
| `DefaultModel` | `string?` | — | e.g. `llama3.2`. | — |
| `DefaultEmbeddingModel` | `string?` | — | e.g. `nomic-embed-text`. | — |

## Per-request options

`ChatOptions` (`Temperature`, `TopP`, `MaxOutputTokens`, `StopSequences`, `ResponseFormat`,
`Tools`, `ToolChoice`, `AdditionalProperties`) is set per request, not through configuration —
see [chat completion](../features/chat-completion.md).
