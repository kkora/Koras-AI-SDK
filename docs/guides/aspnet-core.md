# ASP.NET Core Guide

A walkthrough of [`samples/WebApi.Sample`](../../samples/WebApi.Sample/Program.cs): a
controller-based API with provider fallback as the default client, `AiException`-to-HTTP
mapping, and a health endpoint. For Minimal APIs and SSE streaming, see the
[minimal API guide](minimal-api.md).

## Registration: a fallback default built from what's configured

The sample assembles a fallback chain from whichever hosted providers have keys, with local
Ollama as the always-available last resort:

```csharp
builder.Services.AddControllers();

builder.Services.AddKorasAI(ai =>
{
    var fallbackChain = new List<string>();

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:OpenAI:ApiKey"]))
    {
        ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
        fallbackChain.Add("openai");
    }

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:Anthropic:ApiKey"]))
    {
        ai.AddAnthropic(builder.Configuration.GetSection("Koras:AI:Anthropic"));
        fallbackChain.Add("anthropic");
    }

    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
    fallbackChain.Add("ollama");

    if (fallbackChain.Count > 1)
    {
        ai.AddFallback("resilient", [.. fallbackChain]).AsDefault();
    }

    ai.UseRetry(r => r.MaxAttempts = 3);
});

builder.Services.AddHealthChecks().AddKorasAI("ollama", tags: ["ready"]);
```

- Keys live in user secrets or environment variables — never `appsettings.json`
  ([configuration](../getting-started/configuration.md)).
- The `"resilient"` fallback client becomes the default `IChatClient` when more than one
  provider is available; on transient failures it moves down the chain
  (openai → anthropic → ollama).
- `UseRetry` wraps each candidate, so a provider gets its retries before failover.

## The controller

Constructor injection gives you both the default client and the factory for named access:

```csharp
[ApiController]
[Route("api/[controller]")]
public sealed class ChatController(IChatClient chat, IChatClientFactory clientFactory) : ControllerBase
{
    public sealed record ChatRequestDto(string Prompt, string? Client);

    [HttpPost]
    public async Task<ActionResult<ChatResponseDto>> Complete(ChatRequestDto request, CancellationToken cancellationToken)
    {
        IChatClient client = request.Client is { Length: > 0 } name
            ? clientFactory.GetChatClient(name)   // caller picked a specific provider
            : chat;                               // the resilient default

        ChatResponse response = await client.CompleteAsync(request.Prompt, cancellationToken);
        // ...
    }

    [HttpGet("clients")]
    public ActionResult<IReadOnlyList<string>> Clients() => Ok(clientFactory.ClientNames);
}
```

The action's `CancellationToken` is `HttpContext.RequestAborted` — a disconnected caller
cancels the model call ([cancellation](../concepts/cancellation.md)).

## Mapping AiException to HTTP statuses

The sample maps the taxonomy onto meaningful statuses with exception filters:

```csharp
try
{
    ChatResponse response = await client.CompleteAsync(request.Prompt, cancellationToken);
    return Ok(new ChatResponseDto(response.Text, response.Provider, response.Model,
        response.Usage.InputTokens, response.Usage.OutputTokens));
}
catch (AiException ex) when (ex.Code == AiErrorCode.RateLimited)
{
    return StatusCode(StatusCodes.Status429TooManyRequests,
        new { error = "AI provider rate limited", retryAfterSeconds = ex.RetryAfter?.TotalSeconds });
}
catch (AiException ex) when (ex.Code == AiErrorCode.ContentFiltered)
{
    return UnprocessableEntity(new { error = "The AI provider's safety system blocked this request." });
}
catch (AiException ex) when (ex.IsTransient)
{
    return StatusCode(StatusCodes.Status503ServiceUnavailable,
        new { error = "AI providers are temporarily unavailable.", code = ex.Code.ToString() });
}
```

| `AiErrorCode` | HTTP status |
|---|---|
| `RateLimited` | 429, forwarding `RetryAfter` |
| `ContentFiltered` | 422 Unprocessable Entity |
| any `IsTransient` (retries + fallback exhausted) | 503 Service Unavailable |
| everything else (`Authentication`, `InvalidRequest`, …) | uncaught → 500, a server-side bug or misconfiguration to fix, not to hide |

For larger apps, hoist this mapping into ASP.NET Core exception-handling middleware or an
`IExceptionHandler` so controllers stay clean.

## Health endpoint

```csharp
app.MapControllers();
app.MapHealthChecks("/health");
```

`AddKorasAI("ollama", tags: ["ready"])` probes the Ollama version endpoint (cheap, never a
paid completion). Transient probe failures report `Degraded`; terminal ones `Unhealthy`.
Details and per-provider probe endpoints: [health checks guide](health-checks.md).

## Try it

```sh
dotnet run --project samples/WebApi.Sample

curl -s localhost:5000/api/chat -H "Content-Type: application/json" \
     -d '{"prompt":"Hello!","client":null}'
curl -s localhost:5000/api/chat/clients
curl -s localhost:5000/health
```
