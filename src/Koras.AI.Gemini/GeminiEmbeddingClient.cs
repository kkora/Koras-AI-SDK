using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koras.AI.Providers;

namespace Koras.AI.Gemini;

/// <summary>
/// <see cref="IEmbeddingClient"/> over the Gemini <c>batchEmbedContents</c> REST API.
/// Gemini does not report token usage for embeddings. Thread-safe; intended for singleton use
/// via <c>AddKorasAI</c>.
/// </summary>
public sealed class GeminiEmbeddingClient : ProviderEmbeddingClient
{
    private readonly GeminiOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public GeminiEmbeddingClient(HttpClient httpClient, GeminiOptions options)
        : base(httpClient, providerName: "gemini")
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

        string model = request.Model
            ?? _options.DefaultEmbeddingModel
            ?? throw new AiException(
                $"No embedding model specified: set {nameof(GeminiOptions)}.{nameof(GeminiOptions.DefaultEmbeddingModel)} or pass EmbeddingRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

        var requests = new JsonArray();
        foreach (string value in request.Values)
        {
            var item = new JsonObject
            {
                ["model"] = $"models/{model}",
                ["content"] = new JsonObject
                {
                    ["parts"] = new JsonArray(new JsonObject { ["text"] = value }),
                },
            };
            if (request.Dimensions is { } dimensions)
            {
                item["outputDimensionality"] = dimensions;
            }

            requests.Add(item);
        }

        var body = new JsonObject { ["requests"] = requests };

        string endpoint = _options.Endpoint.AbsoluteUri;
        if (!endpoint.EndsWith('/'))
        {
            endpoint += "/";
        }

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(endpoint), $"models/{Uri.EscapeDataString(model)}:batchEmbedContents"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);

        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        var embeddings = new List<Embedding>(request.Values.Count);
        if (root.TryGetProperty("embeddings", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.TryGetProperty("values", out JsonElement values))
                {
                    var vector = new float[values.GetArrayLength()];
                    var i = 0;
                    foreach (JsonElement component in values.EnumerateArray())
                    {
                        vector[i++] = component.GetSingle();
                    }

                    embeddings.Add(new Embedding(vector, index));
                }

                index++;
            }
        }

        return new EmbeddingResponse
        {
            Embeddings = embeddings,
            Provider = ProviderName,
            Model = model,
        };
    }
}
