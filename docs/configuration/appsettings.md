# Configuration: appsettings.json

Koras.AI binds provider options from configuration sections under `Koras:AI:<Provider>` by
convention. You choose the section at registration — the convention just keeps every app
looking the same:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
    ai.AddAzureOpenAI(builder.Configuration.GetSection("Koras:AI:AzureOpenAI"));
    ai.AddAnthropic(builder.Configuration.GetSection("Koras:AI:Anthropic"));
    ai.AddGemini(builder.Configuration.GetSection("Koras:AI:Gemini"));
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
});
```

## Full example (all providers)

```json
{
  "Koras": {
    "AI": {
      "OpenAI": {
        "DefaultModel": "gpt-4o-mini",
        "DefaultEmbeddingModel": "text-embedding-3-small",
        "Endpoint": "https://api.openai.com/v1/",
        "Organization": "org-example"
      },
      "AzureOpenAI": {
        "Endpoint": "https://my-resource.openai.azure.com",
        "Deployment": "gpt-4o",
        "EmbeddingDeployment": "text-embedding-3-small",
        "ApiVersion": "2024-10-21"
      },
      "Anthropic": {
        "DefaultModel": "claude-sonnet-4-5",
        "DefaultMaxOutputTokens": 4096
      },
      "Gemini": {
        "DefaultModel": "gemini-2.0-flash",
        "DefaultEmbeddingModel": "text-embedding-004"
      },
      "Ollama": {
        "Endpoint": "http://localhost:11434",
        "DefaultModel": "llama3.2",
        "DefaultEmbeddingModel": "nomic-embed-text"
      }
    }
  }
}
```

Note what is *absent*: no `ApiKey` anywhere. See below.

## What must NOT go in appsettings.json: keys

API keys are credentials. `appsettings.json` is committed to source control, baked into
container images, and readable by anyone with repo or image access — a key that lands there
must be considered leaked. Keys belong in:

- **Development:** user secrets — `dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "sk-..."`.
- **Production:** environment variables (`Koras__AI__OpenAI__ApiKey`) or a vault-backed
  configuration provider (Azure Key Vault, Kubernetes Secrets) — see
  [environment variables](environment-variables.md).

All of these merge into the same `Koras:AI:OpenAI` section, so the binding code never
changes. Startup validation enforces the outcome: a missing key fails boot with
`Koras.AI OpenAI client 'openai': ApiKey is required. Provide it via configuration or user
secrets — never source code.`

## Per-environment overrides

`appsettings.{Environment}.json` overlays the base file. Typical split — cheap/local models
in development, production models and endpoints in production:

```json
// appsettings.Development.json
{
  "Koras": {
    "AI": {
      "OpenAI": { "DefaultModel": "gpt-4o-mini" },
      "Ollama": { "Endpoint": "http://localhost:11434", "DefaultModel": "llama3.2" }
    }
  }
}
```

```json
// appsettings.Production.json
{
  "Koras": {
    "AI": {
      "OpenAI": { "DefaultModel": "gpt-4o" }
    }
  }
}
```

Environment variables override both files, which is how keys and last-minute endpoint
changes arrive in deployed environments (see
[precedence](environment-variables.md#precedence)).

## Multiple named clients

Named registrations can bind distinct sections — the section name is yours to choose:

```csharp
ai.AddOpenAI("openai-main", builder.Configuration.GetSection("Koras:AI:OpenAIMain").Bind);
ai.AddOpenAI("openai-batch", builder.Configuration.GetSection("Koras:AI:OpenAIBatch").Bind);
```

## Endpoint rules

Remote endpoints must be HTTPS; plain HTTP is accepted only for loopback addresses
(`localhost`, `127.0.0.1`) — which is why the Ollama default works. A non-loopback HTTP
endpoint fails startup validation — see [validation](validation.md).
