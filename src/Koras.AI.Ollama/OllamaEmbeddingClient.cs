using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koras.AI.Providers;

namespace Koras.AI.Ollama;

/// <summary>
/// <see cref="IEmbeddingClient"/> over the native Ollama <c>/api/embed</c> endpoint.
/// Thread-safe; intended for singleton use via <c>AddKorasAI</c>.
/// </summary>
public sealed class OllamaEmbeddingClient : ProviderEmbeddingClient
{
    private readonly OllamaOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The provider configuration.</param>
    public OllamaEmbeddingClient(HttpClient httpClient, OllamaOptions options)
        : base(httpClient, providerName: "ollama")
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
                $"No embedding model specified: set {nameof(OllamaOptions)}.{nameof(OllamaOptions.DefaultEmbeddingModel)} or pass EmbeddingRequest.Model.",
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

        string endpoint = _options.Endpoint.AbsoluteUri;
        if (!endpoint.EndsWith('/'))
        {
            endpoint += "/";
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(endpoint), "api/embed"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        JsonElement root = document.RootElement;

        var embeddings = new List<Embedding>(request.Values.Count);
        if (root.TryGetProperty("embeddings", out JsonElement vectors) && vectors.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (JsonElement vectorElement in vectors.EnumerateArray())
            {
                var vector = new float[vectorElement.GetArrayLength()];
                var i = 0;
                foreach (JsonElement component in vectorElement.EnumerateArray())
                {
                    vector[i++] = component.GetSingle();
                }

                embeddings.Add(new Embedding(vector, index++));
            }
        }

        return new EmbeddingResponse
        {
            Embeddings = embeddings,
            Provider = ProviderName,
            Model = root.TryGetProperty("model", out JsonElement modelElement) ? modelElement.GetString() : model,
            Usage = new TokenUsage(
                root.TryGetProperty("prompt_eval_count", out JsonElement promptTokens) ? promptTokens.GetInt32() : 0,
                0),
        };
    }
}
