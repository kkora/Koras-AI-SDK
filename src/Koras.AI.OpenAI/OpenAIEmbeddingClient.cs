using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koras.AI.Providers;

namespace Koras.AI.OpenAI;

/// <summary>
/// <see cref="IEmbeddingClient"/> over the OpenAI embeddings REST API. Thread-safe; intended
/// for singleton use via <c>AddKorasAI</c>.
/// </summary>
public class OpenAIEmbeddingClient : ProviderEmbeddingClient
{
    private readonly OpenAIOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public OpenAIEmbeddingClient(HttpClient httpClient, OpenAIOptions options)
        : this(httpClient, options, providerName: "openai")
    {
    }

    /// <summary>Initializes the client with a derived provider name (used by Azure OpenAI).</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    /// <param name="providerName">The provider identifier reported on responses and telemetry.</param>
    protected OpenAIEmbeddingClient(HttpClient httpClient, OpenAIOptions options, string providerName)
        : base(httpClient, providerName)
    {
        _options = Guard.NotNull(options);
    }

    /// <inheritdoc />
    public override async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        if (request.Values.Count == 0)
        {
            throw new ArgumentException("At least one input value is required.", nameof(request));
        }

        using HttpRequestMessage httpRequest = CreateEmbeddingHttpRequest(request);
        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        var embeddings = new List<Embedding>(request.Values.Count);
        foreach (JsonElement item in root.GetProperty("data").EnumerateArray())
        {
            JsonElement vectorElement = item.GetProperty("embedding");
            var vector = new float[vectorElement.GetArrayLength()];
            var i = 0;
            foreach (JsonElement component in vectorElement.EnumerateArray())
            {
                vector[i++] = component.GetSingle();
            }

            embeddings.Add(new Embedding(vector, item.TryGetProperty("index", out JsonElement index) ? index.GetInt32() : embeddings.Count));
        }

        TokenUsage usage = default;
        if (root.TryGetProperty("usage", out JsonElement usageElement) && usageElement.ValueKind == JsonValueKind.Object)
        {
            usage = new TokenUsage(
                usageElement.TryGetProperty("prompt_tokens", out JsonElement input) ? input.GetInt32() : 0,
                0);
        }

        return new EmbeddingResponse
        {
            Embeddings = embeddings,
            Provider = ProviderName,
            Model = root.TryGetProperty("model", out JsonElement model) ? model.GetString() : null,
            Usage = usage,
        };
    }

    /// <summary>Builds the HTTP request for an embeddings call. Overridden by Azure OpenAI.</summary>
    /// <param name="request">The provider-neutral request.</param>
    protected virtual HttpRequestMessage CreateEmbeddingHttpRequest(EmbeddingRequest request)
    {
        string model = request.Model
            ?? _options.DefaultEmbeddingModel
            ?? throw new AiException(
                $"No embedding model specified: set {nameof(OpenAIOptions)}.{nameof(OpenAIOptions.DefaultEmbeddingModel)} or pass EmbeddingRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

        var input = new JsonArray();
        foreach (string value in request.Values)
        {
            input.Add((JsonNode)value);
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["input"] = input,
        };
        if (request.Dimensions is { } dimensions)
        {
            body["dimensions"] = dimensions;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAIUris.Resolve(_options.Endpoint, "embeddings"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (_options.Organization is { Length: > 0 } organization)
        {
            httpRequest.Headers.TryAddWithoutValidation("OpenAI-Organization", organization);
        }

        return httpRequest;
    }
}
