# Configuration Recipes

Building on the [basics](../getting-started/configuration.md): per-environment settings,
several accounts of one provider, and gateway endpoints.

## Environment-specific appsettings

Because provider registration binds plain configuration sections, the standard
`appsettings.{Environment}.json` layering does all the work. Local development on Ollama,
production on OpenAI — same code, different files:

```json
// appsettings.json — shared, non-secret
{
  "Koras": { "AI": {
    "Ollama": { "DefaultModel": "llama3.2" },
    "OpenAI": { "DefaultModel": "gpt-4o-mini" }
  } }
}
```

```json
// appsettings.Production.json — override the model for production
{
  "Koras": { "AI": { "OpenAI": { "DefaultModel": "gpt-4o" } } }
}
```

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:OpenAI:ApiKey"]))
    {
        ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI")).AsDefault();
    }
});
```

The key gate makes the environment self-describing: developers without a key run on Ollama;
any environment where `Koras__AI__OpenAI__ApiKey` is set (secret store, env var, vault)
automatically promotes OpenAI to default. No `#if`, no environment checks in code.

## Multiple named clients of one provider

Register the same provider under different names to talk to two accounts, regions, or
tiers. Names must be explicit and unique:

```json
{
  "Koras": { "AI": {
    "OpenAI-Interactive": { "DefaultModel": "gpt-4o" },
    "OpenAI-Batch":       { "DefaultModel": "gpt-4o-mini" }
  } }
}
```

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI("interactive", o =>
    {
        builder.Configuration.GetSection("Koras:AI:OpenAI-Interactive").Bind(o);
        o.ApiKey = builder.Configuration["Koras:AI:OpenAI-Interactive:ApiKey"];
    }).AsDefault();

    ai.AddOpenAI("batch", o =>
    {
        builder.Configuration.GetSection("Koras:AI:OpenAI-Batch").Bind(o);
        o.ApiKey = builder.Configuration["Koras:AI:OpenAI-Batch:ApiKey"];
    });
});
```

Consume by name through the factory:

```csharp
public sealed class ReportService(IChatClientFactory clients)
{
    public Task<ChatResponse> DraftAsync(string prompt, CancellationToken ct)
        => clients.GetChatClient("batch").CompleteAsync(prompt, ct);
}
```

Each named client gets its own options instance, its own named `HttpClient`
(`Koras.AI.interactive`, `Koras.AI.batch`), its own startup validation (error messages name
the client), and its own tags in telemetry (`koras.ai.client.name`).

## Endpoint overrides for gateways

`OpenAIOptions.Endpoint` points the OpenAI provider at any OpenAI-compatible surface — an
enterprise LLM gateway, a proxy that adds billing headers, or a local emulator:

```json
{
  "Koras": { "AI": { "OpenAI": {
    "Endpoint": "https://llm-gateway.internal.example.com/v1/",
    "DefaultModel": "gpt-4o-mini"
  } } }
}
```

Validation requires HTTPS for non-loopback endpoints; `http://localhost:*` works for local
emulators. The same knob exists on other providers (`OllamaOptions.Endpoint` for remote
Ollama servers, `AnthropicOptions.Endpoint`, `GeminiOptions` endpoint) — and Azure OpenAI
uses `Endpoint` + `Deployment` + `ApiVersion` by design:

```json
{
  "Koras": { "AI": { "AzureOpenAI": {
    "Endpoint": "https://my-resource.openai.azure.com",
    "Deployment": "gpt-4o-mini",
    "ApiVersion": "2024-10-21"
  } } }
}
```

To customize the HTTP pipeline for one client (corporate proxy, extra headers), configure
its named `HttpClient`:

```csharp
builder.Services.AddHttpClient("Koras.AI.openai")
    .ConfigureHttpClient(http => http.DefaultRequestHeaders.Add("X-Team", "search"));
```

## Reminder: options are fixed at startup

All of the above is read when the client is first built and cached for the process
lifetime. Rotating a key or repointing a gateway requires a restart
([thread safety](../concepts/thread-safety.md)).
