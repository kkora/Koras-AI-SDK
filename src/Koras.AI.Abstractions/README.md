# Koras.AI.Abstractions

Provider-neutral abstractions for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
`IChatClient`, `IEmbeddingClient`, chat/streaming/tool/embedding models, and the normalized
`AiException`/`AiErrorCode` error taxonomy.

Reference this package from **libraries** that accept "any AI model" without dictating a
provider. **Applications** install [Koras.AI](https://www.nuget.org/packages/Koras.AI) plus one
or more provider packages (`Koras.AI.OpenAI`, `Koras.AI.Anthropic`, `Koras.AI.AzureOpenAI`,
`Koras.AI.Gemini`, `Koras.AI.Ollama`).

```csharp
public sealed class Summarizer(IChatClient chat)
{
    public async Task<string?> SummarizeAsync(string text, CancellationToken ct = default)
    {
        var response = await chat.CompleteAsync(
            ChatRequest.FromPrompt($"Summarize:\n{text}"), ct);
        return response.Text;
    }
}
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs
