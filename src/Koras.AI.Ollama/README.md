# Koras.AI.Ollama

Ollama provider for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
chat, streaming, tool calling, structured output, and embeddings against local or self-hosted
Ollama — ideal for development without API keys.

```csharp
builder.Services.AddKorasAI(ai => ai.AddOllama(o =>
{
    o.Endpoint = new Uri("http://localhost:11434");
    o.DefaultModel = "llama3.2";
}));
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/provider-ollama.md
