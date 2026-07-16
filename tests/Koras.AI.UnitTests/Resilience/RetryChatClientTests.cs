using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Resilience;

public class RetryChatClientTests
{
    private static ChatRequest Request() => ChatRequest.FromPrompt("hi");

    private static AiException Transient(TimeSpan? retryAfter = null)
        => new("rate limited", AiErrorCode.RateLimited) { RetryAfter = retryAfter };

    [Fact]
    public async Task Succeeds_without_retry_on_first_attempt()
    {
        var inner = new FakeChatClient().EnqueueResponse("ok");
        var client = new RetryChatClient(inner);

        var response = await client.CompleteAsync(Request());

        Assert.Equal("ok", response.Text);
        Assert.Equal(1, inner.CompleteCallCount);
    }

    [Fact]
    public async Task Retries_transient_failures_until_success()
    {
        var time = new ManualTimeProvider();
        var inner = new FakeChatClient()
            .EnqueueError(Transient())
            .EnqueueError(Transient())
            .EnqueueResponse("recovered");
        var client = new RetryChatClient(inner, new RetryOptions { MaxAttempts = 3 }, time);

        Task<ChatResponse> task = client.CompleteAsync(Request());
        await AdvanceUntilComplete(time, task);

        Assert.Equal("recovered", (await task).Text);
        Assert.Equal(3, inner.CompleteCallCount);
    }

    [Fact]
    public async Task Does_not_retry_terminal_failures()
    {
        var inner = new FakeChatClient().EnqueueError(new AiException("bad key", AiErrorCode.Authentication));
        var client = new RetryChatClient(inner);

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(AiErrorCode.Authentication, ex.Code);
        Assert.Equal(1, inner.CompleteCallCount);
    }

    [Fact]
    public async Task Rethrows_original_error_after_exhausting_attempts()
    {
        var time = new ManualTimeProvider();
        var inner = new FakeChatClient()
            .EnqueueError(Transient())
            .EnqueueError(new AiException("final failure", AiErrorCode.ProviderUnavailable));
        var client = new RetryChatClient(inner, new RetryOptions { MaxAttempts = 2 }, time);

        Task<ChatResponse> task = client.CompleteAsync(Request());
        await AdvanceUntilComplete(time, task, expectFailure: true);

        var ex = await Assert.ThrowsAsync<AiException>(() => task);
        Assert.Equal("final failure", ex.Message);
        Assert.Equal(2, inner.CompleteCallCount);
    }

    [Fact]
    public async Task Honors_retry_after_hint()
    {
        var time = new ManualTimeProvider();
        var inner = new FakeChatClient()
            .EnqueueError(Transient(TimeSpan.FromSeconds(9)))
            .EnqueueResponse("ok");
        var client = new RetryChatClient(inner, new RetryOptions(), time);

        Task<ChatResponse> task = client.CompleteAsync(Request());
        await WaitForTimer(time);

        Assert.Equal(TimeSpan.FromSeconds(9), Assert.Single(time.RequestedDelays));
        time.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal("ok", (await task).Text);
    }

    [Fact]
    public async Task Backoff_delays_stay_within_the_configured_cap()
    {
        var time = new ManualTimeProvider();
        var options = new RetryOptions
        {
            MaxAttempts = 4,
            BaseDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(5),
        };
        var inner = new FakeChatClient()
            .EnqueueError(Transient())
            .EnqueueError(Transient())
            .EnqueueError(Transient())
            .EnqueueResponse("ok");
        var client = new RetryChatClient(inner, options, time);

        Task<ChatResponse> task = client.CompleteAsync(Request());
        await AdvanceUntilComplete(time, task);
        await task;

        // Full jitter: each delay is in [0, min(MaxDelay, BaseDelay * 2^(n-1))].
        Assert.All(time.RequestedDelays, delay => Assert.InRange(delay, TimeSpan.Zero, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Caller_cancellation_during_delay_propagates_as_cancellation()
    {
        var time = new ManualTimeProvider();
        using var cts = new CancellationTokenSource();
        var inner = new FakeChatClient().EnqueueError(Transient(TimeSpan.FromSeconds(30)));
        var client = new RetryChatClient(inner, new RetryOptions(), time);

        Task<ChatResponse> task = client.CompleteAsync(Request(), cts.Token);
        await WaitForTimer(time);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Streaming_retries_only_before_first_update()
    {
        var time = new ManualTimeProvider();
        var failsThenStreams = new FakeChatClient
        {
            StreamErrorBeforeFirst = Transient(TimeSpan.FromMilliseconds(1)),
        };
        failsThenStreams.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "hello" });

        var client = new RetryChatClient(failsThenStreams, new RetryOptions(), time);

        // First enumeration attempt fails pre-token → retried after the delay.
        var updates = new List<ChatStreamUpdate>();
        Task consume = Task.Run(async () =>
        {
            await foreach (ChatStreamUpdate update in client.StreamAsync(Request()))
            {
                updates.Add(update);
            }
        });

        await WaitForTimer(time);
        failsThenStreams.StreamErrorBeforeFirst = null; // second attempt succeeds
        time.Advance(TimeSpan.FromSeconds(31));
        await consume;

        Assert.Equal(2, failsThenStreams.StreamCallCount);
        Assert.Equal("hello", Assert.Single(updates).TextDelta);
    }

    [Fact]
    public async Task Streaming_failure_after_first_update_is_not_retried()
    {
        var inner = new FakeChatClient { StreamErrorAfterFirst = Transient() };
        inner.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "a" });
        inner.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "b" });

        var client = new RetryChatClient(inner);

        var received = new List<string?>();
        await Assert.ThrowsAsync<AiException>(async () =>
        {
            await foreach (ChatStreamUpdate update in client.StreamAsync(Request()))
            {
                received.Add(update.TextDelta);
            }
        });

        Assert.Equal(["a"], received);
        Assert.Equal(1, inner.StreamCallCount);
    }

    [Fact]
    public void MaxAttempts_below_one_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxAttempts = 0 });

    private static async Task WaitForTimer(ManualTimeProvider time)
    {
        // The retry delay timer is created asynchronously after the failed attempt.
        for (var i = 0; i < 100 && time.ActiveTimerCount == 0; i++)
        {
            await Task.Delay(10);
        }

        Assert.True(time.ActiveTimerCount > 0, "expected a retry delay timer to be scheduled");
    }

    private static async Task AdvanceUntilComplete(ManualTimeProvider time, Task task, bool expectFailure = false)
    {
        for (var i = 0; i < 200 && !task.IsCompleted; i++)
        {
            if (time.ActiveTimerCount > 0)
            {
                time.Advance(TimeSpan.FromSeconds(31));
            }

            await Task.Delay(10);
        }

        Assert.True(task.IsCompleted, "operation did not complete after advancing time");
        if (!expectFailure)
        {
            await task;
        }
    }
}
