# Request Lifecycle

What actually happens between `await chat.CompleteAsync(...)` and your response. Knowing the
moving parts makes timeouts, retries, and streaming behavior predictable.

## Client construction and caching

1. `services.AddKorasAI(ai => ...)` records *how* to build each named client — nothing is
   constructed yet.
2. The first resolution of `IChatClient` (or `IChatClientFactory.GetChatClient("name")`)
   makes the factory build that client: provider client first, then per-client decorators,
   then global decorators (registration order, innermost first), then logging and telemetry
   outermost.
3. The finished chain is **cached per name**. Every later resolution returns the same
   instance — clients are singletons for the process lifetime, which is safe because all of
   them are thread-safe ([thread safety](thread-safety.md)).

Provider options are read once during this build. Startup validation has already run
(`ValidateOnStart`), so construction does not fail on missing configuration.

## HttpClient usage

Provider clients do not `new HttpClient()`. Each provider registration adds a **named
`HttpClient`** (`Koras.AI.{clientName}`) and resolves it through `IHttpClientFactory`, so
you get handler pooling, DNS rotation, and a hook for customization: configure
`services.AddHttpClient("Koras.AI.openai", ...)` to add proxies, custom handlers, or
timeouts for a specific client.

## Anatomy of a CompleteAsync call

With `UseRetry()` and `UseToolInvocation()` registered:

1. **Telemetry** starts an activity (`chat {model}`) and a duration measurement.
2. **Logging** writes the start record (provider, model, message count — never content).
3. **Tool loop** passes through (no tools in the request) or begins its model↔tool
   iteration.
4. **Retry** starts attempt 1 with a linked `CancellationTokenSource` implementing the
   per-attempt timeout (`RetryOptions.AttemptTimeout`).
5. **Provider client** maps `ChatRequest` to provider JSON, sends it on the named
   `HttpClient`, and maps the response back — or maps the wire error to an `AiException`
   with the right `AiErrorCode`.
6. On a transient failure, retry waits (exponential backoff with jitter, honoring
   `Retry-After`) and repeats step 5 with a fresh attempt timeout, up to `MaxAttempts`.
7. The response bubbles out: the tool loop may add messages and go around again; logging
   writes the completion record with duration and token usage; telemetry tags the span and
   records metrics.

There are no hidden network calls anywhere in the SDK — every network operation is an
explicit `*Async` you invoked.

## Streaming lifecycle

`StreamAsync` returns an `IAsyncEnumerable<ChatStreamUpdate>` and is lazy:

```csharp
IAsyncEnumerable<ChatStreamUpdate> stream = chat.StreamAsync(request); // nothing sent yet

await foreach (ChatStreamUpdate update in stream)   // ← request starts on first MoveNextAsync
{
    Console.Write(update.TextDelta);
}                                                   // ← enumerator disposal releases the connection
```

- **The HTTP request starts on the first `MoveNextAsync`**, not when you call `StreamAsync`.
- Updates arrive as the provider emits them; the terminal update carries the finish reason
  (and usage when the provider reports it).
- **Disposing the enumerator releases the connection.** `await foreach` does this for you,
  including when you `break` early or an exception unwinds. If you enumerate manually,
  dispose the enumerator (`await using`).
- Retry and fallback act only **before the first update is emitted**; after data has been
  observed, a failure mid-stream surfaces as `AiException` to your loop
  ([architecture](architecture.md)).
- The telemetry span spans the whole enumeration and ends when the stream completes or the
  enumerator is disposed.

Abandoning a stream early is legitimate and cheap — disposal cancels the underlying request
so the provider stops generating (and billing) as soon as it notices.

## Where time limits come from

| Limit | Source | Scope |
|---|---|---|
| Per-attempt timeout | `RetryOptions.AttemptTimeout` (default 100 s) | one HTTP attempt |
| Overall caller budget | your `CancellationToken` | the whole call, all attempts |
| Backoff between attempts | `RetryOptions.BaseDelay`/`MaxDelay` + `Retry-After` | between attempts |

The caller's token always wins: when it fires, the call throws
`OperationCanceledException` immediately, regardless of retry state
([cancellation](cancellation.md)).
