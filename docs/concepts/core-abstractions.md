# Core Abstractions

All types below live in the `Koras.AI` namespace, shipped in `Koras.AI.Abstractions`.

## IChatClient

The central contract: complete or stream a conversation. Implementations are thread-safe
singletons; failures surface as `AiException`, caller cancellation as
`OperationCanceledException`.

```csharp
ChatResponse response = await chat.CompleteAsync(
    new ChatRequest { Messages = [ChatMessage.User("Hello!")] });

await foreach (ChatStreamUpdate update in chat.StreamAsync(ChatRequest.FromPrompt("Hello!")))
{
    Console.Write(update.TextDelta);
}
```

`ProviderName` identifies the backing provider (`"openai"`, `"ollama"`, `"fallback"`, …).
Convenience extensions in `ChatClientExtensions` add `CompleteAsync(string prompt)` and the
structured-output `CompleteAsync<T>` overloads.

## IEmbeddingClient

The embeddings counterpart, for providers that support it:

```csharp
EmbeddingResponse result = await embeddings.GenerateAsync(
    new EmbeddingRequest("Koras.AI is a provider-neutral AI SDK for .NET."));
ReadOnlyMemory<float> vector = result.Embeddings[0].Vector;
```

Providers without embeddings (for example Anthropic) throw `AiException` with
`AiErrorCode.NotSupported`.

## ChatMessage and ChatRole

Messages are immutable and built through factory methods; `ChatRole` is an extensible
"enum-like" record struct (`System`, `User`, `Assistant`, `Tool`):

```csharp
List<ChatMessage> messages =
[
    ChatMessage.System("You are a terse assistant."),
    ChatMessage.User("What is a decorator?"),
    ChatMessage.Assistant("A wrapper that adds behavior around an inner implementation."),
    ChatMessage.User("Give a .NET example."),
];
```

Assistant messages can carry `ToolCalls`; tool-result messages are created with
`ChatMessage.ToolResult(toolCallId, content)`.

## ChatRequest and ChatOptions

`ChatRequest` bundles the conversation, an optional model override, and options. Both types
are immutable — build once, reuse freely:

```csharp
var request = new ChatRequest
{
    Messages = messages,
    Model = "gpt-4o",              // null → the client's configured DefaultModel
    Options = new ChatOptions
    {
        Temperature = 0.2,
        MaxOutputTokens = 500,
        StopSequences = ["END"],
    },
};
```

`ChatRequest.FromPrompt(prompt, systemPrompt)` shortcuts the single-turn case.
`ChatOptions.AdditionalProperties` is the provider-specific escape hatch
([overview](overview.md)).

## ChatResponse and ChatStreamUpdate

`CompleteAsync` returns the full response; `StreamAsync` yields incremental updates ending
with a terminal update that carries the finish reason:

```csharp
ChatResponse response = await chat.CompleteAsync(request);
Console.WriteLine(response.Text);              // convenience for Message.Text
Console.WriteLine(response.FinishReason);      // Stop, Length, ToolCalls, ContentFilter…
Console.WriteLine(response.Usage.TotalTokens);
// response.Provider, response.Model, response.ResponseId, response.RawRepresentation
```

`ChatStreamUpdate` exposes `TextDelta`, `ToolCallDelta`, and — on the relevant updates —
`FinishReason` and `Usage`.

## AiTool and ToolCall

`AiTool.Create` builds an invocable tool from a delegate (the JSON Schema is generated from
the parameters); `AiTool.Declare` describes a tool you execute yourself:

```csharp
var weather = AiTool.Create(
    "get_weather",
    "Gets the current weather for a city",
    ([Description("The city name")] string city) => $"18°C and sunny in {city}");

ChatResponse answer = await chat.CompleteAsync(new ChatRequest
{
    Messages = [ChatMessage.User("Weather in Oslo?")],
    Options = new ChatOptions { Tools = [weather] },
});
```

With `ai.UseToolInvocation()` registered, invocable tools run automatically in a loop until
the model answers. Without it, the response's `Message.ToolCalls` contains `ToolCall`
entries (`Id`, `Name`, `ArgumentsJson`, plus `ParseArguments<T>()`) for manual handling.
`ChatOptions.ToolChoice` (`Auto`, `None`, `Required`, `Tool(name)`) steers tool use.

## ChatResponseFormat

Controls the output shape:

```csharp
ChatResponseFormat.Text                                  // default free text
ChatResponseFormat.Json                                  // "JSON mode"
ChatResponseFormat.JsonSchema("recipe", schema)          // your own schema
ChatResponseFormat.ForType<Recipe>()                     // schema generated from T
```

The `CompleteAsync<T>` extension applies `ForType<T>()` automatically and deserializes the
result:

```csharp
ChatResponse<Recipe> recipe = await chat.CompleteAsync<Recipe>("A simple pancake recipe.");
Console.WriteLine(recipe.Value.Name);                    // recipe.Raw is the full ChatResponse
```

## TokenUsage

A record struct with `InputTokens`, `OutputTokens`, and computed `TotalTokens`; instances
add together, which the tool loop uses to report usage across all round-trips:

```csharp
TokenUsage total = firstResponse.Usage + secondResponse.Usage;
Console.WriteLine($"{total.InputTokens} in / {total.OutputTokens} out = {total.TotalTokens}");
```

## Related

- [Error handling](error-handling.md) — `AiException` and `AiErrorCode`.
- [Lifecycle](lifecycle.md) — what actually happens when you call these APIs.
- The full contract listing: [public API design](../api/public-api-design.md).
