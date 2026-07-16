# Worker Service Sample

A `BackgroundService` that summarizes a simulated work queue every 30 seconds, showing the
patterns that matter for unattended workloads:

- `UseRetry` with a tighter `AttemptTimeout` — the worker never hangs on a slow provider.
- Catching `AiException` **after** retries are exhausted: log and skip, never crash the host.
- `stoppingToken` flows into the AI call, so `Ctrl-C` / SIGTERM shut down promptly mid-call.

## Setup & run

```bash
ollama pull llama3.2     # local backend, no keys needed
dotnet run
```

## Expected output

Every 30 seconds an information log like:

```
info: WorkerService.Sample.SummaryWorker[0]
      Batch summarized by ollama (142 tokens):
      - Reset-password bug on mobile: ...
```

## Error scenarios

Stop Ollama while the worker runs: you'll see `Koras.AI retry n/4 scheduled ...` warnings, then
one `Summarization failed with Network; skipping this batch` error — and the worker keeps running.

Docs: [Worker Service guide](../../docs/guides/worker-service.md),
[Resilience](../../docs/features/resilience.md).
