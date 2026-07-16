# Chat Completion

## Overview

Chat completion is the core operation of Koras.AI: send a conversation to a model and receive
its complete response. The provider-neutral `IChatClient.CompleteAsync` accepts a
`ChatRequest` (messages, optional model, optional `ChatOptions`) and returns a `ChatResponse`
carrying the assistant `ChatMessage`, the `ChatFinishReason`, `TokenUsage`, and the raw
provider payload. Every provider — OpenAI, Azure OpenAI, Anthropic, Gemini, Ollama — is
reached through the same interface.

## When to use it

Use `CompleteAsync` whenever you need the full model output in one piece: request/response
APIs, background jobs, summarization, classification. For incremental delivery use
[streaming](streaming.md); for typed results use [structured output](structured-output.md).

## Required packages

- `Koras.AI` (core, includes `Koras.AI.Abstractions`)
- One provider package, for example `Koras.AI.OpenAI` (see the provider guides)

## Basic usage

```csharp
using Koras.AI;

ChatResponse response = await client.CompleteAsync(
    ChatRequest.FromPrompt("Explain HTTP/3 in two sentences.", systemPrompt: "You are concise."));

Console.WriteLine(response.Text);
Console.WriteLine($"{response.Provider}/{response.Model}: {response.Usage.TotalTokens} tokens, {response.FinishReason}");
```

For a bare prompt there is a string convenience overload:

```csharp
ChatResponse response = await client.CompleteAsync("Explain HTTP/3 in two sentences.");
```

Multi-turn conversations build the message list explicitly:

```csharp
var request = new ChatRequest
{
    Messages =
    [
        ChatMessage.System("You are a helpful assistant."),
        ChatMessage.User("What is a monad?"),
        ChatMessage.Assistant("A monad is a composable computation wrapper..."),
        ChatMessage.User("Show a C# example."),
    ],
    Options = new ChatOptions { Temperature = 0.2, MaxOutputTokens = 500 },
};
ChatResponse reply = await client.CompleteAsync(request);
```

`ChatOptions` also exposes `TopP`, `StopSequences`, `ResponseFormat`, `Tools`, `ToolChoice`,
and `AdditionalProperties` (a provider-specific escape hatch merged into the wire request).

## Dependency-injection usage

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
    ai.UseRetry();
});
```

The default `IChatClient` is registered as a singleton; inject it directly. Additional named
clients are resolved through `IChatClientFactory.GetChatClient(name)` — see
[dependency injection](dependency-injection.md).

```csharp
public sealed class SummaryService(IChatClient chat)
{
    public async Task<string?> SummarizeAsync(string text, CancellationToken ct)
    {
        ChatResponse response = await chat.CompleteAsync(
            ChatRequest.FromPrompt($"Summarize:\n{text}"), ct);
        return response.Text;
    }
}
```

## Error handling

All failures throw `AiException` with a provider-neutral `AiErrorCode` (`Authentication`,
`RateLimited`, `InvalidRequest`, `ProviderUnavailable`, ...). Diagnostics — `StatusCode`,
`RetryAfter`, `RequestId`, `ProviderErrorBody` — are attached and scrubbed of credentials.

```csharp
try
{
    ChatResponse response = await chat.CompleteAsync(request, ct);
}
catch (AiException ex) when (ex.Code == AiErrorCode.RateLimited)
{
    // ex.RetryAfter carries the provider's suggested wait, when sent.
}
```

Also check `ChatResponse.FinishReason`: `Length` means the output was truncated by
`MaxOutputTokens`; `ContentFilter` means the safety system intervened. See
[error handling](error-handling.md) and [../architecture/error-model.md](../architecture/error-model.md).

## Cancellation

Every call takes a `CancellationToken` as the last parameter. Caller cancellation surfaces as
`OperationCanceledException`, never `AiException`. A timeout that the SDK itself enforces
(for example `RetryOptions.AttemptTimeout`) surfaces as `AiException` with `AiErrorCode.Timeout`.

## Telemetry

Each completion produces a `chat {model}` activity on the `ActivitySource("Koras.AI")` and
records `koras.ai.client.operation.duration` and `koras.ai.client.token.usage` on the
`Meter("Koras.AI")`. Message content is never captured by default. See [telemetry](telemetry.md).

## Security considerations

Never hardcode API keys — provide them via user secrets, environment variables, or a vault;
provider options validate keys at startup. Treat prompts and completions as potentially
sensitive data; content logging is off unless `ConfigureTelemetry(t => t.EnableSensitiveData = true)`
is set (development only).

## Thread safety

`IChatClient` implementations, `ChatRequest`, `ChatMessage`, and `ChatOptions` are immutable
or thread-safe and intended for singleton use. Reuse request objects freely across threads.

## Testing applications using this feature

Code against `IChatClient` and substitute a fake in tests:

```csharp
private sealed class FakeChatClient : IChatClient
{
    public string ProviderName => "fake";

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse
        {
            Message = ChatMessage.Assistant("canned answer"),
            Provider = ProviderName,
            FinishReason = ChatFinishReason.Stop,
        });

    public IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
```

Register it with `ai.AddClient("fake", _ => new FakeChatClient())` to exercise the full DI and
decorator pipeline.

## Common mistakes

- Forgetting to set `DefaultModel` in the provider options and omitting `ChatRequest.Model` —
  this throws `AiException` with `AiErrorCode.Configuration` at call time.
- Setting both `Temperature` and `TopP`; prefer adjusting one.
- Ignoring `FinishReason.Length` and silently using truncated output.
- Creating clients per request; they are designed as singletons.

## Related features

- [Streaming](streaming.md)
- [Structured output](structured-output.md)
- [Tool calling](tool-calling.md)
- [Resilience](resilience.md)
- [Error handling](error-handling.md)
