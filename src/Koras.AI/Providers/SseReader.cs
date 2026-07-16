using System.Runtime.CompilerServices;
using System.Text;

namespace Koras.AI.Providers;

/// <summary>One server-sent event.</summary>
/// <param name="EventType">The <c>event:</c> field, or <see langword="null"/> for unnamed events.</param>
/// <param name="Data">The concatenated <c>data:</c> payload (multi-line data joined with <c>\n</c>).</param>
public readonly record struct SseEvent(string? EventType, string Data);

/// <summary>
/// A minimal server-sent-events parser for provider streaming responses. Handles multi-line
/// <c>data:</c> fields, <c>event:</c> names, comment lines, and CR/LF variations; ignores
/// <c>id:</c> and <c>retry:</c> fields.
/// </summary>
public static class SseReader
{
    /// <summary>Reads SSE events from a response stream until end of stream.</summary>
    /// <param name="stream">The response body stream.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async IAsyncEnumerable<SseEvent> ReadEventsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

        string? eventType = null;
        StringBuilder? data = null;

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (data is not null)
                {
                    yield return new SseEvent(eventType, data.ToString());
                }

                eventType = null;
                data = null;
                continue;
            }

            if (line[0] == ':')
            {
                continue; // comment / keep-alive
            }

            int colon = line.IndexOf(':', StringComparison.Ordinal);
            string field = colon < 0 ? line : line[..colon];
            string value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "event":
                    eventType = value;
                    break;

                case "data":
                    if (data is null)
                    {
                        data = new StringBuilder(value);
                    }
                    else
                    {
                        data.Append('\n').Append(value);
                    }

                    break;

                default:
                    break; // id:, retry:, unknown fields — ignored
            }
        }

        if (data is not null)
        {
            yield return new SseEvent(eventType, data.ToString());
        }
    }
}
