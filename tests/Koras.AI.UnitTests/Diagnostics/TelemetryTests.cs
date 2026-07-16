using System.Diagnostics;
using System.Diagnostics.Metrics;
using Koras.AI.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Koras.AI.UnitTests.Diagnostics;

public class TelemetryTests
{
    private static (IChatClient Client, ServiceProvider Provider) BuildClient(Action<KorasAiBuilder>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKorasAI(ai =>
        {
            ai.AddClient("test-client", _ => new FakeChatClient("fakeprov")
                .EnqueueResponse("hi", usage: new TokenUsage(11, 3))
                .EnqueueError(new AiException("boom", AiErrorCode.RateLimited)));
            extra?.Invoke(ai);
        });
        ServiceProvider provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IChatClient>(), provider);
    }

    // The Koras.AI ActivitySource is process-global and other test classes run in parallel,
    // so a listener must capture only spans tagged with this test's client name.
    private static ActivityListener ListenToClient(string clientName, List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == KorasAiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("koras.ai.client.name"), clientName))
                {
                    activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task Chat_operations_emit_gen_ai_activities()
    {
        var activities = new List<Activity>();
        using ActivityListener listener = ListenToClient("test-client", activities);

        (IChatClient client, ServiceProvider provider) = BuildClient();
        using (provider)
        {
            await client.CompleteAsync(new ChatRequest { Messages = [ChatMessage.User("q")], Model = "test-model" });

            Activity activity = Assert.Single(activities);
            Assert.Equal("chat test-model", activity.DisplayName);
            Assert.Equal("fakeprov", activity.GetTagItem("gen_ai.system"));
            Assert.Equal("test-client", activity.GetTagItem("koras.ai.client.name"));
            Assert.Equal(11, activity.GetTagItem("gen_ai.usage.input_tokens"));
            Assert.Equal(3, activity.GetTagItem("gen_ai.usage.output_tokens"));

            activities.Clear();
            await Assert.ThrowsAsync<AiException>(() => client.CompleteAsync(ChatRequest.FromPrompt("q")));
            Assert.Equal(ActivityStatusCode.Error, Assert.Single(activities).Status);
            Assert.Equal("rate_limited", activities[0].GetTagItem("error.type"));
        }
    }

    [Fact]
    public async Task Chat_operations_record_duration_and_token_metrics()
    {
        var measurements = new List<(Instrument Instrument, double Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == KorasAiTelemetry.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add((instrument, value, tags.ToArray().ToDictionary(static t => t.Key, static t => t.Value)));
            }
        });
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            lock (measurements)
            {
                measurements.Add((instrument, value, tags.ToArray().ToDictionary(static t => t.Key, static t => t.Value)));
            }
        });
        listener.Start();

        (IChatClient client, ServiceProvider provider) = BuildClient();
        using (provider)
        {
            await client.CompleteAsync(ChatRequest.FromPrompt("q"));
        }

        listener.Dispose();

        lock (measurements)
        {
            Assert.Contains(measurements, m => m.Instrument.Name == "koras.ai.client.operation.duration");
            var tokenMeasurements = measurements.Where(static m => m.Instrument.Name == "koras.ai.client.token.usage").ToList();
            Assert.Contains(tokenMeasurements, m => Equals(m.Tags["gen_ai.token.type"], "input") && m.Value == 11);
            Assert.Contains(tokenMeasurements, m => Equals(m.Tags["gen_ai.token.type"], "output") && m.Value == 3);
        }
    }

    [Fact]
    public async Task Streaming_span_covers_the_whole_stream_and_captures_final_usage()
    {
        var activities = new List<Activity>();
        using ActivityListener listener = ListenToClient("telemetry-streaming", activities);

        var services = new ServiceCollection();
        services.AddKorasAI(ai => ai.AddClient("telemetry-streaming", _ =>
        {
            var fake = new FakeChatClient("fakeprov");
            fake.StreamUpdates.Add(new ChatStreamUpdate { TextDelta = "a" });
            fake.StreamUpdates.Add(new ChatStreamUpdate { FinishReason = ChatFinishReason.Stop, Usage = new TokenUsage(4, 2) });
            return fake;
        }));

        using ServiceProvider provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatClient>();

        await foreach (ChatStreamUpdate _ in client.StreamAsync(ChatRequest.FromPrompt("q")))
        {
        }

        Activity activity = Assert.Single(activities);
        Assert.Equal(4, activity.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.Equal(2, activity.GetTagItem("gen_ai.usage.output_tokens"));
    }

    [Fact]
    public async Task Message_content_is_never_logged_by_default()
    {
        var loggedLines = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new CollectingLoggerProvider(loggedLines)).SetMinimumLevel(LogLevel.Trace));
        services.AddKorasAI(ai => ai.AddClient("c", _ => new FakeChatClient().EnqueueResponse("SECRET-CONTENT")));

        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IChatClient>().CompleteAsync("SECRET-PROMPT");

        Assert.NotEmpty(loggedLines);
        Assert.DoesNotContain(loggedLines, static line => line.Contains("SECRET-PROMPT") || line.Contains("SECRET-CONTENT"));
    }

    [Fact]
    public async Task Message_content_is_logged_at_trace_only_when_opted_in()
    {
        var loggedLines = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(new CollectingLoggerProvider(loggedLines)).SetMinimumLevel(LogLevel.Trace));
        services.AddKorasAI(ai =>
        {
            ai.AddClient("c", _ => new FakeChatClient().EnqueueResponse("VISIBLE-CONTENT"));
            ai.ConfigureTelemetry(t => t.EnableSensitiveData = true);
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IChatClient>().CompleteAsync("VISIBLE-PROMPT");

        Assert.Contains(loggedLines, static line => line.Contains("VISIBLE-PROMPT"));
        Assert.Contains(loggedLines, static line => line.Contains("VISIBLE-CONTENT"));
    }

    private sealed class CollectingLoggerProvider(List<string> lines) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(lines);

        public void Dispose()
        {
        }

        private sealed class CollectingLogger(List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add(formatter(state, exception));
                }
            }
        }
    }
}
