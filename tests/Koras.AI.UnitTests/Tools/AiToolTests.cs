using System.ComponentModel;
using System.Text.Json;

namespace Koras.AI.UnitTests.Tools;

public class AiToolTests
{
    [Fact]
    public void Create_generates_schema_from_delegate_parameters()
    {
        var tool = AiTool.Create(
            "lookup_order",
            "Looks up an order",
            ([Description("The order id")] string orderId, int maxResults = 10) => $"{orderId}:{maxResults}");

        Assert.Equal("lookup_order", tool.Name);
        Assert.True(tool.CanInvoke);

        JsonElement schema = tool.ParametersSchema;
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal("The order id", schema.GetProperty("properties").GetProperty("orderId").GetProperty("description").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        string[] required = [.. schema.GetProperty("required").EnumerateArray().Select(static e => e.GetString()!)];
        Assert.Contains("orderId", required);
        Assert.DoesNotContain("maxResults", required); // has default value → optional
    }

    [Fact]
    public void Create_excludes_cancellation_token_from_schema()
    {
        var tool = AiTool.Create("t", null, (string a, CancellationToken ct) => a);
        Assert.False(tool.ParametersSchema.GetProperty("properties").TryGetProperty("ct", out _));
    }

    [Fact]
    public async Task InvokeAsync_binds_arguments_case_insensitively_and_returns_string_result()
    {
        var tool = AiTool.Create("t", null, (string city, int days) => $"{city}/{days}");
        string result = await tool.InvokeAsync("""{"City":"Oslo","days":3}""");
        Assert.Equal("Oslo/3", result);
    }

    [Fact]
    public async Task InvokeAsync_serializes_non_string_results_as_json()
    {
        var tool = AiTool.Create("t", null, () => new { Answer = 42 });
        string result = await tool.InvokeAsync("{}");
        Assert.Equal("""{"answer":42}""", result);
    }

    [Fact]
    public async Task InvokeAsync_awaits_task_and_valuetask_handlers()
    {
        var taskTool = AiTool.Create("t1", null, async (int x) =>
        {
            await Task.Yield();
            return x * 2;
        });
        Assert.Equal("10", await taskTool.InvokeAsync("""{"x":5}"""));

        var valueTaskTool = AiTool.Create("t2", null, (int x) => new ValueTask<string>($"v{x}"));
        Assert.Equal("v7", await valueTaskTool.InvokeAsync("""{"x":7}"""));
    }

    [Fact]
    public async Task InvokeAsync_uses_defaults_for_omitted_optional_arguments()
    {
        var tool = AiTool.Create("t", null, (string a, string b = "fallback") => $"{a}|{b}");
        Assert.Equal("x|fallback", await tool.InvokeAsync("""{"a":"x"}"""));
    }

    [Fact]
    public async Task InvokeAsync_throws_InvalidResponse_for_missing_required_argument()
    {
        var tool = AiTool.Create("t", null, (string required) => required);
        var ex = await Assert.ThrowsAsync<AiException>(() => tool.InvokeAsync("{}"));
        Assert.Equal(AiErrorCode.InvalidResponse, ex.Code);
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public async Task InvokeAsync_throws_InvalidResponse_for_malformed_json()
    {
        var tool = AiTool.Create("t", null, (string a) => a);
        var ex = await Assert.ThrowsAsync<AiException>(() => tool.InvokeAsync("not json"));
        Assert.Equal(AiErrorCode.InvalidResponse, ex.Code);
    }

    [Fact]
    public async Task InvokeAsync_propagates_handler_exceptions_unwrapped()
    {
        var tool = AiTool.Create("t", null, new Func<string>(() => throw new InvalidOperationException("handler blew up")));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tool.InvokeAsync("{}"));
        Assert.Equal("handler blew up", ex.Message);
    }

    [Fact]
    public async Task InvokeAsync_passes_cancellation_token_to_handler()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken observed = default;
        var tool = AiTool.Create("t", null, (CancellationToken ct) =>
        {
            observed = ct;
            return "ok";
        });

        await tool.InvokeAsync("{}", cts.Token);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task Declared_tool_cannot_be_invoked()
    {
        var tool = AiTool.Declare("t", "desc", AiJsonSchema.FromType<int>());
        Assert.False(tool.CanInvoke);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tool.InvokeAsync("{}"));
    }

    [Fact]
    public void ToolCall_ParseArguments_binds_and_reports_failures()
    {
        var call = new ToolCall { Id = "1", Name = "t", ArgumentsJson = """{"city":"Oslo"}""" };
        var args = call.ParseArguments<WeatherArgs>();
        Assert.Equal("Oslo", args!.City);

        var bad = new ToolCall { Id = "2", Name = "t", ArgumentsJson = "{invalid" };
        var ex = Assert.Throws<AiException>(() => bad.ParseArguments<WeatherArgs>());
        Assert.Equal(AiErrorCode.InvalidResponse, ex.Code);
    }

    private sealed record WeatherArgs(string City);
}
