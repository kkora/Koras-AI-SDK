using System.Text.Json;

namespace Koras.AI;

/// <summary>Convenience operations over <see cref="IChatClient"/>, including structured output.</summary>
public static class ChatClientExtensions
{
    /// <summary>Sends a single user prompt and returns the model's response.</summary>
    /// <param name="client">The chat client.</param>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public static Task<ChatResponse> CompleteAsync(
        this IChatClient client,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(client);
        return client.CompleteAsync(ChatRequest.FromPrompt(prompt), cancellationToken);
    }

    /// <summary>
    /// Requests schema-constrained JSON output for <typeparamref name="T"/> and returns the
    /// deserialized value. When the request does not already specify a
    /// <see cref="ChatOptions.ResponseFormat"/>, <see cref="ChatResponseFormat.ForType{T}"/>
    /// is applied automatically.
    /// </summary>
    /// <typeparam name="T">The structured output contract type.</typeparam>
    /// <param name="client">The chat client.</param>
    /// <param name="request">The conversation and options.</param>
    /// <param name="serializerOptions">Serializer settings for deserializing the output; safe web defaults when omitted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="AiException">
    /// Thrown with <see cref="AiErrorCode.InvalidResponse"/> when the model's output cannot be
    /// deserialized into <typeparamref name="T"/>.
    /// </exception>
    public static async Task<ChatResponse<T>> CompleteAsync<T>(
        this IChatClient client,
        ChatRequest request,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(client);
        Guard.NotNull(request);

        if (request.Options?.ResponseFormat is null)
        {
            ChatOptions options = request.Options ?? new ChatOptions();
            request = new ChatRequest
            {
                Messages = request.Messages,
                Model = request.Model,
                Options = new ChatOptions
                {
                    Temperature = options.Temperature,
                    TopP = options.TopP,
                    MaxOutputTokens = options.MaxOutputTokens,
                    StopSequences = options.StopSequences,
                    Tools = options.Tools,
                    ToolChoice = options.ToolChoice,
                    AdditionalProperties = options.AdditionalProperties,
                    ResponseFormat = ChatResponseFormat.ForType<T>(),
                },
            };
        }

        ChatResponse response = await client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        string json = ExtractJson(response.Text);

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, serializerOptions ?? KorasJson.DefaultOptions);
        }
        catch (JsonException ex)
        {
            throw CreateInvalidStructuredOutput<T>(response, ex);
        }

        if (value is null)
        {
            throw CreateInvalidStructuredOutput<T>(response, innerException: null);
        }

        return new ChatResponse<T> { Value = value, Raw = response };
    }

    /// <summary>Single-prompt structured output convenience overload.</summary>
    /// <typeparam name="T">The structured output contract type.</typeparam>
    /// <param name="client">The chat client.</param>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public static Task<ChatResponse<T>> CompleteAsync<T>(
        this IChatClient client,
        string prompt,
        CancellationToken cancellationToken = default)
        => client.CompleteAsync<T>(ChatRequest.FromPrompt(prompt), serializerOptions: null, cancellationToken);

    private static AiException CreateInvalidStructuredOutput<T>(ChatResponse response, Exception? innerException)
        => new(
            $"The model's output could not be deserialized as {typeof(T).Name}.",
            AiErrorCode.InvalidResponse,
            innerException)
        {
            Provider = response.Provider,
            ProviderErrorBody = response.Text is { Length: > 4096 } text ? text[..4096] : response.Text,
        };

    /// <summary>Strips a Markdown code fence when a lenient provider wrapped the JSON in one.</summary>
    private static string ExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewLine = span.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                span = span[(firstNewLine + 1)..];
                int closing = span.LastIndexOf("```".AsSpan(), StringComparison.Ordinal);
                if (closing >= 0)
                {
                    span = span[..closing];
                }
            }
        }

        return span.Trim().ToString();
    }
}
