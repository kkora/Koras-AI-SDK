# Koras.AI

The core of the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk) — a
provider-neutral AI client for .NET with chat, streaming, structured output, tool calling,
embeddings, retry, provider fallback, and OpenTelemetry-convention telemetry built in.

Install alongside one or more provider packages:

```
dotnet add package Koras.AI.OpenAI        # or .Anthropic, .AzureOpenAI, .Gemini, .Ollama
```

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
    ai.UseRetry();
});

// anywhere:
var response = await chatClient.CompleteAsync("Explain HttpClientFactory in one paragraph.");
Console.WriteLine(response.Text);
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs
