# Koras.AI.OpenAI

OpenAI provider for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
chat, streaming, tool calling, structured output, and embeddings over the OpenAI REST API —
no vendor SDK dependency.

```csharp
builder.Services.AddKorasAI(ai => ai.AddOpenAI(o =>
{
    o.ApiKey = builder.Configuration["OpenAI:ApiKey"]; // never hardcode
    o.DefaultModel = "gpt-4o-mini";
}));
```

Also works with OpenAI-compatible gateways via `o.Endpoint`.
Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/provider-openai.md
