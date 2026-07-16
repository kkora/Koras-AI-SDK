# Minimal API Guide

A walkthrough of [`samples/MinimalApi.Sample`](../../samples/MinimalApi.Sample/Program.cs):
two endpoints, one JSON and one streaming Server-Sent Events (SSE), in under 60 lines. The
step-by-step build of this sample is in
[your first application](../getting-started/first-application.md); this guide focuses on the
streaming details and consuming the stream from a browser.

## The endpoints

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:OpenAI:ApiKey"]))
    {
        ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI")).AsDefault();
    }

    ai.UseRetry();
});

// POST /chat  { "prompt": "..." }
app.MapPost("/chat", async (ChatPrompt body, IChatClient chat, CancellationToken ct) =>
{
    ChatResponse response = await chat.CompleteAsync(body.Prompt, ct);
    return Results.Ok(new { text = response.Text, provider = response.Provider,
        model = response.Model, inputTokens = response.Usage.InputTokens,
        outputTokens = response.Usage.OutputTokens });
});

// POST /chat/stream  { "prompt": "..." }  → text/event-stream
app.MapPost("/chat/stream", async (ChatPrompt body, IChatClient chat, HttpContext context, CancellationToken ct) =>
{
    context.Response.ContentType = "text/event-stream";
    await foreach (ChatStreamUpdate update in chat.StreamAsync(ChatRequest.FromPrompt(body.Prompt), ct))
    {
        if (update.TextDelta is { Length: > 0 } delta)
        {
            await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(delta)}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }

    await context.Response.WriteAsync("data: [DONE]\n\n", ct);
});
```

## Why the streaming endpoint looks like that

- **`text/event-stream`** is the SSE content type; each message is a `data:` line followed
  by a blank line.
- **Each delta is JSON-serialized** (`JsonSerializer.Serialize(delta)`) so newlines and
  special characters inside a token cannot break the SSE framing — the browser parses one
  JSON string per event.
- **`FlushAsync` after every write** defeats response buffering; without it the client sees
  the whole answer at once.
- **The injected `CancellationToken` is `RequestAborted`**, so when the user closes the tab
  the loop throws `OperationCanceledException`, the enumerator is disposed, and the upstream
  model call is cancelled — no tokens wasted
  ([cancellation](../concepts/cancellation.md)).
- **`data: [DONE]`** is a conventional sentinel so clients know the stream finished rather
  than dropped.

## Consuming the stream in the browser

SSE over POST needs `fetch` with a stream reader (the built-in `EventSource` only does GET):

```js
const response = await fetch("/chat/stream", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ prompt: "Tell me a short story." }),
});

const reader = response.body.getReader();
const decoder = new TextDecoder();
let buffer = "";

for (;;) {
  const { value, done } = await reader.read();
  if (done) break;
  buffer += decoder.decode(value, { stream: true });

  let index;
  while ((index = buffer.indexOf("\n\n")) >= 0) {
    const line = buffer.slice(0, index).trim();
    buffer = buffer.slice(index + 2);
    if (!line.startsWith("data: ")) continue;
    const payload = line.slice(6);
    if (payload === "[DONE]") return;
    output.textContent += JSON.parse(payload);   // one token appears at a time
  }
}
```

## Try it from the terminal

```sh
dotnet run --project samples/MinimalApi.Sample

curl -s localhost:5000/chat -H "Content-Type: application/json" \
     -d '{"prompt":"Say hello in Norwegian."}'

curl -N localhost:5000/chat/stream -H "Content-Type: application/json" \
     -d '{"prompt":"Count to 10 slowly."}'
```

`curl -N` disables curl's buffering so the `data:` lines print as they arrive.

## Related

- [First application](../getting-started/first-application.md) — building this from scratch,
  including user secrets.
- [ASP.NET Core guide](aspnet-core.md) — controllers, fallback, error-to-HTTP mapping.
- [Lifecycle](../concepts/lifecycle.md) — when the streaming request actually starts.
