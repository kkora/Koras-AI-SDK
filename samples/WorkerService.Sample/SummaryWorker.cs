using Koras.AI;

namespace WorkerService.Sample;

/// <summary>
/// Simulates a queue-draining background job: every 30 seconds it summarizes a pending batch
/// of "support tickets" with the AI client. Transient provider failures are retried by the
/// SDK; terminal ones are logged and the batch is skipped rather than crashing the host.
/// </summary>
public sealed class SummaryWorker(IChatClient chat, ILogger<SummaryWorker> logger) : BackgroundService
{
    private static readonly string[] FakeTickets =
    [
        "Customer cannot reset their password from the mobile app.",
        "Invoice PDF download returns a 500 error since yesterday.",
        "Feature request: export the dashboard as CSV.",
    ];

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
                        ChatMessage.User(string.Join("\n", FakeTickets)),
                    ],
                    Options = new ChatOptions { MaxOutputTokens = 200 },
                }, stoppingToken);

                logger.LogInformation(
                    "Batch summarized by {Provider} ({Tokens} tokens):\n{Summary}",
                    summary.Provider,
                    summary.Usage.TotalTokens,
                    summary.Text);
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
