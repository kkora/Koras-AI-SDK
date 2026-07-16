using System.Runtime.CompilerServices;

namespace Koras.AI.UnitTests.TestInfrastructure;

/// <summary>A scriptable IChatClient: returns queued results (responses or exceptions) in order.</summary>
public sealed class FakeChatClient(string providerName = "fake") : IChatClient
{
    private readonly Queue<Func<ChatRequest, ChatResponse>> _completions = new();

    public string ProviderName { get; } = providerName;

    public List<ChatRequest> Requests { get; } = [];

    public List<ChatStreamUpdate> StreamUpdates { get; } = [];

    public Exception? StreamErrorBeforeFirst { get; set; }

    public Exception? StreamErrorAfterFirst { get; set; }

    public int CompleteCallCount { get; private set; }

    public int StreamCallCount { get; private set; }

    public FakeChatClient EnqueueResponse(string text, ChatFinishReason? finishReason = null, TokenUsage usage = default)
    {
        _completions.Enqueue(_ => new ChatResponse
        {
            Message = ChatMessage.Assistant(text),
            Provider = ProviderName,
            FinishReason = finishReason ?? ChatFinishReason.Stop,
            Usage = usage,
        });
        return this;
    }

    public FakeChatClient EnqueueResponse(Func<ChatRequest, ChatResponse> factory)
    {
        _completions.Enqueue(factory);
        return this;
    }

    public FakeChatClient EnqueueError(Exception exception)
    {
        _completions.Enqueue(_ => throw exception);
        return this;
    }

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CompleteCallCount++;
        Requests.Add(request);
        if (_completions.Count == 0)
        {
            throw new InvalidOperationException("FakeChatClient has no queued completions left.");
        }

        return Task.FromResult(_completions.Dequeue()(request));
    }

    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamCallCount++;
        Requests.Add(request);
        await Task.Yield();

        if (StreamErrorBeforeFirst is { } beforeFirst)
        {
            throw beforeFirst;
        }

        var first = true;
        foreach (ChatStreamUpdate update in StreamUpdates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!first && StreamErrorAfterFirst is { } afterFirst)
            {
                throw afterFirst;
            }

            yield return update;
            first = false;
        }
    }
}
