# Troubleshooting: Diagnostics

How to see what the SDK is doing: logs, exception diagnostics, activity tracing, and health
probes. For the emitted signals in full, see [observability](../architecture/observability.md).

## Turn on Debug logging

Operation start events log at `Debug` under the `Koras.AI.*` categories; completion logs at
`Information`, retries and failovers at `Warning`, terminal failures at `Error`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.AI": "Debug"
    }
  }
}
```

Typical output while diagnosing:

```text
dbug: Koras.AI chat starting: provider=openai model=gpt-4o-mini streaming=False messages=3
warn: Koras.AI retry 1/3 scheduled in 812ms after RateLimited from provider openai
info: Koras.AI chat completed: provider=openai model=gpt-4o-mini duration=1043ms inputTokens=412 outputTokens=97 finishReason=stop
```

Message *content* never appears at these levels. To see prompts/completions locally, set
`Koras.AI` to `Trace` **and** enable `ConfigureTelemetry(t => t.EnableSensitiveData = true)` —
see [logging](logging.md) for the caveats.

## Reading `RequestId` and `ProviderErrorBody`

Every terminal failure carries the provider's own diagnostics on the `AiException`:

```csharp
try
{
    return await chat.CompleteAsync(request, ct);
}
catch (AiException ex)
{
    logger.LogError(ex,
        "AI call failed: code={Code} status={Status} provider={Provider} requestId={RequestId} body={Body}",
        ex.Code, ex.StatusCode, ex.Provider, ex.RequestId, ex.ProviderErrorBody);
    throw;
}
```

- `RequestId` is the provider's request identifier — quote it when contacting provider
  support; it lets them find the exact request in their logs.
- `ProviderErrorBody` is the raw provider error payload (truncated to 4 KB, credentials
  scrubbed). Provider messages are usually more specific than the normalized code — e.g.
  which parameter was invalid, or `insufficient_quota` vs `rate_limit_exceeded` behind the
  same 429.
- The original exception (e.g. the socket error behind `Network`) is `InnerException`.
- For `InvalidResponse` from structured output, `ProviderErrorBody` contains the model's raw
  text that failed to deserialize.

## Activity tracing

The SDK emits `System.Diagnostics` activities from `ActivitySource("Koras.AI")` — one span
per operation (`chat {model}`, `embeddings {model}`, `execute_tool {tool}`) with OTel GenAI
tags: `gen_ai.system` (provider), `gen_ai.request.model`, token usage, `error.type` (the
`AiErrorCode`, lowercase), and `koras.ai.client.name`. Fallback failovers appear as a
`koras.ai.fallback` span event with source/target client names.

Wire it up with the `Koras.AI.OpenTelemetry` package:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddKorasAI().AddOtlpExporter())
    .WithMetrics(m => m.AddKorasAI().AddOtlpExporter());
```

Tracing answers the questions logs can't:

- **"Why was this request slow?"** — the `chat` span shows whether time went to the
  provider, to retry backoff (repeated attempts inside the span window), or to tool
  execution (`execute_tool` child spans).
- **"Which provider actually served it?"** — `gen_ai.system` on the span, plus
  `koras.ai.fallback` events showing any failover path.
- **"Is one named client misbehaving?"** — filter by `koras.ai.client.name`.

For quick local inspection without an OTel backend, add an `ActivityListener` for the
`"Koras.AI"` source, or check the four meters (`koras.ai.client.operation.duration`,
`...token.usage`, `...retries`, `...fallbacks`) with `dotnet-counters`:

```bash
dotnet-counters monitor --process-id <pid> --counters Koras.AI
```

## Checking the health endpoint

With `Koras.AI.AspNetCore` wired (`AddHealthChecks().AddKorasAI("openai", tags: ["ready"])`),
the health endpoint probes the provider's lightweight endpoint (model list / version — never
a paid completion):

```bash
curl -s http://localhost:5000/health
# Healthy    → provider reachable and credentials accepted
# Degraded   → some (not all) probed named clients failing
# Unhealthy  → the probed client cannot reach/authenticate to its provider
```

An `Unhealthy` result with `Authentication` in the logs isolates config problems from
model-call problems: the probe uses the same credentials and endpoint as real traffic, so if
the probe passes but chat fails, look at model/deployment names rather than keys.

## See also

- [Common errors](common-errors.md) — code-by-code causes and fixes.
- [Logging](logging.md) — categories, event ids, and the sensitive-data switch.
