using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Koras.AI.IntegrationTests;

/// <summary>
/// An in-process Kestrel server that speaks the providers' wire formats, so integration tests
/// exercise the full HTTP stack (connection, headers, SSE framing) without external services.
/// </summary>
public sealed class FakeProviderServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeProviderServer(WebApplication app)
    {
        _app = app;
    }

    public Uri BaseAddress => new(_app.Urls.First());

    public int OpenAIChatRequests { get; private set; }

    public bool FailOpenAIOnce { get; set; }

    public static async Task<FakeProviderServer> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication app = builder.Build();

        var server = new FakeProviderServer(app);

        // --- OpenAI wire format ---
        app.MapPost("/v1/chat/completions", async context =>
        {
            server.OpenAIChatRequests++;
            if (server.FailOpenAIOnce)
            {
                server.FailOpenAIOnce = false;
                context.Response.StatusCode = 429;
                context.Response.Headers.RetryAfter = "0";
                await context.Response.WriteAsync("""{"error":{"message":"slow down","code":"rate_limit_exceeded"}}""");
                return;
            }

            if (context.Request.Headers.Authorization != "Bearer sk-test-integration")
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("""{"error":{"message":"Incorrect API key provided."}}""");
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();
            bool stream = body.Contains("\"stream\":true", StringComparison.Ordinal);

            if (!stream)
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""
                    {"id":"cmpl-int","model":"gpt-test","choices":[{"index":0,"message":{"role":"assistant","content":"integration says hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":4}}
                    """);
                return;
            }

            context.Response.ContentType = "text/event-stream";
            foreach (string chunk in (string[])
            [
                """{"id":"cmpl-int","choices":[{"delta":{"content":"int"}}]}""",
                """{"id":"cmpl-int","choices":[{"delta":{"content":"egration"}}]}""",
                """{"id":"cmpl-int","choices":[{"delta":{},"finish_reason":"stop"}]}""",
                """{"id":"cmpl-int","choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2}}""",
            ])
            {
                await context.Response.WriteAsync($"data: {chunk}\n\n");
                await context.Response.Body.FlushAsync();
            }

            await context.Response.WriteAsync("data: [DONE]\n\n");
        });

        app.MapGet("/v1/models", async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"object":"list","data":[]}""");
        });

        // --- Ollama wire format (JSON lines) ---
        app.MapPost("/api/chat", async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            string body = await reader.ReadToEndAsync();
            bool stream = body.Contains("\"stream\":true", StringComparison.Ordinal);
            context.Response.ContentType = "application/x-ndjson";

            if (!stream)
            {
                await context.Response.WriteAsync(
                    """{"model":"llama-test","message":{"role":"assistant","content":"ollama fallback answer"},"done":true,"done_reason":"stop","prompt_eval_count":2,"eval_count":3}""");
                return;
            }

            var chunks = new StringBuilder()
                .AppendLine("""{"model":"llama-test","message":{"role":"assistant","content":"oll"},"done":false}""")
                .AppendLine("""{"model":"llama-test","message":{"role":"assistant","content":"ama"},"done":false}""")
                .AppendLine("""{"model":"llama-test","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":1,"eval_count":2}""");
            await context.Response.WriteAsync(chunks.ToString());
        });

        app.MapGet("/api/version", async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"version":"0.0-test"}""");
        });

        await app.StartAsync();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
