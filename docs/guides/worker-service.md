# Worker Service Guide

A walkthrough of [`samples/WorkerService.Sample`](../../samples/WorkerService.Sample/Program.cs):
a `BackgroundService` that periodically summarizes a batch of work with an AI call. The
sample encodes the three habits that keep unattended AI workloads healthy: **generous retry
options**, **the stopping token everywhere**, and **skip, don't crash**.

## Registration: retry tuned for unattended work

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
    ai.UseRetry(r =>
    {
        r.MaxAttempts = 4;                          // one more than the default 3
        r.AttemptTimeout = TimeSpan.FromSeconds(60);
    });
});

builder.Services.AddHostedService<SummaryWorker>();

builder.Build().Run();
```

Interactive apps keep retries short because a user is waiting. A background job has no user
— it can afford more attempts and a tighter per-attempt timeout (a hung attempt is better
cut off at 60 s and retried than left holding the default 100 s). Backoff with jitter and
`Retry-After` handling come with `UseRetry` automatically.

## The worker

```csharp
public sealed class SummaryWorker(IChatClient chat, ILogger<SummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        do
        {
            try
            {
                ChatResponse summary = await chat.CompleteAsync(new ChatRequest
                {
                    Messages =
                    [
                        ChatMessage.System("You summarize support tickets into one actionable line each."),
                        ChatMessage.User(string.Join("\n", tickets)),
                    ],
                    Options = new ChatOptions { MaxOutputTokens = 200 },
                }, stoppingToken);

                logger.LogInformation("Batch summarized by {Provider} ({Tokens} tokens):\n{Summary}",
                    summary.Provider, summary.Usage.TotalTokens, summary.Text);
            }
            catch (AiException ex)
            {
                // Retries are exhausted at this point (the SDK's retry decorator ran first).
                logger.LogError(ex, "Summarization failed with {Code}; skipping this batch", ex.Code);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
```

`IChatClient` injects straight into the hosted service — it is a singleton and thread-safe,
so no scope gymnastics are needed ([thread safety](../concepts/thread-safety.md)).

## stoppingToken: prompt, clean shutdown

The host's `stoppingToken` is passed to **both** the timer wait and the AI call. On
`SIGTERM`/`Ctrl+C`:

- a worker idling in `WaitForNextTickAsync` wakes immediately;
- a worker mid-completion cancels the HTTP request and unwinds with
  `OperationCanceledException`, which `BackgroundService` treats as normal shutdown.

Forgetting the token on the AI call is the classic mistake — shutdown then blocks until the
model finishes generating. See [cancellation](../concepts/cancellation.md).

## Skip, don't crash

The `catch (AiException ex)` block is the worker's survival policy. By the time it runs,
the retry decorator has already retried anything transient, so whatever arrives is either
terminal (bad key, invalid request) or an exhausted transient. Neither justifies killing the
host: the batch is logged with its error code and **skipped**, and the loop waits for the
next tick — by which time a rate limit or outage has often cleared on its own.

Refinements for production workers:

- Treat terminal codes differently — `Authentication` or `Configuration` will not fix
  themselves, so alert on them rather than silently retrying every 30 s:

```csharp
catch (AiException ex) when (!ex.IsTransient)
{
    logger.LogCritical(ex, "Non-recoverable AI failure ({Code}); check configuration", ex.Code);
}
catch (AiException ex)
{
    logger.LogError(ex, "Transient AI failure survived retries ({Code}); skipping batch", ex.Code);
}
```

- Cap output with `MaxOutputTokens` (the sample uses 200) — unattended loops with unbounded
  output are a cost leak.
- Keep `OperationCanceledException` uncaught so shutdown stays fast.

## Try it

```sh
dotnet run --project samples/WorkerService.Sample
```

Every 30 seconds the worker logs a summarized batch (requires local Ollama with
`llama3.2` pulled). Press Ctrl+C to watch the clean shutdown path.

## Related

- [Error handling](../concepts/error-handling.md) — the taxonomy behind `ex.Code`.
- [Logging guide](logging.md) — the retry/failure log records this worker emits.
