// Koras.AI worker-service sample: a background service that periodically summarizes a work
// queue with retry-hardened AI calls and graceful shutdown.

using Koras.AI;
using WorkerService.Sample;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
    ai.UseRetry(r =>
    {
        r.MaxAttempts = 4;
        r.AttemptTimeout = TimeSpan.FromSeconds(60);
    });
});

builder.Services.AddHostedService<SummaryWorker>();

builder.Build().Run();
