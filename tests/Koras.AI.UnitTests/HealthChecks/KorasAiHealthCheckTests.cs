using System.Runtime.CompilerServices;
using Koras.AI.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Koras.AI.UnitTests.HealthChecks;

public class KorasAiHealthCheckTests
{
    private sealed class ProbeClient(Exception? failure) : IChatClient, IProviderHealthProbe
    {
        public string ProviderName => "probed";

        public Task ProbeAsync(CancellationToken cancellationToken = default)
            => failure is null ? Task.CompletedTask : Task.FromException(failure);

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("not used in this test");

        public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private static async Task<HealthReport> RunAsync(Func<IServiceProvider, IChatClient> clientFactory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKorasAI(ai => ai.AddClient("probe-me", clientFactory));
        services.AddHealthChecks().AddKorasAI();

        await using ServiceProvider provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
    }

    [Fact]
    public async Task Healthy_when_the_probe_succeeds()
    {
        HealthReport report = await RunAsync(_ => new ProbeClient(failure: null));
        HealthReportEntry entry = Assert.Single(report.Entries).Value;
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Contains("probed", entry.Description);
    }

    [Fact]
    public async Task Degraded_when_the_probe_fails_transiently()
    {
        HealthReport report = await RunAsync(_ => new ProbeClient(new AiException("overloaded", AiErrorCode.ProviderUnavailable)));
        Assert.Equal(HealthStatus.Degraded, Assert.Single(report.Entries).Value.Status);
    }

    [Fact]
    public async Task Unhealthy_when_the_probe_fails_terminally()
    {
        HealthReport report = await RunAsync(_ => new ProbeClient(new AiException("bad key", AiErrorCode.Authentication)));
        Assert.Equal(HealthStatus.Unhealthy, Assert.Single(report.Entries).Value.Status);
    }

    [Fact]
    public async Task Healthy_with_note_when_no_probe_is_available()
    {
        HealthReport report = await RunAsync(_ => new FakeChatClient("no-probe"));
        HealthReportEntry entry = Assert.Single(report.Entries).Value;
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Contains("does not expose a health probe", entry.Description);
    }

    [Fact]
    public async Task Probe_is_discovered_through_the_decorator_chain()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKorasAI(ai =>
        {
            ai.AddClient("wrapped", _ => new ProbeClient(failure: null));
            ai.UseRetry(); // adds decorators above the probe-capable client
        });
        services.AddHealthChecks().AddKorasAI("wrapped");

        await using ServiceProvider provider = services.BuildServiceProvider();
        HealthReport report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, Assert.Single(report.Entries).Value.Status);
        Assert.Contains("is reachable", report.Entries.Single().Value.Description);
    }
}
