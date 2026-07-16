// Koras.AI console sample: chat, streaming, structured output, tool calling, and embeddings.
// Runs against local Ollama by default (no API key needed); set OpenAI:ApiKey via user
// secrets or environment variables to use OpenAI instead:
//   dotnet user-secrets set "OpenAI:ApiKey" "sk-..."

using System.ComponentModel;
using Koras.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string? openAiKey = builder.Configuration["OpenAI:ApiKey"];

builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(o =>
    {
        o.DefaultModel = builder.Configuration["Ollama:Model"] ?? "llama3.2";
        o.DefaultEmbeddingModel = "nomic-embed-text";
    });

    if (!string.IsNullOrEmpty(openAiKey))
    {
        ai.AddOpenAI(o =>
        {
            o.ApiKey = openAiKey;
            o.DefaultModel = "gpt-4o-mini";
            o.DefaultEmbeddingModel = "text-embedding-3-small";
        }).AsDefault();
    }

    ai.UseRetry();
    ai.UseToolInvocation();
});

using IHost host = builder.Build();
var chat = host.Services.GetRequiredService<IChatClient>();
var embeddings = host.Services.GetRequiredService<IEmbeddingClient>();
using var lifetimeCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetimeCts.Cancel();
};

try
{
    Console.WriteLine($"Provider: {chat.ProviderName}\n");

    // 1. Simple chat completion
    Console.WriteLine("== Chat ==");
    ChatResponse answer = await chat.CompleteAsync("In one sentence: what is dependency injection?", lifetimeCts.Token);
    Console.WriteLine(answer.Text);
    Console.WriteLine($"({answer.Usage.InputTokens} in / {answer.Usage.OutputTokens} out tokens)\n");

    // 2. Streaming
    Console.WriteLine("== Streaming ==");
    await foreach (ChatStreamUpdate update in chat.StreamAsync(
        ChatRequest.FromPrompt("Count from 1 to 5 with a word about each number."), lifetimeCts.Token))
    {
        Console.Write(update.TextDelta);
    }

    Console.WriteLine("\n");

    // 3. Structured output
    Console.WriteLine("== Structured output ==");
    ChatResponse<Recipe> recipe = await chat.CompleteAsync<Recipe>(
        "Give me a simple pancake recipe.", lifetimeCts.Token);
    Console.WriteLine($"{recipe.Value.Name}: {string.Join(", ", recipe.Value.Ingredients)} ({recipe.Value.Minutes} min)\n");

    // 4. Tool calling (the UseToolInvocation loop executes the tool automatically)
    Console.WriteLine("== Tool calling ==");
    var weatherTool = AiTool.Create(
        "get_weather",
        "Gets the current weather for a city",
        ([Description("The city name")] string city) => $"18°C and sunny in {city}");

    ChatResponse toolAnswer = await chat.CompleteAsync(new ChatRequest
    {
        Messages = [ChatMessage.User("What's the weather like in Oslo right now?")],
        Options = new ChatOptions { Tools = [weatherTool] },
    }, lifetimeCts.Token);
    Console.WriteLine($"{toolAnswer.Text}\n");

    // 5. Embeddings
    Console.WriteLine("== Embeddings ==");
    EmbeddingResponse vectors = await embeddings.GenerateAsync(
        new EmbeddingRequest("Koras.AI is a provider-neutral AI SDK for .NET."), lifetimeCts.Token);
    Console.WriteLine($"Generated a {vectors.Embeddings[0].Vector.Length}-dimension vector via {vectors.Provider}.");
}
catch (AiException ex) when (ex.Code == AiErrorCode.Network)
{
    Console.Error.WriteLine($"Network error: {ex.Message}");
    Console.Error.WriteLine("Tip: this sample uses local Ollama by default — install it from https://ollama.com and run 'ollama pull llama3.2'.");
    return 1;
}
catch (AiException ex)
{
    Console.Error.WriteLine($"AI error [{ex.Code}] from {ex.Provider}: {ex.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Canceled.");
    return 130;
}

return 0;

/// <summary>The structured-output contract for the recipe demo.</summary>
[Description("A simple cooking recipe")]
public sealed record Recipe(
    [property: Description("The recipe name")] string Name,
    [property: Description("Ingredient list")] IReadOnlyList<string> Ingredients,
    [property: Description("Total minutes to prepare")] int Minutes);
