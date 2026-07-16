# Cancellation

Every async operation in Koras.AI takes a `CancellationToken` as its last parameter and
follows the standard .NET contract: when **your** token fires, the operation throws
`OperationCanceledException` — never `AiException`. Cancellation is not an error.

## The contract

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    ChatResponse response = await chat.CompleteAsync(request, cts.Token);
}
catch (OperationCanceledException)
{
    // Your token fired (here: the 10-second budget elapsed). Not a provider failure.
}
catch (AiException ex)
{
    // A real failure — see error-handling.md.
}
```

The token flows through the whole decorator pipeline — telemetry, tool loop, retry,
fallback, the provider's HTTP call — so cancelling aborts in-flight network I/O, pending
retry delays, and running tool handlers alike.

`AiErrorCode.Canceled` exists only for wrapping attempts inside aggregate reports (fallback
exhaustion); a directly cancelled call always throws `OperationCanceledException`.

## Per-attempt timeout vs. your token

These are different mechanisms with different outcomes:

| | Source | What you observe |
|---|---|---|
| Attempt timeout | `RetryOptions.AttemptTimeout` (default 100 s) | `AiException` with `AiErrorCode.Timeout`, `IsTransient = true` → **retried** |
| Caller cancellation | your `CancellationToken` | `OperationCanceledException` → **not retried**, propagates immediately |

Internally the retry decorator links your token with a per-attempt timer. When the timer
fires but your token has not, the attempt is converted to a transient `Timeout` error and
retried; when your token fires, the cancellation propagates untouched. Your token is the
overall budget; `AttemptTimeout` is the budget for each individual try.

## Mid-stream cancellation

The token passed to `StreamAsync` cancels enumeration at any point:

```csharp
await foreach (ChatStreamUpdate update in chat.StreamAsync(request, ct))
{
    Console.Write(update.TextDelta);   // OperationCanceledException can surface here
}
```

When cancellation strikes mid-stream, the enumerator's disposal (automatic with
`await foreach`) releases the underlying connection, so the provider stops generating.
Breaking out of the loop early — without any cancellation — achieves the same: disposal
closes the stream ([lifecycle](lifecycle.md)).

## ASP.NET Core: RequestAborted

In web apps, pass the request-abort token so a disconnecting client cancels the model call
instead of burning tokens on an answer nobody will read. Minimal APIs bind it automatically
when the handler declares a `CancellationToken` parameter:

```csharp
app.MapPost("/chat", async (ChatPrompt body, IChatClient chat, CancellationToken ct) =>
{
    ChatResponse response = await chat.CompleteAsync(body.Prompt, ct); // ct == RequestAborted
    return Results.Ok(new { text = response.Text });
});
```

The same holds in controllers (`CancellationToken cancellationToken` action parameter) and
is especially valuable for SSE streaming endpoints, where users routinely navigate away
mid-response. ASP.NET Core converts the resulting `OperationCanceledException` into an
aborted response — no error handling needed for the disconnect case.

## Background services: stoppingToken

In a `BackgroundService`, thread the host's `stoppingToken` through every AI call so
`Ctrl+C` / `SIGTERM` shuts down promptly instead of waiting out a completion:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    ChatResponse summary = await chat.CompleteAsync(request, stoppingToken);
}
```

See the [worker service guide](../guides/worker-service.md) for the full pattern.

## Combining budgets

To impose your own deadline on top of an ambient token, link them:

```csharp
using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
linked.CancelAfter(TimeSpan.FromSeconds(30));

ChatResponse response = await chat.CompleteAsync(request, linked.Token);
```

Whichever fires first cancels the call; either way you observe
`OperationCanceledException`.
