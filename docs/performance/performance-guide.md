# Performance Guide

What the SDK does for you, and what to do (and avoid) in hot paths.

## Design choices baked into the SDK

| Choice | Effect |
|---|---|
| Pooled `HttpClient` via `IHttpClientFactory` | Every provider registration calls `AddHttpClient("Koras.AI.<name>")`; handlers are pooled and recycled, so no socket exhaustion and no stale-DNS pinning. |
| Streaming never buffers whole responses | `StreamAsync` yields `ChatStreamUpdate`s as SSE/JSONL events arrive off the wire; memory is proportional to a chunk, not the completion. |
| `LoggerMessage` source generation | All SDK log sites are source-generated (see `LoggingChatClient`); disabled log levels cost a branch, not allocations or boxing. |
| Parse-once templates | `PromptTemplate.Parse(...)` compiles the template once; `Render` reuses the parsed form. Parse at startup, render per request. |
| Cached clients from the factory | `IChatClientFactory.GetChatClient(name)` returns the composed (retry/logging/telemetry-decorated) client from cache; resolution is dictionary-lookup cheap. |
| Metadata-only telemetry by default | No content capture, no span bloat unless `EnableSensitiveData` + Trace are both on. |

## Guidance for consumers

### Reuse clients

Resolve a client once per scope (or inject `IChatClient` directly) and reuse it. Clients are
thread-safe singletons by design; constructing your own `HttpClient` per call defeats the
factory pooling above.

### Streaming is for UX, not throughput

`StreamAsync` improves *time-to-first-token* — perceived latency in chat UIs. It does not make
the overall completion faster and adds per-chunk overhead. For backend jobs that only need the
final text, `CompleteAsync` is the cheaper call.

### Batch embeddings

The embedding clients accept multiple inputs per request. One request with N inputs beats N
requests with one input on every axis: connection reuse, provider-side batching, and rate-limit
budget. Batch as large as your provider's per-request limits allow.

### Tune `MaxOutputTokens`

Output tokens dominate both latency and cost — generation is sequential. Set
`ChatOptions.MaxOutputTokens` to a realistic ceiling for each call site instead of accepting a
large default; a summary endpoint rarely needs 4096 tokens of budget.

### Connection limits under high concurrency

The default handler is `SocketsHttpHandler`, whose `MaxConnectionsPerServer` is unlimited by
default — HTTP/1.1 concurrency scales with sockets, and HTTP/2 multiplexes on fewer
connections. For very high fan-out against one provider host, configure the named handler:

```csharp
services.AddHttpClient("Koras.AI.openai")
    .ConfigureHttpMessageHandlerBuilder(b => b.PrimaryHandler = new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2), // periodic DNS refresh
        EnableMultipleHttp2Connections = true,              // avoid HTTP/2 stream-limit stalls
    });
```

Long-lived streams hold their connection for the stream's lifetime — budget concurrent streams
accordingly.

### When to disable retry

Retry (default 3 attempts, exponential backoff with full jitter, `Retry-After` honored) is
right for most workloads. Skip `UseRetry()` when:

- **You have your own outer retry** (message-queue redelivery, workflow engine) — stacked
  retries multiply: 3 × 3 = 9 provider calls per logical attempt.
- **Latency budget is absolute** — an interactive path that must answer in ~2 s gains nothing
  from a retry that starts after a 1 s+ backoff; fail fast and degrade in the UI instead.
- **Requests are not idempotent for you** — e.g., you bill per attempt.

Also remember the per-attempt timeout (100 s default) bounds worst-case hang time; lower
`RetryOptions.AttemptTimeout` for latency-sensitive paths rather than removing retry entirely.

## Measuring

Use the [benchmarks](benchmarks.md) to measure SDK-side costs, and the built-in
`koras.ai.client.operation.duration` histogram (see
[observability](../architecture/observability.md)) for end-to-end latency in production —
provider inference time dwarfs SDK overhead in every realistic profile.

See also: [memory-management.md](memory-management.md).
