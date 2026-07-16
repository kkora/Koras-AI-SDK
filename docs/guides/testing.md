# Testing Guide

`IChatClient` is a plain interface with no required base class, so testing consumer code
needs no SDK-specific machinery: mock it with your favorite library, or write a small fake.
The SDK's own test suite uses a hand-written fake
([`tests/Koras.AI.UnitTests/TestInfrastructure/FakeChatClient.cs`](../../tests/Koras.AI.UnitTests/TestInfrastructure/FakeChatClient.cs))
— the patterns below are distilled from it.

## A minimal fake

Records requests, returns a canned response, streams from a list:

```csharp
public sealed class FakeChatClient : IChatClient
{
    public string ProviderName => "fake";
    public List<ChatRequest> Requests { get; } = [];
    public string NextText { get; set; } = "ok";
    public List<ChatStreamUpdate> StreamUpdates { get; } = [];

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(new ChatResponse
        {
            Message = ChatMessage.Assistant(NextText),
            Provider = ProviderName,
            FinishReason = ChatFinishReason.Stop,
        });
    }

    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Requests.Add(request);
        await Task.Yield();                          // force genuinely-async enumeration
        foreach (ChatStreamUpdate update in StreamUpdates)
        {
            ct.ThrowIfCancellationRequested();
            yield return update;
        }
    }
}
```

To simulate failures, throw from `CompleteAsync`:
`throw new AiException("boom", AiErrorCode.RateLimited) { RetryAfter = TimeSpan.FromSeconds(2) }` —
this is how you test catch blocks and skip-not-crash policies without a network.

## Asserting on requests

```csharp
[Fact]
public async Task Summarizer_sends_system_instruction_and_caps_tokens()
{
    var fake = new FakeChatClient { NextText = "summary" };
    var sut = new TicketSummarizer(fake);

    await sut.SummarizeAsync("ticket text", CancellationToken.None);

    ChatRequest sent = Assert.Single(fake.Requests);
    Assert.Equal(ChatRole.System, sent.Messages[0].Role);
    Assert.Contains("ticket text", sent.Messages[^1].Text);
    Assert.Equal(200, sent.Options?.MaxOutputTokens);
}
```

## Testing structured output

`CompleteAsync<T>` is an extension over `CompleteAsync`, so a fake that returns JSON text
exercises the full deserialization path, including the schema being applied:

```csharp
[Fact]
public async Task Structured_output_deserializes_and_requests_schema()
{
    var fake = new FakeChatClient { NextText = """{ "name": "Pancakes", "minutes": 20 }""" };

    ChatResponse<Recipe> result = await fake.CompleteAsync<Recipe>("a recipe");

    Assert.Equal("Pancakes", result.Value.Name);
    Assert.IsType<ChatResponseFormat>(fake.Requests[0].Options?.ResponseFormat, exactMatch: false);
}

public sealed record Recipe(string Name, int Minutes);
```

Return malformed JSON to verify your error path: the call throws `AiException` with
`AiErrorCode.InvalidResponse`.

## Testing streaming consumers

```csharp
[Fact]
public async Task Streaming_consumer_concatenates_deltas()
{
    var fake = new FakeChatClient
    {
        StreamUpdates =
        {
            new ChatStreamUpdate { TextDelta = "Hel" },
            new ChatStreamUpdate { TextDelta = "lo" },
            new ChatStreamUpdate { FinishReason = ChatFinishReason.Stop },
        },
    };

    var buffer = new System.Text.StringBuilder();
    await foreach (ChatStreamUpdate update in fake.StreamAsync(ChatRequest.FromPrompt("hi")))
    {
        buffer.Append(update.TextDelta);
    }

    Assert.Equal("Hello", buffer.ToString());
}
```

For cancellation tests, pass a token you cancel between updates and assert
`OperationCanceledException`. The repository's richer fake also supports throwing before or
after the first update — useful for verifying that your code treats pre-stream and
mid-stream failures differently ([lifecycle](../concepts/lifecycle.md)). To override DI in
integration tests, note that `AddKorasAI` registers `IChatClient` with `TryAddSingleton`,
so an earlier registration wins
([advanced DI guide](dependency-injection.md#testing-overrides)).

## Integration testing against Ollama

For tests that should exercise a real model — provider mapping, streaming behavior, tool
calling — Ollama gives you a free, local, keyless target:

```csharp
public sealed class OllamaIntegrationTests
{
    private static IChatClient CreateClient()
    {
        var services = new ServiceCollection();
        services.AddKorasAI(ai => ai.AddOllama(o => o.DefaultModel = "llama3.2"));
        return services.BuildServiceProvider().GetRequiredService<IChatClient>();
    }

    [SkippableFact]   // or gate on an environment variable
    public async Task Completes_against_local_ollama()
    {
        IChatClient chat = CreateClient();
        try
        {
            ChatResponse response = await chat.CompleteAsync("Reply with exactly: pong");
            Assert.False(string.IsNullOrWhiteSpace(response.Text));
            Assert.Equal("ollama", response.Provider);
        }
        catch (AiException ex) when (ex.Code == AiErrorCode.Network)
        {
            Skip.If(true, "Ollama is not running locally.");
        }
    }
}
```

Keep model-dependent assertions loose (models are nondeterministic — assert shape, not
wording), pin a small model in CI, and skip rather than fail when the server is absent so
test runs stay green on machines without Ollama.
