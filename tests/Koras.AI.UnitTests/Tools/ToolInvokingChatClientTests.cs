using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Tools;

public class ToolInvokingChatClientTests
{
    private static ChatResponse ToolCallResponse(string provider, params ToolCall[] calls) => new()
    {
        Message = ChatMessage.Assistant(null, calls),
        Provider = provider,
        FinishReason = ChatFinishReason.ToolCalls,
        Usage = new TokenUsage(10, 5),
    };

    private static ChatRequest RequestWithTools(params AiTool[] tools) => new()
    {
        Messages = [ChatMessage.User("What's the weather in Oslo?")],
        Options = new ChatOptions { Tools = tools },
    };

    [Fact]
    public async Task Executes_tool_calls_and_returns_final_answer()
    {
        var tool = AiTool.Create("get_weather", "Gets weather", (string city) => $"Sunny in {city}");
        var inner = new FakeChatClient()
            .EnqueueResponse(_ => ToolCallResponse("fake", new ToolCall { Id = "1", Name = "get_weather", ArgumentsJson = """{"city":"Oslo"}""" }))
            .EnqueueResponse(request =>
            {
                // The second round-trip must include the assistant tool-call message and the tool result.
                Assert.Equal(3, request.Messages.Count);
                Assert.Equal(ChatRole.Assistant, request.Messages[1].Role);
                Assert.Equal(ChatRole.Tool, request.Messages[2].Role);
                Assert.Equal("Sunny in Oslo", request.Messages[2].Text);
                return new ChatResponse
                {
                    Message = ChatMessage.Assistant("It is sunny in Oslo."),
                    Provider = "fake",
                    FinishReason = ChatFinishReason.Stop,
                    Usage = new TokenUsage(20, 8),
                };
            });

        var client = new ToolInvokingChatClient(inner);
        var response = await client.CompleteAsync(RequestWithTools(tool));

        Assert.Equal("It is sunny in Oslo.", response.Text);
        Assert.Equal(2, inner.CompleteCallCount);
        Assert.Equal(new TokenUsage(30, 13), response.Usage); // usage aggregated across iterations
    }

    [Fact]
    public async Task Returns_response_untouched_when_no_tools_are_registered()
    {
        var inner = new FakeChatClient().EnqueueResponse("plain");
        var client = new ToolInvokingChatClient(inner);

        var response = await client.CompleteAsync(ChatRequest.FromPrompt("hi"));

        Assert.Equal("plain", response.Text);
        Assert.Equal(1, inner.CompleteCallCount);
    }

    [Fact]
    public async Task Declaration_only_tools_are_returned_to_the_caller()
    {
        var declared = AiTool.Declare("manual_tool", null, AiJsonSchema.FromType<string>());
        var inner = new FakeChatClient()
            .EnqueueResponse(_ => ToolCallResponse("fake", new ToolCall { Id = "1", Name = "manual_tool", ArgumentsJson = "{}" }));

        var client = new ToolInvokingChatClient(inner);
        var response = await client.CompleteAsync(RequestWithTools(declared));

        Assert.Single(response.Message.ToolCalls);
        Assert.Equal(1, inner.CompleteCallCount);
    }

    [Fact]
    public async Task Handler_failure_is_reported_to_the_model_by_default()
    {
        var tool = AiTool.Create("boom", null, new Func<string>(() => throw new InvalidOperationException("kaput")));
        var inner = new FakeChatClient()
            .EnqueueResponse(_ => ToolCallResponse("fake", new ToolCall { Id = "1", Name = "boom", ArgumentsJson = "{}" }))
            .EnqueueResponse(request =>
            {
                Assert.Contains("kaput", request.Messages[^1].Text);
                return new ChatResponse { Message = ChatMessage.Assistant("recovered"), Provider = "fake" };
            });

        var client = new ToolInvokingChatClient(inner);
        Assert.Equal("recovered", (await client.CompleteAsync(RequestWithTools(tool))).Text);
    }

    [Fact]
    public async Task Handler_failure_throws_when_policy_is_Throw()
    {
        var tool = AiTool.Create("boom", null, new Func<string>(() => throw new InvalidOperationException("kaput")));
        var inner = new FakeChatClient()
            .EnqueueResponse(_ => ToolCallResponse("fake", new ToolCall { Id = "1", Name = "boom", ArgumentsJson = "{}" }));

        var client = new ToolInvokingChatClient(inner, new ToolInvocationOptions { ErrorBehavior = ToolErrorBehavior.Throw });

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(RequestWithTools(tool)));
        Assert.Equal(AiErrorCode.ToolExecutionFailed, ex.Code);
    }

    [Fact]
    public async Task Loop_that_never_converges_throws_after_max_iterations()
    {
        var tool = AiTool.Create("loop", null, () => "again!");
        var inner = new FakeChatClient();
        for (var i = 0; i < 3; i++)
        {
            inner.EnqueueResponse(_ => ToolCallResponse("fake", new ToolCall { Id = $"{Guid.NewGuid()}", Name = "loop", ArgumentsJson = "{}" }));
        }

        var client = new ToolInvokingChatClient(inner, new ToolInvocationOptions { MaxIterations = 3 });

        var ex = await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(RequestWithTools(tool)));
        Assert.Equal(AiErrorCode.ToolExecutionFailed, ex.Code);
        Assert.Contains("3 iterations", ex.Message);
    }

    [Fact]
    public async Task Cancellation_propagates_into_tool_handlers()
    {
        using var cts = new CancellationTokenSource();
        var tool = AiTool.Create("slow", null, async (CancellationToken ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return "never";
        });
        var inner = new FakeChatClient()
            .EnqueueResponse(_ => ToolCallResponse("fake", new ToolCall { Id = "1", Name = "slow", ArgumentsJson = "{}" }));

        var client = new ToolInvokingChatClient(inner);
        Task<ChatResponse> task = client.CompleteAsync(RequestWithTools(tool), cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Multiple_tool_calls_in_one_response_all_execute()
    {
        var calls = 0;
        var tool = AiTool.Create("count", null, () => Interlocked.Increment(ref calls).ToString());
        var inner = new FakeChatClient()
            .EnqueueResponse(_ => ToolCallResponse(
                "fake",
                new ToolCall { Id = "1", Name = "count", ArgumentsJson = "{}" },
                new ToolCall { Id = "2", Name = "count", ArgumentsJson = "{}" }))
            .EnqueueResponse(request =>
            {
                Assert.Equal(2, request.Messages.Count(static m => m.Role == ChatRole.Tool));
                return new ChatResponse { Message = ChatMessage.Assistant("done"), Provider = "fake" };
            });

        var client = new ToolInvokingChatClient(inner);
        await client.CompleteAsync(RequestWithTools(tool));

        Assert.Equal(2, calls);
    }
}
