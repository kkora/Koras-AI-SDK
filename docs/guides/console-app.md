# Console Application Guide

A walkthrough of [`samples/Console.Sample`](../../samples/Console.Sample/Program.cs), which
exercises the five core features — chat, streaming, structured output, tool calling, and
embeddings — in a single top-level-statements program.

Run it from the repository root (requires local [Ollama](https://ollama.com) with
`ollama pull llama3.2` and `ollama pull nomic-embed-text`):

```sh
dotnet run --project samples/Console.Sample
```

## Host and registration

The sample uses the generic host, so it gets configuration, logging, and DI like any
service:

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string? openAiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o =>
    {
        o.DefaultModel = builder.Configuration["Ollama:Model"] ?? "llama3.2";
        o.DefaultEmbeddingModel = "nomic-embed-text";
    });

    if (!string.IsNullOrEmpty(openAiKey))
    {
        ai.AddOpenAI(o =>
        {
            o.ApiKey = openAiKey;
            o.DefaultModel = "gpt-4o-mini";
            o.DefaultEmbeddingModel = "text-embedding-3-small";
        }).AsDefault();
    }

    ai.UseRetry();
    ai.UseToolInvocation();
});
```

Two things to notice:

- **Keyless by default, upgraded by secret.** Ollama is always registered; OpenAI joins and
  becomes the default (`AsDefault()`) only when
  `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."` has been run.
- `UseRetry()` and `UseToolInvocation()` apply to whichever provider ends up default.

## Resolving clients and Ctrl+C

```csharp
using IHost host = builder.Build();
var chat = host.Services.GetRequiredService<IChatClient>();
var embeddings = host.Services.GetRequiredService<IEmbeddingClient>();

using var lifetimeCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; lifetimeCts.Cancel(); };
```

Every AI call receives `lifetimeCts.Token`, so Ctrl+C cancels in-flight requests cleanly
and the program exits through its `OperationCanceledException` handler (exit code 130).

## The five demos

**Chat** — the string convenience overload, plus token accounting:

```csharp
ChatResponse answer = await chat.CompleteAsync("In one sentence: what is dependency injection?", lifetimeCts.Token);
Console.WriteLine($"{answer.Text} ({answer.Usage.InputTokens} in / {answer.Usage.OutputTokens} out tokens)");
```

**Streaming** — deltas written as they arrive:

```csharp
await foreach (ChatStreamUpdate update in chat.StreamAsync(
    ChatRequest.FromPrompt("Count from 1 to 5 with a word about each number."), lifetimeCts.Token))
{
    Console.Write(update.TextDelta);
}
```

**Structured output** — `CompleteAsync<T>` generates a JSON Schema from the record
(`[Description]` attributes enrich it) and deserializes the reply:

```csharp
ChatResponse<Recipe> recipe = await chat.CompleteAsync<Recipe>("Give me a simple pancake recipe.", lifetimeCts.Token);

[Description("A simple cooking recipe")]
public sealed record Recipe(
    [property: Description("The recipe name")] string Name,
    [property: Description("Ingredient list")] IReadOnlyList<string> Ingredients,
    [property: Description("Total minutes to prepare")] int Minutes);
```

**Tool calling** — because `UseToolInvocation()` is registered, the model's tool call is
executed automatically and the final answer comes back in one `CompleteAsync`:

```csharp
var weatherTool = AiTool.Create(
    "get_weather",
    "Gets the current weather for a city",
    ([Description("The city name")] string city) => $"18°C and sunny in {city}");

ChatResponse toolAnswer = await chat.CompleteAsync(new ChatRequest
{
    Messages = [ChatMessage.User("What's the weather like in Oslo right now?")],
    Options = new ChatOptions { Tools = [weatherTool] },
}, lifetimeCts.Token);
```

**Embeddings** — the sibling client:

```csharp
EmbeddingResponse vectors = await embeddings.GenerateAsync(
    new EmbeddingRequest("Koras.AI is a provider-neutral AI SDK for .NET."), lifetimeCts.Token);
```

## Error handling as an exit-code policy

The sample's catch blocks are a good template for CLI tools — special-case what has a
user-actionable remedy, generalize the rest:

```csharp
catch (AiException ex) when (ex.Code == AiErrorCode.Network)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
    Console.Error.WriteLine("Tip: this sample uses local Ollama by default — install it from https://ollama.com and run 'ollama pull llama3.2'.");
    return 1;
}
catch (AiException ex)
{
    Console.Error.WriteLine($"AI error [{ex.Code}] from {ex.Provider}: {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    return 130;   // conventional SIGINT exit code
}
```

## Related

- [Quick start](../getting-started/quick-start.md) — the minimal version of this app.
- [Error handling](../concepts/error-handling.md), [cancellation](../concepts/cancellation.md).
