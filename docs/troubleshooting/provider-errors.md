# Troubleshooting: Provider-Specific Errors

All providers surface failures through the same `AiException`/`AiErrorCode` taxonomy
(see [common errors](common-errors.md)), but each provider has quirks worth knowing when the
normalized code alone doesn't explain the behavior. `ProviderErrorBody` always carries the
provider's raw message.

## OpenAI

### `insufficient_quota` — the 429 that is not transient

OpenAI uses HTTP 429 for two unrelated conditions. Both map to `AiErrorCode.RateLimited`,
but they differ on `IsTransient`:

| Wire error | `IsTransient` | Meaning |
|---|---|---|
| `rate_limit_exceeded` | `true` | Burst backpressure — retry and fallback handle it |
| `insufficient_quota` | `false` | Your account is out of credit/quota — retrying is pointless |

The retry decorator consults `IsTransient` only, so quota exhaustion fails fast instead of
burning three backoff cycles. If requests suddenly fail with `RateLimited` and no retries in
the logs, check `ProviderErrorBody` for `insufficient_quota` and fix billing — not code.

## Anthropic

### 529 `overloaded_error`

Anthropic signals overload with a nonstandard **529** status, mapped to
`AiErrorCode.ProviderUnavailable` (transient — retried and failed over automatically).
Sustained 529s are an upstream capacity issue; a fallback chain keeps you serving.

### `max_tokens` is mandatory

The Anthropic Messages API rejects requests without an explicit output-token limit. The SDK
always sends one: `ChatOptions.MaxOutputTokens` when set, otherwise
`AnthropicOptions.DefaultMaxOutputTokens` (default 4096). If completions truncate with
`FinishReason == ChatFinishReason.Length`, raise one of those two values. Startup validation
rejects `DefaultMaxOutputTokens <= 0`.

### No embeddings

Anthropic offers no embeddings API. `AddAnthropic` registers a chat client only; routing an
embedding call at Anthropic yields `AiErrorCode.NotSupported`.

## Gemini

### Schema-dialect rejections on structured output

Gemini's `responseSchema` accepts a restricted JSON Schema dialect and rejects requests
containing keywords like `additionalProperties`, `$schema`, or `$defs`. The SDK strips
unsupported keywords from generated schemas automatically, so
`ChatResponseFormat.ForType<T>()` works — but a hand-written schema passed to
`ChatResponseFormat.JsonSchema(...)` that leans on `$defs`/`$ref` composition may fail with
`InvalidRequest`. Keep hand-written schemas for Gemini flat and self-contained.

### Invalid API key arrives as 400, not 401

Gemini reports `API key not valid` with HTTP **400**. The SDK recognizes the message and
maps it to `AiErrorCode.Authentication` — so trust `Code`, not `StatusCode`, when branching.
Note the SDK sends the key as the `x-goog-api-key` header, never in the query string, so
keys don't leak into URL logs.

## Ollama

### Connection refused

If the daemon isn't running (or the endpoint is wrong), calls fail with `AiErrorCode.Network`
and a message hinting `is Ollama running?`. Checklist:

```bash
ollama serve                  # start the daemon (default http://localhost:11434)
ollama pull llama3.2          # the model must be pulled before first use
curl -s http://localhost:11434/api/version
```

In containers, `localhost` refers to the app container — point `OllamaOptions.Endpoint` at
the host (`http://host.docker.internal:11434`) or the Ollama service name. Ollama's default
endpoint is loopback HTTP, which the HTTPS rule permits; a *remote* Ollama should sit behind
TLS.

### Model not pulled

Requesting a model the daemon hasn't pulled fails with `ModelNotFound` and the Ollama
message naming the model; `ollama pull <model>` fixes it.

## Azure OpenAI

### `DeploymentNotFound` → `ModelNotFound`

Azure routes by **deployment name**, not model name. A wrong `Deployment` value (or a
deployment in a different resource) returns `DeploymentNotFound`, which the SDK maps to
`AiErrorCode.ModelNotFound`. Remember:

- `AzureOpenAIOptions.Deployment` is the name *you* gave the deployment in Azure AI Foundry —
  often not equal to the underlying model id.
- `ChatRequest.Model` overrides act as deployment names on Azure.
- Deployments are per-resource: check `Endpoint` and `Deployment` refer to the same resource.

A related trap: chat calls on a client configured with only `EmbeddingDeployment` throw
`AiErrorCode.Configuration` (`Deployment is required for chat operations`).

## See also

- [Common errors](common-errors.md) — the full `AiErrorCode` table.
- [Feature matrix](../features/feature-matrix.md) — per-provider capability support.
- [Error model](../architecture/error-model.md) — the mapping rules providers implement.
