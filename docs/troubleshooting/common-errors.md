# Troubleshooting: Common Errors

Every runtime failure from a Koras.AI client is an `AiException` carrying an `AiErrorCode`
(caller cancellation surfaces as `OperationCanceledException` instead). Inspect `Code`,
`StatusCode`, `RequestId`, and `ProviderErrorBody` — see
[diagnostics](diagnostics.md) and the [error model](../architecture/error-model.md).

## `AiErrorCode` reference

| Code | Transient | Likely causes | Fix |
|---|---|---|---|
| `Authentication` | no | Missing/invalid/revoked API key; wrong key for the endpoint | Verify the key source (env var, vault); check you're not sending an OpenAI key to a gateway expecting its own |
| `PermissionDenied` | no | Key valid but lacks access (org, region, model tier) | Grant model/deployment access in the provider console; check org/project settings |
| `ModelNotFound` | no | Typo'd model name; Azure deployment name ≠ model name; model retired | Use the provider's exact identifier; on Azure pass the *deployment* name (see [provider errors](provider-errors.md)) |
| `InvalidRequest` | no | Malformed request; context length exceeded; missing required field (e.g. Anthropic `max_tokens`) | Read `ProviderErrorBody`; trim history or raise `MaxOutputTokens` sanely |
| `ContentFiltered` | no | Provider safety system blocked input or output | Revise the prompt; on Azure adjust the content-filter policy if appropriate |
| `RateLimited` | usually | 429: burst rate limit (transient) or exhausted quota (`IsTransient=false`) | Transient: retry handles it — check `RetryAfter`. Non-transient (`insufficient_quota`): fix billing/quota |
| `ProviderUnavailable` | yes | 5xx, provider overloaded (incl. Anthropic 529) | Retry/fallback handle it; sustained occurrences → check the provider status page |
| `Network` | yes | DNS/connect/socket failure; Ollama not running; proxy/firewall | Verify endpoint reachability; for Ollama see [provider errors](provider-errors.md) |
| `Timeout` | yes | Attempt exceeded `RetryOptions.AttemptTimeout` (default 100 s) | Raise `AttemptTimeout`, lower `MaxOutputTokens`, or stream instead |
| `Canceled` | no | Appears only inside fallback-exhaustion aggregates | Nothing to fix — your token fired |
| `InvalidResponse` | no | Unparseable success payload; structured output didn't match `T` | Inspect `ProviderErrorBody` (holds the raw text); tighten the schema/prompt |
| `NotSupported` | no | Capability missing on this provider (e.g. Anthropic embeddings) | Use a provider that supports it — see the [feature matrix](../features/feature-matrix.md) |
| `ToolExecutionFailed` | no | Tool handler threw with `ErrorBehavior.Throw`; tool loop exceeded `MaxIterations` | Fix the handler; raise `MaxIterations`; check the model isn't looping on a failing tool |
| `Configuration` | no | Invalid options detected at call time (missing model, missing Azure deployment) | See below |
| `Unknown` | no | Unmapped provider response | File an issue with `ProviderErrorBody` and `RequestId` |

New codes may be added in minor releases — always `default`-case your `switch`.

## Missing model configuration

```text
AiException (Code=Configuration): No model specified: set OpenAIOptions.DefaultModel
or pass ChatRequest.Model.
```

Every request needs a model from one of two places: `ChatRequest.Model` (per request) or the
provider's `DefaultModel` (or `Deployment` on Azure). Set one:

```csharp
ai.AddOpenAI(o => { o.ApiKey = key; o.DefaultModel = "gpt-4o-mini"; });
// or per request:
await chat.CompleteAsync(new ChatRequest { Messages = msgs, Model = "gpt-4o-mini" }, ct);
```

## Duplicate client name

```text
InvalidOperationException: A chat client named 'openai' is already registered.
Client names must be unique; pass an explicit name to register the same provider twice.
```

Thrown at registration time (inside `AddKorasAI`). Each parameterless `AddOpenAI(...)` call
uses the default name `"openai"` — to register two OpenAI clients, name them:

```csharp
ai.AddOpenAI("openai-main", o => { /* ... */ });
ai.AddOpenAI("openai-batch", o => { /* ... */ });
```

## No clients registered

```text
InvalidOperationException: No chat clients are registered. Register a provider inside
AddKorasAI, e.g. services.AddKorasAI(ai => ai.AddOpenAI(...)).
```

You called `AddKorasAI` but the configure delegate registered nothing (a common cause:
every conditional registration was skipped because keys were absent). Ensure at least one
`Add<Provider>` or `AddClient` call always runs — the WebApi sample registers Ollama
unconditionally as the floor.

A related error names a lookup miss:
`No chat client is registered under the name 'openai'. Registered names: ollama.` — the
name passed to `IChatClientFactory.GetChatClient` (or listed in `AddFallback`) doesn't match
a registration. Remember the defaults: `openai`, `azure_openai`, `anthropic`, `gemini`,
`ollama`.

## HTTPS validation failure at startup

```text
OptionsValidationException: Koras.AI OpenAI client 'openai': Endpoint must use HTTPS
(HTTP is allowed only for loopback addresses).
```

Remote endpoints must be `https://`; only loopback hosts (`localhost`, `127.0.0.1`, `[::1]`)
may use plain HTTP. If you're pointing at an internal gateway over HTTP, terminate TLS in
front of it. There is deliberately no bypass flag —
see [validation](../configuration/validation.md).

## See also

- [Provider errors](provider-errors.md) — per-provider quirks behind these codes.
- [Diagnostics](diagnostics.md) — extracting request ids and raw error bodies.
- [FAQ](faq.md)
