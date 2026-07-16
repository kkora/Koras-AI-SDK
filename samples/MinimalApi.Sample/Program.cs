// Koras.AI minimal API sample: a chat endpoint and an SSE streaming endpoint.
// Configuration comes from appsettings.json ("Koras:AI:Ollama") — see README.

using Koras.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:OpenAI:ApiKey"]))
    {
        ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI")).AsDefault();
    }

    ai.UseRetry();
});

WebApplication app = builder.Build();

// POST /chat  { "prompt": "..." }
app.MapPost("/chat", async (ChatPrompt body, IChatClient chat, CancellationToken ct) =>
{
    ChatResponse response = await chat.CompleteAsync(body.Prompt, ct);
    return Results.Ok(new
    {
        text = response.Text,
        provider = response.Provider,
        model = response.Model,
        inputTokens = response.Usage.InputTokens,
        outputTokens = response.Usage.OutputTokens,
    });
});

// POST /chat/stream  { "prompt": "..." }  → text/event-stream
app.MapPost("/chat/stream", async (ChatPrompt body, IChatClient chat, HttpContext context, CancellationToken ct) =>
{
    context.Response.ContentType = "text/event-stream";
    await foreach (ChatStreamUpdate update in chat.StreamAsync(ChatRequest.FromPrompt(body.Prompt), ct))
    {
        if (update.TextDelta is { Length: > 0 } delta)
        {
            await context.Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(delta)}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }

    await context.Response.WriteAsync("data: [DONE]\n\n", ct);
});

app.Run();

internal sealed record ChatPrompt(string Prompt);
