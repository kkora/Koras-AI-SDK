# Troubleshooting: Logging

The SDK logs through standard `Microsoft.Extensions.Logging` with source-generated,
structured messages. No content and no credentials appear at `Debug` or above — content is
opt-in at `Trace` only.

## Categories

Log categories follow the emitting component under the `Koras.AI` namespace, so a single
filter covers everything:

| Category | What it logs |
|---|---|
| `Koras.AI.*` (chat pipeline) | Operation start / completion / terminal failure |
| `Koras.AI.RetryChatClient` | Retry scheduling |
| `Koras.AI.FallbackChatClient` | Failovers between candidates |

```json
{ "Logging": { "LogLevel": { "Koras.AI": "Debug" } } }
```

## Levels and event ids

| EventId | Level | Message template |
|---|---|---|
| 2001 | Debug | `Koras.AI chat starting: provider={Provider} model={Model} streaming={Streaming} messages={MessageCount}` |
| 2002 | Information | `Koras.AI chat completed: provider={Provider} model={Model} duration={DurationMs}ms inputTokens={InputTokens} outputTokens={OutputTokens} finishReason={FinishReason}` |
| 2003 | Error | `Koras.AI chat failed: provider={Provider} model={Model} code={Code} status={StatusCode} requestId={RequestId}` |
| 2004 | Trace | `Koras.AI request content (EnableSensitiveData=true): role={Role} content={Content}` |
| 2005 | Trace | `Koras.AI response content (EnableSensitiveData=true): {Content}` |
| 1001 (`KorasAiRetryScheduled`) | Warning | `Koras.AI retry {Attempt}/{MaxAttempts} scheduled in {DelayMs}ms after {ErrorCode} from provider {Provider}` |
| 3001 (`KorasAiFallback`) | Warning | `Koras.AI fallback: provider {FromProvider} failing over to {ToProvider} after {ErrorCode}` |

All parameters are structured fields — queryable in Seq/ELK/App Insights as `Provider`,
`Code`, `RequestId`, etc., not just message text.

## Sample output

A request that hits a rate limit, retries, then succeeds:

```text
dbug: Koras.AI[2001] Koras.AI chat starting: provider=openai model=gpt-4o-mini streaming=False messages=2
warn: Koras.AI.RetryChatClient[1001] Koras.AI retry 1/3 scheduled in 743ms after RateLimited from provider openai
info: Koras.AI[2002] Koras.AI chat completed: provider=openai model=gpt-4o-mini duration=2211ms inputTokens=180 outputTokens=64 finishReason=stop
```

A failover to a secondary provider:

```text
warn: Koras.AI.FallbackChatClient[3001] Koras.AI fallback: provider openai failing over to ollama after ProviderUnavailable
info: Koras.AI[2002] Koras.AI chat completed: provider=ollama model=llama3.2 duration=904ms inputTokens=180 outputTokens=71 finishReason=stop
```

A terminal failure:

```text
fail: Koras.AI[2003] Koras.AI chat failed: provider=openai model=gpt-4o-mini code=Authentication status=401 requestId=req_a1b2c3
      Koras.AI.AiException: Incorrect API key provided. ...
```

## The sensitive-data switch

Prompts and completions are user data. By default they are **never** logged at any level.
Two conditions must both hold for content to appear (event ids 2004/2005):

1. `ai.ConfigureTelemetry(t => t.EnableSensitiveData = true);`
2. The `Koras.AI` category is enabled at `Trace`.

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o => o.DefaultModel = "llama3.2");
#if DEBUG
    ai.ConfigureTelemetry(t => t.EnableSensitiveData = true);   // local debugging only
#endif
});
```

Caveats:

- **Never enable in production.** Content ends up wherever your logs go — aggregators,
  backups, third-party sinks — with their retention policies, not yours.
- The switch also gates content capture on tracing spans; leaving it `false` keeps spans
  content-free too.
- API keys are excluded regardless: they are scrubbed from exceptions, diagnostics, and
  `ProviderErrorBody` no matter what this switch says. The switch controls *message
  content*, not credentials — there is no switch that logs credentials.

## What the SDK never logs

- API keys and auth headers (scrub list at the provider base; dedicated tests assert absence).
- Message content at `Debug`/`Information`/`Warning`/`Error` — only metadata: provider,
  model, duration, token counts, error code, status, request id.

## See also

- [Diagnostics](diagnostics.md) — combining logs with tracing and health checks.
- [Observability architecture](../architecture/observability.md) — the full signal design.
