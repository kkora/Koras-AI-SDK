# Recipes: Common Scenarios

Copy-paste snippets for everyday tasks. All examples assume a registered client
(see [dependency injection](../features/dependency-injection.md)) and
`using Koras.AI;` at the top of the file.

## Single prompt

```csharp
public sealed class GreetingService(IChatClient chat)
{
    public async Task<string?> GreetAsync(CancellationToken ct)
    {
        ChatResponse response = await chat.CompleteAsync("Say hello in three words.", ct);
        return response.Text;
    }
}
```

## Conversation history loop

`ChatRequest.Messages` carries the whole conversation on every call — the SDK keeps no
hidden state. Append the assistant's reply and the next user turn as you go:

```csharp
var history = new List<ChatMessage> { ChatMessage.System("You are a terse assistant.") };

while (Console.ReadLine() is { Length: > 0 } input)
{
    history.Add(ChatMessage.User(input));
    ChatResponse response = await chat.CompleteAsync(new ChatRequest { Messages = history }, ct);
    history.Add(response.Message);
    Console.WriteLine(response.Text);
}
```

## System prompt

```csharp
ChatRequest request = ChatRequest.FromPrompt(
    "Summarize this incident report.",
    systemPrompt: "You are a security analyst. Answer in bullet points.");

ChatResponse response = await chat.CompleteAsync(request, ct);
```

## JSON mode

`ChatResponseFormat.Json` asks the provider for a syntactically valid JSON object without
constraining its shape:

```csharp
ChatResponse response = await chat.CompleteAsync(new ChatRequest
{
    Messages = [ChatMessage.User("List three fruits as a JSON object with a 'fruits' array.")],
    Options = new ChatOptions { ResponseFormat = ChatResponseFormat.Json },
}, ct);

using var doc = System.Text.Json.JsonDocument.Parse(response.Text!);
```

## Typed extraction (structured output)

`CompleteAsync<T>` generates a JSON schema from `T`, constrains the response to it, and
deserializes the result. Failures surface as `AiException` with
`AiErrorCode.InvalidResponse` — see [structured output](../features/structured-output.md).

```csharp
public sealed record Invoice(string Customer, decimal Total, DateOnly DueDate);

ChatResponse<Invoice> result = await chat.CompleteAsync<Invoice>(
    $"Extract the invoice fields from this email:\n{emailBody}", ct);

Invoice invoice = result.Value;          // typed value
TokenUsage usage = result.Raw.Usage;     // full ChatResponse still available
```

## RAG-ish prompt assembly with PromptTemplate

`PromptTemplate` (namespace `Koras.AI.Templates`) parses once and renders many times —
handy for stitching retrieved context into a prompt:

```csharp
using Koras.AI.Templates;

private static readonly PromptTemplate AnswerTemplate = PromptTemplate.Parse(
    """
    Answer the question using only the context below.

    Context:
    {{context}}

    Question: {{question}}
    """);

string prompt = AnswerTemplate.Render(new
{
    context = string.Join("\n---\n", retrievedChunks),
    question = userQuestion,
});

ChatResponse response = await chat.CompleteAsync(prompt, ct);
```

A missing placeholder value throws `KeyNotFoundException` naming the placeholder, so
template/render mismatches fail loudly.

## Per-request model override

`ChatRequest.Model` overrides the client's `DefaultModel` for a single call:

```csharp
ChatResponse cheap = await chat.CompleteAsync(new ChatRequest
{
    Messages = [ChatMessage.User("Classify: 'great product!' — positive or negative?")],
    Model = "gpt-4o-mini",
}, ct);

ChatResponse strong = await chat.CompleteAsync(new ChatRequest
{
    Messages = [ChatMessage.User("Draft a contract clause about data retention.")],
    Model = "gpt-4o",
    Options = new ChatOptions { Temperature = 0.2, MaxOutputTokens = 800 },
}, ct);
```

If neither `ChatRequest.Model` nor the provider's `DefaultModel` is set, the call throws
`AiException` with `AiErrorCode.Configuration`.

## See also

- [Advanced scenarios](advanced-scenarios.md) — multiple clients, decorators, escape hatches.
- [Chat completion](../features/chat-completion.md) and [streaming](../features/streaming.md).
