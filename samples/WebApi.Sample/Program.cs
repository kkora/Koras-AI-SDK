// Koras.AI Web API sample: controllers, provider fallback, and health checks.
// Ollama is the always-available local fallback; hosted providers activate when their
// API keys are configured (user secrets / environment variables — never appsettings).

using Koras.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddKorasAI(ai =>
{
    var fallbackChain = new List<string>();

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:OpenAI:ApiKey"]))
    {
        ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
        fallbackChain.Add("openai");
    }

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:Anthropic:ApiKey"]))
    {
        ai.AddAnthropic(builder.Configuration.GetSection("Koras:AI:Anthropic"));
        fallbackChain.Add("anthropic");
    }

    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
    fallbackChain.Add("ollama");

    if (fallbackChain.Count > 1)
    {
        ai.AddFallback("resilient", [.. fallbackChain]).AsDefault();
    }

    ai.UseRetry(r => r.MaxAttempts = 3);
});

builder.Services.AddHealthChecks().AddKorasAI("ollama", tags: ["ready"]);

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
