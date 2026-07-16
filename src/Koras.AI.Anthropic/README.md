# Koras.AI.Anthropic

Anthropic (Claude) provider for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
chat, streaming, tool calling, and structured output over the Anthropic Messages API.

```csharp
builder.Services.AddKorasAI(ai => ai.AddAnthropic(o =>
{
    o.ApiKey = builder.Configuration["Anthropic:ApiKey"];
    o.DefaultModel = "claude-sonnet-4-5";
}));
```

Note: Anthropic does not offer an embeddings API — `IEmbeddingClient` calls throw
`AiException` with `AiErrorCode.NotSupported`.
Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/provider-anthropic.md
