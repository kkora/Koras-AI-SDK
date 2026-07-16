using System.Text;
using Koras.AI.Providers;

namespace Koras.AI.UnitTests.Providers;

public class SseReaderTests
{
    private static async Task<List<SseEvent>> ReadAllAsync(string raw)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        var events = new List<SseEvent>();
        await foreach (SseEvent sseEvent in SseReader.ReadEventsAsync(stream))
        {
            events.Add(sseEvent);
        }

        return events;
    }

    [Fact]
    public async Task Parses_simple_data_events()
    {
        var events = await ReadAllAsync("data: {\"a\":1}\n\ndata: [DONE]\n\n");

        Assert.Equal(2, events.Count);
        Assert.Equal("{\"a\":1}", events[0].Data);
        Assert.Equal("[DONE]", events[1].Data);
    }

    [Fact]
    public async Task Parses_named_events()
    {
        var events = await ReadAllAsync("event: message_start\ndata: {}\n\n");
        Assert.Equal("message_start", Assert.Single(events).EventType);
    }

    [Fact]
    public async Task Joins_multi_line_data_with_newlines()
    {
        var events = await ReadAllAsync("data: line1\ndata: line2\n\n");
        Assert.Equal("line1\nline2", Assert.Single(events).Data);
    }

    [Fact]
    public async Task Ignores_comments_and_unknown_fields()
    {
        var events = await ReadAllAsync(": keep-alive\nid: 42\nretry: 1000\ndata: payload\n\n");
        Assert.Equal("payload", Assert.Single(events).Data);
    }

    [Fact]
    public async Task Handles_crlf_line_endings()
    {
        var events = await ReadAllAsync("data: windows\r\n\r\n");
        Assert.Equal("windows", Assert.Single(events).Data);
    }

    [Fact]
    public async Task Handles_data_without_space_after_colon()
    {
        var events = await ReadAllAsync("data:nospace\n\n");
        Assert.Equal("nospace", Assert.Single(events).Data);
    }

    [Fact]
    public async Task Flushes_trailing_event_without_final_blank_line()
    {
        var events = await ReadAllAsync("data: tail");
        Assert.Equal("tail", Assert.Single(events).Data);
    }

    [Fact]
    public async Task Cancellation_stops_the_read()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data: x\n\n"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (SseEvent _ in SseReader.ReadEventsAsync(stream, cts.Token))
            {
            }
        });
    }
}

public class JsonLinesReaderTests
{
    [Fact]
    public async Task Yields_non_empty_lines()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"a\":1}\n\n{\"b\":2}\n"));
        var lines = new List<string>();
        await foreach (string line in JsonLinesReader.ReadLinesAsync(stream))
        {
            lines.Add(line);
        }

        Assert.Equal(["{\"a\":1}", "{\"b\":2}"], lines);
    }
}

public class ProviderErrorsTests
{
    [Theory]
    [InlineData(401, AiErrorCode.Authentication)]
    [InlineData(403, AiErrorCode.PermissionDenied)]
    [InlineData(404, AiErrorCode.ModelNotFound)]
    [InlineData(400, AiErrorCode.InvalidRequest)]
    [InlineData(408, AiErrorCode.Timeout)]
    [InlineData(422, AiErrorCode.InvalidRequest)]
    [InlineData(429, AiErrorCode.RateLimited)]
    [InlineData(500, AiErrorCode.ProviderUnavailable)]
    [InlineData(529, AiErrorCode.ProviderUnavailable)]
    [InlineData(302, AiErrorCode.Unknown)]
    public void MapStatusCode_follows_the_documented_table(int status, AiErrorCode expected)
        => Assert.Equal(expected, ProviderErrors.MapStatusCode(status));

    [Fact]
    public void FromHttpResponse_populates_diagnostics_and_truncates_body()
    {
        var ex = ProviderErrors.FromHttpResponse("openai", 429, new string('x', 10_000), TimeSpan.FromSeconds(3), "req_1");

        Assert.Equal(AiErrorCode.RateLimited, ex.Code);
        Assert.True(ex.IsTransient);
        Assert.Equal(429, ex.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(3), ex.RetryAfter);
        Assert.Equal("req_1", ex.RequestId);
        Assert.Equal(4096, ex.ProviderErrorBody!.Length);
    }

    [Fact]
    public void ParseRetryAfter_reads_delta_seconds()
    {
        using var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Retry-After", "12");
        Assert.Equal(TimeSpan.FromSeconds(12), ProviderErrors.ParseRetryAfter(response.Headers));
    }

    [Fact]
    public void ParseRetryAfter_reads_http_date_relative_to_the_clock()
    {
        var time = new TestInfrastructure.ManualTimeProvider();
        using var response = new HttpResponseMessage();
        response.Headers.TryAddWithoutValidation("Retry-After", time.GetUtcNow().AddSeconds(30).ToString("R"));

        TimeSpan? parsed = ProviderErrors.ParseRetryAfter(response.Headers, time);
        Assert.NotNull(parsed);
        Assert.InRange(parsed.Value, TimeSpan.FromSeconds(29), TimeSpan.FromSeconds(31));
    }

    [Fact]
    public void NotSupported_produces_terminal_error()
    {
        var ex = ProviderErrors.NotSupported("anthropic", "embeddings");
        Assert.Equal(AiErrorCode.NotSupported, ex.Code);
        Assert.False(ex.IsTransient);
    }
}
