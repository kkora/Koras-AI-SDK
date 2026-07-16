using System.Runtime.CompilerServices;
using System.Text;

namespace Koras.AI.Providers;

/// <summary>Reads newline-delimited JSON streaming responses (used by Ollama).</summary>
public static class JsonLinesReader
{
    /// <summary>Reads non-empty lines from a response stream until end of stream.</summary>
    /// <param name="stream">The response body stream.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async IAsyncEnumerable<string> ReadLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }
}
