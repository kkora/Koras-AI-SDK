# Recipes: Advanced Scenarios

Patterns for multi-provider setups, custom decorators, and the provider-specific escape
hatches. See [common scenarios](common-scenarios.md) for the basics.

## Dev on Ollama, prod on Azure OpenAI — driven by configuration

Register both providers conditionally and let the environment's configuration decide which
one exists. No code changes between environments:

```csharp
builder.Services.AddKorasAI(ai =>
{
    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:AzureOpenAI:ApiKey"]))
    {
        ai.AddAzureOpenAI(builder.Configuration.GetSection("Koras:AI:AzureOpenAI")).AsDefault();
    }
    else
    {
        ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama")).AsDefault();
    }
});
```

In development, `appsettings.Development.json` carries the Ollama section; in production the
Azure key arrives via environment variables (`Koras__AI__AzureOpenAI__ApiKey`) — see
[environment variables](../configuration/environment-variables.md).

## Fallback chain combined with retry

`UseRetry` wraps every client — including each fallback candidate — so a transient failure is
first retried against the same provider, then failed over to the next one:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddAzureOpenAI(builder.Configuration.GetSection("Koras:AI:AzureOpenAI"));
    ai.AddAnthropic(builder.Configuration.GetSection("Koras:AI:Anthropic"));
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    ai.AddFallback("resilient", "azure_openai", "anthropic", "ollama").AsDefault();

    ai.UseRetry(r =>
    {
        r.MaxAttempts = 2;                          // keep low when fallback exists
        r.AttemptTimeout = TimeSpan.FromSeconds(30);
    });
});
```

Only errors with `AiException.IsTransient == true` retry or fail over; terminal errors
(authentication, invalid request) surface immediately. If every candidate fails, the last
`AiException` is thrown with an `AggregateException` of all attempts as its inner exception.

## Two named OpenAI clients with different accounts

Pass an explicit name to register the same provider twice (duplicate names throw at
registration). Resolve by name through `IChatClientFactory`:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI("openai-main", builder.Configuration.GetSection("Koras:AI:OpenAIMain").Bind);
    ai.AddOpenAI("openai-batch", o =>
    {
        o.ApiKey = builder.Configuration["Koras:AI:OpenAIBatch:ApiKey"];
        o.Organization = "org-batch";
        o.DefaultModel = "gpt-4o-mini";
    });
});
```

```csharp
public sealed class ReportService(IChatClientFactory clients)
{
    public Task<ChatResponse> DraftAsync(string prompt, CancellationToken ct)
        => clients.GetChatClient("openai-main").CompleteAsync(prompt, ct);

    public Task<ChatResponse> BulkClassifyAsync(string prompt, CancellationToken ct)
        => clients.GetChatClient("openai-batch").CompleteAsync(prompt, ct);
}
```

## Custom audit decorator via `ai.Use`

Derive from `DelegatingChatClient` and attach it globally (every client) or per client.
Decorators apply in registration order, innermost first:

```csharp
public sealed class AuditChatClient(IChatClient inner, IAuditSink sink) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> CompleteAsync(
        ChatRequest request, CancellationToken cancellationToken = default)
    {
        ChatResponse response = await base.CompleteAsync(request, cancellationToken);
        await sink.RecordAsync(ProviderName, response.Model, response.Usage, cancellationToken);
        return response;
    }
}
```

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
    ai.Use((sp, inner) => new AuditChatClient(inner, sp.GetRequiredService<IAuditSink>()));
});
```

## Provider-specific fields via `AdditionalProperties`

Fields the neutral `ChatOptions` doesn't model are merged into the wire request as-is:

```csharp
ChatResponse response = await chat.CompleteAsync(new ChatRequest
{
    Messages = [ChatMessage.User("Hello")],
    Options = new ChatOptions
    {
        AdditionalProperties = new Dictionary<string, object?>
        {
            ["seed"] = 42,                    // OpenAI-specific
            ["presence_penalty"] = 0.5,
        },
    },
}, ct);
```

Keys use the provider's wire names. Prefer first-class `ChatOptions` members where they exist.

## Reading `RawRepresentation`

`ChatResponse.RawRepresentation` is the provider's raw JSON payload (a `JsonElement`; may be
`default` when unavailable) for fields the neutral model doesn't surface:

```csharp
ChatResponse response = await chat.CompleteAsync("Hello", ct);

if (response.RawRepresentation.ValueKind == JsonValueKind.Object &&
    response.RawRepresentation.TryGetProperty("system_fingerprint", out JsonElement fp))
{
    logger.LogInformation("system_fingerprint: {Fingerprint}", fp.GetString());
}
```

`RawRepresentation` is provider-shaped by definition — code reading it is intentionally
provider-coupled, unlike everything else in this SDK.
