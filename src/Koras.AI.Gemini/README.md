# Koras.AI.Gemini

Google Gemini provider for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
chat, streaming, tool calling, structured output, and embeddings over the Gemini REST API.

```csharp
builder.Services.AddKorasAI(ai => ai.AddGemini(o =>
{
    o.ApiKey = builder.Configuration["Gemini:ApiKey"];
    o.DefaultModel = "gemini-2.0-flash";
}));
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/provider-gemini.md
