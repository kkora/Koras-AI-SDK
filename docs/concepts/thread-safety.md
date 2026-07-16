# Thread Safety

Koras.AI is designed for the singleton lifetime: register once, inject everywhere, call
concurrently. This page states exactly what is guaranteed — and the one thing that is not.

## Clients and decorators: singleton-safe

Every public client and decorator is **thread-safe for concurrent singleton use**:

- All provider clients (`OpenAIChatClient`, `OllamaChatClient`, …)
- All built-in decorators (retry, fallback, tool loop, logging, telemetry)
- `FallbackChatClient` and any `DelegatingChatClient` chain built by the DI factory
- `IChatClientFactory` (it caches built clients in a concurrent dictionary)

That is why `AddKorasAI` registers `IChatClient`, `IEmbeddingClient`, and
`IChatClientFactory` as singletons, and why injecting them into singleton services, hosted
services, and concurrently executing request handlers is safe:

```csharp
// Safe: one instance, many concurrent requests.
app.MapPost("/chat", async (ChatPrompt body, IChatClient chat, CancellationToken ct)
    => Results.Ok((await chat.CompleteAsync(body.Prompt, ct)).Text));
```

Custom decorators must uphold the same guarantee: a `DelegatingChatClient` subclass you
plug in with `Use(...)` is constructed once and called from many threads, so keep it
stateless or use thread-safe state ([advanced DI guide](../guides/dependency-injection.md)).

## Requests, options, and messages: immutable

`ChatRequest`, `ChatOptions`, `ChatMessage`, and the response types are immutable after
construction (`init`-only properties, read-only lists). Build them once and share freely:

```csharp
// Safe to reuse across threads and requests.
private static readonly ChatOptions TerseJson = new()
{
    Temperature = 0,
    MaxOutputTokens = 300,
    ResponseFormat = ChatResponseFormat.Json,
};
```

## PromptTemplate: parse once, render many

`PromptTemplate` instances are immutable and thread-safe. Parse at startup (or in a static
field) and render concurrently:

```csharp
private static readonly PromptTemplate Summarize =
    PromptTemplate.Parse("Summarize for {{audience}}:\n{{text}}");

// Any thread, any time:
string prompt = Summarize.Render(new { audience = "executives", text = document });
```

`Parse` is the expensive step; `Render` is allocation-light and lock-free.

## Provider options: fixed after startup

Provider options (`OpenAIOptions`, `OllamaOptions`, …) are read when the factory builds the
client, and the client is cached for the process lifetime. Configuration changes after
startup do **not** flow into running clients. Treat AI configuration as immutable per
process; changing a key or endpoint means restarting the process (or registering a second
named client).

## What is NOT guaranteed

**Tool handlers run exactly as you wrote them — sequentially, within the loop.** When the
tool-invocation loop (`UseToolInvocation`) executes a response's tool calls, it invokes the
handlers one at a time, in the order the model requested them, awaiting each before the
next. The SDK adds no parallelism and no synchronization around your handler code:

```csharp
var lookup = AiTool.Create("lookup_order", "Looks up an order",
    async (string orderId) => await orders.GetAsync(orderId)); // runs sequentially per response
```

Two consequences:

- Handlers do not need to defend against the SDK calling them concurrently *within one
  `CompleteAsync` call* — the loop is sequential.
- But two *concurrent* `CompleteAsync` calls that share a tool instance will run its handler
  concurrently, one invocation per call. If a handler touches shared mutable state, that
  state needs its own synchronization — the SDK does not serialize across requests.

## Rules of thumb

1. Inject `IChatClient` / `IChatClientFactory` into singletons without hesitation.
2. Make shared `ChatOptions`, requests, and templates `static readonly`.
3. Keep custom decorators and tool handlers stateless where possible.
4. Never cache mutable state keyed on "the current request" inside a decorator — use
   locals flowing through the call instead.
