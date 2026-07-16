# Logging Guide

Koras.AI logs through standard `Microsoft.Extensions.Logging` with source-generated,
structured messages. No configuration is needed beyond your normal logging setup — if the
host has an `ILoggerFactory`, the SDK logs. Design background:
[observability](../architecture/observability.md).

## Categories

All categories start with `Koras.AI`, so one filter rule governs the SDK:

| Category | Emits |
|---|---|
| `Koras.AI.ChatClient` | operation start / completed / failed, content (opt-in) |
| `Koras.AI.RetryChatClient` | retry scheduled warnings |
| `Koras.AI.FallbackChatClient` | failover warnings |

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Koras.AI": "Information"
    }
  }
}
```

Set `"Koras.AI": "Debug"` to also see operation-start records, or `"Warning"` to see only
retries, failovers, and failures.

## Event IDs

Stable IDs for filtering and alerting:

| EventId | Level | Meaning |
|---|---|---|
| 1001 `KorasAiRetryScheduled` | Warning | a transient failure will be retried (attempt, delay, error code) |
| 2001 | Debug | chat operation starting (provider, model, streaming flag, message count) |
| 2002 | Information | chat operation completed (provider, model, duration, tokens, finish reason) |
| 2003 | Error | chat operation failed terminally (code, HTTP status, request id) |
| 2004 | Trace | request content — only with `EnableSensitiveData` |
| 2005 | Trace | response content — only with `EnableSensitiveData` |
| 3001 `KorasAiFallback` | Warning | failover from one candidate client to the next |

## Sample output

A healthy completion, a retried rate limit, and a failover look like this in the default
console formatter:

```text
dbug: Koras.AI.ChatClient[2001]
      Koras.AI chat starting: provider=openai model=gpt-4o-mini streaming=False messages=2
warn: Koras.AI.RetryChatClient[1001]
      Koras.AI chat attempt 1/3 failed with RateLimited; retrying in 1247ms (provider=openai)
warn: Koras.AI.FallbackChatClient[3001]
      Koras.AI failing over from openai to ollama after RateLimited
info: Koras.AI.ChatClient[2002]
      Koras.AI chat completed: provider=ollama model=llama3.2 duration=843ms inputTokens=24 outputTokens=57 finishReason=stop
```

And a terminal failure:

```text
fail: Koras.AI.ChatClient[2003]
      Koras.AI chat failed: provider=openai model=gpt-4o-mini code=Authentication status=401 requestId=req_abc123
```

All fields are structured properties (`{Provider}`, `{Code}`, …), so JSON/Seq/OTLP sinks
can query them directly.

## Content is never logged by default

Prompts and responses **never** appear in logs out of the box — records carry metadata only
(provider, model, durations, token counts, error codes). Content capture exists solely for
local debugging and requires two explicit switches:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o => o.DefaultModel = "llama3.2");
    ai.ConfigureTelemetry(t => t.EnableSensitiveData = true);   // switch 1
});
```

```json
{ "Logging": { "LogLevel": { "Koras.AI": "Trace" } } }          // switch 2
```

Only with both does the SDK emit events 2004/2005:

```text
trce: Koras.AI.ChatClient[2004]
      Koras.AI request content (EnableSensitiveData=true): role=user content=What's the weather in Oslo?
trce: Koras.AI.ChatClient[2005]
      Koras.AI response content (EnableSensitiveData=true): It's 18°C and sunny in Oslo.
```

> **Warning:** never enable `EnableSensitiveData` in production. Prompts and responses
> routinely contain end-user personal data; once written to logs they are subject to your
> log retention, shipping, and access-control story. The flag also gates content capture on
> tracing spans ([telemetry guide](telemetry.md)). API keys are never logged regardless of
> any setting.

## What to alert on

- **Event 2003 rate** — terminal failures; a spike in `code=Authentication` means a rotated
  or expired key.
- **Event 1001 rate** — retry pressure; sustained `RateLimited` retries mean you need more
  quota or a fallback.
- **Event 3001 at all** — you are running on a secondary provider; capacity and cost differ.

For metrics-based dashboards on the same signals, prefer the meter instruments in the
[telemetry guide](telemetry.md).
