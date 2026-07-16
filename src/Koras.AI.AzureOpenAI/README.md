# Koras.AI.AzureOpenAI

Azure OpenAI provider for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
chat, streaming, tool calling, structured output, and embeddings against Azure OpenAI
deployments.

```csharp
builder.Services.AddKorasAI(ai => ai.AddAzureOpenAI(o =>
{
    o.Endpoint = new Uri("https://my-resource.openai.azure.com");
    o.Deployment = "gpt-4o-mini";
    o.ApiKey = builder.Configuration["AzureOpenAI:ApiKey"];
}));
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/provider-azure-openai.md
