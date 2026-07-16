# Your First Application

This walkthrough builds the equivalent of `samples/MinimalApi.Sample` from scratch: an
ASP.NET Core Minimal API with a chat endpoint and a Server-Sent Events (SSE) streaming
endpoint, configured through `appsettings.json` and user secrets.

## 1. Create the project

```sh
dotnet new web -n ChatApi
cd ChatApi
dotnet add package Koras.AI.Ollama -v 0.1.0-preview.1
dotnet add package Koras.AI.OpenAI -v 0.1.0-preview.1
```

## 2. Configuration

Non-secret settings go in `appsettings.json` under the conventional `Koras:AI:{Provider}`
sections:

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Koras.AI": "Information" } },
  "Koras": {
    "AI": {
      "Ollama": { "DefaultModel": "llama3.2" },
      "OpenAI": { "DefaultModel": "gpt-4o-mini" }
    }
  }
}
```

The OpenAI API key is a secret — keep it out of `appsettings.json`:

```sh
dotnet user-secrets init
dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "sk-..."
```

In production, supply the same key via an environment variable
(`Koras__AI__OpenAI__ApiKey`) or a vault. See [configuration](configuration.md).

## 3. Program.cs

```csharp
using Koras.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasAI(ai =>
{
    // Ollama is always registered — the local, keyless default.
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    // OpenAI activates (and becomes the default client) only when a key is configured.
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
```

Key points:

- `AddOllama(IConfiguration)` / `AddOpenAI(IConfiguration)` bind the section, validate it,
  and fail at **startup** with an actionable message if something required is missing.
- `IChatClient` is injected directly into endpoint handlers — it is a singleton.
- The handler's `CancellationToken` is ASP.NET Core's request-abort token; passing it means
  a client disconnect cancels the model call ([cancellation](../concepts/cancellation.md)).
- Each text delta is JSON-encoded into an SSE `data:` line and flushed immediately, so the
  browser sees tokens as they arrive.

## 4. Try it

```sh
dotnet run
```

```sh
curl -s localhost:5000/chat -H "Content-Type: application/json" \
     -d '{"prompt":"Say hello in Norwegian."}'

curl -N localhost:5000/chat/stream -H "Content-Type: application/json" \
     -d '{"prompt":"Count to 10 slowly."}'
```

The second command prints `data: "..."` lines incrementally and ends with `data: [DONE]`.

## Next steps

- [Dependency injection](dependency-injection.md) — named clients, fallback, decorators.
- [Minimal API guide](../guides/minimal-api.md) — the full sample, including browser-side SSE.
- [ASP.NET Core guide](../guides/aspnet-core.md) — controllers, error-to-HTTP mapping, health checks.
