# Memory Management

Where the SDK allocates, where it deliberately doesn't, and how to keep long-running AI
workloads flat. Companion to the [performance guide](performance-guide.md).

## Allocation model

### Streaming: one `ChatStreamUpdate` per event

`StreamAsync` allocates one `ChatStreamUpdate` per wire event (SSE chunk or JSON line) and
nothing proportional to the whole completion — the SDK never buffers the full response.
Aggregation is the consumer's choice; do it with a `StringBuilder`, not string concatenation:

```csharp
var text = new StringBuilder();
await foreach (ChatStreamUpdate update in client.StreamAsync(request, ct))
{
    text.Append(update.Text);      // O(n) total, amortized
}
```

`+=` on a string inside that loop is O(n²) allocation for long completions.

### Embeddings: `ReadOnlyMemory<float>`

`Embedding.Vector` is a `ReadOnlyMemory<float>` — vectors are parsed once into a single
`float[]` per embedding and handed to you without copies. Slice and pass the memory around
freely; call `.ToArray()` only when an API genuinely needs an array.

### `RawRepresentation`: pay for it only if you read it

`ChatResponse.RawRepresentation` exposes the full provider payload as a `JsonElement`.
Producing it requires cloning the parsed document (`root.Clone()` in every provider's wire
mapper) so the element outlives the parsing buffers — that clone retains the entire response
JSON in memory for the response's lifetime. If you don't consume `RawRepresentation`, simply
never touch it, and drop `ChatResponse` instances promptly rather than caching them long-term
when payloads are large.

### Bounded diagnostics

- Provider error bodies attached to `AiException.ProviderErrorBody` are capped at **4 KB**
  (`ProviderErrors.Truncate`) — a failing provider returning a megabyte of HTML cannot bloat
  your exception pipeline or log sink.
- SDK logging is `LoggerMessage` source-generated: disabled levels allocate nothing.

### No unbounded collections

The SDK holds no growing state: no conversation history, no response caches, no per-request
accumulators beyond the in-flight operation. The tool loop is bounded by `MaxIterations`
(default 8), and each iteration's messages exist only within the request being built.
Steady-state memory of a correctly used client is flat.

## Large conversations

Chat history is an input you own — the SDK sends exactly the `Messages` you pass, every
request. Long-running conversations therefore grow request size, token cost, and serialization
work linearly per turn *on your side*:

- **Trim history** before each call: keep the system message plus a sliding window of recent
  turns, or summarize older turns into a single compacted message.
- Cap tool-result sizes — a tool returning a 200 KB JSON blob rides along in **every**
  subsequent request of that conversation until you trim it.
- Watch `gen_ai.usage.input_tokens` (see [observability](../architecture/observability.md)):
  steadily climbing input tokens per request is the signature of unbounded history.

A first-party conversation-memory package (windowing/summarizing history stores) is on the
roadmap for **1.1** — until then, history management is deliberately explicit in your code.

## Quick reference

| Surface | Behavior | Your move |
|---|---|---|
| Streaming | One update object per chunk, no full-response buffer | Aggregate with `StringBuilder` |
| Embeddings | Single `float[]` per vector via `ReadOnlyMemory<float>` | Avoid `.ToArray()` copies |
| `RawRepresentation` | Cloned `JsonElement`, retains full payload | Ignore it if unused; don't cache responses |
| Error bodies | Truncated to 4 KB | Nothing — bounded by design |
| Conversation history | Caller-owned, resent fully each turn | Trim/summarize; watch input-token metrics |
