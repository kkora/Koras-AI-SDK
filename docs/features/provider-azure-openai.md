# Azure OpenAI Provider

## Overview

`Koras.AI.AzureOpenAI` targets Azure OpenAI **deployments**. It reuses the OpenAI wire
protocol (`AzureOpenAIChatClient` derives from `OpenAIChatClient`) but swaps in Azure's
deployment-based URLs and `api-key` header authentication. Requests go to:

```
{Endpoint}/openai/deployments/{Deployment}/chat/completions?api-version={ApiVersion}
```

Provider name: `"azure_openai"`; default client name: `"azure_openai"`.

## When to use it

OpenAI models hosted in your Azure subscription — for data residency, private networking, or
enterprise billing. For openai.com or OpenAI-compatible gateways use
[provider-openai.md](provider-openai.md).

## Required packages

- `Koras.AI.AzureOpenAI` (depends on `Koras.AI.OpenAI` and `Koras.AI`).

## Basic configuration

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddAzureOpenAI(o =>
    {
        o.Endpoint = new Uri("https://my-resource.openai.azure.com");
        o.Deployment = "gpt-4o-mini";                    // chat deployment name
        o.EmbeddingDeployment = "text-embedding-3-small"; // embedding deployment name
        o.ApiKey = builder.Configuration["Koras:AI:AzureOpenAI:ApiKey"];
    });
});
```

Configuration binding (conventional section `Koras:AI:AzureOpenAI`):

```csharp
ai.AddAzureOpenAI(builder.Configuration.GetSection("Koras:AI:AzureOpenAI"));
```

Register several named clients to talk to multiple resources or deployments:
`ai.AddAzureOpenAI("gpt4", o => { ... })`.

## Options (`Koras.AI.AzureOpenAI.AzureOpenAIOptions`)

| Option | Type | Default | Notes |
|---|---|---|---|
| `Endpoint` | `Uri?` | — | Required. The resource URL, e.g. `https://my-resource.openai.azure.com`. HTTPS enforced. |
| `Deployment` | `string?` | — | Chat model deployment name. Required for chat. |
| `EmbeddingDeployment` | `string?` | — | Embedding deployment name. Required for embeddings. |
| `ApiKey` | `string?` | — | Required. Sent as the `api-key` header (not `Authorization: Bearer`). |
| `ApiVersion` | `string` | `2024-10-21` | Data-plane API version appended as `?api-version=`. |

Startup validation requires `Endpoint`, `ApiKey`, a non-empty `ApiVersion`, and at least one
of `Deployment`/`EmbeddingDeployment`. Resolving the chat client with no `Deployment` throws
`AiException` with `AiErrorCode.Configuration`.

## Deployments vs. models

Azure routes by deployment name, not model id: `ChatRequest.Model` defaults to `Deployment`
when unset, and if you set it, it must name a deployment on the resource, not an OpenAI model
id. In a [fallback](provider-fallback.md) chain, leave `Model` null so each candidate uses its
own default.

## Capabilities

Chat ✅, streaming ✅ (SSE), tool calling ✅, structured output ✅ (`json_schema`),
embeddings ✅ (via `EmbeddingDeployment`), health probe ✅, `Retry-After` surfaced ✅.

## Dependency-injection usage

`AddAzureOpenAI` registers chat + embedding clients under the same name plus a named
`HttpClient`. See [dependency injection](dependency-injection.md).

## Error mapping

Azure OpenAI shares the OpenAI mapping (same wire protocol):

| Wire condition | `AiErrorCode` |
|---|---|
| 401 (invalid `api-key`) | `Authentication` |
| 403 (network/RBAC deny) | `PermissionDenied` |
| 404 (unknown deployment) | `ModelNotFound` |
| 400 / 422 (bad request, context length) | `InvalidRequest` |
| 429 | `RateLimited` (`RetryAfter` surfaced; `insufficient_quota` marked non-transient) |
| 5xx | `ProviderUnavailable` |
| network failure / timeout | `Network` / `Timeout` |
| content-filter finish | `ContentFiltered` (on response mapping) |

## Health probe

The client implements `IProviderHealthProbe` (inherited): an authenticated GET against the
resource using the `api-key` header. Used by [health checks](health-checks.md); never a paid
completion.

## Cancellation

Standard contract: `OperationCanceledException` on caller cancellation.

## Security considerations

Store the key in Azure Key Vault or environment configuration — never source code (startup
validation enforces presence, not secrecy). Prefer private endpoints where available. The
`api-key` header is scrubbed from all diagnostics.

## Thread safety

Thread-safe singletons, like all Koras.AI clients.

## Testing applications using this feature

Fake `IChatClient` for application tests. For provider-level tests, stub the
`HttpMessageHandler` and assert the request URI contains
`/openai/deployments/{name}/chat/completions?api-version=2024-10-21` and the `api-key` header
— see `tests/Koras.AI.UnitTests/Providers/AzureOpenAIClientTests.cs`.

## Common mistakes

- Passing an OpenAI model id as `ChatRequest.Model` — Azure expects deployment names; you get
  `ModelNotFound` (404).
- Using `Authorization: Bearer` conventions from openai.com; Azure uses the `api-key` header
  (handled for you, but relevant when configuring gateways).
- Forgetting `EmbeddingDeployment` and expecting embeddings to work because chat does.
- Overriding `ApiVersion` to a version that does not support `json_schema` response formats.

## Related features

- [OpenAI provider](provider-openai.md)
- [Chat completion](chat-completion.md) · [Structured output](structured-output.md) ·
  [Embeddings](embeddings.md)
- [Health checks](health-checks.md)
- [Error handling](error-handling.md)
