# Streaming

## Overview

`IChatClient.StreamAsync` delivers the model's response incrementally as an
`IAsyncEnumerable<ChatStreamUpdate>`. Each update carries at most a few fields: `TextDelta`
(a fragment of the answer), `ToolCallDelta` (incremental tool-call data), `FinishReason` (set
on the terminal update), `Usage` (set when the provider reports it), and `ResponseId`. OpenAI,
Azure OpenAI, Anthropic, and Gemini stream over SSE; Ollama streams JSON lines — the shape of
the updates is identical either way.

## When to use it

Stream when a human is waiting: chat UIs, CLI output, server-sent events to a browser.

## When not to use it

Prefer `CompleteAsync` for machine-consumed output, for [structured output](structured-output.md)
(especially on Anthropic, where structured output rides a forced tool call), and when you use
the automatic tool-invocation loop, which applies to `CompleteAsync` only.

## Required packages

- `Koras.AI` plus a provider package (for example `Koras.AI.OpenAI`).

## Basic usage

```csharp
using Koras.AI;

var request = ChatRequest.FromPrompt("Write a haiku about compilers.");

await foreach (ChatStreamUpdate update in client.StreamAsync(request, ct))
{
    if (update.TextDelta is { } delta)
    {
        Console.Write(delta);
    }

    if (update.FinishReason is { } reason)
    {
        Console.WriteLine($"\n[{reason}] tokens: {update.Usage?.TotalTokens}");
    }
}
```

## Execution lifecycle

The network request starts when enumeration starts (first `MoveNextAsync`), not when
`StreamAsync` is called. Disposing the enumerator — which `await foreach` does automatically,
including on early `break` — releases the connection. The final update carries the
`FinishReason`; providers that report streaming usage attach `Usage` on or near that update.

## Dependency-injection usage

Registration is identical to [chat completion](chat-completion.md); `StreamAsync` is available
on the injected `IChatClient`. Global decorators added with `ai.UseRetry()` and
`ai.AddFallback(...)` wrap streaming too, with the semantics described below.

### ASP.NET Core usage

```csharp
app.MapGet("/chat", (IChatClient chat, string prompt, CancellationToken ct) =>
{
    async IAsyncEnumerable<string> Stream()
    {
        await foreach (ChatStreamUpdate u in chat.StreamAsync(ChatRequest.FromPrompt(prompt), ct))
        {
            if (u.TextDelta is { } d)
            {
                yield return d;
            }
        }
    }

    return TypedResults.ServerSentEvents(Stream());
});
```

## Tool calls in streams

Tool calls arrive as `ToolCallDelta` updates: `Index` identifies the call, `Id` and `Name`
arrive on the first delta for that call, and `ArgumentsJsonDelta` fragments concatenate into
the arguments JSON. The automatic tool loop does not run for streams — accumulate the deltas
and dispatch yourself if you stream with tools. See [tool calling](tool-calling.md).

## Error handling

Failures before and during the stream throw `AiException` from the enumeration itself (for
example Anthropic mid-stream `error` events, or Ollama's in-band `error` field, which maps to
`AiErrorCode.ProviderUnavailable`). Wrap the `await foreach` in a try/catch. See
[error handling](error-handling.md).

## Retry and timeout behavior

The retry decorator (`ai.UseRetry()`) retries a stream only if it fails **before the first
update is emitted**; once output has started flowing, a mid-stream failure propagates —
replaying half-delivered output would duplicate it. [Provider fallback](provider-fallback.md)
follows the same rule.

## Cancellation

Pass the caller's `CancellationToken` to `StreamAsync`; cancelling raises
`OperationCanceledException` from the in-flight `MoveNextAsync` and tears down the connection.

## Telemetry

The `chat {model}` activity spans the whole stream and ends when the stream completes. Token
usage counters record whatever the provider reported. See [telemetry](telemetry.md).

## Security considerations

Streamed content is user data: apply the same care as with completions. Content never reaches
logs or spans unless `EnableSensitiveData` is explicitly enabled in development.

## Performance considerations

Updates are yielded as they arrive — avoid buffering the entire stream if you only need
incremental display. If you do need the whole text, prefer `CompleteAsync` and skip the
streaming overhead.

## Thread safety

Clients are thread-safe singletons, but a single `IAsyncEnumerator<ChatStreamUpdate>` must be
consumed by one consumer at a time. Start separate `StreamAsync` calls for concurrent streams.

## Testing applications using this feature

Fake the stream with an async iterator:

```csharp
private sealed class FakeStreamingClient : IChatClient
{
    public string ProviderName => "fake";

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatStreamUpdate { TextDelta = "Hello " };
        yield return new ChatStreamUpdate { TextDelta = "world" };
        yield return new ChatStreamUpdate { FinishReason = ChatFinishReason.Stop, Usage = new TokenUsage(3, 2) };
        await Task.CompletedTask;
    }
}
```

## Common mistakes

- Expecting the request to fire when `StreamAsync` returns — it fires on first enumeration.
- Concatenating only `TextDelta` and ignoring the terminal `FinishReason` (you will miss
  truncation via `Length`).
- Streaming structured output on Anthropic — use `CompleteAsync<T>` there (see
  [provider-anthropic.md](provider-anthropic.md)).
- Assuming retry will resume a broken mid-stream response; it will not.

## Related features

- [Chat completion](chat-completion.md)
- [Tool calling](tool-calling.md)
- [Resilience](resilience.md)
- [Provider fallback](provider-fallback.md)
