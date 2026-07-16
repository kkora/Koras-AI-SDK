using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Fallback;

public class FallbackChatClientTests
{
    private static ChatRequest Request() => ChatRequest.FromPrompt("hi");

    [Fact]
    public async Task Uses_primary_when_it_succeeds()
    {
        var primary = new FakeChatClient("primary").EnqueueResponse("from primary");
        var secondary = new FakeChatClient("secondary");
        var client = new FallbackChatClient([primary, secondary]);

        var response = await client.CompleteAsync(Request());

        Assert.Equal("from primary", response.Text);
        Assert.Equal(0, secondary.CompleteCallCount);
    }

    [Fact]
    public async Task Fails_over_on_transient_errors()
    {
        var primary = new FakeChatClient("primary").EnqueueError(new AiException("down", AiErrorCode.ProviderUnavailable));
        var secondary = new FakeChatClient("secondary").EnqueueResponse("from secondary");
        var client = new FallbackChatClient([primary, secondary]);

        var response = await client.CompleteAsync(Request());

        Assert.Equal("from secondary", response.Text);
        Assert.Equal("secondary", response.Provider);
    }

    [Fact]
    public async Task Does_not_fail_over_on_terminal_errors_by_default()
    {
        var primary = new FakeChatClient("primary").EnqueueError(new AiException("bad key", AiErrorCode.Authentication));
        var secondary = new FakeChatClient("secondary").EnqueueResponse("never used");
        var client = new FallbackChatClient([primary, secondary]);

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(AiErrorCode.Authentication, ex.Code);
        Assert.Equal(0, secondary.CompleteCallCount);
    }

    [Fact]
    public async Task Custom_failover_predicate_overrides_the_default()
    {
        var primary = new FakeChatClient("primary").EnqueueError(new AiException("filtered", AiErrorCode.ContentFiltered));
        var secondary = new FakeChatClient("secondary").EnqueueResponse("relaxed provider");
        var client = new FallbackChatClient([primary, secondary], shouldFailover: static ex => ex.Code == AiErrorCode.ContentFiltered);

        Assert.Equal("relaxed provider", (await client.CompleteAsync(Request())).Text);
    }

    [Fact]
    public async Task Exhaustion_throws_last_error_with_all_attempts_aggregated()
    {
        var first = new FakeChatClient("a").EnqueueError(new AiException("a down", AiErrorCode.ProviderUnavailable));
        var second = new FakeChatClient("b").EnqueueError(new AiException("b down", AiErrorCode.RateLimited));
        var client = new FallbackChatClient([first, second]);

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(Request()));

        Assert.Contains("b down", ex.Message);
        Assert.Equal(AiErrorCode.RateLimited, ex.Code);
        var aggregate = Assert.IsType<AggregateException>(ex.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
    }

    [Fact]
    public async Task Single_candidate_failure_propagates_unwrapped()
    {
        var only = new FakeChatClient("only").EnqueueError(new AiException("down", AiErrorCode.ProviderUnavailable));
        var client = new FallbackChatClient([only]);

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(Request()));
        Assert.Equal("down", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task Streaming_fails_over_before_first_update()
    {
        var primary = new FakeChatClient("primary") { StreamErrorBeforeFirst = new AiException("down", AiErrorCode.ProviderUnavailable) };
        var secondary = new FakeChatClient("secondary");
        secondary.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "streamed" });
        var client = new FallbackChatClient([primary, secondary]);

        var updates = new List<ChatStreamUpdate>();
        await foreach (ChatStreamUpdate update in client.StreamAsync(Request()))
        {
            updates.Add(update);
        }

        Assert.Equal("streamed", Assert.Single(updates).TextDelta);
    }

    [Fact]
    public async Task Streaming_failure_after_first_update_does_not_fail_over()
    {
        var primary = new FakeChatClient("primary") { StreamErrorAfterFirst = new AiException("mid-stream", AiErrorCode.ProviderUnavailable) };
        primary.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "a" });
        primary.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "b" });
        var secondary = new FakeChatClient("secondary");
        var client = new FallbackChatClient([primary, secondary]);

        await Assert.ThrowsAsync<AiException>(async () =>
        {
            await foreach (ChatStreamUpdate _ in client.StreamAsync(Request()))
            {
            }
        });

        Assert.Equal(0, secondary.StreamCallCount);
    }

    [Fact]
    public void Requires_at_least_one_candidate()
        => Assert.Throws<ArgumentException>(() => new FallbackChatClient([]));
}
