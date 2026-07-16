# Quick Start

Five minutes to a first completion. We start with **Ollama** because it needs zero API keys,
then swap in OpenAI with a three-line change.

## 1. Prerequisites

- .NET 8, 9, or 10 SDK
- [Ollama](https://ollama.com) running locally with a model pulled:

```sh
ollama pull llama3.2
```

## 2. Create the project

```sh
dotnet new console -n HelloKoras
cd HelloKoras
dotnet add package Koras.AI.Ollama -v 0.1.0-preview.1
dotnet add package Microsoft.Extensions.Hosting
```

## 3. Program.cs

Replace `Program.cs` with:

```csharp
using Koras.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o => o.DefaultModel = "llama3.2");
    ai.UseRetry();
});

using IHost host = builder.Build();
var chat = host.Services.GetRequiredService<IChatClient>();

try
{
    // One-shot completion
    ChatResponse answer = await chat.CompleteAsync("In one sentence: what is dependency injection?");
    Console.WriteLine(answer.Text);
    Console.WriteLine($"({answer.Usage.InputTokens} in / {answer.Usage.OutputTokens} out tokens)");

    // Streaming
    await foreach (ChatStreamUpdate update in chat.StreamAsync(
        ChatRequest.FromPrompt("Count from 1 to 5 with a word about each number.")))
    {
        Console.Write(update.TextDelta);
    }

    Console.WriteLine();
}
catch (AiException ex) when (ex.Code == AiErrorCode.Network)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
    Console.Error.WriteLine("Is Ollama running? Install it from https://ollama.com and run 'ollama pull llama3.2'.");
}
catch (AiException ex)
{
    Console.Error.WriteLine($"AI error [{ex.Code}] from {ex.Provider}: {ex.Message}");
}
```

## 4. Run it

```sh
dotnet run
```

You should see a one-sentence answer, a token count, and then the streamed counting response
appearing incrementally.

## 5. Swap to OpenAI

Because your code depends only on `IChatClient`, switching providers is a registration
change, not a code change:

```sh
dotnet add package Koras.AI.OpenAI -v 0.1.0-preview.1
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
```

Then replace the `AddOllama` line:

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
```

Everything downstream — completion, streaming, error handling — works unchanged. You can
also keep both registered and pick per environment; see
[dependency injection](dependency-injection.md) for named clients and fallback.

## What you just used

- `AddKorasAI(...)` — the single DI entry point; registers the default `IChatClient` as a
  singleton.
- `ai.UseRetry()` — automatic retry of transient failures (rate limits, network blips) with
  exponential backoff.
- `chat.CompleteAsync(string)` — convenience overload for one-shot prompts.
- `chat.StreamAsync(...)` — `IAsyncEnumerable<ChatStreamUpdate>`; the request starts when
  you begin enumerating.
- `AiException` with `AiErrorCode` — the single, provider-neutral error type
  ([error handling](../concepts/error-handling.md)).

## Next steps

- [Your first application](first-application.md) — a web API with SSE streaming.
- [Configuration](configuration.md) — binding options from `appsettings.json` and secrets.
- The full console walkthrough: [console app guide](../guides/console-app.md).
