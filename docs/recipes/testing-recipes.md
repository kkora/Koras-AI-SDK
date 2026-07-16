# Recipes: Testing

`IChatClient` is a small interface — testing code that uses Koras.AI needs no mocking
framework. These patterns mirror the SDK's own test suite under `tests/`.

## A scriptable fake `IChatClient`

Queue responses (or exceptions) and record every incoming request:

```csharp
public sealed class FakeChatClient(string providerName = "fake") : IChatClient
{
    private readonly Queue<Func<ChatRequest, ChatResponse>> _completions = new();

    public string ProviderName { get; } = providerName;
    public List<ChatRequest> Requests { get; } = [];

    public FakeChatClient EnqueueResponse(string text)
    {
        _completions.Enqueue(_ => new ChatResponse
        {
            Message = ChatMessage.Assistant(text),
            Provider = ProviderName,
            FinishReason = ChatFinishReason.Stop,
        });
        return this;
    }

    public FakeChatClient EnqueueError(Exception exception)
    {
        _completions.Enqueue(_ => throw exception);
        return this;
    }

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        return Task.FromResult(_completions.Dequeue()(request));
    }

    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Requests.Add(request);
        await Task.Yield();
        foreach (char c in (await CompleteAsync(request, ct)).Text ?? "")
        {
            yield return new ChatStreamUpdate { TextDelta = c.ToString() };
        }
    }
}
```

## Scripted multi-turn behavior

Because responses dequeue in order, multi-call flows (tool loops, retries, conversations)
script naturally:

```csharp
var fake = new FakeChatClient()
    .EnqueueError(new AiException("busy", AiErrorCode.RateLimited))   // IsTransient=true
    .EnqueueResponse("recovered");

var sut = new SummaryService(fake);
string result = await sut.SummarizeAsync("text", CancellationToken.None);

Assert.Equal("recovered", result);
Assert.Equal(2, fake.Requests.Count);
```

## Asserting the outgoing `ChatRequest`

The fake records requests, so tests can pin exactly what your code sends — system prompt,
model override, options:

```csharp
await sut.SummarizeAsync("some long text", CancellationToken.None);

ChatRequest sent = fake.Requests.Single();
Assert.Equal(ChatRole.System, sent.Messages[0].Role);
Assert.Contains("some long text", sent.Messages[^1].Text);
Assert.Equal(0.2, sent.Options?.Temperature);
```

## Injecting the fake through DI

Exercise your real composition (including decorators) by registering the fake as a client:

```csharp
var services = new ServiceCollection();
services.AddLogging();
services.AddKorasAI(ai =>
{
    ai.AddClient("fake", _ => fake).AsDefault();
    ai.UseRetry(r => r.MaxAttempts = 2);      // the decorator under test wraps the fake
});
await using ServiceProvider provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatClient>();
```

## Integration tests against local Ollama

For real end-to-end coverage without API keys or cost, point the Ollama provider at a local
daemon (`ollama serve` + `ollama pull llama3.2`) and skip when it is absent:

```csharp
[Fact]
public async Task Completes_against_local_ollama()
{
    var services = new ServiceCollection();
    services.AddKorasAI(ai => ai.AddOllama(o => o.DefaultModel = "llama3.2"));
    await using ServiceProvider provider = services.BuildServiceProvider();
    var client = provider.GetRequiredService<IChatClient>();

    try
    {
        ChatResponse response = await client.CompleteAsync("Reply with the word: pong");
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }
    catch (AiException ex) when (ex.Code == AiErrorCode.Network)
    {
        return; // Ollama not running on this machine/CI agent — treat as skipped
    }
}
```

## Wire-level fakes: the `FakeProviderServer` approach

The SDK's own integration tests (`tests/Koras.AI.IntegrationTests/FakeProviderServer.cs`)
start an in-process Kestrel server that speaks the providers' actual wire formats — OpenAI
JSON + SSE on `/v1/chat/completions`, Ollama JSON-lines on `/api/chat` — then point real
clients at `http://127.0.0.1:<port>`. That exercises the full HTTP stack (headers, SSE
framing, retries against real 429s) with zero external dependencies. Loopback endpoints are
exempt from the HTTPS rule, so this works with startup validation enabled.

Use this pattern when you need to test *your* behavior under provider failure modes (429 with
`Retry-After`, 401, malformed SSE) rather than the SDK's — enqueue the status codes your
scenario needs and assert on request counts.

## See also

- [Testing docs](../testing/) — the SDK's own test strategy.
- [Error handling](../features/error-handling.md) — codes to simulate in failure tests.
