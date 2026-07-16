using System.Text.Json;

namespace Koras.AI.Providers;

/// <summary>Shared HTTP send + error-normalization logic for provider base classes.</summary>
internal static class ProviderHttp
{
    public static async Task<JsonDocument> SendAndParseAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        string providerName,
        Func<string, string?> errorMessageExtractor,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendAsync(
            httpClient, request, providerName, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

        using (response)
        {
            await EnsureSuccessAsync(response, providerName, errorMessageExtractor, cancellationToken).ConfigureAwait(false);

            try
            {
                Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            catch (JsonException ex)
            {
                throw ProviderErrors.InvalidResponse(providerName, body: null, ex);
            }
        }
    }

    public static async Task<HttpResponseMessage> SendForStreamAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        string providerName,
        Func<string, string?> errorMessageExtractor,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendAsync(
            httpClient, request, providerName, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureSuccessAsync(response, providerName, errorMessageExtractor, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        string providerName,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            string? hint = ex.HttpRequestError == HttpRequestError.ConnectionError
                ? $"Verify the endpoint '{request.RequestUri?.GetLeftPart(UriPartial.Authority)}' is reachable."
                : null;
            throw ProviderErrors.Network(providerName, ex, hint);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed (not the caller's token).
            throw new AiException(
                $"The {providerName} request timed out after {httpClient.Timeout.TotalSeconds:0.#}s (HttpClient.Timeout).",
                AiErrorCode.Timeout,
                ex)
            {
                Provider = providerName,
            };
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string providerName,
        Func<string, string?> errorMessageExtractor,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string? message = null;
        try
        {
            message = errorMessageExtractor(body);
        }
        catch (JsonException)
        {
            // Non-JSON error body; use the generic message.
        }

        string? requestId = GetHeader(response, "x-request-id") ?? GetHeader(response, "request-id");
        TimeSpan? retryAfter = ProviderErrors.ParseRetryAfter(response.Headers);

        throw ProviderErrors.FromHttpResponse(
            providerName,
            (int)response.StatusCode,
            body,
            retryAfter,
            requestId,
            message is null ? null : $"The {providerName} request failed with HTTP {(int)response.StatusCode}: {message}");
    }

    private static string? GetHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
}
