using System.ComponentModel;
using Koras.AI.UnitTests.TestInfrastructure;

namespace Koras.AI.UnitTests.Core;

public class ChatClientExtensionsTests
{
    private sealed record Invoice([property: Description("Invoice number")] string Number, decimal Total);

    [Fact]
    public async Task CompleteAsync_with_prompt_wraps_it_in_a_request()
    {
        var inner = new FakeChatClient().EnqueueResponse("answer");
        var response = await inner.CompleteAsync("question");

        Assert.Equal("answer", response.Text);
        Assert.Equal(ChatRole.User, Assert.Single(Assert.Single(inner.Requests).Messages).Role);
    }

    [Fact]
    public async Task Structured_output_applies_schema_format_and_deserializes()
    {
        var inner = new FakeChatClient().EnqueueResponse("""{"number":"INV-1","total":99.5}""");

        ChatResponse<Invoice> result = await inner.CompleteAsync<Invoice>(ChatRequest.FromPrompt("extract"));

        Assert.Equal("INV-1", result.Value.Number);
        Assert.Equal(99.5m, result.Value.Total);

        var appliedFormat = Assert.IsType<JsonSchemaChatResponseFormat>(inner.Requests[0].Options?.ResponseFormat);
        Assert.Equal("Invoice", appliedFormat.Name);
    }

    [Fact]
    public async Task Structured_output_preserves_existing_response_format()
    {
        var custom = ChatResponseFormat.JsonSchema("custom", AiJsonSchema.FromType<Invoice>());
        var inner = new FakeChatClient().EnqueueResponse("""{"number":"INV-2","total":1}""");

        await inner.CompleteAsync<Invoice>(new ChatRequest
        {
            Messages = [ChatMessage.User("extract")],
            Options = new ChatOptions { ResponseFormat = custom },
        });

        Assert.Same(custom, inner.Requests[0].Options?.ResponseFormat);
    }

    [Fact]
    public async Task Structured_output_strips_markdown_code_fences()
    {
        var inner = new FakeChatClient().EnqueueResponse("```json\n{\"number\":\"INV-3\",\"total\":2}\n```");
        var result = await inner.CompleteAsync<Invoice>("extract");
        Assert.Equal("INV-3", result.Value.Number);
    }

    [Fact]
    public async Task Structured_output_failure_surfaces_as_InvalidResponse_with_body()
    {
        var inner = new FakeChatClient().EnqueueResponse("I could not produce JSON, sorry!");

        var ex = await Assert.ThrowsAsync<AiException>(() => inner.CompleteAsync<Invoice>("extract"));

        Assert.Equal(AiErrorCode.InvalidResponse, ex.Code);
        Assert.Contains("Invoice", ex.Message);
        Assert.Contains("sorry", ex.ProviderErrorBody);
    }

    [Fact]
    public async Task Structured_output_null_json_surfaces_as_InvalidResponse()
    {
        var inner = new FakeChatClient().EnqueueResponse("null");
        await Assert.ThrowsAsync<AiException>(() => inner.CompleteAsync<Invoice>("extract"));
    }

    [Fact]
    public void DelegatingChatClient_GetService_unwraps_the_decorator_chain()
    {
        var inner = new FakeChatClient("inner");
        var wrapped = new ToolInvokingChatClient(new RetryChatClient(inner));

        Assert.Same(inner, wrapped.GetService(typeof(FakeChatClient)));
        Assert.IsType<RetryChatClient>(wrapped.GetService(typeof(RetryChatClient)));
        Assert.Null(wrapped.GetService(typeof(IProviderHealthProbe)));
    }
}
